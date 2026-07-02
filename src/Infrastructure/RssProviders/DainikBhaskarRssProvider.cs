using Microsoft.Extensions.Logging;

namespace Infrastructure.RssProviders;

/// <summary>
/// Dainik Bhaskar (bhaskar.com, Hindi) RSS integration - rss-v1--category-{numericId}.xml, where
/// the id-to-category mapping isn't visible from the URL itself (resolved by fetching each
/// candidate id's own <c>&lt;title&gt;</c> during discovery). Feed URLs live entirely in
/// configuration under NewsCrawler:Providers[Name="DainikBhaskar"]:Feeds, never hardcoded here.
/// </summary>
public sealed class DainikBhaskarRssProvider : BaseRssProvider
{
    public const string ProviderName = "DainikBhaskar";
    public const string ClientName = "DainikBhaskarRssClient";

    public DainikBhaskarRssProvider(IHttpClientFactory httpClientFactory, ILogger<DainikBhaskarRssProvider> logger)
        : base(httpClientFactory, logger)
    {
    }

    public override string Name => ProviderName;

    protected override string HttpClientName => ClientName;
}
