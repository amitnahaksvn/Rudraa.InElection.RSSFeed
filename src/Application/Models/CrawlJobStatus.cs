namespace Application.Models;

/// <summary>Point-in-time snapshot of a provider's recurring crawl job, read straight from Hangfire.</summary>
public sealed record CrawlJobStatus(
    string JobId,
    string Provider,
    string Cron,
    string TimeZone,
    DateTimeOffset? NextExecution,
    DateTimeOffset? LastExecution,
    string? LastJobId,
    string? LastJobState,
    string? LastErrorType,
    string? LastErrorMessage);
