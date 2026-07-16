using System.Diagnostics;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Application.Abstractions;
using Application.Models;
using Application.Options;

namespace Infrastructure.RssProviders;

/// <summary>
/// Shared Atom 1.0 parsing pipeline for QuintType-CMS-hosted publishers (The Quint, Free Press
/// Journal, National Herald all serve identical &lt;entry&gt;/&lt;published&gt;/&lt;id&gt;/
/// &lt;link href="..." rel="alternate"&gt; Atom feeds from the same
/// prod-qt-images.s3.amazonaws.com/production/{site}/feed.xml pattern - not RSS 2.0) - deliberately
/// NOT a <see cref="BaseRssProvider"/> subclass, same reasoning as <see cref="YouTubeRssProvider"/>:
/// a different element vocabulary throughout, not a spec-tolerance quirk that fits the RSS 2.0
/// pipeline. Unlike YouTube's entries (which carry their own media:thumbnail), these entries have
/// no image tag at all, so every article's image comes from the og:image HTML fallback -
/// <see cref="BaseRssProvider.TryExtractOgImageAsync"/> is reused here rather than duplicated,
/// since it's already provider-agnostic (just fetches a URL and reads a meta tag).
/// </summary>
public abstract class BaseAtomRssProvider : IRssProvider
{
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;

    protected BaseAtomRssProvider(IHttpClientFactory httpClientFactory, ILogger logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public abstract string Name { get; }

    protected abstract string HttpClientName { get; }

    public async Task<IReadOnlyList<FeedFetchResult>> FetchAllFeedsAsync(
        IReadOnlyList<RssFeedOptions> feeds,
        CancellationToken cancellationToken)
    {
        var results = new List<FeedFetchResult>(feeds.Count);

        foreach (var feed in feeds)
        {
            results.Add(await FetchFeedAsync(feed, cancellationToken));
        }

        return results;
    }

    private async Task<FeedFetchResult> FetchFeedAsync(RssFeedOptions feed, CancellationToken cancellationToken)
    {
        var fetchedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        string? rawXml = null;
        int? httpStatusCode = null;

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(feed.Url, cancellationToken);
            httpStatusCode = (int)response.StatusCode;
            // Body read before the status check throws, not after - same reasoning as
            // BaseRssProvider.FetchFeedAsync: a non-2xx response's body is still captured for
            // diagnostics/the error log instead of being discarded.
            rawXml = await response.Content.ReadAsStringAsync(cancellationToken);
            response.EnsureSuccessStatusCode();

            var document = XDocument.Parse(rawXml);
            var articles = new List<NormalizedArticle>();
            foreach (var entry in document.Descendants(Atom + "entry"))
            {
                var article = await ParseEntryAsync(entry, feed, cancellationToken);
                if (article is not null)
                {
                    articles.Add(article);
                }
            }

            return new FeedFetchResult
            {
                FeedName = feed.Name,
                FeedUrl = feed.Url,
                Success = true,
                Articles = articles,
                FetchedAt = fetchedAt,
                HttpStatusCode = httpStatusCode,
                RawXml = rawXml,
                ContentHash = BaseRssProvider.ComputeContentHash(rawXml),
                ProcessingDurationMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Same reasoning as BaseRssProvider.FetchFeedAsync: catches everything, including a
            // dead/hanging feed's own HttpClient timeout, without ever crashing the host.
            _logger.LogError(ex, "Failed to fetch/parse feed {Provider}/{Feed} ({Url})", Name, feed.Name, feed.Url);
            return new FeedFetchResult
            {
                FeedName = feed.Name,
                FeedUrl = feed.Url,
                Success = false,
                Error = ex.Message,
                FetchedAt = fetchedAt,
                HttpStatusCode = httpStatusCode,
                RawXml = rawXml,
                ContentHash = rawXml is not null ? BaseRssProvider.ComputeContentHash(rawXml) : null,
                ProcessingDurationMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    private async Task<NormalizedArticle?> ParseEntryAsync(XElement entry, RssFeedOptions feed, CancellationToken cancellationToken)
    {
        var title = entry.Element(Atom + "title")?.Value.Trim();
        var link = entry.Elements(Atom + "link")
            .FirstOrDefault(e => (string?)e.Attribute("rel") is null or "alternate")?
            .Attribute("href")?.Value;

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link))
        {
            return null;
        }

        var id = entry.Element(Atom + "id")?.Value.Trim();
        var author = entry.Element(Atom + "author")?.Element(Atom + "name")?.Value.Trim();
        var summary = entry.Element(Atom + "summary")?.Value.Trim();
        var publishedRaw = entry.Element(Atom + "published")?.Value ?? entry.Element(Atom + "updated")?.Value;
        // .ToUniversalTime() so PublishedAt is consistently UTC (Offset=00:00) regardless of
        // whatever offset the feed's own <published>/<updated> timestamp carries.
        var publishedAt = DateTimeOffset.TryParse(publishedRaw, out var parsed) ? parsed.ToUniversalTime() : (DateTimeOffset?)null;
        var tags = entry.Elements(Atom + "category")
            .Select(c => c.Attribute("term")?.Value)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t!)
            .ToList();

        var imageUrl = await BaseRssProvider.TryExtractOgImageAsync(
            _httpClientFactory.CreateClient(HttpClientName), link, _logger, cancellationToken)
            ?? feed.DefaultImageUrl;

        return new NormalizedArticle
        {
            Provider = Name,
            FeedName = feed.Name,
            Category = feed.Category,
            Title = title,
            Summary = summary,
            Content = summary,
            Url = link,
            OriginalGuid = id,
            Author = author,
            Language = feed.Language,
            ImageUrl = imageUrl,
            PublishedAt = publishedAt,
            Tags = tags,
            Source = feed.Url
        };
    }
}
