using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Application.Abstractions;
using Application.Models;
using Domain.Entities;
using Domain.Enums;

namespace Application.Services;

/// <summary>
/// Downloads, deduplicates, and persists new posts for one <see cref="SocialMediaSource"/> - the
/// Social pipeline's counterpart to <c>DynamicFeedIngestionService</c> (which lives in
/// Infrastructure instead, since it needs <c>BaseRssProvider</c>'s parsing helpers directly). This
/// one belongs in Application because it only ever talks to abstractions - the actual per-platform
/// HTTP/XML/JSON work lives behind <see cref="ISocialPlatformFetcher"/>, implemented in
/// Infrastructure - which lets it reuse <see cref="ArticlePersister"/> the same way
/// <c>NewsCrawlerOrchestrator</c>/<c>NewsApiCrawlerOrchestrator</c> already do, rather than
/// duplicating the dedup/upsert-counting loop a third time.
/// </summary>
public sealed class SocialMediaIngestionService : ISocialMediaIngestionService
{
    private readonly ISocialMediaSourceRepository _sourceRepository;
    private readonly IEnumerable<ISocialPlatformFetcher> _fetchers;
    private readonly INewsArticleRepository _articleRepository;
    private readonly ICrawlHistoryRepository _historyRepository;
    private readonly IErrorLogRepository _errorLogRepository;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<SocialMediaIngestionService> _logger;

    public SocialMediaIngestionService(
        ISocialMediaSourceRepository sourceRepository,
        IEnumerable<ISocialPlatformFetcher> fetchers,
        INewsArticleRepository articleRepository,
        ICrawlHistoryRepository historyRepository,
        IErrorLogRepository errorLogRepository,
        IHostEnvironment hostEnvironment,
        ILogger<SocialMediaIngestionService> logger)
    {
        _sourceRepository = sourceRepository;
        _fetchers = fetchers;
        _articleRepository = articleRepository;
        _historyRepository = historyRepository;
        _errorLogRepository = errorLogRepository;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task RunAsync(string socialMediaSourceId, CancellationToken cancellationToken)
    {
        var source = await _sourceRepository.GetByIdAsync(socialMediaSourceId, cancellationToken);
        if (source is null || !source.Enabled)
        {
            _logger.LogWarning("SocialMediaSource {SocialMediaSourceId} not found or disabled - skipping", socialMediaSourceId);
            return;
        }

        var fetcher = _fetchers.FirstOrDefault(f => f.Platform == source.Platform);
        if (fetcher is null)
        {
            // Not an error - Platform is a recognized enum value (Facebook/Telegram/Website/Rss)
            // with no fetcher wired up yet, same "not implemented" state every other unwired
            // option in this codebase already has (e.g. Telegram's Bot API, documented in CLAUDE.md).
            _logger.LogWarning(
                "No ISocialPlatformFetcher registered for platform {Platform} - skipping SocialMediaSource '{Name}'",
                source.Platform, source.Name);
            return;
        }

        var history = new CrawlHistory { StartTime = DateTimeOffset.UtcNow, FeedCount = 1, Status = CrawlStatus.Running };
        history.Id = await _historyRepository.InsertAsync(history, cancellationToken);

        _logger.LogInformation("[{RunId}] Started: {Platform}/{Name}", history.Id, source.Platform, source.Name);

        // Per-source timeout (SocialMediaSource.TimeoutSeconds), layered on the caller's own
        // cancellation - same reasoning as every other per-source/per-feed fetch in this codebase.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(source.TimeoutSeconds));

        try
        {
            var articles = await fetcher.FetchAsync(source, timeoutCts.Token);

            var (inserted, updated, duplicates) = await ArticlePersister.PersistAsync(
                _articleRepository,
                articles.Select(a => a with { Country = source.Country }),
                _logger,
                cancellationToken);

            await _sourceRepository.UpdateLastPolledAtAsync(source.Id, DateTimeOffset.UtcNow, cancellationToken);

            history.EndTime = DateTimeOffset.UtcNow;
            history.Duration = history.EndTime - history.StartTime;
            history.NewArticles = inserted;
            history.UpdatedArticles = updated;
            history.DuplicateArticles = duplicates;
            history.Status = CrawlStatus.Completed;
            await _historyRepository.UpdateAsync(history, cancellationToken);

            _logger.LogInformation(
                "[{RunId}] Completed: {Platform}/{Name} - {New} new, {Updated} updated, {Duplicate} duplicate ({Duration})",
                history.Id, source.Platform, source.Name, inserted, updated, duplicates, history.Duration);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Catches everything, including a linked-token timeout from the source's own
            // TimeoutSeconds - never crashes the host; the next scheduled tick tries again.
            _logger.LogError(ex, "[{RunId}] Failed: {Platform}/{Name}", history.Id, source.Platform, source.Name);

            history.EndTime = DateTimeOffset.UtcNow;
            history.Duration = history.EndTime - history.StartTime;
            history.Status = CrawlStatus.Failed;
            history.Error = ex.Message;
            history.FailedFeeds = [source.Name];
            await _historyRepository.UpdateAsync(history, cancellationToken);

            await ErrorLogRecorder.RecordAsync(
                _errorLogRepository,
                ErrorNotification.FromException(
                    ex,
                    _hostEnvironment.EnvironmentName,
                    _hostEnvironment.ApplicationName,
                    operation: "Social Media Fetch",
                    provider: source.Platform.ToString(),
                    feedOrApiName: source.Name,
                    country: source.Country,
                    sourceUrl: source.Url,
                    correlationId: history.Id,
                    hangfireJobId: ExecutionContextAccessor.CurrentHangfireJobId,
                    executionDuration: history.Duration),
                _logger,
                cancellationToken);
        }
    }
}
