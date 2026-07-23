using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Application.Abstractions;
using Application.Options;
using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.Seed;

/// <summary>
/// One-time migration from the legacy JSON provider catalog (<c>NewsCrawler.appsettings.json</c>/
/// <c>NewsApiCrawler</c>, merged from <c>Countries.Rss</c>/<c>Countries.Api</c> by
/// <c>WebPlatform.SplitCountryConfigLoader</c>) into the database-backed
/// <see cref="CrawlCountry"/>/<see cref="ProviderSchedule"/>/<see cref="CrawlFeed"/> collections -
/// run exactly once, by hand, via each host's own <c>--migrate-catalog</c> flag, never on a normal
/// startup. Guarded at the top: if any <see cref="CrawlCountry"/> already exists for a pipeline,
/// that whole pipeline is skipped entirely, so accidentally invoking this twice can never
/// duplicate feeds. Provider-level rows use <see cref="IProviderScheduleRepository.SeedIfMissingAsync"/>
/// (not overwrite) specifically because <c>ProviderSchedule</c> already existed before this
/// migration - a provider whose Enabled/Cron/TimeZone was already live-edited via the Provider
/// Management page keeps that edit; only the brand-new catalog fields
/// (SaveRawResponses/BaseUrl/AuthType/AuthParamName/TimeoutSeconds) get backfilled onto an
/// already-existing row, via <see cref="IProviderScheduleRepository.BackfillCatalogFieldsAsync"/>.
/// </summary>
public sealed class CrawlCatalogMigrationSeeder
{
    private readonly ICrawlCountryRepository _countries;
    private readonly IProviderScheduleRepository _schedules;
    private readonly ICrawlFeedRepository _feeds;
    private readonly NewsCrawlerOptions _rssOptions;
    private readonly NewsApiCrawlerOptions _apiOptions;
    private readonly ILogger<CrawlCatalogMigrationSeeder> _logger;

    public CrawlCatalogMigrationSeeder(
        ICrawlCountryRepository countries,
        IProviderScheduleRepository schedules,
        ICrawlFeedRepository feeds,
        IOptions<NewsCrawlerOptions> rssOptions,
        IOptions<NewsApiCrawlerOptions> apiOptions,
        ILogger<CrawlCatalogMigrationSeeder> logger)
    {
        _countries = countries;
        _schedules = schedules;
        _feeds = feeds;
        _rssOptions = rssOptions.Value;
        _apiOptions = apiOptions.Value;
        _logger = logger;
    }

    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        var existingRssCountries = await _countries.GetAllAsync(CrawlPipeline.Rss, cancellationToken);
        if (existingRssCountries.Count > 0)
        {
            _logger.LogWarning("{Count} CrawlCountry rows already exist for Rss - skipping RSS migration (already run once).", existingRssCountries.Count);
        }
        else
        {
            await MigrateRssAsync(cancellationToken);
        }

        var existingApiCountries = await _countries.GetAllAsync(CrawlPipeline.Api, cancellationToken);
        if (existingApiCountries.Count > 0)
        {
            _logger.LogWarning("{Count} CrawlCountry rows already exist for Api - skipping API migration (already run once).", existingApiCountries.Count);
        }
        else
        {
            await MigrateApiAsync(cancellationToken);
        }
    }

    private async Task MigrateRssAsync(CancellationToken cancellationToken)
    {
        int countryCount = 0, providerCount = 0, backfilledCount = 0, feedCount = 0;

        foreach (var country in _rssOptions.Countries)
        {
            await _countries.SeedIfMissingAsync(
                new CrawlCountry { Pipeline = CrawlPipeline.Rss, Name = country.Name, Enabled = country.Enabled },
                cancellationToken);
            countryCount++;

            foreach (var provider in country.Providers)
            {
                // ProviderSchedule's identity is (Pipeline, Provider, Country) - a provider name
                // legitimately appearing under more than one country in the old JSON (a shared
                // "global aggregator" configured once per country) now gets one schedule row per
                // country, not a collapsed single row.
                var alreadyExisted = await _schedules.GetAsync(CrawlPipeline.Rss, provider.Name, country.Name, cancellationToken) is not null;

                await _schedules.SeedIfMissingAsync(
                    new ProviderSchedule
                    {
                        Pipeline = CrawlPipeline.Rss,
                        Provider = provider.Name,
                        Country = country.Name,
                        Enabled = provider.Enabled,
                        Cron = provider.Cron,
                        TimeZone = "UTC",
                        SaveRawResponses = provider.SaveRawResponses,
                        UpdatedAt = DateTimeOffset.UtcNow
                    },
                    cancellationToken);

                if (alreadyExisted)
                {
                    await _schedules.BackfillCatalogFieldsAsync(
                        CrawlPipeline.Rss, provider.Name, country.Name, provider.SaveRawResponses, null, null, null, null, cancellationToken);
                    backfilledCount++;
                }

                providerCount++;

                foreach (var feed in provider.Feeds)
                {
                    await _feeds.CreateAsync(
                        new CrawlFeed
                        {
                            Pipeline = CrawlPipeline.Rss,
                            Provider = provider.Name,
                            Country = country.Name,
                            Name = feed.Name,
                            Url = feed.Url,
                            Category = feed.Category,
                            Language = feed.Language,
                            Enabled = feed.Enabled,
                            DefaultImageUrl = feed.DefaultImageUrl
                        },
                        cancellationToken);
                    feedCount++;
                }
            }
        }

        _logger.LogInformation(
            "RSS catalog migration complete: {Countries} countries, {Providers} providers ({Backfilled} backfilled), {Feeds} feeds",
            countryCount, providerCount, backfilledCount, feedCount);
    }

    private async Task MigrateApiAsync(CancellationToken cancellationToken)
    {
        int countryCount = 0, providerCount = 0, backfilledCount = 0, endpointCount = 0;

        foreach (var country in _apiOptions.Countries)
        {
            await _countries.SeedIfMissingAsync(
                new CrawlCountry { Pipeline = CrawlPipeline.Api, Name = country.Name, Enabled = country.Enabled },
                cancellationToken);
            countryCount++;

            foreach (var provider in country.Providers)
            {
                // Same (Pipeline, Provider, Country) identity reasoning as MigrateRssAsync above.
                var alreadyExisted = await _schedules.GetAsync(CrawlPipeline.Api, provider.Name, country.Name, cancellationToken) is not null;

                await _schedules.SeedIfMissingAsync(
                    new ProviderSchedule
                    {
                        Pipeline = CrawlPipeline.Api,
                        Provider = provider.Name,
                        Country = country.Name,
                        Enabled = provider.Enabled,
                        Cron = provider.Cron,
                        TimeZone = "UTC",
                        BaseUrl = provider.BaseUrl,
                        AuthType = provider.AuthType,
                        AuthParamName = provider.AuthParamName,
                        TimeoutSeconds = provider.TimeoutSeconds,
                        UpdatedAt = DateTimeOffset.UtcNow
                    },
                    cancellationToken);

                if (alreadyExisted)
                {
                    await _schedules.BackfillCatalogFieldsAsync(
                        CrawlPipeline.Api, provider.Name, country.Name, true, provider.BaseUrl, provider.AuthType, provider.AuthParamName, provider.TimeoutSeconds, cancellationToken);
                    backfilledCount++;
                }

                providerCount++;

                foreach (var endpoint in provider.Endpoints)
                {
                    await _feeds.CreateAsync(
                        new CrawlFeed
                        {
                            Pipeline = CrawlPipeline.Api,
                            Provider = provider.Name,
                            Country = country.Name,
                            Name = endpoint.Name,
                            Url = endpoint.Endpoint,
                            Category = endpoint.Category,
                            Language = endpoint.Language,
                            Enabled = endpoint.Enabled,
                            QueryParameters = endpoint.QueryParameters
                        },
                        cancellationToken);
                    endpointCount++;
                }
            }
        }

        _logger.LogInformation(
            "API catalog migration complete: {Countries} countries, {Providers} providers ({Backfilled} backfilled), {Endpoints} endpoints",
            countryCount, providerCount, backfilledCount, endpointCount);
    }
}
