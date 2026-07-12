using Application.Abstractions;
using Domain.Entities;
using Domain.Enums;

namespace Application.Services;

/// <summary>
/// Shared start/complete bookkeeping for <see cref="JobExecutionLog"/>, used by every Hangfire
/// executor that has no other persisted execution record (the crawl/API/social pipelines already
/// get this via their own <c>CrawlHistory</c> row, written by their own orchestrator - wrapping
/// them here too would just be a second, redundant record of the same run).
/// </summary>
public sealed class JobExecutionLogger
{
    private readonly IJobExecutionLogRepository _repository;

    public JobExecutionLogger(IJobExecutionLogRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Records a Running row before <paramref name="action"/> starts, then Succeeded/Failed once it
    /// finishes - always updating CompletedAt/Status in a <c>finally</c> so a log entry never gets
    /// stuck showing "Running" forever, even if <paramref name="action"/> throws. The exception (if
    /// any) is rethrown after being recorded, so Hangfire's own dashboard/retry behavior for the job
    /// itself is completely unaffected by this wrapper.
    /// </summary>
    public async Task RunAsync(string jobId, string jobName, string? hangfireJobId, Func<Task> action, CancellationToken cancellationToken)
    {
        var log = new JobExecutionLog
        {
            JobId = jobId,
            JobName = jobName,
            HangfireJobId = hangfireJobId,
            StartedAt = DateTimeOffset.UtcNow,
            Status = JobExecutionStatus.Running
        };
        await _repository.InsertAsync(log, cancellationToken);

        try
        {
            await action();
            log.Status = JobExecutionStatus.Succeeded;
        }
        catch (Exception ex)
        {
            log.Status = JobExecutionStatus.Failed;
            log.ErrorMessage = ex.Message;
            throw;
        }
        finally
        {
            log.CompletedAt = DateTimeOffset.UtcNow;
            await _repository.UpdateAsync(log, cancellationToken);
        }
    }
}
