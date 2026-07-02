using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Application.Abstractions;
using Application.Models;
using Application.Options;

namespace Infrastructure.RssProviders;

/// <summary>
/// Shared download/parse/normalize pipeline for RSS 2.0 feeds. Concrete providers
/// (<see cref="AajTakRssProvider"/> today, ANI/NDTV/PIB/etc. in later phases) only need to
/// supply <see cref="Name"/> and an <see cref="IHttpClientFactory"/> client name - everything
/// else (XML parsing, image extraction, tag extraction, per-feed error isolation) is common.
/// </summary>
public abstract partial class BaseRssProvider : IRssProvider
{
    private static readonly XNamespace Media = "http://search.yahoo.com/mrss/";
    private static readonly XNamespace Content = "http://purl.org/rss/1.0/modules/content/";
    private static readonly XNamespace DublinCore = "http://purl.org/dc/elements/1.1/";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;

    protected BaseRssProvider(IHttpClientFactory httpClientFactory, ILogger logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public abstract string Name { get; }

    /// <summary>Named <see cref="HttpClient"/> registered for this provider in DI.</summary>
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
            response.EnsureSuccessStatusCode();

            rawXml = await response.Content.ReadAsStringAsync(cancellationToken);
            var document = XDocument.Parse(rawXml);

            var articles = new List<NormalizedArticle>();
            foreach (var item in document.Descendants("item"))
            {
                var article = await ParseItemAsync(item, feed, cancellationToken);
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
                ContentHash = ComputeContentHash(rawXml),
                ProcessingDurationMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Catches everything, including a TaskCanceledException from HttpClient's own
            // per-request Timeout - that is a dead/hanging feed, not our caller asking to stop,
            // and must never crash the host. Only lets an exception through uncaught when our
            // own cancellationToken was actually the one that fired (real shutdown/cancellation).
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

    private static string ComputeContentHash(string rawXml) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawXml))).ToLowerInvariant();

    private async Task<NormalizedArticle?> ParseItemAsync(XElement item, RssFeedOptions feed, CancellationToken cancellationToken)
    {
        var title = item.Element("title")?.Value.Trim();
        var link = item.Element("link")?.Value.Trim();

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link))
        {
            return null;
        }

        var guid = item.Element("guid")?.Value.Trim();
        var description = item.Element("description")?.Value.Trim();
        var encodedContent = item.Element(Content + "encoded")?.Value.Trim();
        var author = item.Element("author")?.Value.Trim() ?? item.Element(DublinCore + "creator")?.Value.Trim();
        var tags = item.Elements("category").Select(e => e.Value.Trim()).Where(t => t.Length > 0).ToList();

        // Case-insensitive lookup: Zee News emits lowercase <pubdate>, AajTak/ABP the spec's <pubDate>.
        var pubDateRaw = item.Elements()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, "pubDate", StringComparison.OrdinalIgnoreCase))?
            .Value;
        var publishedAt = ParsePublishDate(pubDateRaw);
        var imageUrl = ExtractImage(item) ?? await TryExtractOgImageAsync(link, cancellationToken);

        return new NormalizedArticle
        {
            Provider = Name,
            FeedName = feed.Name,
            Category = feed.Category,
            Title = title,
            Summary = StripHtml(description),
            Content = encodedContent ?? description,
            Url = link,
            OriginalGuid = string.IsNullOrWhiteSpace(guid) ? null : guid,
            Author = string.IsNullOrWhiteSpace(author) ? null : author,
            Language = feed.Language,
            ImageUrl = imageUrl,
            PublishedAt = publishedAt,
            Tags = tags,
            Source = feed.Url
        };
    }

    private static string? ExtractImage(XElement item)
    {
        var mediaContent = item.Elements(Media + "content")
            .FirstOrDefault(e => (string?)e.Attribute("url") is not null);
        if (mediaContent is not null)
        {
            return (string?)mediaContent.Attribute("url");
        }

        var mediaThumbnail = item.Element(Media + "thumbnail");
        if (mediaThumbnail is not null)
        {
            var url = (string?)mediaThumbnail.Attribute("url");
            if (!string.IsNullOrWhiteSpace(url))
            {
                return url;
            }
        }

        var enclosure = item.Elements("enclosure")
            .FirstOrDefault(e => ((string?)e.Attribute("type"))?.StartsWith("image", StringComparison.OrdinalIgnoreCase) == true);
        if (enclosure is not null)
        {
            return (string?)enclosure.Attribute("url");
        }

        return null;
    }

    private async Task<string?> TryExtractOgImageAsync(string articleUrl, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(articleUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var match = OgImageRegex().Match(html);
            return match.Success ? match.Groups["url"].Value : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "og:image fallback lookup failed for {Url}", articleUrl);
            return null;
        }
    }

    private static DateTimeOffset? ParsePublishDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        if (DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        // Zee News emits a nonstandard "Thursday, July 02, 2026, 14:08 GMT +5:30" - drop the
        // literal "GMT" (leaving the trailing numeric offset) and pad the offset's hour to two
        // digits ("+5:30" -> "+05:30") so the standard parser accepts it. The Week emits
        // "Thu May 28 21:09:41 IST 2026" (Java's Date.toString() format) - .NET's parser doesn't
        // recognize the "IST" zone abbreviation at all (only "GMT"/"UTC"/numeric offsets), so it's
        // replaced with its fixed numeric offset - unambiguously India Standard Time for every
        // provider in this app, not Israel/Ireland's IST.
        var cleaned = trimmed.Replace("GMT", string.Empty, StringComparison.OrdinalIgnoreCase);
        cleaned = IstAbbreviationRegex().Replace(cleaned, " +05:30 ");
        cleaned = WhitespaceRegex().Replace(cleaned, " ").Trim();
        cleaned = SingleDigitUtcOffsetRegex().Replace(cleaned, "${sign}0${hour}:");

        if (DateTimeOffset.TryParse(cleaned, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
        {
            return parsed;
        }

        // Java's Date.toString() token order ("ddd MMM d HH:mm:ss zzz yyyy" - weekday, month, day,
        // time, offset, *then* year) isn't one .NET's parser accepts at all, regardless of the
        // zone-abbreviation fix above, so it's reordered into "d MMM yyyy HH:mm:ss zzz" - which is.
        var reorderMatch = JavaDateOrderRegex().Match(cleaned);
        if (!reorderMatch.Success)
        {
            return null;
        }

        var reordered =
            $"{reorderMatch.Groups["day"].Value} {reorderMatch.Groups["month"].Value} {reorderMatch.Groups["year"].Value} " +
            $"{reorderMatch.Groups["time"].Value} {reorderMatch.Groups["offset"].Value}";

        return DateTimeOffset.TryParse(reordered, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)
            ? parsed
            : null;
    }

    private static string? StripHtml(string? html) =>
        string.IsNullOrWhiteSpace(html) ? null : HtmlTagRegex().Replace(html, string.Empty).Trim();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"(?<sign>[+-])(?<hour>\d):")]
    private static partial Regex SingleDigitUtcOffsetRegex();

    [GeneratedRegex(@"\bIST\b")]
    private static partial Regex IstAbbreviationRegex();

    [GeneratedRegex(
        @"^\w{3}\s+(?<month>\w{3})\s+(?<day>\d{1,2})\s+(?<time>\d{2}:\d{2}:\d{2})\s+(?<offset>[+-]\d{2}:\d{2})\s+(?<year>\d{4})$")]
    private static partial Regex JavaDateOrderRegex();

    [GeneratedRegex(
        """<meta[^>]+property=["']og:image["'][^>]+content=["'](?<url>[^"']+)["']""",
        RegexOptions.IgnoreCase)]
    private static partial Regex OgImageRegex();
}
