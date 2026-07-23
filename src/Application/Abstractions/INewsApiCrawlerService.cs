using Domain.Entities;

namespace Application.Abstractions;

/// <summary>Orchestrates one end-to-end news-API crawl run across every enabled provider. The <see cref="INewsCrawlerService"/> counterpart for JSON APIs.</summary>
public interface INewsApiCrawlerService
{
    /// <summary>Crawls every enabled news-API provider.</summary>
    Task<CrawlHistory> RunCrawlAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Crawls only the given (still individually enabled) provider-country schedule - used by the
    /// Hangfire executor, which fires per (Provider, Country) schedule row, since the same provider
    /// class can be scheduled independently for more than one country.
    /// </summary>
    Task<CrawlHistory> RunCrawlAsync(string provider, string country, CancellationToken cancellationToken);
}
