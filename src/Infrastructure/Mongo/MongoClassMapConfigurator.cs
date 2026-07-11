using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;
using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.Mongo;

/// <summary>
/// Registers BSON class maps for Domain entities so the Domain layer stays free of any
/// MongoDB.Driver/Bson attribute dependency. Must run once before the first Mongo operation.
/// </summary>
public static class MongoClassMapConfigurator
{
    private static int _configured;

    public static void Configure()
    {
        if (Interlocked.Exchange(ref _configured, 1) == 1)
        {
            return;
        }

        // Scoped to this app's own Domain entities only (`_ => true` previously applied this
        // process-wide, to every BSON-serialized type including Hangfire.Mongo's own internal
        // documents - its migration/query code expects its own PascalCase field names like
        // "Field"/"StateHistory", and this convention was silently camelCasing them too, causing
        // "Element 'Field' not found" migration crashes and "conditional update did not apply"
        // queue-ack warnings against documents whose field names didn't match what Hangfire itself
        // was reading/writing).
        ConventionRegistry.Register(
            "PoliticalNewsConventions",
            new ConventionPack { new CamelCaseElementNameConvention(), new IgnoreExtraElementsConvention(true) },
            t => t.Namespace == typeof(NewsArticle).Namespace);

        // Stored as the string "Rss"/"Api", not the default int32, so the origin of an article is
        // legible straight out of a Mongo query/Compass view without cross-referencing the enum.
        BsonSerializer.RegisterSerializer(typeof(ArticleSourceType), new EnumSerializer<ArticleSourceType>(BsonType.String));

        // Same reasoning for SocialMediaSource's own two enums - "YouTube"/"Politician" readable
        // directly in Mongo, not a bare int32.
        BsonSerializer.RegisterSerializer(typeof(SocialPlatform), new EnumSerializer<SocialPlatform>(BsonType.String));
        BsonSerializer.RegisterSerializer(typeof(SourceEntityType), new EnumSerializer<SourceEntityType>(BsonType.String));

        // Same reasoning again - "Rss"/"Api"/"Social" readable directly in Mongo. Backward
        // compatible with CrawlHistory documents already persisted as the old default int32
        // (0/1/2): the driver's EnumSerializer deserializes either representation regardless of
        // which BsonType it's configured to *write* going forward.
        BsonSerializer.RegisterSerializer(typeof(CrawlPipeline), new EnumSerializer<CrawlPipeline>(BsonType.String));

        BsonClassMap.RegisterClassMap<NewsArticle>(cm =>
        {
            cm.AutoMap();
            cm.MapIdMember(x => x.Id).SetIdGenerator(StringObjectIdGenerator.Instance);
        });

        BsonClassMap.RegisterClassMap<ArticleFingerprint>(cm =>
        {
            cm.AutoMap();
            cm.MapIdMember(x => x.Id).SetIdGenerator(StringObjectIdGenerator.Instance);
        });

        BsonClassMap.RegisterClassMap<CrawlHistory>(cm =>
        {
            cm.AutoMap();
            cm.MapIdMember(x => x.Id).SetIdGenerator(StringObjectIdGenerator.Instance);
        });

        BsonClassMap.RegisterClassMap<CrawlLock>(cm =>
        {
            cm.AutoMap();
            cm.MapIdMember(x => x.Id).SetIdGenerator(null);
        });

        BsonClassMap.RegisterClassMap<RssRawResponse>(cm =>
        {
            cm.AutoMap();
            cm.MapIdMember(x => x.Id).SetIdGenerator(StringObjectIdGenerator.Instance);
        });

        BsonClassMap.RegisterClassMap<FeedSource>(cm =>
        {
            cm.AutoMap();
            cm.MapIdMember(x => x.Id).SetIdGenerator(StringObjectIdGenerator.Instance);
        });

        BsonClassMap.RegisterClassMap<FeedErrorLog>(cm =>
        {
            cm.AutoMap();
            cm.MapIdMember(x => x.Id).SetIdGenerator(StringObjectIdGenerator.Instance);
        });

        BsonClassMap.RegisterClassMap<ErrorLog>(cm =>
        {
            cm.AutoMap();
            cm.MapIdMember(x => x.Id).SetIdGenerator(StringObjectIdGenerator.Instance);
        });

        BsonClassMap.RegisterClassMap<SocialMediaSource>(cm =>
        {
            cm.AutoMap();
            cm.MapIdMember(x => x.Id).SetIdGenerator(StringObjectIdGenerator.Instance);
        });

        BsonClassMap.RegisterClassMap<ProviderSchedule>(cm =>
        {
            cm.AutoMap();
            cm.MapIdMember(x => x.Id).SetIdGenerator(StringObjectIdGenerator.Instance);
        });
    }
}
