namespace Application.Options;

/// <summary>
/// Root configuration section ("Hangfire") controlling how <c>Web</c>'s Hangfire server itself is
/// tuned - which queues it pulls jobs from and how many it processes concurrently. Web is the only
/// host that calls <c>AddHangfireServer()</c> - it owns both HTTP serving and job execution.
///
/// This exists so a future deployment could split job processing into separate replica groups of
/// the same image, each scaled independently, by giving each group a different
/// <see cref="Queues"/> value via environment variable (e.g. one group with
/// <c>Hangfire__Queues__0=rss</c>, another with <c>Hangfire__Queues__0=api</c>) - a lever kept
/// available even though the current deployment runs everything as one free-tier-friendly process.
/// </summary>
public sealed class HangfireOptions
{
    public const string SectionName = "Hangfire";

    /// <summary>
    /// Queue names this server instance pulls jobs from, in priority order. Every recurring RSS
    /// crawl job is tagged <c>[Queue("rss")]</c> on <c>HangfireCrawlJobExecutor</c>, every JSON
    /// news-API fetch job <c>[Queue("api")]</c> on <c>HangfireNewsApiJobExecutor</c>, every Social
    /// pipeline poll job <c>[Queue("social")]</c> on <c>HangfireSocialMediaJobExecutor</c>;
    /// "default" is included so untagged jobs (e.g. the raw-response cleanup job) still run. All
    /// four are listed by default so a single instance processes everything out of the box; a
    /// deployment that wants these as independently scaled replica groups would set this to just
    /// <c>["rss"]</c> on one group, <c>["api"]</c> on another, <c>["social"]</c> on a third.
    /// </summary>
    public string[] Queues { get; set; } = ["rss", "api", "social", "default"];

    /// <summary>
    /// Concurrent jobs this server instance processes. Null keeps Hangfire's own default
    /// (<c>Environment.ProcessorCount * 5</c>).
    /// </summary>
    public int? WorkerCount { get; set; }
}
