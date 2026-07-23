using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Database-backed record of one RSS feed or JSON-API endpoint belonging to a provider - the sole
/// replacement for <c>RssFeedOptions</c>/<c>NewsApiEndpointOptions</c> from the retired
/// <c>NewsCrawler.appsettings.json</c>/<c>NewsApiCrawler</c> JSON files. Unlike
/// <see cref="ProviderSchedule"/>/<see cref="CrawlCountry"/> (identified by a natural
/// Pipeline+name key), a feed has no reliable natural key of its own - two feeds under the same
/// provider can share a Name, and Url alone isn't guaranteed unique either - so <see cref="Id"/>
/// is this record's real identity for create/update/delete. <see cref="Provider"/> plus
/// <see cref="Country"/> together (matching <see cref="ProviderSchedule.Provider"/>/
/// <see cref="ProviderSchedule.Country"/>) are how it's grouped under its parent schedule - a
/// provider configured once per country (e.g. SerpApiGoogleNews) has a disjoint set of feeds per
/// country, not one shared list.
/// </summary>
public sealed class CrawlFeed
{
    public string Id { get; set; } = string.Empty;

    public CrawlPipeline Pipeline { get; set; }

    public string Provider { get; set; } = string.Empty;

    /// <summary>Which provider-schedule this feed belongs to, matching <see cref="ProviderSchedule.Country"/> - see this entity's own doc comment.</summary>
    public string Country { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>RSS: the feed's full URL. API: the path appended to the provider's own BaseUrl (e.g. "everything").</summary>
    public string Url { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Language { get; set; } = "hi";

    public bool Enabled { get; set; } = true;

    /// <summary>RSS-only: fallback image URL used when an item carries no image of its own. Null for API-pipeline rows.</summary>
    public string? DefaultImageUrl { get; set; }

    /// <summary>API-only: every query parameter this endpoint's request needs besides the API key itself. Null for RSS-pipeline rows.</summary>
    public Dictionary<string, string>? QueryParameters { get; set; }
}
