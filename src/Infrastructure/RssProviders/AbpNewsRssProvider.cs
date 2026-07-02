using Microsoft.Extensions.Logging;

namespace Infrastructure.RssProviders;

/// <summary>
/// ABP Live (abplive.com) RSS integration. Feed URLs live entirely in configuration under
/// NewsCrawler:Providers[Name="ABPNews"]:Feeds, never hardcoded here.
/// </summary>
public sealed class AbpNewsRssProvider : BaseRssProvider
{
    public const string ProviderName = "ABPNews";
    public const string ClientName = "AbpNewsRssClient";

    public AbpNewsRssProvider(IHttpClientFactory httpClientFactory, ILogger<AbpNewsRssProvider> logger)
        : base(httpClientFactory, logger)
    {
    }

    public override string Name => ProviderName;

    protected override string HttpClientName => ClientName;
}
