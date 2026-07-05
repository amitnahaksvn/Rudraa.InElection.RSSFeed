using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Application.Models;
using Application.Options;

namespace Infrastructure.NewsApiProviders;

/// <summary>
/// YouTube Data API v3 keyword search (www.googleapis.com/youtube/v3/search) - query param key.
/// A genuinely different capability from the existing <c>YouTubeRssProvider</c>: that one reads
/// one already-known channel's own Atom feed (no key needed); this one searches across all of
/// YouTube by keyword (<c>type=video</c>), so it belongs in the JSON-API pipeline instead of the
/// RSS one. Each result's <c>id.videoId</c> becomes the watch-page URL, matching how
/// <c>YouTubeRssProvider</c> normalizes a channel video - including the <c>"video"</c> tag - so
/// both pipelines produce the same shape for a video result. This environment's egress policy
/// blocks googleapis.com outright, so - same "best-effort, confirm once enabled" caveat as
/// <see cref="ApContentApiProvider"/> - field names should be re-checked against a live response
/// before relying on this in production. Free tier: 10,000 quota units/day (a search call costs
/// 100 units, so ~100 searches/day) - https://developers.google.com/youtube/v3/getting-started.
/// </summary>
public sealed class YouTubeDataApiProvider : BaseNewsApiProvider
{
    public const string ProviderName = "YouTubeDataApi";

    public YouTubeDataApiProvider(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<YouTubeDataApiProvider> logger)
        : base(httpClientFactory, configuration, logger)
    {
    }

    public override string Name => ProviderName;

    protected override IReadOnlyList<NormalizedArticle> ParseArticles(string json, NewsApiEndpointOptions endpoint)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("items", out var itemsElement) ||
            itemsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var articles = new List<NormalizedArticle>();
        foreach (var item in itemsElement.EnumerateArray())
        {
            var videoId = item.TryGetProperty("id", out var idElement) ? idElement.GetStringOrNull("videoId") : null;
            var snippet = item.TryGetProperty("snippet", out var snippetElement) ? snippetElement : default;
            var title = snippet.ValueKind == JsonValueKind.Object ? snippet.GetStringOrNull("title") : null;
            if (string.IsNullOrWhiteSpace(videoId) || string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var channelTitle = snippet.GetStringOrNull("channelTitle");
            var thumbnails = snippet.TryGetProperty("thumbnails", out var thumbnailsElement) ? thumbnailsElement : default;
            var thumbnail = thumbnails.ValueKind == JsonValueKind.Object && thumbnails.TryGetProperty("high", out var highThumbnail)
                ? highThumbnail.GetStringOrNull("url")
                : null;

            articles.Add(new NormalizedArticle
            {
                Provider = Name,
                FeedName = channelTitle ?? Name,
                Category = endpoint.Category,
                Title = title,
                Summary = snippet.GetStringOrNull("description"),
                Url = $"https://www.youtube.com/watch?v={videoId}",
                OriginalGuid = videoId,
                Language = endpoint.Language,
                ImageUrl = thumbnail,
                PublishedAt = snippet.GetDateTimeOrNull("publishedAt"),
                Tags = ["video"],
                Source = channelTitle ?? "YouTube"
            });
        }

        return articles;
    }
}
