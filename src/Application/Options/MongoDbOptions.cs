using System.ComponentModel.DataAnnotations;

namespace Application.Options;

/// <summary>
/// Root configuration section ("MongoDb") controlling the database connection and collection names.
/// </summary>
public sealed class MongoDbOptions
{
    public const string SectionName = "MongoDb";

    [Required]
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";

    [Required]
    public string DatabaseName { get; set; } = "PoliticalNewsDb";

    [Required]
    public string NewsArticlesCollection { get; set; } = "NewsArticles";

    [Required]
    public string CrawlHistoryCollection { get; set; } = "CrawlHistory";

    [Required]
    public string CrawlLockCollection { get; set; } = "CrawlLock";

    [Required]
    public string RssRawResponsesCollection { get; set; } = "RssRawResponses";

    /// <summary>Mongo-driven feed configuration read by <c>DynamicFeedIngestionService</c> - the file-free alternative to <c>NewsCrawler.appsettings.json</c>.</summary>
    [Required]
    public string FeedSourcesCollection { get; set; } = "FeedSources";

    [Required]
    public string FeedErrorLogsCollection { get; set; } = "FeedErrorLogs";

    /// <summary>General app-wide exception log (crawl failures, dynamic feed failures, unhandled HTTP request exceptions) - see <c>Domain.Entities.ErrorLog</c>.</summary>
    [Required]
    public string ErrorLogsCollection { get; set; } = "ErrorLogs";

    /// <summary>Mongo-driven channel list for the Social pipeline (YouTube today; Facebook/Telegram/Website/Rss recognized but not yet fetched) - see <c>Domain.Entities.SocialMediaSource</c>/<c>SocialMediaIngestionService</c>.</summary>
    [Required]
    public string SocialMediaSourcesCollection { get; set; } = "SocialMediaSources";
}
