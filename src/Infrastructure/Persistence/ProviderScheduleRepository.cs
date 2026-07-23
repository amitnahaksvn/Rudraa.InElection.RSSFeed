using MongoDB.Bson;
using MongoDB.Driver;
using Application.Abstractions;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Mongo;

namespace Infrastructure.Persistence;

public sealed class ProviderScheduleRepository : IProviderScheduleRepository
{
    private const string LegacyIndexName = "ux_providerschedule_pipeline_provider";
    private const string IndexName = "ux_providerschedule_pipeline_provider_country";

    private readonly IMongoCollection<ProviderSchedule> _collection;

    public ProviderScheduleRepository(MongoDbContext context)
    {
        _collection = context.ProviderSchedules;
    }

    public async Task<IReadOnlyList<ProviderSchedule>> GetAllAsync(CrawlPipeline pipeline, CancellationToken cancellationToken) =>
        await _collection.Find(s => s.Pipeline == pipeline).ToListAsync(cancellationToken);

    public Task<ProviderSchedule?> GetAsync(CrawlPipeline pipeline, string provider, string country, CancellationToken cancellationToken) =>
        _collection.Find(BuildKeyFilter(pipeline, provider, country)).FirstOrDefaultAsync(cancellationToken)!;

    // A single atomic upsert with $setOnInsert - only writes when no document exists yet for this
    // (Pipeline, Provider, Country) triple, and does so without a separate existence-check round
    // trip (unlike a plain "find then insert", this can't race with a concurrent seed of the same
    // provider-country either, backed by the unique index below). Id is deliberately part of the
    // $setOnInsert, not left for Mongo to assign - an upsert's server-side insert bypasses the C#
    // driver's own StringObjectIdGenerator entirely (that only runs for InsertOneAsync), so without
    // this the server would assign a native BSON ObjectId _id that then fails to deserialize back
    // into the string-typed Id property on every subsequent read.
    public Task SeedIfMissingAsync(ProviderSchedule schedule, CancellationToken cancellationToken)
    {
        var filter = BuildKeyFilter(schedule.Pipeline, schedule.Provider, schedule.Country);
        var update = Builders<ProviderSchedule>.Update
            .SetOnInsert(s => s.Id, ObjectId.GenerateNewId().ToString())
            .SetOnInsert(s => s.Pipeline, schedule.Pipeline)
            .SetOnInsert(s => s.Provider, schedule.Provider)
            .SetOnInsert(s => s.Country, schedule.Country)
            .SetOnInsert(s => s.Enabled, schedule.Enabled)
            .SetOnInsert(s => s.Cron, schedule.Cron)
            .SetOnInsert(s => s.TimeZone, schedule.TimeZone)
            .SetOnInsert(s => s.SaveRawResponses, schedule.SaveRawResponses)
            .SetOnInsert(s => s.BaseUrl, schedule.BaseUrl)
            .SetOnInsert(s => s.AuthType, schedule.AuthType)
            .SetOnInsert(s => s.AuthParamName, schedule.AuthParamName)
            .SetOnInsert(s => s.TimeoutSeconds, schedule.TimeoutSeconds)
            .SetOnInsert(s => s.UpdatedAt, schedule.UpdatedAt);

        return _collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken);
    }

    // Same Id-on-insert reasoning as SeedIfMissingAsync above - this upsert can also be the very
    // first write for a provider-country (e.g. a user adds a brand-new provider from the UI).
    public Task UpsertAsync(ProviderSchedule schedule, CancellationToken cancellationToken)
    {
        var filter = BuildKeyFilter(schedule.Pipeline, schedule.Provider, schedule.Country);
        var update = Builders<ProviderSchedule>.Update
            .SetOnInsert(s => s.Id, ObjectId.GenerateNewId().ToString())
            .Set(s => s.Enabled, schedule.Enabled)
            .Set(s => s.Cron, schedule.Cron)
            .Set(s => s.TimeZone, schedule.TimeZone)
            .Set(s => s.SaveRawResponses, schedule.SaveRawResponses)
            .Set(s => s.BaseUrl, schedule.BaseUrl)
            .Set(s => s.AuthType, schedule.AuthType)
            .Set(s => s.AuthParamName, schedule.AuthParamName)
            .Set(s => s.TimeoutSeconds, schedule.TimeoutSeconds)
            .Set(s => s.UpdatedAt, schedule.UpdatedAt);

        return _collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken);
    }

    public async Task BackfillCatalogFieldsAsync(
        CrawlPipeline pipeline,
        string provider,
        string country,
        bool saveRawResponses,
        string? baseUrl,
        ApiAuthType? authType,
        string? authParamName,
        int? timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var update = Builders<ProviderSchedule>.Update
            .Set(s => s.SaveRawResponses, saveRawResponses)
            .Set(s => s.BaseUrl, baseUrl)
            .Set(s => s.AuthType, authType)
            .Set(s => s.AuthParamName, authParamName)
            .Set(s => s.TimeoutSeconds, timeoutSeconds);

        await _collection.UpdateOneAsync(BuildKeyFilter(pipeline, provider, country), update, cancellationToken: cancellationToken);
    }

    public async Task<bool> DeleteAsync(CrawlPipeline pipeline, string provider, string country, CancellationToken cancellationToken)
    {
        var result = await _collection.DeleteOneAsync(BuildKeyFilter(pipeline, provider, country), cancellationToken);
        return result.DeletedCount > 0;
    }

    private static FilterDefinition<ProviderSchedule> BuildKeyFilter(CrawlPipeline pipeline, string provider, string country) =>
        Builders<ProviderSchedule>.Filter.And(
            Builders<ProviderSchedule>.Filter.Eq(s => s.Pipeline, pipeline),
            Builders<ProviderSchedule>.Filter.Eq(s => s.Provider, provider),
            Builders<ProviderSchedule>.Filter.Eq(s => s.Country, country));

    // The old 2-field unique index can't just be widened in place under the same name - MongoDB
    // rejects a createIndexes call reusing an existing name with a different key spec (same
    // limitation already handled for the RawResponseRetention TTL index, see
    // RssRawResponseRepository.EnsureTtlIndexAsync) - so this drops the old index by name first if
    // it's still present, then creates the new 3-field one under a new name. Widening the key from
    // (Pipeline, Provider) to (Pipeline, Provider, Country) is safe against existing data (any
    // 2-field-unique dataset is trivially 3-field-unique too), so this is purely about working
    // around Mongo's no-alter-in-place limitation, not a data conflict.
    public async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var existingIndexes = await (await _collection.Indexes.ListAsync(cancellationToken)).ToListAsync(cancellationToken);
        if (existingIndexes.Any(i => i["name"].AsString == LegacyIndexName))
        {
            await _collection.Indexes.DropOneAsync(LegacyIndexName, cancellationToken);
        }

        var model = new CreateIndexModel<ProviderSchedule>(
            Builders<ProviderSchedule>.IndexKeys.Ascending(s => s.Pipeline).Ascending(s => s.Provider).Ascending(s => s.Country),
            new CreateIndexOptions { Name = IndexName, Unique = true });

        await _collection.Indexes.CreateOneAsync(model, cancellationToken: cancellationToken);
    }
}
