namespace Infrastructure.Scheduling;

/// <summary>
/// Single source of truth for the recurring-job-id naming convention, shared between whoever
/// registers a provider's job (<c>HangfireRecurringJobRegistrar</c>) and whoever triggers it on
/// demand (<see cref="HangfireCrawlJobTrigger"/>) so the two can never drift apart.
/// </summary>
public static class HangfireJobIds
{
    /// <summary>
    /// A "::" separator, not "-" or ":" - provider names are fixed C# class names, but country
    /// names are user-editable (see <c>UpsertCountryCommand</c>), so a distinctive separator rules
    /// out a future collision between two different (provider, country) pairs (e.g. a country
    /// someone names "Foo-Bar" colliding with provider "Foo" + country "Bar").
    /// </summary>
    private const string Separator = "::";

    public static string NewsCrawl(string providerName, string country) => $"news-crawl-{providerName}{Separator}{country}";

    /// <summary>The pre-per-country job id format, kept only so callers can recognize/sweep a job registered before this provider was split across countries.</summary>
    public static string LegacyNewsCrawl(string providerName) => $"news-crawl-{providerName}";

    /// <summary>Job id for the recurring job that batches unsent <c>ErrorLog</c> rows into a summary email.</summary>
    public const string ErrorNotificationDispatch = "dispatch-error-notifications";

    /// <summary>Job id for a <see cref="Domain.Entities.FeedSource"/>-driven feed, keyed by its own SourceCode (e.g. "dynamic-feed-PIB").</summary>
    public static string DynamicFeed(string sourceCode) => $"dynamic-feed-{sourceCode}";

    /// <summary>Job id for a JSON news-API provider-country schedule (e.g. "news-api-SerpApiGoogleNews::India").</summary>
    public static string NewsApi(string providerName, string country) => $"news-api-{providerName}{Separator}{country}";

    /// <summary>The pre-per-country job id format, kept only so callers can recognize/sweep a job registered before this provider was split across countries.</summary>
    public static string LegacyNewsApi(string providerName) => $"news-api-{providerName}";

    /// <summary>
    /// Job id for a <see cref="Domain.Entities.SocialMediaSource"/>-driven channel, keyed by its
    /// own Mongo Id rather than a short code like <see cref="DynamicFeed"/> uses - a
    /// <see cref="Domain.Entities.SocialMediaSource"/> has no separate short-key field, only a
    /// display <c>Name</c> (not guaranteed unique or job-id-safe) and its own Id (guaranteed
    /// unique and stable).
    /// </summary>
    public static string SocialMedia(string sourceId) => $"social-media-{sourceId}";
}
