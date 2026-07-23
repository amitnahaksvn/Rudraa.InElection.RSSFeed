using System.Collections.Concurrent;
using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.Caching.Memory;
using Application.Abstractions;
using Application.Models;
using Domain.Enums;

namespace Infrastructure.Scheduling;

public sealed class HangfireCrawlJobStatusReader : ICrawlJobStatusReader
{
    // Same concurrency this codebase already settled on for the identical problem shape in
    // HangfireRecurringJobRegistrar (see its own doc comment) - one Hangfire/Mongo round trip per
    // job id is unavoidable (StorageConnectionExtensions.GetRecurringJobs iterates ids one at a
    // time internally, calling GetAllEntriesFromHash - and, for a failed last run,
    // JobDetails - even when given every id in a single call), so the only lever is running many
    // of those round trips concurrently instead of sequentially.
    private const int LookupConcurrency = 64;

    // How long one provider-country's schedule snapshot is trusted before re-fetching. A recurring
    // job's Cron/NextExecution/LastExecution/LastJobState change at most once per that schedule's
    // own cron tick (the shortest configured is every few minutes) - caching for this short a
    // window keeps the crawl-report page feeling instant on a second load (tab switch, date-range
    // tweak) without ever showing meaningfully stale schedule data.
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(20);

    private readonly JobStorage _jobStorage;
    private readonly IMemoryCache _cache;

    /// <summary>See HangfireRecurringJobRegistrar's identical static constructor for why this exists: Parallel.ForEach dispatches blocking calls onto the CLR ThreadPool, which only grows slowly under its own throttled "hill-climbing" algorithm when starved - raising the floor once, up front, means the concurrency this class asks for is actually available rather than queuing behind a cold pool.</summary>
    static HangfireCrawlJobStatusReader()
    {
        ThreadPool.GetMinThreads(out _, out var completionPortMin);
        ThreadPool.SetMinThreads(LookupConcurrency * 2, completionPortMin);
    }

    public HangfireCrawlJobStatusReader(JobStorage jobStorage, IMemoryCache cache)
    {
        _jobStorage = jobStorage;
        _cache = cache;
    }

    public CrawlJobStatus? GetStatus(CrawlPipeline pipeline, string providerName, string country) =>
        GetStatuses(pipeline, [(providerName, country)]).GetValueOrDefault((providerName, country));

    /// <summary>
    /// A naive per-provider loop here measured ~58-65s end to end against this app's 236 RSS
    /// providers (network round trips to a remote Atlas cluster, not CPU) - unacceptable for a
    /// page a person is actively waiting on. Fanning the same per-provider-country lookup out
    /// across <see cref="LookupConcurrency"/> concurrent connections, plus the short cache above,
    /// cut that down to roughly a couple of seconds on a cold cache and near-instant on a warm one.
    /// </summary>
    public IReadOnlyDictionary<(string Provider, string Country), CrawlJobStatus> GetStatuses(
        CrawlPipeline pipeline, IReadOnlyCollection<(string Provider, string Country)> providerCountries)
    {
        if (providerCountries.Count == 0)
        {
            return new Dictionary<(string, string), CrawlJobStatus>();
        }

        var result = new ConcurrentDictionary<(string, string), CrawlJobStatus>();
        var uncached = new List<(string Provider, string Country)>();

        foreach (var providerCountry in providerCountries)
        {
            if (_cache.TryGetValue(CacheKey(pipeline, providerCountry.Provider, providerCountry.Country), out CacheEntry entry))
            {
                if (entry.Status is not null)
                {
                    result[providerCountry] = entry.Status;
                }
            }
            else
            {
                uncached.Add(providerCountry);
            }
        }

        Parallel.ForEach(uncached, new ParallelOptions { MaxDegreeOfParallelism = LookupConcurrency }, providerCountry =>
        {
            var status = GetStatusUncached(pipeline, providerCountry.Provider, providerCountry.Country);
            _cache.Set(CacheKey(pipeline, providerCountry.Provider, providerCountry.Country), new CacheEntry(status), CacheDuration);
            if (status is not null)
            {
                result[providerCountry] = status;
            }
        });

        return result;
    }

    /// <summary>Internal (not private) so <see cref="HangfireCrawlJobTrigger"/> can evict a provider-country's entry the moment it changes that job - otherwise a status read shortly after an enable/disable could serve this cache's stale pre-change answer for up to <see cref="CacheDuration"/>.</summary>
    internal static string CacheKey(CrawlPipeline pipeline, string providerName, string country) => $"crawl-job-status:{pipeline}:{providerName}::{country}";

    private readonly record struct CacheEntry(CrawlJobStatus? Status);

    private CrawlJobStatus? GetStatusUncached(CrawlPipeline pipeline, string providerName, string country)
    {
        var jobId = pipeline == CrawlPipeline.Api
            ? HangfireJobIds.NewsApi(providerName, country)
            : HangfireJobIds.NewsCrawl(providerName, country);

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
            Country: country,
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
