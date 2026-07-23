using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Application.Abstractions;
using Application.Options;
using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.Seed;

/// <summary>
/// One-time migration closing the gap left by <see cref="CrawlCatalogMigrationSeeder"/>: that
/// original migration collapsed a provider configured under multiple JSON country blocks (e.g.
/// SerpApiGoogleNews under India/UK/Canada/Australia) into a single <see cref="ProviderSchedule"/>
/// row - whichever country happened to win the insert race - while still creating one
/// <see cref="CrawlFeed"/> row per country appearance. Since <see cref="ProviderSchedule"/>'s
/// identity is now (Pipeline, Provider, Country) instead of (Pipeline, Provider), every article
/// from that one collapsed schedule got mislabeled with a single country regardless of which
/// country's endpoint it actually came from.
///
/// This migrator only needs to backfill <see cref="CrawlFeed.Country"/> on the already-existing
/// feed/endpoint documents, matched back against the still-on-disk JSON
/// (<c>NewsCrawler.appsettings.json</c>/<c>NewsApiCrawler</c>) by whole-shape equality
/// (Provider, Name, Url, QueryParameters) - not a guessed query-parameter key, since some
/// providers' country signal lives inside a free-text value (e.g. GDELT's
/// <c>query: "India politics sourcecountry:IN"</c>) rather than a distinguishing key. It
/// deliberately does <em>not</em> write any new <see cref="ProviderSchedule"/> rows itself -
/// once <see cref="IProviderScheduleRepository"/>'s key includes Country, the already-existing
/// <see cref="ProviderScheduleSeeder"/> (which already runs on every RssService/ApiService
/// startup) inserts the missing per-country schedule rows on its own, using each country's own
/// real JSON-configured Cron/Enabled - genuine historical data, not a cloned guess.
///
/// Idempotent/re-runnable: only feeds whose <see cref="CrawlFeed.Country"/> is still empty are
/// touched, so a second run is a no-op. Run exactly once, by hand, via WebApp's own
/// <c>--migrate-provider-countries</c> flag - never on a normal startup.
/// </summary>
public sealed class ProviderCountrySplitMigrator
{
    private readonly ICrawlFeedRepository _feeds;
    private readonly IProviderScheduleRepository _schedules;
    private readonly NewsCrawlerOptions _rssOptions;
    private readonly NewsApiCrawlerOptions _apiOptions;
    private readonly ILogger<ProviderCountrySplitMigrator> _logger;

    public ProviderCountrySplitMigrator(
        ICrawlFeedRepository feeds,
        IProviderScheduleRepository schedules,
        IOptions<NewsCrawlerOptions> rssOptions,
        IOptions<NewsApiCrawlerOptions> apiOptions,
        ILogger<ProviderCountrySplitMigrator> logger)
    {
        _feeds = feeds;
        _schedules = schedules;
        _rssOptions = rssOptions.Value;
        _apiOptions = apiOptions.Value;
        _logger = logger;
    }

    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        // The --migrate-provider-countries CLI path builds its own throwaway ServiceProvider and
        // never goes through MongoIndexInitializerHostedService's normal startup sequence, so the
        // (Pipeline, Provider, Country) unique index has to be created explicitly here - otherwise
        // ProviderScheduleSeeder's own next-startup pass would race against an index that doesn't
        // exist yet.
        await _schedules.EnsureIndexesAsync(cancellationToken);
        await _feeds.EnsureIndexesAsync(cancellationToken);

        var rssBackfilled = await BackfillRssAsync(cancellationToken);
        var apiBackfilled = await BackfillApiAsync(cancellationToken);

        _logger.LogInformation(
            "Provider-country split migration complete: {RssBackfilled} RSS feeds, {ApiBackfilled} API endpoints backfilled with Country. " +
            "Restart RssService/ApiService next so ProviderScheduleSeeder inserts the missing per-country schedule rows.",
            rssBackfilled, apiBackfilled);
    }

    private async Task<int> BackfillRssAsync(CancellationToken cancellationToken)
    {
        var existingFeeds = await _feeds.GetAllAsync(CrawlPipeline.Rss, cancellationToken);
        var backfilled = 0;

        foreach (var country in _rssOptions.Countries)
        {
            foreach (var provider in country.Providers)
            {
                foreach (var feed in provider.Feeds)
                {
                    var match = existingFeeds.FirstOrDefault(f =>
                        string.IsNullOrEmpty(f.Country) &&
                        string.Equals(f.Provider, provider.Name, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(f.Name, feed.Name, StringComparison.Ordinal) &&
                        string.Equals(f.Url, feed.Url, StringComparison.Ordinal));

                    if (match is null)
                    {
                        continue;
                    }

                    match.Country = country.Name;
                    await _feeds.UpdateAsync(match, cancellationToken);
                    backfilled++;
                }
            }
        }

        return backfilled;
    }

    private async Task<int> BackfillApiAsync(CancellationToken cancellationToken)
    {
        var existingFeeds = await _feeds.GetAllAsync(CrawlPipeline.Api, cancellationToken);
        var backfilled = 0;

        foreach (var country in _apiOptions.Countries)
        {
            foreach (var provider in country.Providers)
            {
                foreach (var endpoint in provider.Endpoints)
                {
                    var match = existingFeeds.FirstOrDefault(f =>
                        string.IsNullOrEmpty(f.Country) &&
                        string.Equals(f.Provider, provider.Name, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(f.Name, endpoint.Name, StringComparison.Ordinal) &&
                        string.Equals(f.Url, endpoint.Endpoint, StringComparison.Ordinal) &&
                        QueryParametersEqual(f.QueryParameters, endpoint.QueryParameters));

                    if (match is null)
                    {
                        continue;
                    }

                    match.Country = country.Name;
                    await _feeds.UpdateAsync(match, cancellationToken);
                    backfilled++;
                }
            }
        }

        return backfilled;
    }

    // Full dictionary-content equality, not reference equality - this is what actually
    // distinguishes e.g. SerpApiGoogleNews's 4 country-specific endpoints from one another
    // (identical Name/Url, different QueryParameters), so it has to be exact.
    private static bool QueryParametersEqual(Dictionary<string, string>? a, Dictionary<string, string>? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        if (a.Count != b.Count)
        {
            return false;
        }

        foreach (var (key, value) in a)
        {
            if (!b.TryGetValue(key, out var otherValue) || otherValue != value)
            {
                return false;
            }
        }

        return true;
    }
}
