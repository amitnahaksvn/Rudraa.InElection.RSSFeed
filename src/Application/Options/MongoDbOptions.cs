namespace Application.Options;

/// <summary>
/// Root configuration section ("MongoDb") controlling the database connection and collection names.
/// </summary>
public sealed class MongoDbOptions
{
    public const string SectionName = "MongoDb";

    public string ConnectionString { get; set; } = "mongodb://localhost:27017";

    public string DatabaseName { get; set; } = "PoliticalNewsDb";

    public string NewsArticlesCollection { get; set; } = "NewsArticles";

    public string CrawlHistoryCollection { get; set; } = "CrawlHistory";

    public string CrawlLockCollection { get; set; } = "CrawlLock";

    public string RssRawResponsesCollection { get; set; } = "RssRawResponses";
}
