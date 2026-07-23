using Mediator;
using Application.Abstractions;
using Application.Options;
using Application.Providers.Dtos;
using Domain.Enums;

namespace Application.Providers.Commands.TestApiEndpoint;

/// <summary>On-demand connectivity/content test for one already-configured JSON news-API endpoint, triggered from the Provider Management page's Test button. Never persists anything - it's a pure diagnostic fetch, not a real crawl run.</summary>
public sealed record TestApiEndpointCommand(string EndpointId) : IRequest<ProviderTestResultDto>;

public sealed class TestApiEndpointCommandHandler : IRequestHandler<TestApiEndpointCommand, ProviderTestResultDto>
{
    private readonly IEnumerable<INewsApiProvider> _providers;
    private readonly ICrawlFeedRepository _feeds;
    private readonly IProviderScheduleRepository _schedules;

    public TestApiEndpointCommandHandler(IEnumerable<INewsApiProvider> providers, ICrawlFeedRepository feeds, IProviderScheduleRepository schedules)
    {
        _providers = providers;
        _feeds = feeds;
        _schedules = schedules;
    }

    public async ValueTask<ProviderTestResultDto> Handle(TestApiEndpointCommand request, CancellationToken cancellationToken)
    {
        var endpoint = await _feeds.GetByIdAsync(request.EndpointId, cancellationToken);
        if (endpoint is null)
        {
            return ProviderTestResultDto.NotFound($"No endpoint found with id '{request.EndpointId}'.");
        }

        var providerImpl = _providers.FirstOrDefault(p => p.Name == endpoint.Provider);
        if (providerImpl is null)
        {
            return ProviderTestResultDto.NotFound($"No API provider registered with name '{endpoint.Provider}'.");
        }

        var schedule = await _schedules.GetAsync(CrawlPipeline.Api, endpoint.Provider, endpoint.Country, cancellationToken);
        if (schedule is null)
        {
            return ProviderTestResultDto.NotFound($"No provider record found for '{endpoint.Provider}'.");
        }

        // FetchAllEndpointsAsync only ever iterates the Endpoints list it's given, so passing a
        // one-item copy (rather than the real, possibly dozens-of-endpoints-long list) tests just
        // the requested endpoint without also hitting every other one - important for providers
        // with per-request rate limits or metered API keys. Enabled is forced true for the same
        // "Test is an explicit one-off diagnostic action" reasoning as TestRssFeedCommandHandler.
        var testOptions = new NewsApiProviderOptions
        {
            Name = schedule.Provider,
            Enabled = true,
            Cron = schedule.Cron,
            BaseUrl = schedule.BaseUrl ?? string.Empty,
            AuthType = schedule.AuthType ?? ApiAuthType.QueryParameter,
            AuthParamName = schedule.AuthParamName ?? "apiKey",
            TimeoutSeconds = schedule.TimeoutSeconds ?? 120,
            Endpoints =
            [
                new NewsApiEndpointOptions
                {
                    Name = endpoint.Name,
                    Endpoint = endpoint.Url,
                    QueryParameters = endpoint.QueryParameters ?? [],
                    Category = endpoint.Category,
                    Language = endpoint.Language,
                    Enabled = true,
                },
            ],
        };

        var results = await providerImpl.FetchAllEndpointsAsync(testOptions, cancellationToken);
        var result = results.FirstOrDefault();
        return result is null
            ? ProviderTestResultDto.NotFound("The provider returned no result for this endpoint.")
            : ProviderTestResultDto.FromApiResult(result);
    }
}
