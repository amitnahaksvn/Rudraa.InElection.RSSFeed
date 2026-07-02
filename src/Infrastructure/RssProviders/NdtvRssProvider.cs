using Microsoft.Extensions.Logging;

namespace Infrastructure.RssProviders;

/// <summary>
/// NDTV RSS integration - NDTV publishes its feeds through FeedBurner
/// (feeds.feedburner.com/ndtvnews-*), not ndtv.com itself. Feed URLs live entirely in
/// configuration under NewsCrawler:Providers[Name="NDTV"]:Feeds, never hardcoded here.
/// </summary>
public sealed class NdtvRssProvider : BaseRssProvider
{
    public const string ProviderName = "NDTV";
    public const string ClientName = "NdtvRssClient";

    public NdtvRssProvider(IHttpClientFactory httpClientFactory, ILogger<NdtvRssProvider> logger)
        : base(httpClientFactory, logger)
    {
    }

    public override string Name => ProviderName;

    protected override string HttpClientName => ClientName;
}
