using System.Reflection;
using Hangfire;
using Application.DependencyInjection;
using Application.Options;
using Infrastructure.DependencyInjection;
using Infrastructure.Scheduling;
using Application.Abstractions;
using WebPlatform;
using WebPlatform.Hubs;
using WebPlatform.Infrastructure;
using WebPlatform.Options;
using Scalar.AspNetCore;

// `dotnet run --project src/WebApp -- --init-db` creates every MongoDB collection/index (see
// MongoIndexInitializerHostedService) and exits - a repeatable, idempotent database setup script
// with no Kestrel/HTTP surface started. Safe to run from any of the three processes (WebApp,
// RssService, ApiService) - all point at the same database and this is purely idempotent index
// creation.
var initDbOnly = args.Contains("--init-db", StringComparer.OrdinalIgnoreCase);

// `dotnet run --project src/WebApp -- --migrate-catalog` runs CrawlCatalogMigrationSeeder once
// and exits - only WebApp loads both NewsCrawler and NewsApiCrawler config trees (see the
// SplitCountryConfigLoader call below), so this is the one host that can migrate both pipelines
// in a single invocation.
var migrateCatalogOnly = args.Contains("--migrate-catalog", StringComparer.OrdinalIgnoreCase);

var builder = WebApplication.CreateBuilder(args);

// Unlike RssService/ApiService (which each load only their own pipeline's config), WebApp loads
// BOTH - its Provider Management page and trigger/schedule actions work across RSS and JSON-API
// alike, so it needs to see every configured provider from both trees. Each call inserts its own
// merged config blob before the environment-variables source (see SplitCountryConfigLoader's own
// doc comment for why), so NewsCrawler__*/NewsApiCrawler__* env vars still override either.
SplitCountryConfigLoader.InsertBeforeEnvironmentVariables(
    builder.Configuration, AppContext.BaseDirectory,
    new SplitCountryConfigLoader.Pipeline("WebRssFeed.appsettings.json", "Countries.Rss", "NewsCrawler"),
    new SplitCountryConfigLoader.Pipeline("WebApiFeed.appsettings.json", "Countries.Api", "NewsApiCrawler"));

// Render's "Secret Files" feature mounts an uploaded file at /etc/secrets/<filename> for
// Docker-based services (render.com/docs/configure-environment-variables) - this lets the same
// nested JSON shape already used here for MongoDb/Email/NewsApiKeys secrets keep working as-is in
// production, instead of converting every key into an individual NewsApiKeys__X-style env var.
// Optional and appended normally (highest priority in the config chain, unlike the config above)
// so it's a no-op wherever the file doesn't exist (local dev, tests, Azure) and always wins over
// appsettings.json's own (now-empty) placeholder values when Render does provide it.
builder.Configuration.AddJsonFile("/etc/secrets/appsettings.secrets.json", optional: true, reloadOnChange: false);

builder.AddServiceDefaults();

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

var mongoConnectionString = InfrastructureServiceCollectionExtensions.ResolveMongoConnectionString(
    builder.Configuration, builder.Configuration[$"{MongoDbOptions.SectionName}:ConnectionString"] ?? new MongoDbOptions().ConnectionString);
var mongoDatabaseName = builder.Configuration[$"{MongoDbOptions.SectionName}:DatabaseName"] ?? new MongoDbOptions().DatabaseName;

// Same connection string/database/"hangfire" prefix as RssService/ApiService - this is what makes
// the dashboard below (UseHangfireDashboard) able to show every job from both of them. WebApp
// deliberately never calls AddHangfireServer() - it only enqueues/manages jobs against this same
// storage (IRecurringJobManager/ICrawlJobTrigger need no running server in-process for that), it
// never executes one itself.
builder.Services.AddSharedHangfireStorage(mongoConnectionString, mongoDatabaseName);

if (initDbOnly)
{
    await InitDbRunner.RunAsync(builder.Services);
    return;
}

if (migrateCatalogOnly)
{
    await MigrateCatalogRunner.RunAsync(builder.Services);
    return;
}

builder.Services
    .AddOptions<ApiOptions>()
    .Bind(builder.Configuration.GetSection(ApiOptions.SectionName));

builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
builder.Services.AddProblemDetails();

// Backs the error-monitor's live cross-tab/cross-user sync (see IErrorLogNotifier) - a resolve or
// comment broadcasts to every connected client. WebApp is the only place this runs now (there's
// only one admin SPA), so unlike the brief two-full-copies period, there's no cross-host sync gap
// to worry about anymore - every client is connected to this same process's hub.
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
            Description = "Read-only access to crawled news articles and crawl run history, plus manual crawl triggers for both pipelines."
        });

        // Endpoint groups set WithName(handler.Method.Name) (see EndpointRouteBuilderExtensions);
        // surface that same name as the OpenAPI operationId for stable NSwag/typed-client generation.
        options.CustomOperationIds(apiDescription =>
            apiDescription.ActionDescriptor.EndpointMetadata.OfType<IEndpointNameMetadata>().FirstOrDefault()?.EndpointName);
    });
}

var app = builder.Build();

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseExceptionHandler();

// Serves the admin React app's built assets (see src/WebApp/ClientApp) - harmless to leave on
// unconditionally, since it only serves files that already exist under wwwroot; the page itself
// (the SPA fallback routes below) is gated separately via EnableErrorDashboard/etc.
//
// Explicit Cache-Control is required here, not optional: with no header at all, browsers apply
// their own heuristic caching to index.html and can keep serving a stale copy - referencing a JS
// bundle filename that a later deploy has already deleted - well past that deploy, with no way to
// tell without a manual hard refresh (confirmed live: this is exactly what made a deploy look like
// it hadn't shipped). index.html itself must always revalidate (no-cache, not no-store - a 304 on
// an unchanged deploy still skips re-downloading it); every other file under wwwroot/assets is
// content-hashed by Vite (a new build never reuses an old filename), so those are safe to cache
// forever.
var indexHtmlCacheOptions = new StaticFileOptions
{
    OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache"
};

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context => context.Context.Response.Headers.CacheControl =
        context.File.Name.Equals("index.html", StringComparison.OrdinalIgnoreCase)
            ? "no-cache"
            : "public,max-age=31536000,immutable"
});

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Deliberately no UseHttpsRedirection(): under Aspire the HTTPS port is assigned dynamically per
// run (ASPNETCORE_HTTPS_PORT can go stale and redirect to the wrong port), and on a PaaS like
// Render.com/Azure App Service, TLS is terminated at the platform's edge and this container only
// ever sees plain HTTP internally - redirecting there would either loop or send clients to the
// wrong place either way.

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
}

// The News Feed page is the actual product this app produces (already-crawled, already-public
// articles), so a bare URL with no path goes straight there - not gated behind EnableSwagger,
// unlike the redirect this replaced, which stopped existing at all whenever Swagger was disabled.
// API docs are still reachable directly at /scalar/v1 when EnableSwagger is on, just no longer
// the default landing page.
app.MapGet("/", () => Results.Redirect("/feed")).ExcludeFromDescription();

app.MapDefaultEndpoints();
// Scans both this host's own executing assembly (CrawlTrigger, Providers) and the shared
// WebPlatform assembly (News, ErrorLogs, Crawl's status/history/report endpoints) for
// IEndpointGroup implementations - see WebApplicationExtensions.MapEndpoints's own doc comment.
app.MapEndpoints(Assembly.GetExecutingAssembly(), typeof(IEndpointGroup).Assembly);
app.MapHub<ErrorLogHub>("/hubs/errorlogs");

// Unlike the admin dashboards below (errors/providers/reports - each gated behind its own
// EnableXDashboard flag since they surface sensitive internals or trigger real outbound calls),
// the News Feed page just reads already-crawled, already-public news articles - the actual output
// this app produces - so it's mapped unconditionally rather than behind a flag.
app.MapFallbackToFile("/feed", "index.html", indexHtmlCacheOptions);
app.MapFallbackToFile("/feed/{**slug}", "index.html", indexHtmlCacheOptions);

if (builder.Configuration.GetValue($"{ApiOptions.SectionName}:EnableHangfireDashboard", false))
{
    // Deliberately open, by request, after being warned this means anyone who reaches this URL
    // can view job internals and trigger/delete jobs, not just view them. Authorization must be
    // set explicitly to an empty filter list - Hangfire.AspNetCore's own UseHangfireDashboard
    // default (when no DashboardOptions is passed) is LocalRequestsOnlyAuthorizationFilter, which
    // 401s any request that didn't originate from localhost - confirmed directly: this worked in
    // every local/Aspire test during development (genuinely local requests) and was never caught
    // until the first real remote request against an actual Azure deployment. Reads the shared
    // Hangfire Mongo storage - shows every job from both RssService and ApiService here, in one
    // place, even though WebApp itself never executes any of them. Weigh the open access against
    // the convenience before re-enabling EnableHangfireDashboard on a public deployment.
    app.UseHangfireDashboard("/hangfire", new DashboardOptions { Authorization = [] });
}

if (builder.Configuration.GetValue($"{ApiOptions.SectionName}:EnableErrorDashboard", false))
{
    // Same "no built-in auth, off by default" trade-off as EnableHangfireDashboard above - the
    // error-monitor page surfaces stack traces and raw request/response bodies. The underlying
    // api/errors/* JSON endpoints stay mapped regardless (same always-on trust model as every
    // other endpoint in this app); only the SPA page itself is gated here. Two fallback routes
    // because a catch-all route parameter doesn't match the bare "/errors" path with zero segments.
    app.MapFallbackToFile("/errors", "index.html", indexHtmlCacheOptions);
    app.MapFallbackToFile("/errors/{**slug}", "index.html", indexHtmlCacheOptions);
}

if (builder.Configuration.GetValue($"{ApiOptions.SectionName}:EnableProviderDashboard", false))
{
    // Same "no built-in auth, off by default" trade-off as EnableErrorDashboard above - see
    // ApiOptions.EnableProviderDashboard's own doc comment for why this one specifically also
    // gates real outbound HTTP calls, not just data visibility. The underlying api/providers/*
    // JSON endpoints stay mapped regardless (same always-on trust model as every other endpoint
    // in this app); only the SPA page itself is gated here.
    app.MapFallbackToFile("/providers", "index.html", indexHtmlCacheOptions);
    app.MapFallbackToFile("/providers/{**slug}", "index.html", indexHtmlCacheOptions);
}

if (builder.Configuration.GetValue($"{ApiOptions.SectionName}:EnableCrawlReportDashboard", false))
{
    // Same "no built-in auth, off by default" trade-off as EnableProviderDashboard above - see
    // ApiOptions.EnableCrawlReportDashboard's own doc comment. The underlying api/crawl/report and
    // api/crawl/history JSON endpoints stay mapped regardless (same always-on trust model as every
    // other endpoint in this app); only the SPA page itself is gated here.
    app.MapFallbackToFile("/reports", "index.html", indexHtmlCacheOptions);
    app.MapFallbackToFile("/reports/{**slug}", "index.html", indexHtmlCacheOptions);
}

app.Run();
