using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Application.Abstractions;
using Application.Models;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.RssProviders;

namespace Infrastructure.Social;

/// <summary>
/// Fetches one YouTube channel's Atom feed (<c>youtube.com/feeds/videos.xml?channel_id=...</c>)
/// for a Mongo-driven <see cref="SocialMediaSource"/> - the DB-driven counterpart to the
/// file-configured <see cref="YouTubeRssProvider"/>. Deliberately a separate class rather than
/// reusing <see cref="YouTubeRssProvider"/> directly: that one's per-entry parser is tightly typed
/// to <c>RssFeedOptions</c> (name/category/language from a config feed entry), whereas this reads
/// the same fields off a <see cref="SocialMediaSource"/> document instead - same Atom shape
/// (entry/published/id/link/media:group), same reasoning for why it isn't RSS 2.0 either, just a
/// different config source. Reuses <see cref="YouTubeRssProvider.ClientName"/>'s already-registered
/// HttpClient rather than adding a second one for the exact same target domain.
/// </summary>
public sealed class YouTubeChannelFetcher : ISocialPlatformFetcher
{
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace Media = "http://search.yahoo.com/mrss/";
    private static readonly XNamespace Yt = "http://www.youtube.com/xml/schemas/2015";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<YouTubeChannelFetcher> _logger;

    public YouTubeChannelFetcher(IHttpClientFactory httpClientFactory, ILogger<YouTubeChannelFetcher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public SocialPlatform Platform => SocialPlatform.YouTube;

    public async Task<IReadOnlyList<NormalizedArticle>> FetchAsync(SocialMediaSource source, CancellationToken cancellationToken)
    {
        var feedUrl = $"https://www.youtube.com/feeds/videos.xml?channel_id={Uri.EscapeDataString(source.Identifier)}";

        var client = _httpClientFactory.CreateClient(YouTubeRssProvider.ClientName);
        using var response = await client.GetAsync(feedUrl, cancellationToken);
        // Body read before the status check throws, not after, so a non-2xx response's body is
        // still captured (via the exception's Data / logs) for diagnostics instead of being
        // discarded - same reasoning as every other fetch in this codebase.
        var rawXml = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        var document = XDocument.Parse(rawXml);

        return document.Descendants(Atom + "entry")
            .Select(entry => ParseEntry(entry, source, feedUrl))
            .Where(article => article is not null)
            .Select(article => article!)
            .ToList();
    }

    private static NormalizedArticle? ParseEntry(XElement entry, SocialMediaSource source, string feedUrl)
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
        var publishedAt = DateTimeOffset.TryParse(entry.Element(Atom + "published")?.Value, out var parsed)
            ? parsed
            : (DateTimeOffset?)null;

        var mediaGroup = entry.Element(Media + "group");
        var description = mediaGroup?.Element(Media + "description")?.Value.Trim();
        var thumbnail = mediaGroup?.Element(Media + "thumbnail")?.Attribute("url")?.Value;

        return new NormalizedArticle
        {
            Provider = source.Platform.ToString(),
            FeedName = source.Name,
            Category = source.Category,
            Title = title,
            Summary = Truncate(description, 500),
            Content = description,
            Url = link,
            OriginalGuid = videoId,
            Author = author,
            Language = source.Language,
            Country = source.Country,
            ImageUrl = thumbnail,
            PublishedAt = publishedAt,
            Tags = ["video"],
            Source = feedUrl
        };
    }

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength];
}
