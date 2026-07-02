using Microsoft.Extensions.Logging;

namespace Infrastructure.RssProviders;

/// <summary>
/// India TV (indiatvnews.com) RSS integration. Feed URLs live entirely in configuration under
/// NewsCrawler:Providers[Name="IndiaTV"]:Feeds, never hardcoded here.
/// </summary>
public sealed class IndiaTvRssProvider : BaseRssProvider
{
    public const string ProviderName = "IndiaTV";
    public const string ClientName = "IndiaTvRssClient";

    public IndiaTvRssProvider(IHttpClientFactory httpClientFactory, ILogger<IndiaTvRssProvider> logger)
        : base(httpClientFactory, logger)
    {
    }

    public override string Name => ProviderName;

    protected override string HttpClientName => ClientName;
}
