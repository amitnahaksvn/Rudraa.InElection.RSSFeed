namespace Domain.Entities;

/// <summary>
/// A single captured failure from anywhere in the app - an RSS/API crawl, dynamic feed ingestion,
/// or an unhandled HTTP request exception - persisted immediately instead of emailed immediately.
/// <c>Application.Services.ErrorNotificationDispatchService</c> (its own Hangfire recurring job)
/// batches every row with <see cref="IsSent"/> still false into one summary email on its own
/// schedule, then flips <see cref="IsSent"/>/<see cref="SentOn"/> - so a burst of failures produces
/// one readable email instead of one per error, and nothing is ever lost even if the email pipeline
/// itself is briefly down (it just gets picked up on the next dispatch tick).
/// </summary>
public sealed class ErrorLog
{
    public string Id { get; set; } = string.Empty;

    public string ExceptionType { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? StackTrace { get; set; }

    /// <summary>Type + message of the innermost exception, formatted as one string; null when there was no inner exception.</summary>
    public string? InnerException { get; set; }

    /// <summary>What was being attempted, e.g. "RSS Feed Fetch", "News API Fetch", "Dynamic Feed Fetch", "Crawl Run", "HTTP Request".</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Custom/app-specific error code, e.g. an HTTP status code as text or a provider-specific code. Null when not applicable.</summary>
    public string? ErrorCode { get; set; }

    /// <summary>Populated only for errors raised while handling an inbound HTTP request (see <c>ProblemDetailsExceptionHandler</c>); null for background-job failures.</summary>
    public string? RequestPath { get; set; }

    public string? HttpMethod { get; set; }

    public string? QueryString { get; set; }

    /// <summary>Sanitize before persisting - never store credentials/tokens/PII here.</summary>
    public string? RequestBody { get; set; }

    /// <summary>Reserved for when this app gains authenticated users - always null today.</summary>
    public string? UserId { get; set; }

    /// <summary>Reserved for when this app gains authenticated users - always null today.</summary>
    public string? UserName { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string Environment { get; set; } = string.Empty;

    public string ApplicationName { get; set; } = string.Empty;

    public string? ServiceName { get; set; }

    public string? MachineName { get; set; }

    public string? AssemblyVersion { get; set; }

    /// <summary>Distributed-tracing id - <c>Activity.Current?.Id</c> for background work, <c>HttpContext.TraceIdentifier</c> for requests.</summary>
    public string? TraceId { get; set; }

    /// <summary>The <c>CrawlHistory</c> run id this error occurred within, when applicable.</summary>
    public string? CorrelationId { get; set; }

    public string? HangfireJobId { get; set; }

    /// <summary>Provider key, e.g. "AajTak"/"NewsApiOrg". Null for errors not tied to a specific provider.</summary>
    public string? Provider { get; set; }

    /// <summary>Feed name (RSS) or endpoint name (JSON API).</summary>
    public string? FeedOrApiName { get; set; }

    /// <summary>Country the failing feed's publisher is based in - lets a batch of pending errors be scanned by country at a glance, not just by provider name.</summary>
    public string? Country { get; set; }

    public string? SourceUrl { get; set; }

    public int? HttpStatusCode { get; set; }

    public string? ResponseBody { get; set; }

    public TimeSpan? ExecutionDuration { get; set; }

    /// <summary>Free-form JSON for anything not covered by a named field above.</summary>
    public string? AdditionalData { get; set; }

    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;

    public bool IsSent { get; set; }

    public DateTimeOffset? SentOn { get; set; }

    /// <summary>Manually acknowledged via the error-monitor UI/API - independent of <see cref="IsSent"/>, which only tracks whether the batch-email pipeline has picked this row up.</summary>
    public bool IsResolved { get; set; }

    public DateTimeOffset? ResolvedOn { get; set; }

    /// <summary>Audit trail for this row - one entry per resolved/unresolved toggle (comment required) or standalone comment (status left unchanged), newest last. See <see cref="ErrorLogHistoryEntry"/>.</summary>
    public List<ErrorLogHistoryEntry> History { get; set; } = [];
}
