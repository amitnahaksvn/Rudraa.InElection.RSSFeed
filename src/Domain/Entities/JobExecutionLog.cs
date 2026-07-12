using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// One execution of a generic (non-crawl) recurring Hangfire job - the keep-alive self-ping, the
/// raw-response cleanup, the error-notification dispatch. Deliberately separate from
/// <see cref="CrawlHistory"/>: that collection already records RSS/API/Social crawl runs in detail
/// (providers, article counts, failed feeds) via each orchestrator itself, so duplicating that
/// here would just be a second, redundant record of the same event. This collection exists purely
/// for jobs that had no persisted execution record anywhere before it.
/// </summary>
public sealed class JobExecutionLog
{
    public string Id { get; set; } = string.Empty;

    /// <summary>The Hangfire recurring-job id, e.g. "keep-alive-ping" - see <c>Infrastructure.Scheduling.HangfireJobIds</c>.</summary>
    public string JobId { get; set; } = string.Empty;

    /// <summary>Human-readable name for display, e.g. "Keep-alive self-ping".</summary>
    public string JobName { get; set; } = string.Empty;

    /// <summary>The specific Hangfire background job execution's own id, for cross-referencing against the Hangfire dashboard if it's enabled.</summary>
    public string? HangfireJobId { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    /// <summary>Null while the job is still running.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    public JobExecutionStatus Status { get; set; } = JobExecutionStatus.Running;

    /// <summary>Populated only when Status is Failed.</summary>
    public string? ErrorMessage { get; set; }
}
