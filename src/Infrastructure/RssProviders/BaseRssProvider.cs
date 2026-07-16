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

    /// <summary>
    /// Builds the actual URL to fetch for a configured feed. Every existing provider leaves this
    /// as the identity (<see cref="RssFeedOptions.Url"/> is already the literal feed URL), but
    /// <see cref="GoogleNewsRssProvider"/> overrides it to treat <c>Url</c> as a search topic and
    /// build a Google News search-feed URL from it - letting a new topic be added purely via
    /// configuration (one list entry) rather than a hardcoded URL per topic.
    /// </summary>
    protected virtual string ResolveFeedUrl(RssFeedOptions feed) => feed.Url;

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
        var url = ResolveFeedUrl(feed);

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(url, cancellationToken);
            httpStatusCode = (int)response.StatusCode;
            // Body read before the status check throws, not after, so a non-2xx response's body
            // (an error page, a WAF block, a JSON error payload) is still captured for
            // diagnostics/the monitoring-alert email instead of being discarded.
            rawXml = await response.Content.ReadAsStringAsync(cancellationToken);
            response.EnsureSuccessStatusCode();

            var document = XDocument.Parse(rawXml);

            var articles = new List<NormalizedArticle>();
            foreach (var item in document.Descendants("item"))
            {
                var article = await ParseItemAsync(item, feed, url, cancellationToken);
                if (article is not null)
                {
                    articles.Add(article);
                }
            }

            return new FeedFetchResult
            {
                FeedName = feed.Name,
                FeedUrl = url,
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
            _logger.LogError(ex, "Failed to fetch/parse feed {Provider}/{Feed} ({Url})", Name, feed.Name, url);
            return new FeedFetchResult
            {
                FeedName = feed.Name,
                FeedUrl = url,
                Success = false,
                Error = ex.Message,
                ExceptionType = ex.GetType().FullName ?? ex.GetType().Name,
                StackTrace = ex.StackTrace,
                InnerException = ex.InnerException is { } inner ? $"{inner.GetType().FullName}: {inner.Message}" : null,
                FetchedAt = fetchedAt,
                HttpStatusCode = httpStatusCode,
                RawXml = rawXml,
                ContentHash = rawXml is not null ? ComputeContentHash(rawXml) : null,
                ProcessingDurationMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <summary>Widened to internal so <c>DynamicFeedIngestionService</c> (Mongo-driven feeds) reuses the exact same hashing, not a duplicate.</summary>
    internal static string ComputeContentHash(string rawXml) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawXml))).ToLowerInvariant();

    private async Task<NormalizedArticle?> ParseItemAsync(XElement item, RssFeedOptions feed, string feedUrl, CancellationToken cancellationToken)
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

        // Case-insensitive lookup: Zee News emits lowercase <pubdate>, AajTak/ABP the spec's
        // <pubDate>. RSS 1.0/RDF feeds (The Asahi Shimbun) have no <pubDate> at all and use
        // Dublin Core's <dc:date> instead - falls back to that only when no <pubDate>-named
        // element exists, so it never overrides a real <pubDate> when both happened to be present.
        var pubDateRaw = item.Elements()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, "pubDate", StringComparison.OrdinalIgnoreCase))?
            .Value
            ?? item.Element(DublinCore + "date")?.Value;
        var publishedAt = ParsePublishDate(pubDateRaw);
        var imageUrl = ExtractImage(item)
            ?? await TryExtractOgImageAsync(_httpClientFactory.CreateClient(HttpClientName), link, _logger, cancellationToken)
            ?? feed.DefaultImageUrl;

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
            Source = feedUrl
        };
    }

    /// <summary>Widened to internal so <c>DynamicFeedIngestionService</c> (Mongo-driven feeds) reuses the exact same extraction, not a duplicate.</summary>
    internal static string? ExtractImage(XElement item)
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

    /// <summary>
    /// Widened to internal-static (taking the caller's HttpClient/logger rather than instance
    /// fields) so <c>DynamicFeedIngestionService</c> (Mongo-driven feeds) reuses the exact same
    /// fallback, not a duplicate.
    /// </summary>
    internal static async Task<string?> TryExtractOgImageAsync(HttpClient client, string articleUrl, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
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
            logger.LogDebug(ex, "og:image fallback lookup failed for {Url}", articleUrl);
            return null;
        }
    }

    /// <summary>Widened to internal so <c>DynamicFeedIngestionService</c> (Mongo-driven feeds) reuses the exact same parsing, not a duplicate.</summary>
    // A feed's own pubDate carries whatever offset the publisher happened to report (IST, EDT,
    // GMT, ...) - DateTimeOffset.TryParse preserves that offset as-is rather than normalizing it,
    // so every successful parse below is explicitly converted with .ToUniversalTime() before
    // returning. The point in time is identical either way; this just makes what's actually stored
    // in NewsArticle.PublishedAt consistently UTC (Offset=00:00) like CrawledAt/UpdatedAt already
    // are, instead of varying per publisher's own reported zone.
    internal static DateTimeOffset? ParsePublishDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        if (DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed.ToUniversalTime();
        }

        // Zee News emits a nonstandard "Thursday, July 02, 2026, 14:08 GMT +5:30" - drop the
        // literal "GMT" (leaving the trailing numeric offset) and pad the offset's hour to two
        // digits ("+5:30" -> "+05:30") so the standard parser accepts it. The Week emits
        // "Thu May 28 21:09:41 IST 2026" (Java's Date.toString() format) - .NET's parser doesn't
        // recognize the "IST" zone abbreviation at all (only "GMT"/"UTC"/numeric offsets), so it's
        // replaced with its fixed numeric offset - unambiguously India Standard Time for every
        // provider in this app, not Israel/Ireland's IST. CBC (Canada) emits
        // "Wed, 24 Jun 2026 21:33:43 EDT" - same unrecognized-abbreviation problem, just North
        // American zones instead of IST; replaced with each abbreviation's own fixed numeric
        // offset (not DST-aware, but CBC's own abbreviation already encodes DST vs standard time,
        // so a fixed EDT->-04:00/EST->-05:00 style mapping is exact, not an approximation).
        var cleaned = trimmed.Replace("GMT", string.Empty, StringComparison.OrdinalIgnoreCase);
        cleaned = IstAbbreviationRegex().Replace(cleaned, " +05:30 ");
        cleaned = UsTimeZoneAbbreviationRegex().Replace(cleaned, match => $" {UsTimeZoneOffsets[match.Value.ToUpperInvariant()]} ");
        cleaned = WhitespaceRegex().Replace(cleaned, " ").Trim();
        cleaned = SingleDigitUtcOffsetRegex().Replace(cleaned, "${sign}0${hour}:");

        if (DateTimeOffset.TryParse(cleaned, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
        {
            return parsed.ToUniversalTime();
        }

        // Java's Date.toString() token order ("ddd MMM d HH:mm:ss zzz yyyy" - weekday, month, day,
        // time, offset, *then* year) isn't one .NET's parser accepts at all, regardless of the
        // zone-abbreviation fix above, so it's reordered into "d MMM yyyy HH:mm:ss zzz" - which is.
        // A non-match here isn't a dead end - it just means this specific tier doesn't apply,
        // falling through to the Hindi-month/NL Times tiers below rather than giving up immediately.
        var reorderMatch = JavaDateOrderRegex().Match(cleaned);
        if (reorderMatch.Success)
        {
            var reordered =
                $"{reorderMatch.Groups["day"].Value} {reorderMatch.Groups["month"].Value} {reorderMatch.Groups["year"].Value} " +
                $"{reorderMatch.Groups["time"].Value} {reorderMatch.Groups["offset"].Value}";

            if (DateTimeOffset.TryParse(reordered, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            {
                return parsed.ToUniversalTime();
            }
        }

        // MPInfo (Madhya Pradesh) emits a Hindi weekday name that its own feed generator has
        // double-HTML-escaped into literal, undecodable "&#2360;&#2379;..." text (a bug on their
        // side, not recoverable), followed by a *written-out Hindi month name* - e.g.
        // "&amp;#2360;&amp;#2379;&amp;#2350;&amp;#2357;&amp;#2366;&amp;#2352;, जुलाई 13, 2026,
        // 21:46 IST". None of .NET's parser tiers above recognize a Hindi month name (InvariantCulture
        // only knows English names), so the whole leading weekday token is discarded (it's redundant
        // once day/month/year are known) and the month name is mapped to its numeric equivalent
        // before falling through to the existing IST-abbreviation handling above. A feed with the
        // time genuinely omitted (MPInfo's own CM_News sub-feed truncates some items to just
        // "..., 2026,  " with nothing after) has nothing to recover and correctly falls through to
        // null. Matched against the original trimmed string, not the GMT/IST-substituted `cleaned`
        // above - that substitution already rewrote the trailing literal "IST" this regex looks for
        // into "+05:30", so matching against `cleaned` here would never find it.
        var hindiMonthMatch = HindiMonthDateRegex().Match(trimmed);
        if (hindiMonthMatch.Success && HindiMonths.TryGetValue(hindiMonthMatch.Groups["month"].Value, out var englishMonth))
        {
            var reconstructed =
                $"{hindiMonthMatch.Groups["day"].Value} {englishMonth} {hindiMonthMatch.Groups["year"].Value} " +
                $"{hindiMonthMatch.Groups["time"].Value} +05:30";

            if (DateTimeOffset.TryParse(reconstructed, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            {
                return parsed.ToUniversalTime();
            }
        }

        // NL Times (Netherlands) emits no offset/zone at all - "13 July 2026 - 21:10" - which
        // DateTimeStyles.None would otherwise silently interpret using whatever machine-local time
        // zone happens to be running this process (wrong, and non-deterministic across dev/prod).
        // Parsed as a plain local DateTime, then anchored to the Netherlands' own actual UTC offset
        // for that specific date via TimeZoneInfo (Amsterdam observes CET/CEST DST, unlike India's
        // fixed IST, so this can't be a single hardcoded constant the way the EDT/EST tier above is).
        var nlTimesMatch = NlTimesDateRegex().Match(trimmed);
        if (nlTimesMatch.Success &&
            DateTime.TryParseExact(
                $"{nlTimesMatch.Groups["day"].Value} {nlTimesMatch.Groups["month"].Value} {nlTimesMatch.Groups["year"].Value} {nlTimesMatch.Groups["time"].Value}",
                "d MMMM yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var nlLocal))
        {
            var offset = NetherlandsTimeZone?.GetUtcOffset(nlLocal) ?? TimeSpan.Zero;
            return new DateTimeOffset(nlLocal, offset).ToUniversalTime();
        }

        return null;
    }

    // Resolved once - null (falling back to a UTC offset above) only if the host's tzdata lacks
    // this id, same "don't crash startup/parsing over a missing tzdata entry" fallback already used
    // for RawResponseCleanupCron's Asia/Kolkata lookup.
    private static readonly TimeZoneInfo? NetherlandsTimeZone = ResolveNetherlandsTimeZone();

    private static TimeZoneInfo? ResolveNetherlandsTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam");
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return null;
        }
    }

    private static readonly Dictionary<string, string> HindiMonths = new()
    {
        ["जनवरी"] = "January",
        ["फरवरी"] = "February",
        ["मार्च"] = "March",
        ["अप्रैल"] = "April",
        ["मई"] = "May",
        ["जून"] = "June",
        ["जुलाई"] = "July",
        ["अगस्त"] = "August",
        ["सितंबर"] = "September",
        ["सितम्बर"] = "September",
        ["अक्टूबर"] = "October",
        ["नवंबर"] = "November",
        ["नवम्बर"] = "November",
        ["दिसंबर"] = "December",
        ["दिसम्बर"] = "December",
    };

    [GeneratedRegex(@"(?<month>जनवरी|फरवरी|मार्च|अप्रैल|मई|जून|जुलाई|अगस्त|सितंबर|सितम्बर|अक्टूबर|नवंबर|नवम्बर|दिसंबर|दिसम्बर)\s+(?<day>\d{1,2}),\s*(?<year>\d{4}),\s*(?<time>\d{1,2}:\d{2})\s*IST")]
    private static partial Regex HindiMonthDateRegex();

    [GeneratedRegex(@"^(?<day>\d{1,2})\s+(?<month>\w+)\s+(?<year>\d{4})\s*-\s*(?<time>\d{2}:\d{2})$")]
    private static partial Regex NlTimesDateRegex();

    /// <summary>Widened to internal so <c>DynamicFeedIngestionService</c> (Mongo-driven feeds) reuses the exact same stripping, not a duplicate.</summary>
    internal static string? StripHtml(string? html) =>
        string.IsNullOrWhiteSpace(html) ? null : HtmlTagRegex().Replace(html, string.Empty).Trim();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"(?<sign>[+-])(?<hour>\d):")]
    private static partial Regex SingleDigitUtcOffsetRegex();

    [GeneratedRegex(@"\bIST\b")]
    private static partial Regex IstAbbreviationRegex();

    private static readonly Dictionary<string, string> UsTimeZoneOffsets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["EDT"] = "-04:00",
        ["EST"] = "-05:00",
        ["CDT"] = "-05:00",
        ["CST"] = "-06:00",
        ["MDT"] = "-06:00",
        ["MST"] = "-07:00",
        ["PDT"] = "-07:00",
        ["PST"] = "-08:00",
    };

    [GeneratedRegex(@"\b(?:EDT|EST|CDT|CST|MDT|MST|PDT|PST)\b")]
    private static partial Regex UsTimeZoneAbbreviationRegex();

    [GeneratedRegex(
        @"^\w{3}\s+(?<month>\w{3})\s+(?<day>\d{1,2})\s+(?<time>\d{2}:\d{2}:\d{2})\s+(?<offset>[+-]\d{2}:\d{2})\s+(?<year>\d{4})$")]
    private static partial Regex JavaDateOrderRegex();

    [GeneratedRegex(
        """<meta[^>]+property=["']og:image["'][^>]+content=["'](?<url>[^"']+)["']""",
        RegexOptions.IgnoreCase)]
    private static partial Regex OgImageRegex();
}
