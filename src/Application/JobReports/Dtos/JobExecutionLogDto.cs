using Domain.Entities;

namespace Application.JobReports.Dtos;

/// <summary>Read projection of a <see cref="JobExecutionLog"/> record - backs the job-report page.</summary>
public sealed record JobExecutionLogDto(
    string Id,
    string JobId,
    string JobName,
    string? HangfireJobId,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    TimeSpan? Duration,
    string Status,
    string? ErrorMessage)
{
    public static JobExecutionLogDto FromDomain(JobExecutionLog log) => new(
        log.Id,
        log.JobId,
        log.JobName,
        log.HangfireJobId,
        log.StartedAt,
        log.CompletedAt,
        log.CompletedAt.HasValue ? log.CompletedAt.Value - log.StartedAt : null,
        log.Status.ToString(),
        log.ErrorMessage);
}
