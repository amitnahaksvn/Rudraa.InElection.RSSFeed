using Application.Models;
using Domain.Entities;

namespace Application.Abstractions;

public interface IJobExecutionLogRepository
{
    Task InsertAsync(JobExecutionLog log, CancellationToken cancellationToken);

    Task UpdateAsync(JobExecutionLog log, CancellationToken cancellationToken);

    /// <summary>Newest-first, optionally narrowed by job id/status/date range - backs the job-report page.</summary>
    Task<IReadOnlyList<JobExecutionLog>> GetFilteredAsync(JobExecutionLogFilter filter, CancellationToken cancellationToken);

    Task EnsureIndexesAsync(CancellationToken cancellationToken);
}
