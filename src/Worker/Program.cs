using Hangfire;
using Hangfire.Mongo;
using Hangfire.Mongo.Migration.Strategies;
using Hangfire.Mongo.Migration.Strategies.Backup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Application.DependencyInjection;
using Application.Options;
using Infrastructure.DependencyInjection;
using Infrastructure.Scheduling;

// `dotnet run --project src/Worker -- --init-db` creates every MongoDB
// collection/index (see MongoIndexInitializerHostedService) and exits - a repeatable, idempotent
// database setup script with no separate scheduler/crawl started.
var initDbOnly = args.Contains("--init-db", StringComparer.OrdinalIgnoreCase);

var builder = Host.CreateApplicationBuilder(args);

// Shared with Web (see NewsCrawler.appsettings.json at the src/ root) so both
// processes read the exact same provider/feed/schedule config from one file, not a duplicated copy.
// AppContext.BaseDirectory (not ContentRootPath, which `dotnet run` sets to the project source
// directory) is what's consistent between `dotnet run` and a published/Docker deployment - the
// .csproj's linked Content item copies the file there under both build and publish. Inserted
// before the environment-variables source (rather than appended, which is CreateApplicationBuilder's
// default for a source added afterwards) so NewsCrawler__* env vars - e.g. from an AWS/Azure
// secret injected into the container's environment - can still override this file, not the reverse.
InsertNewsCrawlerConfigBeforeEnvironmentVariables(builder.Configuration);

builder.AddServiceDefaults();

builder.Services.Configure<HostOptions>(options =>
{
    // Graceful shutdown: give an in-flight crawl run time to finish before the process exits.
    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

if (!initDbOnly)
{
    // Each provider's own Cron (NewsCrawler.appsettings.json) becomes a native Hangfire recurring
    // job - see RegisterNewsCrawlerRecurringJobs below - rather than a hand-rolled polling loop.
    var mongoConnectionString = InfrastructureServiceCollectionExtensions.ResolveMongoConnectionString(
        builder.Configuration, builder.Configuration[$"{MongoDbOptions.SectionName}:ConnectionString"] ?? new MongoDbOptions().ConnectionString);
    var mongoDatabaseName = builder.Configuration[$"{MongoDbOptions.SectionName}:DatabaseName"] ?? new MongoDbOptions().DatabaseName;

    builder.Services.AddHangfire(config => config
        .UseMongoStorage(mongoConnectionString, mongoDatabaseName, new MongoStorageOptions
        {
            Prefix = "hangfire",
            // Hangfire.Mongo pings the database synchronously the first time storage is resolved
            // and throws (crashing the whole host) if that single ping doesn't answer within 5s -
            // too fragile against an Atlas cluster's normal latency variance. Mongo connectivity is
            // already verified elsewhere (MongoDbOptions.ValidateOnStart, MongoIndexInitializerHostedService).
            CheckConnection = false,
            MigrationOptions = new MongoMigrationOptions
            {
                MigrationStrategy = new MigrateMongoMigrationStrategy(),
                BackupStrategy = new CollectionMongoBackupStrategy()
            }
        }));

    // Configurable per deployment (env vars, e.g. Hangfire__Queues__0=api, Hangfire__WorkerCount=5)
    // so a production rollout can run separate replica groups of this exact same image - one
    // scaled for "rss" queue jobs, another for a future "api" queue - without a second service.
    var hangfireOptions = new HangfireOptions();
    builder.Configuration.GetSection(HangfireOptions.SectionName).Bind(hangfireOptions);
    builder.Services.AddHangfireServer(options =>
    {
        options.Queues = hangfireOptions.Queues;
        if (hangfireOptions.WorkerCount is { } workerCount)
        {
            options.WorkerCount = workerCount;
        }
    });
}

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();

if (initDbOnly)
{
    logger.LogInformation("--init-db: creating MongoDB collections/indexes only");
    foreach (var hostedService in host.Services.GetServices<IHostedService>())
    {
        await hostedService.StartAsync(CancellationToken.None);
    }
    logger.LogInformation("--init-db: done");
    return;
}

RegisterNewsCrawlerRecurringJobs(host.Services, logger);
RegisterRawResponseCleanupRecurringJob(host.Services, logger);

logger.LogInformation("Worker application started");

await host.RunAsync();

static void RegisterNewsCrawlerRecurringJobs(IServiceProvider services, ILogger logger)
{
    var options = services.GetRequiredService<IOptions<NewsCrawlerOptions>>().Value;

    if (!options.Enabled)
    {
        logger.LogWarning(
            "NewsCrawler is disabled via configuration ({Section}:Enabled=false) - no recurring jobs registered",
            NewsCrawlerOptions.SectionName);
        return;
    }

    // The service-based IRecurringJobManager (not the static RecurringJob facade, which relies on
    // a process-wide JobStorage.Current that only gets set once the Hangfire server hosted service
    // has actually started) is required here because this runs before host.RunAsync().
    var recurringJobManager = services.GetRequiredService<IRecurringJobManager>();

    foreach (var provider in options.Providers.Where(p => p.Enabled))
    {
        if (string.IsNullOrWhiteSpace(provider.Cron))
        {
            logger.LogWarning("Provider '{Provider}' has no Cron configured - no recurring job registered", provider.Name);
            continue;
        }

        var jobId = HangfireJobIds.NewsCrawl(provider.Name);

        // AddOrUpdate is idempotent on jobId - re-registering on every startup keeps the recurring
        // job's cron expression in sync with config without ever creating duplicate jobs. Targets
        // HangfireCrawlJobExecutor (not INewsCrawlerService directly) so the dashboard shows a
        // friendly "Crawl AajTak" name and every log line from the run carries this job's own id.
        recurringJobManager.AddOrUpdate<HangfireCrawlJobExecutor>(
            jobId,
            executor => executor.RunAsync(provider.Name, null!, CancellationToken.None),
            provider.Cron,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        logger.LogInformation(
            "Registered Hangfire recurring job '{JobId}' for provider '{Provider}' with cron '{Cron}'",
            jobId, provider.Name, provider.Cron);
    }
}

static void RegisterRawResponseCleanupRecurringJob(IServiceProvider services, ILogger logger)
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
    // than throwing (which would otherwise crash the whole Worker at startup) if the host's tzdata
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
        new RecurringJobOptions { TimeZone = timeZone });

    logger.LogInformation(
        "Registered Hangfire recurring job '{JobId}' with cron '{Cron}' ({TimeZone}), retention {Retention}",
        HangfireJobIds.RawResponseCleanup, options.RawResponseCleanupCron, timeZone.Id, options.RawResponseRetention);
}

static void InsertNewsCrawlerConfigBeforeEnvironmentVariables(IConfigurationBuilder configuration)
{
    var source = new JsonConfigurationSource
    {
        Path = Path.Combine(AppContext.BaseDirectory, "NewsCrawler.appsettings.json"),
        Optional = false,
        ReloadOnChange = true
    };
    source.ResolveFileProvider();

    var envVariablesIndex = configuration.Sources.ToList().FindIndex(s => s is EnvironmentVariablesConfigurationSource);
    if (envVariablesIndex < 0)
    {
        configuration.Add(source);
        return;
    }

    configuration.Sources.Insert(envVariablesIndex, source);
}
