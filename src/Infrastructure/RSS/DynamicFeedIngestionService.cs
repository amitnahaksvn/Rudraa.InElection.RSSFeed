using System.Diagnostics;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Application.Abstractions;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.RssProviders;

namespace Infrastructure.RSS;

/// <summary>
/// Downloads, parses, deduplicates, and persists one <see cref="FeedSource"/>-driven feed - the
/// generic counterpart to the 30+ file-configured <c>IRssProvider</c> classes, reusing their exact
/// parsing helpers (<see cref="BaseRssProvider"/>'s internal statics, widened for this purpose) and
/// the same <see cref="INewsArticleRepository.UpsertAsync"/> three-tier dedup (Url -> GUID -> Hash)
/// everything else already goes through - a <see cref="FeedSource"/> just skips having its own
/// dedicated provider class, since it needs no publisher-specific spec-tolerance quirks.
/// </summary>
public sealed class DynamicFeedIngestionService : IDynamicFeedIngestionService
{
    public const string HttpClientName = "DynamicFeedClient";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IFeedSourceRepository _feedSourceRepository;
    private readonly INewsArticleRepository _articleRepository;
    private readonly ICrawlHistoryRepository _historyRepository;
    private readonly IRssRawResponseRepository _rawResponseRepository;
    private readonly IFeedErrorLogRepository _errorLogRepository;
    private readonly ILogger<DynamicFeedIngestionService> _logger;

    public DynamicFeedIngestionService(
        IHttpClientFactory httpClientFactory,
        IFeedSourceRepository feedSourceRepository,
        INewsArticleRepository articleRepository,
        ICrawlHistoryRepository historyRepository,
        IRssRawResponseRepository rawResponseRepository,
        IFeedErrorLogRepository errorLogRepository,
        ILogger<DynamicFeedIngestionService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _feedSourceRepository = feedSourceRepository;
        _articleRepository = articleRepository;
        _historyRepository = historyRepository;
        _rawResponseRepository = rawResponseRepository;
        _errorLogRepository = errorLogRepository;
        _logger = logger;
    }

    public async Task RunAsync(string feedSourceId, CancellationToken cancellationToken)
    {
        var feedSource = await _feedSourceRepository.GetByIdAsync(feedSourceId, cancellationToken);
        if (feedSource is null || !feedSource.IsActive)
        {
            _logger.LogWarning("FeedSource {FeedSourceId} not found or inactive - skipping", feedSourceId);
            return;
        }

        var startedOn = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        var history = new CrawlHistory { StartTime = startedOn, FeedCount = 1, Status = CrawlStatus.Running };
        var historyId = await _historyRepository.InsertAsync(history, cancellationToken);
        history.Id = historyId;

        _logger.LogInformation("[{RunId}] Started: {SourceCode}", history.Id, feedSource.SourceCode);

        // Per-feed timeout (FeedSource.TimeoutSeconds), layered on the caller's own cancellation -
        // a linked token so a slow/hanging feed can never block past its configured budget without
        // also respecting host shutdown.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(feedSource.TimeoutSeconds));
        var linkedToken = timeoutCts.Token;

        string? rawXml = null;
        int? httpStatusCode = null;
        var newItems = 0;
        var updatedItems = 0;
        var duplicateItems = 0;
        var totalItems = 0;

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(feedSource.FeedUrl, linkedToken);
            httpStatusCode = (int)response.StatusCode;
            response.EnsureSuccessStatusCode();

            rawXml = await response.Content.ReadAsStringAsync(linkedToken);
            var document = XDocument.Parse(rawXml);

            foreach (var item in document.Descendants("item"))
            {
                totalItems++;
                var article = await ParseItemAsync(item, feedSource, linkedToken);
                if (article is null)
                {
                    continue;
                }

                var outcome = await _articleRepository.UpsertAsync(article, linkedToken);
                switch (outcome)
                {
                    case ArticleUpsertOutcome.Inserted:
                        newItems++;
                        break;
                    case ArticleUpsertOutcome.Updated:
                        updatedItems++;
                        break;
                    default:
                        duplicateItems++;
                        break;
                }
            }

            await _rawResponseRepository.InsertAsync(
                new RssRawResponse
                {
                    Provider = feedSource.SourceCode,
                    FeedName = feedSource.FeedName,
                    FeedUrl = feedSource.FeedUrl,
                    FetchedAt = startedOn,
                    HttpStatusCode = httpStatusCode,
                    RawXml = rawXml,
                    ContentHash = BaseRssProvider.ComputeContentHash(rawXml),
                    ParseSucceeded = true,
                    ProcessingDurationMs = stopwatch.ElapsedMilliseconds,
                    CreatedAt = DateTimeOffset.UtcNow
                },
                cancellationToken);

            await _feedSourceRepository.UpdateLastFetchedOnAsync(feedSource.Id, DateTimeOffset.UtcNow, cancellationToken);

            history.EndTime = DateTimeOffset.UtcNow;
            history.Duration = stopwatch.Elapsed;
            history.NewArticles = newItems;
            history.UpdatedArticles = updatedItems;
            history.DuplicateArticles = duplicateItems;
            history.Status = CrawlStatus.Completed;
            await _historyRepository.UpdateAsync(history, cancellationToken);

            _logger.LogInformation(
                "[{RunId}] Completed: {SourceCode} - {Total} items, {New} new, {Updated} updated, {Duplicate} duplicate ({DurationMs}ms)",
                history.Id, feedSource.SourceCode, totalItems, newItems, updatedItems, duplicateItems, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Catches everything, including a linked-token timeout from the feed's own
            // TimeoutSeconds - never crashes the host; the next scheduled tick tries again.
            _logger.LogError(ex, "[{RunId}] Failed: {SourceCode} ({FeedUrl})", history.Id, feedSource.SourceCode, feedSource.FeedUrl);

            history.EndTime = DateTimeOffset.UtcNow;
            history.Duration = stopwatch.Elapsed;
            history.Status = CrawlStatus.Failed;
            history.Error = ex.Message;
            history.FailedFeeds = [feedSource.FeedName];
            await _historyRepository.UpdateAsync(history, cancellationToken);

            await _errorLogRepository.InsertAsync(
                new FeedErrorLog
                {
                    FeedSourceId = feedSource.Id,
                    OccurredOn = DateTimeOffset.UtcNow,
                    Exception = $"{ex.GetType().FullName}: {ex.Message}",
                    StackTrace = ex.StackTrace,
                    RetryCount = 0,
                    Resolved = false
                },
                cancellationToken);
        }
    }

    private async Task<NewsArticle?> ParseItemAsync(XElement item, FeedSource feedSource, CancellationToken cancellationToken)
    {
        var title = item.Element("title")?.Value.Trim();
        var link = item.Element("link")?.Value.Trim();

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link))
        {
            return null;
        }

        var guid = item.Element("guid")?.Value.Trim();
        var description = item.Element("description")?.Value.Trim();
        var author = item.Element("author")?.Value.Trim();
        var tags = item.Elements("category").Select(e => e.Value.Trim()).Where(t => t.Length > 0).ToList();

        var pubDateRaw = item.Elements()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, "pubDate", StringComparison.OrdinalIgnoreCase))?
            .Value;
        var publishedAt = BaseRssProvider.ParsePublishDate(pubDateRaw);

        var imageUrl = BaseRssProvider.ExtractImage(item)
            ?? await BaseRssProvider.TryExtractOgImageAsync(
                _httpClientFactory.CreateClient(HttpClientName), link, _logger, cancellationToken);

        var now = DateTimeOffset.UtcNow;

        return new NewsArticle
        {
            Provider = feedSource.SourceCode,
            FeedName = feedSource.FeedName,
            Category = feedSource.Category,
            Title = title,
            Summary = BaseRssProvider.StripHtml(description),
            Content = description,
            Url = link,
            OriginalGuid = string.IsNullOrWhiteSpace(guid) ? null : guid,
            Author = string.IsNullOrWhiteSpace(author) ? null : author,
            Language = feedSource.Language,
            ImageUrl = imageUrl,
            PublishedAt = publishedAt,
            CrawledAt = now,
            UpdatedAt = now,
            Tags = tags,
            Source = feedSource.FeedUrl,
            Hash = ArticleHasher.ComputeHash(title, publishedAt),
            IsActive = true
        };
    }
}
