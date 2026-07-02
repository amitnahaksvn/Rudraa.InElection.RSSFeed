using Microsoft.Extensions.Logging;

namespace Infrastructure.RssProviders;

/// <summary>
/// Zee News (zeenews.india.com) RSS integration. Feed URLs live entirely in configuration under
/// NewsCrawler:Providers[Name="ZeeNews"]:Feeds, never hardcoded here. Zee's feeds carry no image
/// tags at all, so every article's image comes from BaseRssProvider's og:image HTML fallback -
/// one extra HTTP request per new article.
/// </summary>
public sealed class ZeeNewsRssProvider : BaseRssProvider
{
    public const string ProviderName = "ZeeNews";
    public const string ClientName = "ZeeNewsRssClient";

    public ZeeNewsRssProvider(IHttpClientFactory httpClientFactory, ILogger<ZeeNewsRssProvider> logger)
        : base(httpClientFactory, logger)
    {
    }

    public override string Name => ProviderName;

    protected override string HttpClientName => ClientName;
}
