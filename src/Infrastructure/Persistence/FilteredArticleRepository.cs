using MongoDB.Driver;
using Application.Abstractions;
using Domain.Entities;
using Infrastructure.Mongo;

namespace Infrastructure.Persistence;

public sealed class FilteredArticleRepository : IFilteredArticleRepository
{
    private readonly IMongoCollection<FilteredArticle> _collection;

    public FilteredArticleRepository(MongoDbContext context)
    {
        _collection = context.FilteredArticles;
    }

    public Task InsertAsync(FilteredArticle article, CancellationToken cancellationToken) =>
        _collection.InsertOneAsync(article, options: null, cancellationToken);

    public async Task<IReadOnlyList<FilteredArticle>> GetPagedAsync(int skip, int limit, CancellationToken cancellationToken) =>
        await _collection
            .Find(FilterDefinition<FilteredArticle>.Empty)
            .SortByDescending(a => a.PulledAt)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync(cancellationToken);

    public Task<long> CountAsync(CancellationToken cancellationToken) =>
        _collection.CountDocumentsAsync(FilterDefinition<FilteredArticle>.Empty, cancellationToken: cancellationToken);

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var result = await _collection.DeleteOneAsync(a => a.Id == id, cancellationToken);
        return result.DeletedCount > 0;
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var models = new List<CreateIndexModel<FilteredArticle>>
        {
            new(Builders<FilteredArticle>.IndexKeys.Descending(a => a.PulledAt),
                new CreateIndexOptions { Name = "ix_filteredarticle_pulledat" }),
            new(Builders<FilteredArticle>.IndexKeys.Ascending(a => a.Provider),
                new CreateIndexOptions { Name = "ix_filteredarticle_provider" })
        };

        await _collection.Indexes.CreateManyAsync(models, cancellationToken);
    }
}
