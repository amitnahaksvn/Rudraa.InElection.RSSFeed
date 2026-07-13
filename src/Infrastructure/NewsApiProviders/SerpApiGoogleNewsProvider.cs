using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Application.Models;
using Application.Options;

namespace Infrastructure.NewsApiProviders;

/// <summary>
/// SerpAPI's Google News engine (serpapi.com/search.json?engine=google_news) - query param
/// <c>api_key</c>. A paid scraping proxy for Google News search results (distinct from the
/// existing free, direct <c>GoogleNewsRssProvider</c> RSS feed - this one is JSON, costs per
/// search, and is registered under a different config <c>Name</c> so the two never collide).
/// Free tier: ~100 searches/month trial. https://serpapi.com/pricing
/// </summary>
public sealed class SerpApiGoogleNewsProvider : BaseNewsApiProvider
{
    public const string ProviderName = "SerpApiGoogleNews";

    public SerpApiGoogleNewsProvider(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<SerpApiGoogleNewsProvider> logger)
        : base(httpClientFactory, configuration, logger)
    {
    }

    public override string Name => ProviderName;

    protected override IReadOnlyList<NormalizedArticle> ParseArticles(string json, NewsApiEndpointOptions endpoint)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("news_results", out var resultsElement))
        {
            return [];
        }

        var articles = new List<NormalizedArticle>();
        foreach (var item in resultsElement.EnumerateArray())
        {
            var title = item.GetStringOrNull("title");
            var url = item.GetStringOrNull("link");
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            var sourceName = item.TryGetProperty("source", out var source) ? source.GetStringOrNull("name") : null;

            articles.Add(new NormalizedArticle
            {
                Provider = Name,
                FeedName = sourceName ?? Name,
                Category = endpoint.Category,
                Title = title,
                Summary = item.GetStringOrNull("snippet"),
                Url = url,
                Language = endpoint.Language,
                ImageUrl = item.GetStringOrNull("thumbnail"),
                PublishedAt = ParseRelativeOrAbsoluteDate(item.GetStringOrNull("date")),
                Source = sourceName ?? "Google News (via SerpAPI)"
            });
        }

        return articles;
    }

    /// <summary>SerpAPI's Google News "date" field is often a relative string ("2 hours ago") rather than a timestamp - only the absolute-date case is parseable; relative strings correctly fall through to null (CrawledAt still records when this crawler saw it). .ToUniversalTime() so a successful parse is consistently UTC (Offset=00:00).</summary>
    private static DateTimeOffset? ParseRelativeOrAbsoluteDate(string? raw) =>
        !string.IsNullOrWhiteSpace(raw) && DateTimeOffset.TryParse(raw, out var parsed) ? parsed.ToUniversalTime() : null;
}
