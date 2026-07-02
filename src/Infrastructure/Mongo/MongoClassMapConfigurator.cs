using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.IdGenerators;
using Domain.Entities;

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

        ConventionRegistry.Register(
            "PoliticalNewsConventions",
            new ConventionPack { new CamelCaseElementNameConvention(), new IgnoreExtraElementsConvention(true) },
            _ => true);

        BsonClassMap.RegisterClassMap<NewsArticle>(cm =>
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
    }
}
