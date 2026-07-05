using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Application.Models;
using Application.Options;

namespace Infrastructure.NewsApiProviders;

/// <summary>
/// Polygon.io ticker news (api.polygon.io/v2/reference/news) - query param apiKey. Response root
/// is <c>{"results":[...], "status":"OK", "count":..., "next_url":...}</c>, per Polygon's own
/// published API reference; this environment's egress policy blocks api.polygon.io outright (a
/// network-layer 403 from the session's own proxy, not from Polygon), so - same "best-effort,
/// confirm once enabled" caveat as <see cref="ApContentApiProvider"/>/<see cref="DataGovInProvider"/>
/// - field names should be re-checked against a live response before relying on this in
/// production. Free tier: 5 requests/minute, end-of-day data only.
/// </summary>
public sealed class PolygonIoProvider : BaseNewsApiProvider
{
    public const string ProviderName = "PolygonIo";

    public PolygonIoProvider(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<PolygonIoProvider> logger)
        : base(httpClientFactory, configuration, logger)
    {
    }

    public override string Name => ProviderName;

    protected override IReadOnlyList<NormalizedArticle> ParseArticles(string json, NewsApiEndpointOptions endpoint)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("results", out var resultsElement) ||
            resultsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var articles = new List<NormalizedArticle>();
        foreach (var item in resultsElement.EnumerateArray())
        {
            var title = item.GetStringOrNull("title");
            var url = item.GetStringOrNull("article_url");
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            var publisher = item.TryGetProperty("publisher", out var publisherElement) ? publisherElement : default;
            var publisherName = publisher.ValueKind == JsonValueKind.Object ? publisherElement.GetStringOrNull("name") : null;

            articles.Add(new NormalizedArticle
            {
                Provider = Name,
                FeedName = publisherName ?? Name,
                Category = endpoint.Category,
                Title = title,
                Summary = item.GetStringOrNull("description"),
                Url = url,
                OriginalGuid = item.GetStringOrNull("id"),
                Author = item.GetStringOrNull("author"),
                Language = endpoint.Language,
                ImageUrl = item.GetStringOrNull("image_url"),
                PublishedAt = item.GetDateTimeOrNull("published_utc"),
                Source = publisherName ?? "Polygon.io"
            });
        }

        return articles;
    }
}
