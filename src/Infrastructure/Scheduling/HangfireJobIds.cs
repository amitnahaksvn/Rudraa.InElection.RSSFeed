namespace Infrastructure.Scheduling;

/// <summary>
/// Single source of truth for the recurring-job-id naming convention, shared between whoever
/// registers a provider's job (<c>Worker</c>) and whoever triggers it on demand
/// (<see cref="HangfireCrawlJobTrigger"/>) so the two can never drift apart.
/// </summary>
public static class HangfireJobIds
{
    public static string NewsCrawl(string providerName) => $"news-crawl-{providerName}";

    public const string RawResponseCleanup = "cleanup-raw-responses";

    public const string ErrorNotificationDispatch = "dispatch-error-notifications";

    /// <summary>
    /// See <c>HangfireKeepAliveExecutor</c> - keeps a free-tier host from spinning down. Suffixed
    /// by <see cref="Application.Options.KeepAliveOptions.AppName"/> because, once more than one
    /// host points at the same shared Hangfire Mongo storage (WebRssFeed + WebApiFeed), a single
    /// fixed job id would have the second host's own registration silently overwrite the first
    /// host's - only one app's self-ping target would survive in storage, leaving the other
    /// permanently unregistered.
    /// </summary>
    public static string KeepAlivePing(string appName) => $"keep-alive-ping-{appName}";

    /// <summary>Job id for a <see cref="Domain.Entities.FeedSource"/>-driven feed, keyed by its own SourceCode (e.g. "dynamic-feed-PIB").</summary>
    public static string DynamicFeed(string sourceCode) => $"dynamic-feed-{sourceCode}";

    /// <summary>Job id for a JSON news-API provider (e.g. "news-api-NewsApiOrg").</summary>
    public static string NewsApi(string providerName) => $"news-api-{providerName}";

    /// <summary>
    /// Job id for a <see cref="Domain.Entities.SocialMediaSource"/>-driven channel, keyed by its
    /// own Mongo Id rather than a short code like <see cref="DynamicFeed"/> uses - a
    /// <see cref="Domain.Entities.SocialMediaSource"/> has no separate short-key field, only a
    /// display <c>Name</c> (not guaranteed unique or job-id-safe) and its own Id (guaranteed
    /// unique and stable).
    /// </summary>
    public static string SocialMedia(string sourceId) => $"social-media-{sourceId}";
}
