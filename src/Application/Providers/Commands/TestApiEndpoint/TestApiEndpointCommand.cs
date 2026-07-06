using Mediator;
using Microsoft.Extensions.Options;
using Application.Abstractions;
using Application.Options;
using Application.Providers.Dtos;

namespace Application.Providers.Commands.TestApiEndpoint;

/// <summary>On-demand connectivity/content test for one already-configured JSON news-API endpoint, triggered from the Provider Management page's Test button. Never persists anything - it's a pure diagnostic fetch, not a real crawl run.</summary>
public sealed record TestApiEndpointCommand(string Country, string Provider, string EndpointName) : IRequest<ProviderTestResultDto>;

public sealed class TestApiEndpointCommandHandler : IRequestHandler<TestApiEndpointCommand, ProviderTestResultDto>
{
    private readonly IEnumerable<INewsApiProvider> _providers;
    private readonly IOptions<NewsApiCrawlerOptions> _options;

    public TestApiEndpointCommandHandler(IEnumerable<INewsApiProvider> providers, IOptions<NewsApiCrawlerOptions> options)
    {
        _providers = providers;
        _options = options;
    }

    public async ValueTask<ProviderTestResultDto> Handle(TestApiEndpointCommand request, CancellationToken cancellationToken)
    {
        var providerImpl = _providers.FirstOrDefault(p => p.Name == request.Provider);
        if (providerImpl is null)
        {
            return ProviderTestResultDto.NotFound($"No API provider registered with name '{request.Provider}'.");
        }

        var providerOptions = _options.Value.Countries
            .FirstOrDefault(c => c.Name == request.Country)
            ?.Providers.FirstOrDefault(p => p.Name == request.Provider);
        var endpoint = providerOptions?.Endpoints.FirstOrDefault(e => e.Name == request.EndpointName);
        if (providerOptions is null || endpoint is null)
        {
            return ProviderTestResultDto.NotFound(
                $"No endpoint '{request.EndpointName}' configured for provider '{request.Provider}' under country '{request.Country}'.");
        }

        // FetchAllEndpointsAsync only ever iterates the Endpoints list it's given, so passing a
        // one-item copy (rather than the real, possibly dozens-of-endpoints-long list) tests just
        // the requested endpoint without also hitting every other one - important for providers
        // with per-request rate limits or metered API keys. Enabled is forced true for the same
        // "Test is an explicit one-off diagnostic action" reasoning as TestRssFeedCommandHandler.
        var testOptions = new NewsApiProviderOptions
        {
            Name = providerOptions.Name,
            Enabled = true,
            Cron = providerOptions.Cron,
            BaseUrl = providerOptions.BaseUrl,
            AuthType = providerOptions.AuthType,
            AuthParamName = providerOptions.AuthParamName,
            TimeoutSeconds = providerOptions.TimeoutSeconds,
            Endpoints =
            [
                new NewsApiEndpointOptions
                {
                    Name = endpoint.Name,
                    Endpoint = endpoint.Endpoint,
                    QueryParameters = endpoint.QueryParameters,
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
