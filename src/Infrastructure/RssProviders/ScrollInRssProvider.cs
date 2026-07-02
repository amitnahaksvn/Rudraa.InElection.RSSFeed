using Microsoft.Extensions.Logging;

namespace Infrastructure.RssProviders;

/// <summary>
/// Scroll.in RSS integration - published through FeedBurner
/// (feeds.feedburner.com/ScrollinArticles.rss), discoverable only via the &lt;link rel="alternate"&gt;
/// tag on scroll.in's homepage, not any guessable scroll.in URL. Feed URLs live entirely in
/// configuration under NewsCrawler:Providers[Name="ScrollIn"]:Feeds, never hardcoded here.
/// </summary>
public sealed class ScrollInRssProvider : BaseRssProvider
{
    public const string ProviderName = "ScrollIn";
    public const string ClientName = "ScrollInRssClient";

    public ScrollInRssProvider(IHttpClientFactory httpClientFactory, ILogger<ScrollInRssProvider> logger)
        : base(httpClientFactory, logger)
    {
    }

    public override string Name => ProviderName;

    protected override string HttpClientName => ClientName;
}
