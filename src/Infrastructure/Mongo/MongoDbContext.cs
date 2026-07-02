using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Application.Options;
using Domain.Entities;

namespace Infrastructure.Mongo;

/// <summary>
/// Single access point to the configured Mongo database and its collections.
/// Registered as a singleton - <see cref="IMongoClient"/>/<see cref="IMongoDatabase"/> are thread-safe.
/// </summary>
public sealed class MongoDbContext
{
    public MongoDbContext(IOptions<MongoDbOptions> options)
    {
        var settings = options.Value;
        Client = new MongoClient(settings.ConnectionString);
        Database = Client.GetDatabase(settings.DatabaseName);

        NewsArticles = Database.GetCollection<NewsArticle>(settings.NewsArticlesCollection);
        CrawlHistory = Database.GetCollection<CrawlHistory>(settings.CrawlHistoryCollection);
        CrawlLocks = Database.GetCollection<CrawlLock>(settings.CrawlLockCollection);
        RssRawResponses = Database.GetCollection<RssRawResponse>(settings.RssRawResponsesCollection);
    }

    public IMongoClient Client { get; }

    public IMongoDatabase Database { get; }

    public IMongoCollection<NewsArticle> NewsArticles { get; }

    public IMongoCollection<CrawlHistory> CrawlHistory { get; }

    public IMongoCollection<CrawlLock> CrawlLocks { get; }

    public IMongoCollection<RssRawResponse> RssRawResponses { get; }
}
