namespace Application.Options;

/// <summary>
/// Root configuration section ("KeepAlive") controlling the self-ping recurring job
/// (<c>Infrastructure.Scheduling.HangfireKeepAliveExecutor</c>) that keeps a free-tier host (e.g.
/// Render, whose free Web Service spins down after ~15 minutes with no inbound HTTP traffic) from
/// sleeping, without needing a third-party uptime monitor configured by hand.
/// </summary>
public sealed class KeepAliveOptions
{
    public const string SectionName = "KeepAlive";

    /// <summary>
    /// Short, stable identifier for this host (e.g. "rss", "api") - suffixed onto the recurring
    /// job's id (see <c>Infrastructure.Scheduling.HangfireJobIds.KeepAlivePing</c>) so two hosts
    /// sharing one Hangfire Mongo storage each get their own self-ping job instead of the second
    /// host's registration silently overwriting the first's under one fixed id. Must be set (and
    /// must differ) per host once more than one host shares the same storage.
    /// </summary>
    public string AppName { get; set; } = "default";

    /// <summary>Every minute by default - comfortably under Render's free-tier ~15-minute inactivity spin-down window, with large margin.</summary>
    public string Cron { get; set; } = "* * * * *";

    /// <summary>
    /// Explicit override for the full URL to ping (e.g. "https://myapp.onrender.com/alive"). Left
    /// unset by default: the job instead falls back to Render's own <c>RENDER_EXTERNAL_URL</c>
    /// environment variable (set automatically on every Render web service, no configuration
    /// needed there) plus "/alive". Neither is set outside Render (local dev, docker-compose, the
    /// Aspire AppHost), so the job safely no-ops rather than pinging a meaningless localhost URL.
    /// </summary>
    public string? PingUrl { get; set; }
}
