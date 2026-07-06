using Mediator;
using Microsoft.Extensions.Options;
using Application.Abstractions;
using Application.Options;
using Application.Providers.Dtos;

namespace Application.Providers.Commands.TestRssFeed;

/// <summary>On-demand connectivity/content test for one already-configured RSS feed, triggered from the Provider Management page's Test button. Never persists anything - it's a pure diagnostic fetch, not a real crawl run.</summary>
public sealed record TestRssFeedCommand(string Country, string Provider, string FeedUrl) : IRequest<ProviderTestResultDto>;

public sealed class TestRssFeedCommandHandler : IRequestHandler<TestRssFeedCommand, ProviderTestResultDto>
{
    private readonly IEnumerable<IRssProvider> _providers;
    private readonly IOptions<NewsCrawlerOptions> _options;

    public TestRssFeedCommandHandler(IEnumerable<IRssProvider> providers, IOptions<NewsCrawlerOptions> options)
    {
        _providers = providers;
        _options = options;
    }

    public async ValueTask<ProviderTestResultDto> Handle(TestRssFeedCommand request, CancellationToken cancellationToken)
    {
        var providerImpl = _providers.FirstOrDefault(p => p.Name == request.Provider);
        if (providerImpl is null)
        {
            return ProviderTestResultDto.NotFound($"No RSS provider registered with name '{request.Provider}'.");
        }

        var feed = _options.Value.Countries
            .FirstOrDefault(c => c.Name == request.Country)
            ?.Providers.FirstOrDefault(p => p.Name == request.Provider)
            ?.Feeds.FirstOrDefault(f => f.Url == request.FeedUrl);
        if (feed is null)
        {
            return ProviderTestResultDto.NotFound(
                $"No feed '{request.FeedUrl}' configured for provider '{request.Provider}' under country '{request.Country}'.");
        }

        // Forced enabled regardless of the feed's own configured value - "Test" is an explicit,
        // one-off diagnostic action, so a feed that's currently disabled should still be testable.
        var testFeed = new RssFeedOptions
        {
            Name = feed.Name,
            Url = feed.Url,
            Category = feed.Category,
            Language = feed.Language,
            Enabled = true,
        };

        var results = await providerImpl.FetchAllFeedsAsync([testFeed], cancellationToken);
        var result = results.FirstOrDefault();
        return result is null
            ? ProviderTestResultDto.NotFound("The provider returned no result for this feed.")
            : ProviderTestResultDto.FromFeedResult(result);
    }
}
