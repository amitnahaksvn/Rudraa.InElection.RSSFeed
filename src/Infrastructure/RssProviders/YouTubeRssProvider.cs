using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Application.Abstractions;
using Application.Models;
using Application.Options;

namespace Infrastructure.RssProviders;

/// <summary>
/// YouTube channel RSS integration - deliberately NOT a <see cref="BaseRssProvider"/> subclass,
/// because YouTube serves Atom 1.0 (&lt;entry&gt;/&lt;published&gt;/&lt;id&gt;/
/// &lt;link href="..."&gt;), not RSS 2.0 (&lt;item&gt;/&lt;pubDate&gt;/&lt;guid&gt;/
/// &lt;link&gt;text&lt;/link&gt;) - different element names and structure throughout, not a
/// BaseRssProvider-style parsing quirk that fits the shared pipeline.
///
/// A configured feed's <see cref="RssFeedOptions.Url"/> is the full
/// <c>https://www.youtube.com/feeds/videos.xml?channel_id={id}</c> URL for that channel - channel
/// ids are opaque (<c>UC...</c>) and have to be resolved once (from the channel page's own
/// <c>"externalId"</c> JSON field, or YouTube's search results) before adding a config entry;
/// from then on, adding another channel is purely a configuration change - one new
/// <c>Feeds</c> list entry - same as every other provider.
///
/// Each video is normalized into the same <see cref="NormalizedArticle"/> shape everything else
/// uses (Content/Summary from the video's description, ImageUrl from its thumbnail, Url the watch
/// page) so it flows through the existing dedup/persistence/API pipeline unchanged - there is no
/// separate "video" concept in the domain model.
/// </summary>
public sealed class YouTubeRssProvider : IRssProvider
{
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace Media = "http://search.yahoo.com/mrss/";
    private static readonly XNamespace Yt = "http://www.youtube.com/xml/schemas/2015";

    public const string ProviderName = "YouTube";
    public const string ClientName = "YouTubeRssClient";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<YouTubeRssProvider> _logger;

    public YouTubeRssProvider(IHttpClientFactory httpClientFactory, ILogger<YouTubeRssProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string Name => ProviderName;

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
            var client = _httpClientFactory.CreateClient(ClientName);
            using var response = await client.GetAsync(feed.Url, cancellationToken);
            httpStatusCode = (int)response.StatusCode;
            // Body read before the status check throws, not after, so a non-2xx response's body
            // is still captured for diagnostics/the monitoring-alert email instead of being
            // discarded - same reasoning as BaseRssProvider.FetchFeedAsync.
            rawXml = await response.Content.ReadAsStringAsync(cancellationToken);
            response.EnsureSuccessStatusCode();

            var document = XDocument.Parse(rawXml);

            var articles = document.Descendants(Atom + "entry")
                .Select(entry => ParseEntry(entry, feed))
                .Where(article => article is not null)
                .Select(article => article!)
                .ToList();

            return new FeedFetchResult
            {
                FeedName = feed.Name,
                FeedUrl = feed.Url,
                Success = true,
                Articles = articles,
                FetchedAt = fetchedAt,
                HttpStatusCode = httpStatusCode,
                RawXml = rawXml,
                ContentHash = ComputeContentHash(rawXml),
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
                ContentHash = rawXml is not null ? ComputeContentHash(rawXml) : null,
                ProcessingDurationMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    private static NormalizedArticle? ParseEntry(XElement entry, RssFeedOptions feed)
    {
        var title = entry.Element(Atom + "title")?.Value.Trim();
        var link = entry.Elements(Atom + "link")
            .FirstOrDefault(e => (string?)e.Attribute("rel") is null or "alternate")?
            .Attribute("href")?.Value;

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link))
        {
            return null;
        }

        var videoId = entry.Element(Yt + "videoId")?.Value.Trim();
        var author = entry.Element(Atom + "author")?.Element(Atom + "name")?.Value.Trim();
        // .ToUniversalTime() so PublishedAt is consistently UTC (Offset=00:00).
        var publishedAt = DateTimeOffset.TryParse(entry.Element(Atom + "published")?.Value, out var parsed)
            ? parsed.ToUniversalTime()
            : (DateTimeOffset?)null;

        var mediaGroup = entry.Element(Media + "group");
        var description = mediaGroup?.Element(Media + "description")?.Value.Trim();
        var thumbnail = mediaGroup?.Element(Media + "thumbnail")?.Attribute("url")?.Value;

        return new NormalizedArticle
        {
            Provider = ProviderName,
            FeedName = feed.Name,
            Category = feed.Category,
            Title = title,
            Summary = Truncate(description, 500),
            Content = description,
            Url = link,
            OriginalGuid = videoId,
            Author = author,
            Language = feed.Language,
            ImageUrl = thumbnail,
            PublishedAt = publishedAt,
            Tags = ["video"],
            Source = feed.Url
        };
    }

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength];

    private static string ComputeContentHash(string rawXml) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawXml))).ToLowerInvariant();
}
