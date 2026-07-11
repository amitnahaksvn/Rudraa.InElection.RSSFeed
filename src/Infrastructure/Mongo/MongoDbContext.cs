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
        ArticleFingerprints = Database.GetCollection<ArticleFingerprint>(settings.ArticleFingerprintsCollection);
        CrawlHistory = Database.GetCollection<CrawlHistory>(settings.CrawlHistoryCollection);
        CrawlLocks = Database.GetCollection<CrawlLock>(settings.CrawlLockCollection);
        RssRawResponses = Database.GetCollection<RssRawResponse>(settings.RssRawResponsesCollection);
        FeedSources = Database.GetCollection<FeedSource>(settings.FeedSourcesCollection);
        FeedErrorLogs = Database.GetCollection<FeedErrorLog>(settings.FeedErrorLogsCollection);
        ErrorLogs = Database.GetCollection<ErrorLog>(settings.ErrorLogsCollection);
        SocialMediaSources = Database.GetCollection<SocialMediaSource>(settings.SocialMediaSourcesCollection);
        ProviderSchedules = Database.GetCollection<ProviderSchedule>(settings.ProviderSchedulesCollection);
    }

    public IMongoClient Client { get; }

    public IMongoDatabase Database { get; }

    public IMongoCollection<NewsArticle> NewsArticles { get; }

    /// <summary>Lean duplicate-detection records - see <see cref="Application.Options.MongoDbOptions.ArticleFingerprintsCollection"/>.</summary>
    public IMongoCollection<ArticleFingerprint> ArticleFingerprints { get; }

    public IMongoCollection<CrawlHistory> CrawlHistory { get; }

    public IMongoCollection<CrawlLock> CrawlLocks { get; }

    public IMongoCollection<RssRawResponse> RssRawResponses { get; }

    /// <summary>Mongo-driven feed configuration - see <see cref="Application.Options.MongoDbOptions.FeedSourcesCollection"/>.</summary>
    public IMongoCollection<FeedSource> FeedSources { get; }

    public IMongoCollection<FeedErrorLog> FeedErrorLogs { get; }

    public IMongoCollection<ErrorLog> ErrorLogs { get; }

    /// <summary>Mongo-driven channel list for the Social pipeline - see <see cref="Application.Options.MongoDbOptions.SocialMediaSourcesCollection"/>.</summary>
    public IMongoCollection<SocialMediaSource> SocialMediaSources { get; }

    /// <summary>Database-backed provider Enabled/Cron/TimeZone - see <see cref="Application.Options.MongoDbOptions.ProviderSchedulesCollection"/>.</summary>
    public IMongoCollection<ProviderSchedule> ProviderSchedules { get; }
}
