using Microsoft.Extensions.Logging;

namespace Infrastructure.RssProviders;

/// <summary>
/// OneIndia (oneindia.com) RSS integration - feeds live under /rss/feeds/{slug}-fb.xml,
/// discovered via the site's own /rss/ index page. Its CDN returns 403 to crawler-style User-Agents
/// while serving the same public feeds to browser UAs (same behavior as News18), so this
/// provider's named HttpClient is registered with the browser User-Agent in
/// InfrastructureServiceCollectionExtensions. Feed URLs live entirely in configuration under
/// NewsCrawler:Providers[Name="OneIndia"]:Feeds, never hardcoded here.
/// </summary>
public sealed class OneIndiaRssProvider : BaseRssProvider
{
    public const string ProviderName = "OneIndia";
    public const string ClientName = "OneIndiaRssClient";

    public OneIndiaRssProvider(IHttpClientFactory httpClientFactory, ILogger<OneIndiaRssProvider> logger)
        : base(httpClientFactory, logger)
    {
    }

    public override string Name => ProviderName;

    protected override string HttpClientName => ClientName;
}
