using Microsoft.Extensions.Logging;

namespace Infrastructure.RssProviders;

/// <summary>
/// News18 (news18.com) RSS integration. Feed URLs live entirely in configuration under
/// NewsCrawler:Providers[Name="News18"]:Feeds, never hardcoded here. News18's CDN (Akamai)
/// returns 403 for crawler-style User-Agents while serving the same public feeds to browser
/// UAs, so this provider's named HttpClient is registered with a browser-style UA (see
/// InfrastructureServiceCollectionExtensions) - the only provider that differs there.
/// </summary>
public sealed class News18RssProvider : BaseRssProvider
{
    public const string ProviderName = "News18";
    public const string ClientName = "News18RssClient";

    public News18RssProvider(IHttpClientFactory httpClientFactory, ILogger<News18RssProvider> logger)
        : base(httpClientFactory, logger)
    {
    }

    public override string Name => ProviderName;

    protected override string HttpClientName => ClientName;
}
