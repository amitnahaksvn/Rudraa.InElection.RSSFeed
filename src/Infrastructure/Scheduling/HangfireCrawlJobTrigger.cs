using Hangfire;
using Application.Abstractions;

namespace Infrastructure.Scheduling;

public sealed class HangfireCrawlJobTrigger : ICrawlJobTrigger
{
    private readonly IRecurringJobManager _recurringJobManager;

    public HangfireCrawlJobTrigger(IRecurringJobManager recurringJobManager)
    {
        _recurringJobManager = recurringJobManager;
    }

    public string TriggerNow(string providerName)
    {
        var jobId = HangfireJobIds.NewsCrawl(providerName);
        _recurringJobManager.Trigger(jobId);
        return jobId;
    }

    public string CreateOrUpdate(string providerName, string cronExpression, string timeZoneId)
    {
        var jobId = HangfireJobIds.NewsCrawl(providerName);
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        // Fixed to always crawl exactly this one, already-validated provider through
        // HangfireCrawlJobExecutor - never a caller-supplied method/class - so this can never
        // become an arbitrary-code-execution API.
        _recurringJobManager.AddOrUpdate<HangfireCrawlJobExecutor>(
            jobId,
            executor => executor.RunAsync(providerName, null!, CancellationToken.None),
            cronExpression,
            new RecurringJobOptions { TimeZone = timeZone });

        return jobId;
    }
}
