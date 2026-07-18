namespace WebPlatform;

/// <summary>
/// Backs `dotnet run -- --init-db`: builds a throwaway ServiceProvider from the host's own
/// registrations and starts every <see cref="IHostedService"/> (in particular
/// MongoIndexInitializerHostedService) so every MongoDB collection/index is created without ever
/// starting Kestrel/Hangfire. Sole ServiceProvider built in this mode - the caller returns
/// immediately after this runs, so the normal builder.Build()/app.Run() path never executes and
/// there's no risk of the usual "two copies of singletons" problem ASP0000 warns about.
/// </summary>
public static class InitDbRunner
{
    public static async Task RunAsync(IServiceCollection services)
    {
#pragma warning disable ASP0000
        using var initProvider = services.BuildServiceProvider();
#pragma warning restore ASP0000
        var initLogger = initProvider.GetRequiredService<ILoggerFactory>().CreateLogger("InitDb");

        initLogger.LogInformation("--init-db: creating MongoDB collections/indexes only");
        foreach (var hostedService in initProvider.GetServices<IHostedService>())
        {
            await hostedService.StartAsync(CancellationToken.None);
        }
        initLogger.LogInformation("--init-db: done");
    }
}
