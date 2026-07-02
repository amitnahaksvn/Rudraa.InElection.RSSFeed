using MongoDB.Bson;
using MongoDB.Driver;
using Application.Abstractions;
using Domain.Entities;
using Infrastructure.Mongo;

namespace Infrastructure.Persistence;

public sealed class CrawlHistoryRepository : ICrawlHistoryRepository
{
    private readonly IMongoCollection<CrawlHistory> _collection;

    public CrawlHistoryRepository(MongoDbContext context)
    {
        _collection = context.CrawlHistory;
    }

    public async Task<string> InsertAsync(CrawlHistory history, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(history.Id))
        {
            history.Id = ObjectId.GenerateNewId().ToString();
        }

        await _collection.InsertOneAsync(history, options: null, cancellationToken);
        return history.Id;
    }

    public Task UpdateAsync(CrawlHistory history, CancellationToken cancellationToken) =>
        _collection.ReplaceOneAsync(h => h.Id == history.Id, history, cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<CrawlHistory>> GetRecentAsync(int count, CancellationToken cancellationToken) =>
        await _collection.Find(FilterDefinition<CrawlHistory>.Empty)
            .SortByDescending(h => h.StartTime)
            .Limit(count)
            .ToListAsync(cancellationToken);

    public async Task<CrawlHistory?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
        await _collection.Find(h => h.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var model = new CreateIndexModel<CrawlHistory>(
            Builders<CrawlHistory>.IndexKeys.Descending(h => h.StartTime),
            new CreateIndexOptions { Name = "ix_crawlhistory_starttime" });

        await _collection.Indexes.CreateOneAsync(model, cancellationToken: cancellationToken);
    }
}
