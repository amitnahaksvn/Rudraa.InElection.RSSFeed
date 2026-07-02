using Domain.Entities;
using Domain.Enums;

namespace Application.Crawl.Dtos;

/// <summary>Read projection of a <see cref="CrawlHistory"/> record.</summary>
public sealed record CrawlHistoryDto(
    string Id,
    DateTimeOffset StartTime,
    DateTimeOffset? EndTime,
    TimeSpan? Duration,
    int FeedCount,
    int NewArticles,
    int UpdatedArticles,
    int DuplicateArticles,
    IReadOnlyList<string> FailedFeeds,
    string Status,
    string? Error)
{
    /// <summary>True when this run did not execute because another run already held the crawl lock.</summary>
    public bool WasSkipped => Status == nameof(CrawlStatus.Skipped);

    public static CrawlHistoryDto FromDomain(CrawlHistory history) => new(
        history.Id,
        history.StartTime,
        history.EndTime,
        history.Duration,
        history.FeedCount,
        history.NewArticles,
        history.UpdatedArticles,
        history.DuplicateArticles,
        history.FailedFeeds,
        history.Status.ToString(),
        history.Error);
}
