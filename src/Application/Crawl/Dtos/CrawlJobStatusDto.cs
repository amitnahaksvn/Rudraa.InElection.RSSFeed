using Application.Models;

namespace Application.Crawl.Dtos;

/// <summary>What a provider's recurring crawl job is doing/has done, read straight from Hangfire.</summary>
public sealed record CrawlJobStatusDto(
    string JobId,
    string Provider,
    string Cron,
    string TimeZone,
    DateTimeOffset? NextExecution,
    DateTimeOffset? LastExecution,
    string? LastJobId,
    string? LastJobState,
    string? LastErrorType,
    string? LastErrorMessage)
{
    public static CrawlJobStatusDto FromModel(CrawlJobStatus status) => new(
        status.JobId,
        status.Provider,
        status.Cron,
        status.TimeZone,
        status.NextExecution,
        status.LastExecution,
        status.LastJobId,
        status.LastJobState,
        status.LastErrorType,
        status.LastErrorMessage);
}
