using MongoDB.Bson;
using MongoDB.Driver;
using Application.Abstractions;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Mongo;

namespace Infrastructure.Persistence;

public sealed class CrawlFeedRepository : ICrawlFeedRepository
{
    private readonly IMongoCollection<CrawlFeed> _collection;

    public CrawlFeedRepository(MongoDbContext context)
    {
        _collection = context.CrawlFeeds;
    }

    public async Task<IReadOnlyList<CrawlFeed>> GetAllAsync(CrawlPipeline pipeline, CancellationToken cancellationToken) =>
        await _collection.Find(f => f.Pipeline == pipeline).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CrawlFeed>> GetByProviderAsync(CrawlPipeline pipeline, string provider, CancellationToken cancellationToken) =>
        await _collection.Find(f => f.Pipeline == pipeline && f.Provider == provider).ToListAsync(cancellationToken);

    public Task<CrawlFeed?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
        _collection.Find(f => f.Id == id).FirstOrDefaultAsync(cancellationToken)!;

    public async Task<string> CreateAsync(CrawlFeed feed, CancellationToken cancellationToken)
    {
        feed.Id = ObjectId.GenerateNewId().ToString();
        await _collection.InsertOneAsync(feed, options: null, cancellationToken);
        return feed.Id;
    }

    public async Task<bool> UpdateAsync(CrawlFeed feed, CancellationToken cancellationToken)
    {
        var update = Builders<CrawlFeed>.Update
            .Set(f => f.Provider, feed.Provider)
            .Set(f => f.Country, feed.Country)
            .Set(f => f.Name, feed.Name)
            .Set(f => f.Url, feed.Url)
            .Set(f => f.Category, feed.Category)
            .Set(f => f.Language, feed.Language)
            .Set(f => f.Enabled, feed.Enabled)
            .Set(f => f.DefaultImageUrl, feed.DefaultImageUrl)
            .Set(f => f.QueryParameters, feed.QueryParameters);

        var result = await _collection.UpdateOneAsync(f => f.Id == feed.Id, update, cancellationToken: cancellationToken);
        return result.MatchedCount > 0;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var result = await _collection.DeleteOneAsync(f => f.Id == id, cancellationToken);
        return result.DeletedCount > 0;
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var models = new List<CreateIndexModel<CrawlFeed>>
        {
            new(Builders<CrawlFeed>.IndexKeys.Ascending(f => f.Pipeline).Ascending(f => f.Provider),
                new CreateIndexOptions { Name = "ix_crawlfeed_pipeline_provider" })
        };

        await _collection.Indexes.CreateManyAsync(models, cancellationToken);
    }
}
