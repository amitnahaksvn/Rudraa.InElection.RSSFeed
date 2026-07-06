using Mediator;
using Microsoft.Extensions.Options;
using Application.Options;
using Application.Providers.Dtos;

namespace Application.Providers.Queries.GetApiProviders;

/// <summary>Every configured JSON news-API provider (across every country), flattened for the Provider Management page's "APIs" tab. Pure configuration reflection - no I/O.</summary>
public sealed record GetApiProvidersQuery : IRequest<IReadOnlyList<ApiProviderSummaryDto>>;

public sealed class GetApiProvidersQueryHandler : IRequestHandler<GetApiProvidersQuery, IReadOnlyList<ApiProviderSummaryDto>>
{
    private readonly IOptions<NewsApiCrawlerOptions> _options;

    public GetApiProvidersQueryHandler(IOptions<NewsApiCrawlerOptions> options)
    {
        _options = options;
    }

    public ValueTask<IReadOnlyList<ApiProviderSummaryDto>> Handle(GetApiProvidersQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<ApiProviderSummaryDto> result = _options.Value.Countries
            .SelectMany(country => country.Providers.Select(provider => new ApiProviderSummaryDto(
                country.Name,
                provider.Name,
                country.Enabled && provider.Enabled,
                provider.Cron,
                provider.BaseUrl,
                provider.AuthType.ToString(),
                BuildDescription(provider),
                provider.Endpoints.Select(e => new ApiEndpointSummaryDto(e.Name, e.Endpoint, e.Category, e.Language, e.Enabled)).ToList())))
            .ToList();

        return ValueTask.FromResult(result);
    }

    // Same reasoning as GetRssProvidersQueryHandler's own BuildDescription - computed from the
    // endpoint list rather than hand-written per provider, so it can't go stale.
    private static string BuildDescription(NewsApiProviderOptions provider)
    {
        var enabledCount = provider.Endpoints.Count(e => e.Enabled);
        var categories = provider.Endpoints
            .Select(e => e.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .Take(4)
            .ToList();

        var categoryText = categories.Count > 0 ? $" covering {string.Join(", ", categories)}" : string.Empty;
        var endpointWord = provider.Endpoints.Count == 1 ? "endpoint" : "endpoints";
        return $"{enabledCount} of {provider.Endpoints.Count} {endpointWord} enabled{categoryText}.";
    }
}
