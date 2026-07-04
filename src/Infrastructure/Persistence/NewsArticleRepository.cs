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

            try
            {
                await _collection.InsertOneAsync(article, options: null, cancellationToken);
                return ArticleUpsertOutcome.Inserted;
            }
            catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                // Another concurrent insert (a different provider's parallel crawl, or a manual
                // trigger overlapping a still-running scheduled one) won the race on the Url/Hash
                // unique index between this method's find-checks above and this InsertOneAsync -
                // the article is already persisted, so this is a duplicate, not a real failure.
                // Without this, an uncaught MongoWriteException here would abort the *entire*
                // crawl run as Failed over what is actually a successful dedup.
                return ArticleUpsertOutcome.DuplicateSkipped;
            }
        }

        var contentChanged =
            existing.Url != article.Url ||
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

        await EnsureUniqueHashIndexAsync(cancellationToken);
    }

    private const string HashIndexName = "ux_news_hash";
    private const string LegacyHashIndexName = "ix_news_hash";

    /// <summary>
    /// The Hash tier of <c>UpsertAsync</c>'s dedup check (Url -&gt; OriginalGuid -&gt; Hash) was
    /// only ever a non-atomic check-then-act over a plain, non-unique index - two providers
    /// crawling the same story in the same tick (parallel by design, see
    /// NewsCrawlerOrchestrator) could both pass FindByHashAsync before either had inserted,
    /// violating the "articles are never duplicated" invariant. A unique index makes the DB
    /// itself the source of truth; InsertOneAsync's catch for ServerErrorCategory.DuplicateKey
    /// in UpsertAsync now closes the race for both the Url and Hash tiers.
    /// </summary>
    private async Task EnsureUniqueHashIndexAsync(CancellationToken cancellationToken)
    {
        var existingIndexes = await (await _collection.Indexes.ListAsync(cancellationToken)).ToListAsync(cancellationToken);

        if (existingIndexes.Any(i => i["name"].AsString == HashIndexName))
        {
            return;
        }

        if (existingIndexes.Any(i => i["name"].AsString == LegacyHashIndexName))
        {
            await _collection.Indexes.DropOneAsync(LegacyHashIndexName, cancellationToken);
        }

        try
        {
            await _collection.Indexes.CreateOneAsync(
                new CreateIndexModel<NewsArticle>(
                    Builders<NewsArticle>.IndexKeys.Ascending(a => a.Hash),
                    new CreateIndexOptions { Unique = true, Name = HashIndexName }),
                cancellationToken: cancellationToken);
        }
        catch (MongoCommandException ex) when (ex.Code == 11000)
        {
            // Data from before this constraint existed already has two articles sharing the same
            // Hash (same normalized title + PublishedAt) - the unique index build itself fails
            // over pre-existing duplicates, so this falls back to the application-level
            // FindByHashAsync pre-check rather than crashing MongoIndexInitializerHostedService
            // (and the whole host) over data that needs a manual cleanup pass, not a startup
            // failure. Concurrent inserts racing on a brand-new hash remain unprotected until the
            // existing duplicates are removed and this index is retried.
        }
    }
}
