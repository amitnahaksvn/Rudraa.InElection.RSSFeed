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
// can target a known URL - also matches Web's own launchSettings.json default, so
// it's the same address whether launched through the AppHost or standalone.
const int webPort = 5096;

// Web now owns both the HTTP API and Hangfire job execution - there is no separate Worker
// resource anymore (retired so this app fits a free-tier host with no paid background-worker
// service required).
builder.AddProject<Projects.Web>("web")
    .WithReference(mongoConnectionString)
    .WaitFor(mongoConnectionString)
    .WithHttpEndpoint(port: webPort)
    .WithExternalHttpEndpoints();

var app = builder.Build();

// Opens Web's own endpoints once it's had time to finish starting - the Aspire dashboard itself
// already auto-opens via launchSettings.json's launchBrowser. Best-effort only: a missing
// display/browser (e.g. headless CI) is swallowed, never fails the AppHost itself.
_ = Task.Run(async () =>
{
    await Task.Delay(TimeSpan.FromSeconds(8));
    TryOpenBrowser($"http://localhost:{webPort}/scalar/v1");
    TryOpenBrowser($"http://localhost:{webPort}/hangfire");
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
