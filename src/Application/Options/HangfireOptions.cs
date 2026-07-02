namespace Application.Options;

/// <summary>
/// Root configuration section ("Hangfire") controlling how Worker's Hangfire server itself is
/// tuned - which queues it pulls jobs from and how many it processes concurrently. Only Worker
/// reads this (it's the only host that calls <c>AddHangfireServer()</c>); Web's Hangfire
/// registration is storage-only, for the dashboard.
///
/// This exists so a production deployment can run separate replica groups of the exact same
/// Worker image, each scaled independently, by giving each group a different <see cref="Queues"/>
/// value via environment variable (e.g. one group with <c>Hangfire__Queues__0=rss</c>, another
/// with <c>Hangfire__Queues__0=api</c>) rather than needing a second service/codebase.
/// </summary>
public sealed class HangfireOptions
{
    public const string SectionName = "Hangfire";

    /// <summary>
    /// Queue names this server instance pulls jobs from, in priority order. Every recurring RSS
    /// crawl job is tagged <c>[Queue("rss")]</c> on <c>HangfireCrawlJobExecutor</c>; "default" is
    /// included so untagged jobs (e.g. the raw-response cleanup job) still run on a single-queue
    /// deployment. A deployment that wants to run RSS crawling and (future) news-API fetching as
    /// independently scaled replica groups would set this to just <c>["rss"]</c> on one group and
    /// <c>["api"]</c> on another.
    /// </summary>
    public string[] Queues { get; set; } = ["rss", "default"];

    /// <summary>
    /// Concurrent jobs this server instance processes. Null keeps Hangfire's own default
    /// (<c>Environment.ProcessorCount * 5</c>).
    /// </summary>
    public int? WorkerCount { get; set; }
}
