using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

// Toggle where Mongo comes from without touching any downstream project:
//   true  (default) - spins up a local Mongo container, zero credentials required.
//   false            - reads a real connection string named "mongodb" from this AppHost's own
//                       configuration (user-secrets/env var), e.g. an existing Atlas cluster.
// Set via appsettings.json/user-secrets/env var: "UseLocalMongo": false.
var useLocalMongo = builder.Configuration.GetValue("UseLocalMongo", true);

var mongoConnectionString = useLocalMongo
    ? builder.AddMongoDB("mongo").WithLifetime(ContainerLifetime.Persistent).AddDatabase("mongodb")
    : builder.AddConnectionString("mongodb");

// Pinned (rather than Aspire's usual dynamically-assigned port) so the browser-auto-open below
// can target a known URL - also matches each project's own launchSettings.json default, so it's
// the same address whether launched through the AppHost or standalone.
const int webAppPort = 5095;
const int rssPort = 5096;
const int apiPort = 5097;

// Three independent processes, all pointed at the same Mongo (that shared connection string/
// database is what keeps their data - and Hangfire job storage - unified despite running
// separately):
//   - WebApp: the one admin site/dashboard/read API. Never executes a crawl job itself, only
//     enqueues/manages them against the shared Hangfire storage.
//   - RssService: headless worker, owns RSS + Dynamic-feed job execution.
//   - ApiService: headless worker, owns JSON-API + Social job execution.
// Retired from the single combined Web process this used to be, and then from a brief
// two-full-copies split, before settling on this shape (see git history/CLAUDE.md) - one process
// so a pipeline's job volume can never starve another's, but only one admin site instead of
// duplicating it per pipeline.
builder.AddProject<Projects.WebApp>("webapp")
    .WithReference(mongoConnectionString)
    .WaitFor(mongoConnectionString)
    .WithHttpEndpoint(port: webAppPort)
    .WithExternalHttpEndpoints();

builder.AddProject<Projects.RssService>("rssservice")
    .WithReference(mongoConnectionString)
    .WaitFor(mongoConnectionString)
    .WithHttpEndpoint(port: rssPort)
    .WithExternalHttpEndpoints();

builder.AddProject<Projects.ApiService>("apiservice")
    .WithReference(mongoConnectionString)
    .WaitFor(mongoConnectionString)
    .WithHttpEndpoint(port: apiPort)
    .WithExternalHttpEndpoints();

var app = builder.Build();

// Opens WebApp's own endpoints once it's had time to finish starting - the Aspire dashboard
// itself already auto-opens via launchSettings.json's launchBrowser. RssService/ApiService have
// no real pages to open (just a health-check listener), so only WebApp is worth launching a
// browser tab for. Best-effort only: a missing display/browser (e.g. headless CI) is swallowed,
// never fails the AppHost itself.
_ = Task.Run(async () =>
{
    await Task.Delay(TimeSpan.FromSeconds(8));
    TryOpenBrowser($"http://localhost:{webAppPort}/scalar/v1");
});

app.Run();

static void TryOpenBrowser(string url)
{
    try
    {
        if (OperatingSystem.IsMacOS())
        {
            System.Diagnostics.Process.Start("open", url);
        }
        else if (OperatingSystem.IsWindows())
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        else
        {
            System.Diagnostics.Process.Start("xdg-open", url);
        }
    }
    catch
    {
        // No display/browser available (e.g. headless CI) - not fatal to the AppHost.
    }
}
