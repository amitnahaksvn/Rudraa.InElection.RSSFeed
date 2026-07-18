using Hangfire;
using Application.Abstractions;
using Application.DependencyInjection;
using Application.Options;
using Infrastructure.DependencyInjection;
using Infrastructure.Scheduling;
using WebPlatform;

// `dotnet run --project src/RssService -- --init-db` creates every MongoDB collection/index (see
// MongoIndexInitializerHostedService) and exits - a repeatable, idempotent database setup script
// with no Kestrel/HTTP surface started. Safe to run from any of the three processes (WebApp,
// RssService, ApiService) - all point at the same database and this is purely idempotent index
// creation.
var initDbOnly = args.Contains("--init-db", StringComparer.OrdinalIgnoreCase);

var builder = WebApplication.CreateBuilder(args);

// RssService is a headless worker, not a real website - see WebApp for the one admin
// SPA/dashboard. It still needs to be an ASP.NET Core "web" host rather than a plain console app
// because free-tier hosting (Render, Azure App Service Free) only offers a free plan for
// HTTP-listening apps, not true background workers - MapDefaultEndpoints() below is what that
// listener is actually for (health checks + the keep-alive self-ping target), not real traffic.
SplitCountryConfigLoader.InsertBeforeEnvironmentVariables(
    builder.Configuration, AppContext.BaseDirectory,
    new SplitCountryConfigLoader.Pipeline("WebRssFeed.appsettings.json", "Countries.Rss", "NewsCrawler"));

// Render's "Secret Files" feature mounts an uploaded file at /etc/secrets/<filename> for
// Docker-based services (render.com/docs/configure-environment-variables) - this lets the same
// nested JSON shape already used here for MongoDb secrets keep working as-is in production,
// instead of converting every key into an individual env var. Optional and appended normally
// (highest priority in the config chain, unlike the config above) so it's a no-op wherever the
// file doesn't exist (local dev, tests, Azure).
builder.Configuration.AddJsonFile("/etc/secrets/appsettings.secrets.json", optional: true, reloadOnChange: false);

builder.AddServiceDefaults();

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

// RssService never serves the error-monitor SPA (only WebApp does, via a real SignalR
// notifier) - this is a no-op stand-in so the error-log resolve/comment command handlers (which
// take IErrorLogNotifier unconditionally) can still be constructed. Neither is ever actually
// invoked here, since this process maps no ErrorLogs endpoints at all.
builder.Services.AddSingleton<IErrorLogNotifier, NullErrorLogNotifier>();

var mongoConnectionString = InfrastructureServiceCollectionExtensions.ResolveMongoConnectionString(
    builder.Configuration, builder.Configuration[$"{MongoDbOptions.SectionName}:ConnectionString"] ?? new MongoDbOptions().ConnectionString);
var mongoDatabaseName = builder.Configuration[$"{MongoDbOptions.SectionName}:DatabaseName"] ?? new MongoDbOptions().DatabaseName;

// Same connection string/database/"hangfire" prefix as ApiService/WebApp - this is what makes the
// job storage genuinely shared: WebApp's own dashboard can see every job registered here, even
// though it never executes any of them itself.
builder.Services.AddSharedHangfireStorage(mongoConnectionString, mongoDatabaseName);

if (initDbOnly)
{
    await InitDbRunner.RunAsync(builder.Services);
    return;
}

// RssService owns the RSS + Dynamic-feed pipelines' Hangfire job execution - ApiService owns
// JSON-API + Social instead, WebApp owns none (it only enqueues/manages jobs against this same
// shared storage). Still configurable per deployment (env vars, e.g. Hangfire__WorkerCount=2) for
// Azure F1's tight CPU-minute budget.
var hangfireOptions = new HangfireOptions();
builder.Configuration.GetSection(HangfireOptions.SectionName).Bind(hangfireOptions);

// Falls back here, in Program.cs, rather than via a property initializer on
// HangfireOptions.Queues itself - see that property's own doc comment for the config binding
// pitfall (array-append, not replace) that fallback would silently reintroduce. Kept in its own
// variable (not just assigned straight to options.Queues below) so the dashboard's
// RegisterServiceScopedRecurringJobsPage call further down can filter against the exact same
// resolved list this server actually processes, rather than a second hardcoded copy that could
// drift out of sync.
var resolvedQueues = hangfireOptions.Queues is { Length: > 0 }
    ? hangfireOptions.Queues
    : ["keepalive", "rss", "default"];

builder.Services.AddHangfireServer(options =>
{
    options.Queues = resolvedQueues;
    if (hangfireOptions.WorkerCount is { } workerCount)
    {
        options.WorkerCount = workerCount;
    }
});

builder.Services.AddHealthChecks().AddMongoDb(name: "mongodb");

var app = builder.Build();

// Registers/refreshes every Hangfire recurring job this host owns (RSS providers, Mongo-driven
// dynamic feeds, raw-response cleanup, error-notification dispatch) against this process's own
// Hangfire server registered above - re-synced on every startup. Fire-and-forget, not awaited:
// with 260+ RSS providers this can take real time even with the concurrent registration inside
// HangfireRecurringJobRegistrar (each AddOrUpdate is its own Mongo round trip), and none of it
// needs to finish before the health-check listener below can start serving - the Hangfire
// Server's own RecurringJobScheduler dispatcher (already running via AddHangfireServer above)
// picks up newly-registered jobs on its own polling interval regardless of when this finishes.
var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();

// Registered synchronously, in its own try/catch, deliberately outside the fire-and-forget block
// below - this job's entire purpose is keeping the host from spinning down, so it shouldn't be
// left unregistered just because an unrelated step (seeding 260+ provider schedules, say) throws
// first inside that same all-or-nothing background task. It's also cheap (one AddOrUpdate call,
// not 260+ of them), so there's no startup-latency reason to defer it either.
try
{
    HangfireRecurringJobRegistrar.RegisterKeepAliveRecurringJob(app.Services, startupLogger);
}
catch (Exception ex)
{
    startupLogger.LogCritical(ex, "Failed to register the keep-alive self-ping recurring job");
}

_ = Task.Run(async () =>
{
    try
    {
        // Seeds ProviderSchedule from this host's own config (RSS only - its own config tree has
        // no NewsApiCrawler:Countries at all, so the API half of this seeder call is naturally a
        // no-op here) before the registrar below reads it, so a brand-new provider is represented
        // in the database from its very first startup.
        await HangfireRecurringJobRegistrar.SeedProviderSchedulesAsync(app.Services, startupLogger);

        // Moves any already-seeded ProviderSchedule document still on the legacy */20 * * * *
        // cron onto its provider's new, researched cron - must run before the registrar below so
        // it reads the upgraded value, not the stale one.
        await HangfireRecurringJobRegistrar.UpgradeLegacyProviderCronsAsync(app.Services, startupLogger);

        await HangfireRecurringJobRegistrar.RegisterNewsCrawlerRecurringJobsAsync(app.Services, startupLogger);
        HangfireRecurringJobRegistrar.RegisterRawResponseCleanupRecurringJob(app.Services, startupLogger);
        await HangfireRecurringJobRegistrar.SeedAndRegisterDynamicFeedRecurringJobsAsync(app.Services, startupLogger);
        HangfireRecurringJobRegistrar.RegisterErrorNotificationDispatchRecurringJob(app.Services, startupLogger);
    }
    catch (Exception ex)
    {
        // Background task - nothing upstream would ever observe this exception otherwise, which
        // would silently leave every recurring job unregistered until the next restart.
        startupLogger.LogCritical(ex, "Hangfire recurring-job registration failed in the background");
    }
});

// The entire HTTP surface this process exposes: /health and /alive (from ServiceDefaults) plus
// its own Hangfire dashboard - no SPA, no read API, no Swagger, see WebApp for all of that.
// /alive is also the self-ping target for HangfireKeepAliveExecutor's own recurring job,
// registered above. Reads the same shared Hangfire Mongo storage as WebApp/ApiService, so it
// technically shows every job from all three processes, not just this one's own rss/keepalive
// queues - open by default (no config flag), same "deliberately open, by request" reasoning as
// WebApp's own dashboard. Authorization must be set explicitly to an empty filter list -
// Hangfire.AspNetCore's own UseHangfireDashboard default (when no DashboardOptions is passed) is
// LocalRequestsOnlyAuthorizationFilter, which 401s any request that isn't from localhost.
app.UseHangfireDashboard("/hangfire", new DashboardOptions { Authorization = [] });

// The default "Recurring Jobs" page above still shows every job from all three processes (shared
// storage - see UseHangfireDashboard's own comment). This adds a second, filtered page scoped to
// just the queue(s) this process's own Hangfire server was configured with above, so RssService's
// dashboard has a place that shows only the RSS jobs actually running here.
WebPlatform.Hangfire.ServiceScopedDashboardExtensions.RegisterServiceScopedRecurringJobsPage(resolvedQueues, "RSS jobs (this service)");

app.MapDefaultEndpoints();

app.Run();
