namespace Application.Options;

/// <summary>
/// Root configuration section ("NewsCrawler") controlling the crawl scheduler and providers.
/// </summary>
public sealed class NewsCrawlerOptions
{
    public const string SectionName = "NewsCrawler";

    public bool Enabled { get; set; } = true;

    /// <summary>Max number of articles persisted per feed, per run.</summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>Name of the distributed lock used to prevent overlapping/concurrent crawl runs.</summary>
    public string LockName { get; set; } = "news-crawler";

    /// <summary>How long a crawl lock is held before it is considered stale and reclaimable.</summary>
    public TimeSpan LockTtl { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Per-feed HTTP timeout.</summary>
    public TimeSpan FeedTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long raw RSS responses (<c>RssRawResponses</c>) are kept - enforced both passively by
    /// a Mongo TTL index and actively by the scheduled job on <see cref="RawResponseCleanupCron"/>.
    /// </summary>
    public TimeSpan RawResponseRetention { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Standard 5-field cron expression for the recurring job that deletes raw responses older
    /// than <see cref="RawResponseRetention"/>. Default: every day at 05:00 UTC.
    /// </summary>
    public string RawResponseCleanupCron { get; set; } = "0 5 * * *";

    /// <summary>
    /// Master switch for persisting raw RSS responses to <c>RssRawResponses</c>. A provider only
    /// has its raw responses saved when this is true AND that provider's own
    /// <see cref="RssProviderOptions.SaveRawResponses"/> is also true.
    /// </summary>
    public bool SaveRawResponses { get; set; } = true;

    public List<RssProviderOptions> Providers { get; set; } = [];
}
