namespace Application.Options;

/// <summary>
/// Root configuration section ("Hangfire") controlling how a host's own Hangfire server is tuned -
/// which queues it pulls jobs from and how many it processes concurrently. WebRssFeed and
/// WebApiFeed each bind their own instance of this and each supplies its own fallback default for
/// <see cref="Queues"/> in its own <c>Program.cs</c> if unconfigured (WebRssFeed:
/// keepalive/rss/default; WebApiFeed: keepalive/api/social) - <em>not</em> a shared default on
/// this class, since a shared "process everything" default would be actively wrong for either
/// host once split (see <see cref="Queues"/>'s own doc comment for why it can't default to a
/// non-empty array here at all).
/// </summary>
public sealed class HangfireOptions
{
    public const string SectionName = "Hangfire";

    /// <summary>
    /// Queue names this server instance pulls jobs from, in priority order - Hangfire always
    /// drains an earlier-listed queue before touching a later one, on every fetch cycle. Every
    /// recurring RSS crawl job is tagged <c>[Queue("rss")]</c> on <c>HangfireCrawlJobExecutor</c>,
    /// every JSON news-API fetch job <c>[Queue("api")]</c> on <c>HangfireNewsApiJobExecutor</c>,
    /// every Social pipeline poll job <c>[Queue("social")]</c> on
    /// <c>HangfireSocialMediaJobExecutor</c>; "default" is for untagged jobs (e.g. the
    /// raw-response cleanup and error-notification-dispatch jobs, WebRssFeed's own).
    ///
    /// "keepalive" should always be listed first, ahead of every content-crawling queue, and is
    /// exactly one job: <c>HangfireKeepAliveExecutor</c>'s self-ping, tagged <c>[Queue("keepalive")]</c>.
    /// This isn't just tidiness - a burst of many freshly-due rss/api crawl jobs (the normal shape
    /// right after any wake-up, when many providers' crons have all come due at once) can occupy a
    /// small WorkerCount for minutes; if keep-alive shared a later queue's priority, it could be
    /// starved long enough to miss its own deadline, letting a free-tier host spin back down
    /// despite the self-ping technically running successfully every time it does get a turn -
    /// confirmed happening in production before this queue was split out. Giving it its own
    /// always-drained-first queue means it can never be delayed by however much crawl volume is
    /// queued behind it, regardless of WorkerCount.
    ///
    /// Deliberately defaults to an empty array here, <em>not</em> a hardcoded queue list: .NET's
    /// <see cref="Microsoft.Extensions.Configuration.ConfigurationBinder"/> APPENDS a config
    /// section's array items after whatever a target array property is already initialized to,
    /// rather than replacing it - confirmed directly (a `new HangfireOptions()` default of
    /// `["keepalive","rss","api","social","default"]` bound against WebApiFeed's own configured
    /// `["keepalive","api","social"]` resolved to all 8 entries concatenated, silently re-adding
    /// "rss" to a host that must never process it). Each host's own `Program.cs` supplies its own
    /// safe fallback explicitly, after binding, instead of relying on a property initializer here.
    /// </summary>
    public string[] Queues { get; set; } = [];

    /// <summary>
    /// Concurrent jobs this server instance processes. Null keeps Hangfire's own default
    /// (<c>Environment.ProcessorCount * 5</c>).
    /// </summary>
    public int? WorkerCount { get; set; }
}
