using Hangfire;
using Hangfire.Storage;
using Application.Abstractions;
using Application.Models;

namespace Infrastructure.Scheduling;

public sealed class HangfireCrawlJobStatusReader : ICrawlJobStatusReader
{
    private readonly JobStorage _jobStorage;

    public HangfireCrawlJobStatusReader(JobStorage jobStorage)
    {
        _jobStorage = jobStorage;
    }

    public CrawlJobStatus? GetStatus(string providerName)
    {
        var jobId = HangfireJobIds.NewsCrawl(providerName);

        using var connection = _jobStorage.GetConnection();
        var recurringJob = connection.GetRecurringJobs(new[] { jobId }).SingleOrDefault();

        if (recurringJob is null)
        {
            return null;
        }

        string? lastErrorType = null;
        string? lastErrorMessage = null;

        if (recurringJob.LastJobState == "Failed" && !string.IsNullOrEmpty(recurringJob.LastJobId))
        {
            var details = _jobStorage.GetMonitoringApi().JobDetails(recurringJob.LastJobId);
            var failedState = details?.History?.FirstOrDefault(h => h.StateName == "Failed");
            if (failedState is not null)
            {
                failedState.Data.TryGetValue("ExceptionType", out lastErrorType);
                failedState.Data.TryGetValue("ExceptionMessage", out lastErrorMessage);
            }
        }
        else if (!string.IsNullOrEmpty(recurringJob.Error))
        {
            // A recurring-job-level error (e.g. an invalid cron expression) rather than a
            // failure of a specific run.
            lastErrorMessage = recurringJob.Error;
        }

        return new CrawlJobStatus(
            JobId: jobId,
            Provider: providerName,
            Cron: recurringJob.Cron,
            TimeZone: recurringJob.TimeZoneId,
            NextExecution: ToUtcOffset(recurringJob.NextExecution),
            LastExecution: ToUtcOffset(recurringJob.LastExecution),
            LastJobId: recurringJob.LastJobId,
            LastJobState: recurringJob.LastJobState,
            LastErrorType: lastErrorType,
            LastErrorMessage: lastErrorMessage);
    }

    private static DateTimeOffset? ToUtcOffset(DateTime? value) =>
        value.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)) : null;
}
