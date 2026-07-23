using Mediator;
using Application.Abstractions;
using Application.Providers.Dtos;
using Domain.Entities;
using Domain.Enums;

namespace Application.Providers.Queries.GetRssProviders;

/// <summary>Every configured RSS provider (across every country), flattened for the Provider Management page's "RSS Feeds" tab. Fully database-backed - see <see cref="ICrawlCountryRepository"/>/<see cref="IProviderScheduleRepository"/>/<see cref="ICrawlFeedRepository"/>.</summary>
public sealed record GetRssProvidersQuery : IRequest<IReadOnlyList<RssProviderSummaryDto>>;

public sealed class GetRssProvidersQueryHandler : IRequestHandler<GetRssProvidersQuery, IReadOnlyList<RssProviderSummaryDto>>
{
    private readonly ICrawlCountryRepository _countries;
    private readonly IProviderScheduleRepository _schedules;
    private readonly ICrawlFeedRepository _feeds;

    public GetRssProvidersQueryHandler(ICrawlCountryRepository countries, IProviderScheduleRepository schedules, ICrawlFeedRepository feeds)
    {
        _countries = countries;
        _schedules = schedules;
        _feeds = feeds;
    }

    public async ValueTask<IReadOnlyList<RssProviderSummaryDto>> Handle(GetRssProvidersQuery request, CancellationToken cancellationToken)
    {
        var countries = await _countries.GetAllAsync(CrawlPipeline.Rss, cancellationToken);
        var countryEnabledByName = countries.ToDictionary(c => c.Name, c => c.Enabled, StringComparer.OrdinalIgnoreCase);

        var schedules = await _schedules.GetAllAsync(CrawlPipeline.Rss, cancellationToken);
        var feeds = await _feeds.GetAllAsync(CrawlPipeline.Rss, cancellationToken);
        var feedsByProviderCountry = feeds
            .GroupBy(f => (f.Provider.ToUpperInvariant(), f.Country.ToUpperInvariant()))
            .ToDictionary(g => g.Key, g => g.ToList());
        var feedsByProviderOnly = feeds
            .GroupBy(f => f.Provider, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        return schedules.Select(schedule =>
        {
            // Falls back to every feed under the bare provider name if the exact (Provider,
            // Country) pair isn't found yet - same migration-window safety net as the
            // orchestrators' own BuildProviderOptions (see that method's doc comment).
            if (!feedsByProviderCountry.TryGetValue((schedule.Provider.ToUpperInvariant(), schedule.Country.ToUpperInvariant()), out var providerFeeds))
            {
                feedsByProviderOnly.TryGetValue(schedule.Provider, out providerFeeds);
            }
            providerFeeds ??= [];
            var countryEnabled = countryEnabledByName.TryGetValue(schedule.Country, out var enabled) && enabled;

            return new RssProviderSummaryDto(
                schedule.Country,
                schedule.Provider,
                countryEnabled && schedule.Enabled,
                schedule.Cron,
                schedule.TimeZone,
                schedule.SaveRawResponses,
                BuildDescription(providerFeeds),
                providerFeeds.Select(f => new RssFeedSummaryDto(f.Id, f.Name, f.Url, f.Category, f.Language, f.Enabled)).ToList());
        }).ToList();
    }

    // No free-text description is stored per provider - written by hand for every one of the 200+
    // RSS providers wouldn't stay maintained - this is computed instead from the feed list, so it
    // can never drift out of sync.
    private static string BuildDescription(IReadOnlyList<CrawlFeed> feeds)
    {
        var enabledCount = feeds.Count(f => f.Enabled);
        var categories = feeds
            .Select(f => f.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .Take(4)
            .ToList();

        var categoryText = categories.Count > 0 ? $" covering {string.Join(", ", categories)}" : string.Empty;
        var feedWord = feeds.Count == 1 ? "feed" : "feeds";
        return $"{enabledCount} of {feeds.Count} {feedWord} enabled{categoryText}.";
    }
}
