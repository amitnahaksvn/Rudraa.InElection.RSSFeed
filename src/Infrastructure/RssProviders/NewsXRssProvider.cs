using Microsoft.Extensions.Logging;

namespace Infrastructure.RssProviders;

/// <summary>
/// NewsX (newsx.com) RSS integration - standard WordPress /feed and /category/{name}/feed
/// endpoints. Feed URLs live entirely in configuration under
/// NewsCrawler:Providers[Name="NewsX"]:Feeds, never hardcoded here.
/// </summary>
public sealed class NewsXRssProvider : BaseRssProvider
{
    public const string ProviderName = "NewsX";
    public const string ClientName = "NewsXRssClient";

    public NewsXRssProvider(IHttpClientFactory httpClientFactory, ILogger<NewsXRssProvider> logger)
        : base(httpClientFactory, logger)
    {
    }

    public override string Name => ProviderName;

    protected override string HttpClientName => ClientName;
}
