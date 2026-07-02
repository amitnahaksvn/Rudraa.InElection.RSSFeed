using MongoDB.Driver;
using Application.Abstractions;
using Domain.Entities;
using Infrastructure.Mongo;

namespace Infrastructure.Persistence;

public sealed class NewsArticleRepository : INewsArticleRepository
{
    private readonly IMongoCollection<NewsArticle> _collection;

    public NewsArticleRepository(MongoDbContext context)
    {
        _collection = context.NewsArticles;
    }

    public Task<NewsArticle?> FindByUrlAsync(string url, CancellationToken cancellationToken) =>
        _collection.Find(a => a.Url == url).FirstOrDefaultAsync(cancellationToken)!;

    public Task<NewsArticle?> FindByOriginalGuidAsync(string originalGuid, CancellationToken cancellationToken) =>
        _collection.Find(a => a.OriginalGuid == originalGuid).FirstOrDefaultAsync(cancellationToken)!;

    public Task<NewsArticle?> FindByHashAsync(string hash, CancellationToken cancellationToken) =>
        _collection.Find(a => a.Hash == hash).FirstOrDefaultAsync(cancellationToken)!;

    public async Task<ArticleUpsertOutcome> UpsertAsync(NewsArticle article, CancellationToken cancellationToken)
    {
        var existing = await FindByUrlAsync(article.Url, cancellationToken);

        existing ??= !string.IsNullOrEmpty(article.OriginalGuid)
            ? await FindByOriginalGuidAsync(article.OriginalGuid, cancellationToken)
            : null;

        if (existing is null)
        {
            var byHash = await FindByHashAsync(article.Hash, cancellationToken);
            if (byHash is not null)
            {
                // Same story (Title + PublishedAt) already stored under a different Url/guid.
                return ArticleUpsertOutcome.DuplicateSkipped;
            }

            await _collection.InsertOneAsync(article, options: null, cancellationToken);
            return ArticleUpsertOutcome.Inserted;
        }

        var contentChanged =
            existing.Title != article.Title ||
            existing.Summary != article.Summary ||
            existing.Content != article.Content ||
            existing.ImageUrl != article.ImageUrl;

        if (!contentChanged)
        {
            return ArticleUpsertOutcome.DuplicateSkipped;
        }

        article.Id = existing.Id;
        article.CrawledAt = existing.CrawledAt;
        article.UpdatedAt = DateTimeOffset.UtcNow;

        await _collection.ReplaceOneAsync(a => a.Id == existing.Id, article, cancellationToken: cancellationToken);
        return ArticleUpsertOutcome.Updated;
    }

    public async Task<IReadOnlyList<NewsArticle>> GetLatestAsync(int count, CancellationToken cancellationToken) =>
        await _collection.Find(a => a.IsActive)
            .SortByDescending(a => a.CrawledAt)
            .Limit(count)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<NewsArticle>> GetByProviderAsync(string provider, int count, CancellationToken cancellationToken) =>
        await _collection.Find(a => a.IsActive && a.Provider == provider)
            .SortByDescending(a => a.CrawledAt)
            .Limit(count)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<NewsArticle>> GetByCategoryAsync(string category, int count, CancellationToken cancellationToken) =>
        await _collection.Find(a => a.IsActive && a.Category == category)
            .SortByDescending(a => a.CrawledAt)
            .Limit(count)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<NewsArticle>> SearchAsync(string query, int count, CancellationToken cancellationToken)
    {
        var filter = Builders<NewsArticle>.Filter.And(
            Builders<NewsArticle>.Filter.Eq(a => a.IsActive, true),
            Builders<NewsArticle>.Filter.Or(
                Builders<NewsArticle>.Filter.Regex(a => a.Title, new MongoDB.Bson.BsonRegularExpression(query, "i")),
                Builders<NewsArticle>.Filter.Regex(a => a.Summary, new MongoDB.Bson.BsonRegularExpression(query, "i"))));

        return await _collection.Find(filter)
            .SortByDescending(a => a.CrawledAt)
            .Limit(count)
            .ToListAsync(cancellationToken);
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var models = new List<CreateIndexModel<NewsArticle>>
        {
            new(Builders<NewsArticle>.IndexKeys.Ascending(a => a.Url),
                new CreateIndexOptions { Unique = true, Name = "ux_news_url" }),
            new(Builders<NewsArticle>.IndexKeys.Ascending(a => a.OriginalGuid),
                new CreateIndexOptions { Name = "ix_news_originalguid" }),
            new(Builders<NewsArticle>.IndexKeys.Ascending(a => a.Hash),
                new CreateIndexOptions { Name = "ix_news_hash" }),
            new(Builders<NewsArticle>.IndexKeys.Descending(a => a.PublishedAt),
                new CreateIndexOptions { Name = "ix_news_publishedat" }),
            new(Builders<NewsArticle>.IndexKeys.Descending(a => a.CrawledAt),
                new CreateIndexOptions { Name = "ix_news_crawledat" }),
            new(Builders<NewsArticle>.IndexKeys.Ascending(a => a.Provider),
                new CreateIndexOptions { Name = "ix_news_provider" }),
            new(Builders<NewsArticle>.IndexKeys.Ascending(a => a.Category),
                new CreateIndexOptions { Name = "ix_news_category" })
        };

        await _collection.Indexes.CreateManyAsync(models, cancellationToken);
    }
}
