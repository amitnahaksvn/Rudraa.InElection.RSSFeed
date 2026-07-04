using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Application.Abstractions;
using Application.Options;
using Infrastructure.Seed;

namespace Infrastructure.Scheduling;

/// <summary>
/// Registers every Hangfire recurring job (RSS providers, JSON news-API providers, Mongo-driven
/// dynamic feeds, raw-response cleanup) against whichever host calls it at startup. Originally
/// four local functions inside <c>Worker/Program.cs</c> - pulled out into Infrastructure so any
/// host running the Hangfire server (today just <c>Web</c>, after Worker was retired in favor of
/// running everything in one free-tier-friendly process) can register the exact same schedule
/// with one call, instead of duplicating this logic per host.
/// </summary>
public static class HangfireRecurringJobRegistrar
{
    public static void RegisterNewsCrawlerRecurringJobs(IServiceProvider services, ILogger logger)
    {
        var options = services.GetRequiredService<IOptions<NewsCrawlerOptions>>().Value;

        if (!options.Enabled)
        {
            logger.LogWarning(
                "NewsCrawler is disabled via configuration ({Section}:Enabled=false) - no recurring jobs registered",
                NewsCrawlerOptions.SectionName);
            return;
        }

        // The service-based IRecurringJobManager (not the static RecurringJob facade, which relies
        // on a process-wide JobStorage.Current that only gets set once the Hangfire server hosted
        // service has actually started) is required here because this runs before host.Run()/app.Run().
        var recurringJobManager = services.GetRequiredService<IRecurringJobManager>();
        var enabledJobIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var provider in options.Providers.Where(p => p.Enabled))
        {
            if (string.IsNullOrWhiteSpace(provider.Cron))
            {
                logger.LogWarning("Provider '{Provider}' has no Cron configured - no recurring job registered", provider.Name);
                continue;
            }

            var jobId = HangfireJobIds.NewsCrawl(provider.Name);
            enabledJobIds.Add(jobId);

            // AddOrUpdate is idempotent on jobId - re-registering on every startup keeps the recurring
            // job's cron expression in sync with config without ever creating duplicate jobs. Targets
            // HangfireCrawlJobExecutor (not INewsCrawlerService directly) so the dashboard shows a
            // friendly "Crawl AajTak" name and every log line from the run carries this job's own id.
            recurringJobManager.AddOrUpdate<HangfireCrawlJobExecutor>(
                jobId,
                executor => executor.RunAsync(provider.Name, null!, CancellationToken.None),
                provider.Cron,
                RecurringJobOptionsFactory.Create(TimeZoneInfo.Utc));

            logger.LogInformation(
                "Registered Hangfire recurring job '{JobId}' for provider '{Provider}' with cron '{Cron}'",
                jobId, provider.Name, provider.Cron);
        }

        // Same "zombie job" concern as RegisterNewsApiRecurringJobs below: AddOrUpdate never
        // removes a job for a provider that was previously enabled and is now disabled (or
        // deleted from config entirely), which would otherwise leave it firing forever on its old
        // schedule after a redeploy. Sweep every "news-crawl-*" job actually registered in
        // Hangfire storage and drop any that no longer corresponds to a currently-enabled provider.
        var jobStorage = services.GetRequiredService<JobStorage>();
        using var connection = jobStorage.GetConnection();
        var staleJobIds = connection.GetRecurringJobs()
            .Select(j => j.Id)
            .Where(id => id.StartsWith("news-crawl-", StringComparison.Ordinal) && !enabledJobIds.Contains(id))
            .ToList();

        foreach (var staleJobId in staleJobIds)
        {
            recurringJobManager.RemoveIfExists(staleJobId);
            logger.LogInformation("Removed stale Hangfire recurring job '{JobId}' - provider is disabled or no longer configured", staleJobId);
        }
    }

    public static void RegisterRawResponseCleanupRecurringJob(IServiceProvider services, ILogger logger)
    {
        var options = services.GetRequiredService<IOptions<NewsCrawlerOptions>>().Value;

        if (string.IsNullOrWhiteSpace(options.RawResponseCleanupCron))
        {
            logger.LogWarning("NewsCrawler:RawResponseCleanupCron is not configured - no cleanup job registered");
            return;
        }

        var recurringJobManager = services.GetRequiredService<IRecurringJobManager>();

        // India Standard Time, not UTC: every provider/user of this app is India-focused, so a
        // "5 AM" schedule with no explicit zone is assumed to mean 5 AM IST. Falls back to UTC rather
        // than throwing (which would otherwise crash the whole host at startup) if the host's tzdata
        // doesn't have this id for some reason - the job still runs daily, just at a different clock hour.
        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            logger.LogWarning(ex, "Could not resolve time zone 'Asia/Kolkata' - falling back to UTC for the raw-response cleanup job");
            timeZone = TimeZoneInfo.Utc;
        }

        recurringJobManager.AddOrUpdate<HangfireRawResponseCleanupExecutor>(
            HangfireJobIds.RawResponseCleanup,
            executor => executor.RunAsync(options.RawResponseRetention, null!, CancellationToken.None),
            options.RawResponseCleanupCron,
            RecurringJobOptionsFactory.Create(timeZone));

        logger.LogInformation(
            "Registered Hangfire recurring job '{JobId}' with cron '{Cron}' ({TimeZone}), retention {Retention}",
            HangfireJobIds.RawResponseCleanup, options.RawResponseCleanupCron, timeZone.Id, options.RawResponseRetention);
    }

    /// <summary>
    /// Bootstraps the Mongo-driven <c>FeedSource</c> pipeline: seeds the Phase 1 PIB feed if missing,
    /// then registers one Hangfire recurring job per currently-active <c>FeedSource</c> document.
    /// Unlike <see cref="RegisterNewsCrawlerRecurringJobs"/> (whose provider list is fixed at compile
    /// time - a new provider needs a new C# class), this list comes from Mongo, so it can grow purely
    /// via document inserts. Re-synced on every startup, same as every other recurring-job
    /// registration here - a brand-new FeedSource document takes effect on the next restart,
    /// not instantly (consistent with how a NewsCrawler.appsettings.json change already behaves here).
    /// </summary>
    public static async Task SeedAndRegisterDynamicFeedRecurringJobsAsync(IServiceProvider services, ILogger logger)
    {
        var seeder = services.GetRequiredService<FeedSourceSeeder>();
        await seeder.SeedAsync(CancellationToken.None);

        var feedSourceRepository = services.GetRequiredService<IFeedSourceRepository>();
        var activeFeedSources = await feedSourceRepository.GetActiveAsync(CancellationToken.None);

        var recurringJobManager = services.GetRequiredService<IRecurringJobManager>();
        var enabledJobIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var feedSource in activeFeedSources)
        {
            var cron = BuildCronForInterval(feedSource.FetchIntervalMinutes);
            var jobId = HangfireJobIds.DynamicFeed(feedSource.SourceCode);
            enabledJobIds.Add(jobId);

            recurringJobManager.AddOrUpdate<HangfireDynamicFeedJobExecutor>(
                jobId,
                executor => executor.RunAsync(feedSource.Id, null!, CancellationToken.None),
                cron,
                RecurringJobOptionsFactory.Create(TimeZoneInfo.Utc));

            logger.LogInformation(
                "Registered Hangfire recurring job '{JobId}' for FeedSource '{SourceCode}' with cron '{Cron}'",
                jobId, feedSource.SourceCode, cron);
        }

        // Same "zombie job" concern as the other two registrars: a FeedSource that's deactivated
        // or deleted from Mongo would otherwise leave its Hangfire job firing forever on its old
        // schedule after a redeploy, since AddOrUpdate never removes jobs on its own.
        var jobStorage = services.GetRequiredService<JobStorage>();
        using var connection = jobStorage.GetConnection();
        var staleJobIds = connection.GetRecurringJobs()
            .Select(j => j.Id)
            .Where(id => id.StartsWith("dynamic-feed-", StringComparison.Ordinal) && !enabledJobIds.Contains(id))
            .ToList();

        foreach (var staleJobId in staleJobIds)
        {
            recurringJobManager.RemoveIfExists(staleJobId);
            logger.LogInformation("Removed stale Hangfire recurring job '{JobId}' - FeedSource is inactive or no longer exists", staleJobId);
        }
    }

    /// <summary>
    /// Registers one Hangfire recurring job per enabled <see cref="NewsApiCrawlerOptions"/> provider -
    /// the <see cref="RegisterNewsCrawlerRecurringJobs"/> counterpart for JSON news-API providers.
    /// Targets <see cref="HangfireNewsApiJobExecutor"/> (tagged <c>[Queue("api")]</c>), never
    /// <see cref="INewsApiCrawlerService"/> directly, same reasoning as every other job registration
    /// here.
    /// </summary>
    public static void RegisterNewsApiRecurringJobs(IServiceProvider services, ILogger logger)
    {
        var options = services.GetRequiredService<IOptions<NewsApiCrawlerOptions>>().Value;

        if (!options.Enabled)
        {
            logger.LogWarning(
                "NewsApiCrawler is disabled via configuration ({Section}:Enabled=false) - no recurring jobs registered",
                NewsApiCrawlerOptions.SectionName);
            return;
        }

        var recurringJobManager = services.GetRequiredService<IRecurringJobManager>();
        var enabledJobIds = new HashSet<string>(StringComparer.Ordinal);

        // Fetched once, up front, so the loop below can tell a brand-new provider (never
        // registered before) apart from one that already existed from a previous startup -
        // AddOrUpdate itself can't distinguish the two, since it's idempotent either way.
        var jobStorage = services.GetRequiredService<JobStorage>();
        using var connection = jobStorage.GetConnection();
        var preExistingJobIds = new HashSet<string>(
            connection.GetRecurringJobs().Select(j => j.Id),
            StringComparer.Ordinal);

        foreach (var provider in options.Providers.Where(p => p.Enabled))
        {
            if (string.IsNullOrWhiteSpace(provider.Cron))
            {
                logger.LogWarning("News API provider '{Provider}' has no Cron configured - no recurring job registered", provider.Name);
                continue;
            }

            var jobId = HangfireJobIds.NewsApi(provider.Name);
            enabledJobIds.Add(jobId);
            var isNewJob = !preExistingJobIds.Contains(jobId);

            recurringJobManager.AddOrUpdate<HangfireNewsApiJobExecutor>(
                jobId,
                executor => executor.RunAsync(provider.Name, null!, CancellationToken.None),
                provider.Cron,
                RecurringJobOptionsFactory.Create(TimeZoneInfo.Utc));

            if (isNewJob)
            {
                // Run once immediately the first time this provider's job is ever registered,
                // so a newly-added provider doesn't just sit idle until its next cron tick -
                // but not on every subsequent restart/deploy, which would otherwise re-run (and
                // re-alert on) every provider, including ones with known unresolved issues, on
                // every single deploy or free-tier hibernate-wake.
                recurringJobManager.Trigger(jobId);
            }

            logger.LogInformation(
                "Registered Hangfire recurring job '{JobId}' for news API provider '{Provider}' with cron '{Cron}'{TriggeredSuffix}",
                jobId, provider.Name, provider.Cron, isNewJob ? " (triggered immediately - new job)" : string.Empty);
        }

        // AddOrUpdate only ever adds/updates - it never removes a job for a provider that was
        // previously enabled and is now disabled (or deleted from config entirely), which would
        // otherwise leave a "zombie" recurring job firing forever on its old schedule. Sweep every
        // "news-api-*" job actually registered in Hangfire storage and drop any that no longer
        // corresponds to a currently-enabled provider.
        var staleJobIds = preExistingJobIds
            .Where(id => id.StartsWith("news-api-", StringComparison.Ordinal) && !enabledJobIds.Contains(id))
            .ToList();

        foreach (var staleJobId in staleJobIds)
        {
            recurringJobManager.RemoveIfExists(staleJobId);
            logger.LogInformation("Removed stale Hangfire recurring job '{JobId}' - provider is disabled or no longer configured", staleJobId);
        }
    }

    /// <summary>Builds a 5-field cron expression from a plain minute interval, since FeedSource stores <c>FetchIntervalMinutes</c>, not a raw cron string.</summary>
    private static string BuildCronForInterval(int fetchIntervalMinutes)
    {
        if (fetchIntervalMinutes is >= 1 and <= 59)
        {
            return $"*/{fetchIntervalMinutes} * * * *";
        }

        if (fetchIntervalMinutes % 60 == 0 && fetchIntervalMinutes <= 1440)
        {
            return $"0 */{fetchIntervalMinutes / 60} * * *";
        }

        // Not a clean minutes-or-hours interval (e.g. 90) - hourly is a safe, deterministic fallback
        // rather than producing an invalid/misleading cron expression.
        return "0 * * * *";
    }
}
