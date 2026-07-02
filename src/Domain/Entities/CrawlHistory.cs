using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// A record of a single crawler execution, spanning every provider/feed processed in that run.
/// </summary>
public sealed class CrawlHistory
{
    public string Id { get; set; } = string.Empty;

    public DateTimeOffset StartTime { get; set; }

    public DateTimeOffset? EndTime { get; set; }

    public TimeSpan? Duration { get; set; }

    public int FeedCount { get; set; }

    public int NewArticles { get; set; }

    public int UpdatedArticles { get; set; }

    public int DuplicateArticles { get; set; }

    public List<string> FailedFeeds { get; set; } = [];

    public CrawlStatus Status { get; set; } = CrawlStatus.Running;

    public string? Error { get; set; }
}
