using Microsoft.Extensions.Logging;

namespace Infrastructure.RssProviders;

/// <summary>
/// The Mirror (mirror.co.uk, United Kingdom) RSS integration - mirror.co.uk/news/rss.xml. Feed
/// URL lives entirely in configuration under
/// NewsCrawler:Countries[Name="United Kingdom"]:Providers[Name="Mirror"]:Feeds, never hardcoded here.
/// </summary>
public sealed class MirrorRssProvider : BaseRssProvider
{
    public const string ProviderName = "Mirror";
    public const string ClientName = "MirrorRssClient";

    public MirrorRssProvider(IHttpClientFactory httpClientFactory, ILogger<MirrorRssProvider> logger)
        : base(httpClientFactory, logger)
    {
    }

    public override string Name => ProviderName;

    protected override string HttpClientName => ClientName;
}
