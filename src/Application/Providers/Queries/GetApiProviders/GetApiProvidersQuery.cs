using Mediator;
using Application.Abstractions;
using Application.Providers.Dtos;
using Domain.Entities;
using Domain.Enums;

namespace Application.Providers.Queries.GetApiProviders;

/// <summary>Every configured JSON news-API provider (across every country), flattened for the Provider Management page's "APIs" tab. Fully database-backed - see <see cref="ICrawlCountryRepository"/>/<see cref="IProviderScheduleRepository"/>/<see cref="ICrawlFeedRepository"/>.</summary>
public sealed record GetApiProvidersQuery : IRequest<IReadOnlyList<ApiProviderSummaryDto>>;

public sealed class GetApiProvidersQueryHandler : IRequestHandler<GetApiProvidersQuery, IReadOnlyList<ApiProviderSummaryDto>>
{
    private readonly ICrawlCountryRepository _countries;
    private readonly IProviderScheduleRepository _schedules;
    private readonly ICrawlFeedRepository _feeds;

    public GetApiProvidersQueryHandler(ICrawlCountryRepository countries, IProviderScheduleRepository schedules, ICrawlFeedRepository feeds)
    {
        _countries = countries;
        _schedules = schedules;
        _feeds = feeds;
    }

    public async ValueTask<IReadOnlyList<ApiProviderSummaryDto>> Handle(GetApiProvidersQuery request, CancellationToken cancellationToken)
    {
        var countries = await _countries.GetAllAsync(CrawlPipeline.Api, cancellationToken);
        var countryEnabledByName = countries.ToDictionary(c => c.Name, c => c.Enabled, StringComparer.OrdinalIgnoreCase);

        var schedules = await _schedules.GetAllAsync(CrawlPipeline.Api, cancellationToken);
        var endpoints = await _feeds.GetAllAsync(CrawlPipeline.Api, cancellationToken);
        var endpointsByProviderCountry = endpoints
            .GroupBy(e => (e.Provider.ToUpperInvariant(), e.Country.ToUpperInvariant()))
            .ToDictionary(g => g.Key, g => g.ToList());
        var endpointsByProviderOnly = endpoints
            .GroupBy(e => e.Provider, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        return schedules.Select(schedule =>
        {
            // Falls back to every endpoint under the bare provider name if the exact (Provider,
            // Country) pair isn't found yet - same migration-window safety net as the
            // orchestrators' own BuildProviderOptions (see that method's doc comment).
            if (!endpointsByProviderCountry.TryGetValue((schedule.Provider.ToUpperInvariant(), schedule.Country.ToUpperInvariant()), out var providerEndpoints))
            {
                endpointsByProviderOnly.TryGetValue(schedule.Provider, out providerEndpoints);
            }
            providerEndpoints ??= [];
            var countryEnabled = countryEnabledByName.TryGetValue(schedule.Country, out var enabled) && enabled;
            var baseUrl = schedule.BaseUrl ?? string.Empty;

            return new ApiProviderSummaryDto(
                schedule.Country,
                schedule.Provider,
                countryEnabled && schedule.Enabled,
                schedule.Cron,
                schedule.TimeZone,
                baseUrl,
                (schedule.AuthType ?? Domain.Enums.ApiAuthType.QueryParameter).ToString(),
                schedule.AuthParamName ?? "apiKey",
                schedule.TimeoutSeconds ?? 120,
                BuildDescription(providerEndpoints),
                providerEndpoints
                    .Select(e => new ApiEndpointSummaryDto(e.Id, e.Name, e.Url, BuildEndpointUrl(baseUrl, e.Url), e.Category, e.Language, e.Enabled))
                    .ToList());
        }).ToList();
    }

    // Same join `BaseNewsApiProvider.BuildRequestUrl` uses at fetch time (Infrastructure isn't
    // referenceable from here, so this mirrors it rather than reusing it) - minus query
    // parameters/auth, since this is display-only and must never risk leaking an API key.
    private static string BuildEndpointUrl(string baseUrl, string endpoint)
    {
        var trimmedBase = baseUrl.TrimEnd('/');
        var trimmedPath = endpoint.Trim('/');
        return trimmedPath.Length == 0 ? trimmedBase : $"{trimmedBase}/{trimmedPath}";
    }

    // Same reasoning as GetRssProvidersQueryHandler's own BuildDescription - computed from the
    // endpoint list rather than hand-written per provider, so it can't go stale.
    private static string BuildDescription(IReadOnlyList<CrawlFeed> endpoints)
    {
        var enabledCount = endpoints.Count(e => e.Enabled);
        var categories = endpoints
            .Select(e => e.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .Take(4)
            .ToList();

        var categoryText = categories.Count > 0 ? $" covering {string.Join(", ", categories)}" : string.Empty;
        var endpointWord = endpoints.Count == 1 ? "endpoint" : "endpoints";
        return $"{enabledCount} of {endpoints.Count} {endpointWord} enabled{categoryText}.";
    }
}
