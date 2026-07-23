using Application.Models;
using Domain.Enums;

namespace Application.Abstractions;

public interface ICrawlJobStatusReader
{
    /// <summary>RSS and API providers register their recurring job under different id schemes ("news-crawl-{provider}::{country}" vs "news-api-{provider}::{country}") - <paramref name="pipeline"/> picks which one to look up. Social has no recurring per-source job status reader today.</summary>
    /// <returns>Null if no recurring job is registered for that provider-country.</returns>
    CrawlJobStatus? GetStatus(CrawlPipeline pipeline, string providerName, string country);

    /// <summary>
    /// Batched counterpart to <see cref="GetStatus"/> - the crawl-report page needs one
    /// provider-country's status per row, and a provider-at-a-time loop over
    /// <see cref="GetStatus"/> means one Hangfire/Mongo round trip per row (270+ provider-country
    /// schedules), which measured out to ~58 seconds end to end for the pre-per-country provider
    /// count alone. This resolves every requested provider-country's status in a single underlying
    /// query instead.
    /// </summary>
    /// <returns>Keyed by (provider name, country); a provider-country with no registered recurring job is simply absent from the result.</returns>
    IReadOnlyDictionary<(string Provider, string Country), CrawlJobStatus> GetStatuses(
        CrawlPipeline pipeline, IReadOnlyCollection<(string Provider, string Country)> providerCountries);
}
