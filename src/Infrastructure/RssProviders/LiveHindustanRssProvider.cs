using Microsoft.Extensions.Logging;

namespace Infrastructure.RssProviders;

/// <summary>
/// Live Hindustan (livehindustan.com, Hindi) RSS integration - served from a separate api.
/// subdomain (api.livehindustan.com/feeds/rss/{slug}/rssfeed.xml), discoverable via the site's own
/// /rss/ index page. Feed URLs live entirely in configuration under
/// NewsCrawler:Providers[Name="LiveHindustan"]:Feeds, never hardcoded here.
/// </summary>
public sealed class LiveHindustanRssProvider : BaseRssProvider
{
    public const string ProviderName = "LiveHindustan";
    public const string ClientName = "LiveHindustanRssClient";

    public LiveHindustanRssProvider(IHttpClientFactory httpClientFactory, ILogger<LiveHindustanRssProvider> logger)
        : base(httpClientFactory, logger)
    {
    }

    public override string Name => ProviderName;

    protected override string HttpClientName => ClientName;
}
