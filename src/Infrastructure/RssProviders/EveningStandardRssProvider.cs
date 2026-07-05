using Microsoft.Extensions.Logging;

namespace Infrastructure.RssProviders;

/// <summary>
/// Evening Standard (standard.co.uk, United Kingdom) RSS integration -
/// standard.co.uk/news/rss. Feed URL lives entirely in configuration under
/// NewsCrawler:Countries[Name="United Kingdom"]:Providers[Name="EveningStandard"]:Feeds, never
/// hardcoded here.
/// </summary>
public sealed class EveningStandardRssProvider : BaseRssProvider
{
    public const string ProviderName = "EveningStandard";
    public const string ClientName = "EveningStandardRssClient";

    public EveningStandardRssProvider(IHttpClientFactory httpClientFactory, ILogger<EveningStandardRssProvider> logger)
        : base(httpClientFactory, logger)
    {
    }

    public override string Name => ProviderName;

    protected override string HttpClientName => ClientName;
}
