using Domain.Entities;

namespace Application.Abstractions;

/// <summary>Orchestrates one end-to-end crawl run across every enabled provider/feed.</summary>
public interface INewsCrawlerService
{
    /// <summary>Crawls every enabled provider.</summary>
    Task<CrawlHistory> RunCrawlAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Crawls only the given (still individually enabled) providers - used by the scheduler when,
    /// on a given tick, only some providers' own cron schedules are due.
    /// </summary>
    Task<CrawlHistory> RunCrawlAsync(IReadOnlyCollection<string> providerNames, CancellationToken cancellationToken);
}
