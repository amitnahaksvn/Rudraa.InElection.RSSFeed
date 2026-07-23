using Infrastructure.Seed;

namespace WebPlatform;

/// <summary>
/// Backs `dotnet run -- --migrate-provider-countries`: the one-time, by-hand invocation of
/// <see cref="ProviderCountrySplitMigrator"/> that backfills <c>CrawlFeed.Country</c> for
/// providers configured under more than one JSON country block, closing the gap
/// <see cref="MigrateCatalogRunner"/>'s original migration left behind. Same throwaway-
/// ServiceProvider shape as <see cref="InitDbRunner"/>/<see cref="MigrateCatalogRunner"/>, for the
/// same reason - the caller returns immediately after this runs, so builder.Build()/app.Run()
/// never executes.
/// </summary>
public static class MigrateProviderCountriesRunner
{
    public static async Task RunAsync(IServiceCollection services)
    {
#pragma warning disable ASP0000
        using var provider = services.BuildServiceProvider();
#pragma warning restore ASP0000
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("MigrateProviderCountries");

        logger.LogInformation("--migrate-provider-countries: backfilling CrawlFeed.Country for multi-country providers");
        var migrator = provider.GetRequiredService<ProviderCountrySplitMigrator>();
        await migrator.MigrateAsync(CancellationToken.None);
        logger.LogInformation("--migrate-provider-countries: done");
    }
}
