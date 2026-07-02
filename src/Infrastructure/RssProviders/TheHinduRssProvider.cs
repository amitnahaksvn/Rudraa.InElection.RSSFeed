using Microsoft.Extensions.Logging;

namespace Infrastructure.RssProviders;

/// <summary>
/// The Hindu (thehindu.com) RSS integration - feeds follow the {section}/feeder/default.rss
/// pattern. Feed URLs live entirely in configuration under
/// NewsCrawler:Providers[Name="TheHindu"]:Feeds, never hardcoded here.
/// </summary>
public sealed class TheHinduRssProvider : BaseRssProvider
{
    public const string ProviderName = "TheHindu";
    public const string ClientName = "TheHinduRssClient";

    public TheHinduRssProvider(IHttpClientFactory httpClientFactory, ILogger<TheHinduRssProvider> logger)
        : base(httpClientFactory, logger)
    {
    }

    public override string Name => ProviderName;

    protected override string HttpClientName => ClientName;
}
