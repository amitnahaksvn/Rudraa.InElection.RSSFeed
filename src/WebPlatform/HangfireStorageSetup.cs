using Hangfire;
using Hangfire.Mongo;
using Hangfire.Mongo.Migration.Strategies;
using Hangfire.Mongo.Migration.Strategies.Backup;

namespace WebPlatform;

/// <summary>
/// Shared Hangfire-Mongo storage setup for both WebRssFeed and WebApiFeed - both point at the
/// same connection string/database/"hangfire" prefix, which is what makes their recurring-job
/// storage genuinely shared: either host's own Hangfire dashboard can see both hosts' jobs,
/// regardless of which one actually executes them.
/// </summary>
public static class HangfireStorageSetup
{
    public static IServiceCollection AddSharedHangfireStorage(
        this IServiceCollection services, string mongoConnectionString, string mongoDatabaseName) =>
        services.AddHangfire(config => config
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
}
