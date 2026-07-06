using System.Reflection;
using Hangfire;
using Hangfire.Mongo;
using Hangfire.Mongo.Migration.Strategies;
using Hangfire.Mongo.Migration.Strategies.Backup;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;
using Application.DependencyInjection;
using Application.Options;
using Infrastructure.DependencyInjection;
using Infrastructure.Scheduling;
using Application.Abstractions;
using Web.Hubs;
using Web.Infrastructure;
using Web.Options;
using Scalar.AspNetCore;

// `dotnet run --project src/Web -- --init-db` creates every MongoDB
// collection/index (see MongoIndexInitializerHostedService) and exits - a repeatable, idempotent
// database setup script with no Kestrel/HTTP surface started.
var initDbOnly = args.Contains("--init-db", StringComparer.OrdinalIgnoreCase);

var builder = WebApplication.CreateBuilder(args);

// Single source of truth for NewsCrawler:* (providers/feeds/schedules) - see
// NewsCrawler.appsettings.json at the src/ root. AppContext.BaseDirectory (not ContentRootPath,
// which `dotnet run` sets to the project source directory) is what's consistent between
// `dotnet run` and a published/Docker deployment - the .csproj's linked Content item copies the
// file there under both build and publish. Inserted before the environment-variables source
// (rather than appended, which is CreateBuilder's default for a source added afterwards) so
// NewsCrawler__* env vars - e.g. from an AWS/Azure secret injected into the container's
// environment - can still override this file, not the reverse.
InsertNewsCrawlerConfigBeforeEnvironmentVariables(builder.Configuration);

// Render's "Secret Files" feature mounts an uploaded file at /etc/secrets/<filename> for
// Docker-based services (render.com/docs/configure-environment-variables) - this lets the same
// nested JSON shape already used here for MongoDb/Email/NewsApiKeys secrets keep working as-is in
// production, instead of converting every key into an individual NewsApiKeys__X-style env var.
// Optional and appended normally (highest priority in the config chain, unlike NewsCrawler's file
// above) so it's a no-op wherever the file doesn't exist (local dev, tests) and always wins over
// appsettings.json's own (now-empty) placeholder values when Render does provide it.
builder.Configuration.AddJsonFile("/etc/secrets/appsettings.secrets.json", optional: true, reloadOnChange: false);

builder.AddServiceDefaults();

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

var mongoConnectionString = InfrastructureServiceCollectionExtensions.ResolveMongoConnectionString(
    builder.Configuration, builder.Configuration[$"{MongoDbOptions.SectionName}:ConnectionString"] ?? new MongoDbOptions().ConnectionString);
var mongoDatabaseName = builder.Configuration[$"{MongoDbOptions.SectionName}:DatabaseName"] ?? new MongoDbOptions().DatabaseName;

builder.Services.AddHangfire(config => config
    .UseMongoStorage(mongoConnectionString, mongoDatabaseName, new MongoStorageOptions
    {
        Prefix = "hangfire",
        // Hangfire.Mongo pings the database synchronously the first time storage is resolved and
        // throws (crashing the whole host) if that single ping doesn't answer within 5s - too
        // fragile against an Atlas cluster's normal latency variance. Mongo connectivity is already
        // verified elsewhere (MongoDbOptions.ValidateOnStart, MongoIndexInitializerHostedService).
        CheckConnection = false,
        MigrationOptions = new MongoMigrationOptions
        {
            MigrationStrategy = new MigrateMongoMigrationStrategy(),
            BackupStrategy = new CollectionMongoBackupStrategy()
        }
    }));

if (!initDbOnly)
{
    // Web now owns both the HTTP API and Hangfire job execution - there is no separate Worker
    // process anymore (retired so this app fits a free-tier host with no paid background-worker
    // service required; see git history for the two-process version if that's ever needed again).
    // Still configurable per deployment (env vars, e.g. Hangfire__Queues__0=api,
    // Hangfire__WorkerCount=5) in case a future split back into replica groups is ever wanted.
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

if (initDbOnly)
{
    // Sole ServiceProvider built in this process for this mode (we `return` right after, the
    // normal `builder.Build()`/Kestrel path below never runs), so there's no risk of the usual
    // "two copies of singletons" problem ASP0000 warns about.
#pragma warning disable ASP0000
    using var initProvider = builder.Services.BuildServiceProvider();
#pragma warning restore ASP0000
    var initLogger = initProvider.GetRequiredService<ILogger<Program>>();

    initLogger.LogInformation("--init-db: creating MongoDB collections/indexes only");
    foreach (var hostedService in initProvider.GetServices<IHostedService>())
    {
        await hostedService.StartAsync(CancellationToken.None);
    }
    initLogger.LogInformation("--init-db: done");
    return;
}

builder.Services
    .AddOptions<ApiOptions>()
    .Bind(builder.Configuration.GetSection(ApiOptions.SectionName));

builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
builder.Services.AddProblemDetails();

// Backs the error-monitor's live cross-tab/cross-user sync (see IErrorLogNotifier) - a resolve or
// comment broadcasts to every connected client instead of requiring a manual refresh elsewhere.
builder.Services.AddSignalR();
builder.Services.AddSingleton<IErrorLogNotifier, SignalRErrorLogNotifier>();

builder.Services.AddHealthChecks().AddMongoDb(name: "mongodb");

var enableSwagger = builder.Configuration.GetValue($"{ApiOptions.SectionName}:EnableSwagger", true);
if (enableSwagger)
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new()
        {
            Title = "Political News Crawler API",
            Version = "v1",
            Description = "Read-only access to crawled news articles and crawl run history, plus a manual crawl trigger."
        });

        // Endpoint groups set WithName(handler.Method.Name) (see EndpointRouteBuilderExtensions);
        // surface that same name as the OpenAPI operationId for stable NSwag/typed-client generation.
        options.CustomOperationIds(apiDescription =>
            apiDescription.ActionDescriptor.EndpointMetadata.OfType<IEndpointNameMetadata>().FirstOrDefault()?.EndpointName);
    });
}

var app = builder.Build();

// Registers/refreshes every Hangfire recurring job (RSS providers, JSON news-API providers,
// Mongo-driven dynamic feeds, raw-response cleanup) against this process's own Hangfire server
// registered above - re-synced on every startup, same idempotent AddOrUpdate behaviour this used
// to have when it ran in the now-retired Worker process. Fire-and-forget, not awaited: with 260+
// providers this can take real time even with the concurrent registration inside
// HangfireRecurringJobRegistrar (each AddOrUpdate is its own Mongo round trip), and none of it
// needs to finish before the HTTP pipeline below can start serving - Scalar/Swagger/health checks/
// the Hangfire dashboard itself have no dependency on recurring-job metadata already existing in
// Mongo, and the Hangfire Server's own RecurringJobScheduler dispatcher (already running via
// AddHangfireServer above) picks up newly-registered jobs on its own polling interval regardless
// of when this finishes. Blocking app.Run() on this was the actual cause of "the app takes a long
// time to become reachable at startup" - moving it off the startup path fixes that directly,
// rather than only shaving the registration work itself down further.
var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
_ = Task.Run(async () =>
{
    try
    {
        HangfireRecurringJobRegistrar.RegisterNewsCrawlerRecurringJobs(app.Services, startupLogger);
        HangfireRecurringJobRegistrar.RegisterRawResponseCleanupRecurringJob(app.Services, startupLogger);
        await HangfireRecurringJobRegistrar.SeedAndRegisterDynamicFeedRecurringJobsAsync(app.Services, startupLogger);
        HangfireRecurringJobRegistrar.RegisterNewsApiRecurringJobs(app.Services, startupLogger);
        HangfireRecurringJobRegistrar.RegisterErrorNotificationDispatchRecurringJob(app.Services, startupLogger);
        await HangfireRecurringJobRegistrar.SeedAndRegisterSocialMediaRecurringJobsAsync(app.Services, startupLogger);
    }
    catch (Exception ex)
    {
        // Background task - nothing upstream would ever observe this exception otherwise, which
        // would silently leave every recurring job unregistered until the next restart.
        startupLogger.LogCritical(ex, "Hangfire recurring-job registration failed in the background");
    }
});

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseExceptionHandler();

// Serves the error-monitor React app's built assets (see src/Web/ClientApp) - harmless to leave
// on unconditionally, since it only serves files that already exist under wwwroot; the page
// itself (the SPA fallback routes below) is gated separately via EnableErrorDashboard.
app.UseStaticFiles();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Deliberately no UseHttpsRedirection(): under Aspire the HTTPS port is assigned dynamically per
// run (ASPNETCORE_HTTPS_PORT can go stale and redirect to the wrong port), and on a PaaS like
// Render.com, TLS is terminated at the platform's edge and this container only ever sees plain
// HTTP internally - redirecting there would either loop or send clients to the wrong place either way.

if (enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Scalar reads the same Swashbuckle-generated document - no second OpenAPI generator needed.
    app.MapScalarApiReference(options =>
    {
        options.WithOpenApiRoutePattern("/swagger/{documentName}/swagger.json");
        options.Title = "Political News Crawler API";
    });

    app.MapGet("/", () => Results.Redirect("/scalar/v1")).ExcludeFromDescription();
}

app.MapDefaultEndpoints();
app.MapEndpoints(Assembly.GetExecutingAssembly());
app.MapHub<ErrorLogHub>("/hubs/errorlogs");

if (builder.Configuration.GetValue($"{ApiOptions.SectionName}:EnableHangfireDashboard", false))
{
    // No authorization filter - deliberately open, by request, after being warned this means
    // anyone who reaches this URL can view job internals and trigger/delete jobs, not just view
    // them (Hangfire's dashboard has no built-in auth of its own). Weigh that against the
    // convenience before re-enabling EnableHangfireDashboard on a public deployment.
    app.UseHangfireDashboard("/hangfire");
}

if (builder.Configuration.GetValue($"{ApiOptions.SectionName}:EnableErrorDashboard", false))
{
    // Same "no built-in auth, off by default" trade-off as EnableHangfireDashboard above - the
    // error-monitor page surfaces stack traces and raw request/response bodies. The underlying
    // api/errors/* JSON endpoints stay mapped regardless (same always-on trust model as every
    // other endpoint in this app); only the SPA page itself is gated here. Two fallback routes
    // because a catch-all route parameter doesn't match the bare "/errors" path with zero segments.
    app.MapFallbackToFile("/errors", "index.html");
    app.MapFallbackToFile("/errors/{**slug}", "index.html");
}

app.Run();

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
