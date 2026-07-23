using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// The full database-backed catalog record for one RSS or JSON-API provider - not just its
/// schedule (Enabled/Cron/TimeZone) but also the request-shape fields that used to live in
/// <c>NewsCrawler.appsettings.json</c>/<c>NewsApiCrawler</c> (<see cref="SaveRawResponses"/> for
/// RSS; <see cref="BaseUrl"/>/<see cref="AuthType"/>/<see cref="AuthParamName"/>/
/// <see cref="TimeoutSeconds"/> for JSON-API providers) - the JSON files and their
/// Country/Provider/Feed hierarchy have been retired entirely in favor of this collection plus
/// <see cref="CrawlCountry"/>/<see cref="CrawlFeed"/>. Fully editable from the Provider Management
/// page, no redeploy needed for any change.
///
/// <see cref="Country"/> is part of this record's own identity - the (Pipeline, Provider, Country)
/// triple is unique, not (Pipeline, Provider) alone. Several "global aggregator" providers (e.g.
/// SerpApiGoogleNews, NewsApiOrg, GDELT) are genuinely configured once per country, each with its
/// own Cron/Enabled/query-parameters - a provider's <see cref="Provider"/> name still matches the
/// <c>Name</c> exposed by its registered <c>IRssProvider</c>/<c>INewsApiProvider</c> C# class and
/// stays unique per pipeline, but the same class can be scheduled independently for more than one
/// country at once. Hangfire job ids and the crawl lock name are keyed on (Provider, Country)
/// together, not Provider alone, precisely so two country-schedules of the same provider can run
/// concurrently without starving each other - the same reasoning that already keeps different
/// providers from sharing one global lock.
/// </summary>
public sealed class ProviderSchedule
{
    public string Id { get; set; } = string.Empty;

    public CrawlPipeline Pipeline { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public string Cron { get; set; } = string.Empty;

    public string TimeZone { get; set; } = "UTC";

    /// <summary>RSS-only: per-provider half of the raw-response-save toggle (the other half, <c>NewsCrawlerOptions.SaveRawResponses</c>, stays a global scalar setting). Meaningless for API-pipeline rows.</summary>
    public bool SaveRawResponses { get; set; } = true;

    /// <summary>API-only: scheme+host, no trailing slash, e.g. "https://newsapi.org/v2". Null for RSS-pipeline rows.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>API-only. Null for RSS-pipeline rows.</summary>
    public ApiAuthType? AuthType { get; set; }

    /// <summary>API-only: query-string key or header name the API key is attached under. Null for RSS-pipeline rows.</summary>
    public string? AuthParamName { get; set; }

    /// <summary>API-only. Null for RSS-pipeline rows.</summary>
    public int? TimeoutSeconds { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
