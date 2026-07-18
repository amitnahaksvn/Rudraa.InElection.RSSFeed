namespace Application.Options;

/// <summary>
/// Configuration block for a single JSON news-API provider (NewsAPI.org, GNews, TheNewsAPI,
/// Currents, Mediastack, NewsData.io, WorldNewsAPI). Mirrors <see cref="RssProviderOptions"/>'s
/// role for RSS feeds: everything about the request - base URL, auth, and the list of endpoints
/// actually called - is data here, never hardcoded in a provider class. A provider can (and
/// usually does) expose several endpoints (search vs top-headlines, latest vs search, ...) - see
/// <see cref="Endpoints"/>, the <see cref="RssProviderOptions.Feeds"/> counterpart - all fetched
/// together on this provider's single <see cref="Cron"/>, one Hangfire job per provider (not per
/// endpoint), same as RSS.
/// </summary>
public sealed class NewsApiProviderOptions
{
    /// <summary>Must match the <c>Name</c> exposed by the corresponding <see cref="Abstractions.INewsApiProvider"/>.</summary>
    public string Name { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    /// <summary>This provider's own standard 5-field cron expression - independent of every other provider's schedule and every RSS provider's.</summary>
    public string Cron { get; set; } = string.Empty;

    /// <summary>Scheme+host, no trailing slash, e.g. "https://newsapi.org/v2".</summary>
    public string BaseUrl { get; set; } = string.Empty;

    public ApiAuthType AuthType { get; set; } = ApiAuthType.QueryParameter;

    /// <summary>Query-string key (when <see cref="AuthType"/> is <see cref="ApiAuthType.QueryParameter"/>) or header name (when <see cref="ApiAuthType.HttpHeader"/>) the API key is attached under.</summary>
    public string AuthParamName { get; set; } = "apiKey";

    public int TimeoutSeconds { get; set; } = 120;

    public List<NewsApiEndpointOptions> Endpoints { get; set; } = [];
}
