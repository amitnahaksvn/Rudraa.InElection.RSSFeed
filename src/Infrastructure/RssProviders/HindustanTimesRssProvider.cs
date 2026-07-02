using Microsoft.Extensions.Logging;

namespace Infrastructure.RssProviders;

/// <summary>
/// Hindustan Times (hindustantimes.com) RSS integration - feeds/rss/{section}/rssfeed.xml. Feed
/// URLs live entirely in configuration under NewsCrawler:Providers[Name="HindustanTimes"]:Feeds,
/// never hardcoded here.
/// </summary>
public sealed class HindustanTimesRssProvider : BaseRssProvider
{
    public const string ProviderName = "HindustanTimes";
    public const string ClientName = "HindustanTimesRssClient";

    public HindustanTimesRssProvider(IHttpClientFactory httpClientFactory, ILogger<HindustanTimesRssProvider> logger)
        : base(httpClientFactory, logger)
    {
    }

    public override string Name => ProviderName;

    protected override string HttpClientName => ClientName;
}
