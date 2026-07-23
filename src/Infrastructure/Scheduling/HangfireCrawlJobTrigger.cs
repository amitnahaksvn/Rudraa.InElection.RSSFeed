using Hangfire;
using Microsoft.Extensions.Caching.Memory;
using Application.Abstractions;
using Domain.Enums;

namespace Infrastructure.Scheduling;

public sealed class HangfireCrawlJobTrigger : ICrawlJobTrigger
{
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly IMemoryCache _cache;

    public HangfireCrawlJobTrigger(IRecurringJobManager recurringJobManager, IMemoryCache cache)
    {
        _recurringJobManager = recurringJobManager;
        _cache = cache;
    }

    public string TriggerNow(CrawlPipeline pipeline, string providerName, string country)
    {
        var jobId = BuildJobId(pipeline, providerName, country);
        _recurringJobManager.Trigger(jobId);
        return jobId;
    }

    public string CreateOrUpdate(CrawlPipeline pipeline, string providerName, string country, string cronExpression, string timeZoneId)
    {
        var jobId = BuildJobId(pipeline, providerName, country);
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var options = new RecurringJobOptions { TimeZone = timeZone };

        // Fixed to always crawl exactly this one, already-validated provider-country schedule
        // through the pipeline's own executor - never a caller-supplied method/class - so this can
        // never become an arbitrary-code-execution API.
        if (pipeline == CrawlPipeline.Api)
        {
            _recurringJobManager.AddOrUpdate<HangfireNewsApiJobExecutor>(
                jobId, executor => executor.RunAsync(providerName, country, null!, CancellationToken.None), cronExpression, options);
        }
        else
        {
            _recurringJobManager.AddOrUpdate<HangfireCrawlJobExecutor>(
                jobId, executor => executor.RunAsync(providerName, country, null!, CancellationToken.None), cronExpression, options);
        }

        InvalidateStatusCache(pipeline, providerName, country);
        return jobId;
    }

    public void Remove(CrawlPipeline pipeline, string providerName, string country)
    {
        _recurringJobManager.RemoveIfExists(BuildJobId(pipeline, providerName, country));
        InvalidateStatusCache(pipeline, providerName, country);
    }

    // HangfireCrawlJobStatusReader caches a provider-country's status (including "no job
    // registered") for a short window to keep the crawl-report page fast - without this, a status
    // read shortly after this class enables/disables a job could serve that cache's stale
    // pre-change answer for up to its own CacheDuration, which would make a just-applied schedule
    // edit look like it hadn't taken effect.
    private void InvalidateStatusCache(CrawlPipeline pipeline, string providerName, string country) =>
        _cache.Remove(HangfireCrawlJobStatusReader.CacheKey(pipeline, providerName, country));

    private static string BuildJobId(CrawlPipeline pipeline, string providerName, string country) =>
        pipeline == CrawlPipeline.Api ? HangfireJobIds.NewsApi(providerName, country) : HangfireJobIds.NewsCrawl(providerName, country);
}
