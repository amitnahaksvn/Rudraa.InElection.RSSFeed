using MongoDB.Bson;
using MongoDB.Driver;
using Application.Abstractions;
using Domain.Entities;
using Infrastructure.Mongo;

namespace Infrastructure.Persistence;

public sealed class RssRawResponseRepository : IRssRawResponseRepository
{
    private readonly IMongoCollection<RssRawResponse> _collection;

    public RssRawResponseRepository(MongoDbContext context)
    {
        _collection = context.RssRawResponses;
    }

    public Task InsertAsync(RssRawResponse response, CancellationToken cancellationToken) =>
        _collection.InsertOneAsync(response, options: null, cancellationToken);

    public async Task<IReadOnlyList<RssRawResponse>> GetRecentAsync(
        string provider, string feedName, int count, CancellationToken cancellationToken) =>
        await _collection
            .Find(r => r.Provider == provider && r.FeedName == feedName)
            .SortByDescending(r => r.FetchedAt)
            .Limit(count)
            .ToListAsync(cancellationToken);

    public async Task<long> DeleteOlderThanAsync(DateTimeOffset olderThan, CancellationToken cancellationToken)
    {
        var result = await _collection.DeleteManyAsync(r => r.CreatedAt < olderThan, cancellationToken);
        return result.DeletedCount;
    }

    private const string TtlIndexName = "ttl_rawresponse_createdat";

    public async Task EnsureIndexesAsync(TimeSpan retention, CancellationToken cancellationToken)
    {
        var models = new List<CreateIndexModel<RssRawResponse>>
        {
            new(Builders<RssRawResponse>.IndexKeys.Ascending(r => r.Provider).Ascending(r => r.FeedName),
                new CreateIndexOptions { Name = "ix_rawresponse_provider_feed" }),
            new(Builders<RssRawResponse>.IndexKeys.Descending(r => r.FetchedAt),
                new CreateIndexOptions { Name = "ix_rawresponse_fetchedat" }),
            new(Builders<RssRawResponse>.IndexKeys.Ascending(r => r.ContentHash),
                new CreateIndexOptions { Name = "ix_rawresponse_contenthash" })
        };

        await _collection.Indexes.CreateManyAsync(models, cancellationToken);

        // The TTL index's expireAfterSeconds is data (the configured retention), not schema - it
        // changes whenever NewsCrawler:RawResponseRetention changes (e.g. 30 -> 7 days), and
        // MongoDB's createIndexes rejects an index that already exists under the same name with
        // different options rather than updating it in place. So this index is handled separately:
        // drop and recreate only when the existing one's expireAfterSeconds actually differs.
        await EnsureTtlIndexAsync(retention, cancellationToken);
    }

    private async Task EnsureTtlIndexAsync(TimeSpan retention, CancellationToken cancellationToken)
    {
        var existing = await _collection.Indexes.ListAsync(cancellationToken);
        var existingIndexes = await existing.ToListAsync(cancellationToken);
        var ttlIndex = existingIndexes.FirstOrDefault(i => i["name"].AsString == TtlIndexName);

        if (ttlIndex is not null)
        {
            var currentExpireAfterSeconds = ttlIndex.GetValue("expireAfterSeconds", BsonNull.Value);
            var desiredExpireAfterSeconds = (long)retention.TotalSeconds;

            if (currentExpireAfterSeconds.IsNumeric && currentExpireAfterSeconds.ToInt64() == desiredExpireAfterSeconds)
            {
                return;
            }

            await _collection.Indexes.DropOneAsync(TtlIndexName, cancellationToken);
        }

        await _collection.Indexes.CreateOneAsync(
            new CreateIndexModel<RssRawResponse>(
                Builders<RssRawResponse>.IndexKeys.Ascending(r => r.CreatedAt),
                new CreateIndexOptions { Name = TtlIndexName, ExpireAfter = retention }),
            cancellationToken: cancellationToken);
    }
}
