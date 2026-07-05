using Microsoft.Extensions.Logging;

namespace Infrastructure.RssProviders;

/// <summary>
/// Metro (metro.co.uk, United Kingdom) RSS integration - standard WordPress /feed. Feed URL
/// lives entirely in configuration under
/// NewsCrawler:Countries[Name="United Kingdom"]:Providers[Name="Metro"]:Feeds, never hardcoded here.
/// </summary>
public sealed class MetroRssProvider : BaseRssProvider
{
    public const string ProviderName = "Metro";
    public const string ClientName = "MetroRssClient";

    public MetroRssProvider(IHttpClientFactory httpClientFactory, ILogger<MetroRssProvider> logger)
        : base(httpClientFactory, logger)
    {
    }

    public override string Name => ProviderName;

    protected override string HttpClientName => ClientName;
}
