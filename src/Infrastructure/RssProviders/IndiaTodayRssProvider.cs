using Microsoft.Extensions.Logging;

namespace Infrastructure.RssProviders;

/// <summary>
/// India Today (indiatoday.in) RSS integration - uses a numeric-id scheme (/rss/{id}), discovered
/// via the site's own /rss index page. Feed URLs live entirely in configuration under
/// NewsCrawler:Providers[Name="IndiaToday"]:Feeds, never hardcoded here.
/// </summary>
public sealed class IndiaTodayRssProvider : BaseRssProvider
{
    public const string ProviderName = "IndiaToday";
    public const string ClientName = "IndiaTodayRssClient";

    public IndiaTodayRssProvider(IHttpClientFactory httpClientFactory, ILogger<IndiaTodayRssProvider> logger)
        : base(httpClientFactory, logger)
    {
    }

    public override string Name => ProviderName;

    protected override string HttpClientName => ClientName;
}
