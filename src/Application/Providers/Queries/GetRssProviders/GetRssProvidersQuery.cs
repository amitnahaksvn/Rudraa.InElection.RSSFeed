using Mediator;
using Microsoft.Extensions.Options;
using Application.Options;
using Application.Providers.Dtos;

namespace Application.Providers.Queries.GetRssProviders;

/// <summary>Every configured RSS provider (across every country), flattened for the Provider Management page's "RSS Feeds" tab. Pure configuration reflection - no I/O.</summary>
public sealed record GetRssProvidersQuery : IRequest<IReadOnlyList<RssProviderSummaryDto>>;

public sealed class GetRssProvidersQueryHandler : IRequestHandler<GetRssProvidersQuery, IReadOnlyList<RssProviderSummaryDto>>
{
    private readonly IOptions<NewsCrawlerOptions> _options;

    public GetRssProvidersQueryHandler(IOptions<NewsCrawlerOptions> options)
    {
        _options = options;
    }

    public ValueTask<IReadOnlyList<RssProviderSummaryDto>> Handle(GetRssProvidersQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<RssProviderSummaryDto> result = _options.Value.Countries
            .SelectMany(country => country.Providers.Select(provider => new RssProviderSummaryDto(
                country.Name,
                provider.Name,
                country.Enabled && provider.Enabled,
                provider.Cron,
                BuildDescription(provider),
                provider.Feeds.Select(f => new RssFeedSummaryDto(f.Name, f.Url, f.Category, f.Language, f.Enabled)).ToList())))
            .ToList();

        return ValueTask.FromResult(result);
    }

    // No free-text description is stored per provider in configuration (writing one by hand for
    // every one of the 200+ RSS providers wouldn't stay maintained) - this is computed instead
    // from what's already there, so it can never drift out of sync with the actual feed list.
    private static string BuildDescription(RssProviderOptions provider)
    {
        var enabledCount = provider.Feeds.Count(f => f.Enabled);
        var categories = provider.Feeds
            .Select(f => f.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .Take(4)
            .ToList();

        var categoryText = categories.Count > 0 ? $" covering {string.Join(", ", categories)}" : string.Empty;
        var feedWord = provider.Feeds.Count == 1 ? "feed" : "feeds";
        return $"{enabledCount} of {provider.Feeds.Count} {feedWord} enabled{categoryText}.";
    }
}
