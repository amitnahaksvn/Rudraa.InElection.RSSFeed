using Microsoft.Extensions.Logging;

namespace Infrastructure.RssProviders;

/// <summary>
/// BBC Sport (bbc.co.uk/sport, United Kingdom) RSS integration - feeds.bbci.co.uk/sport/rss.xml.
/// A separate provider from BBCNews since it's a distinct BBC feed/section, not a category under
/// the news feed. Feed URL lives entirely in configuration under
/// NewsCrawler:Countries[Name="United Kingdom"]:Providers[Name="BBCSport"]:Feeds, never hardcoded here.
/// </summary>
public sealed class BbcSportRssProvider : BaseRssProvider
{
    public const string ProviderName = "BBCSport";
    public const string ClientName = "BbcSportRssClient";

    public BbcSportRssProvider(IHttpClientFactory httpClientFactory, ILogger<BbcSportRssProvider> logger)
        : base(httpClientFactory, logger)
    {
    }

    public override string Name => ProviderName;

    protected override string HttpClientName => ClientName;
}
