using Microsoft.Extensions.Logging;

namespace Infrastructure.RssProviders;

/// <summary>
/// Mint (livemint.com) RSS integration - rss/{section} pattern. Feed URLs live entirely in
/// configuration under NewsCrawler:Providers[Name="Mint"]:Feeds, never hardcoded here.
/// </summary>
public sealed class MintRssProvider : BaseRssProvider
{
    public const string ProviderName = "Mint";
    public const string ClientName = "MintRssClient";

    public MintRssProvider(IHttpClientFactory httpClientFactory, ILogger<MintRssProvider> logger)
        : base(httpClientFactory, logger)
    {
    }

    public override string Name => ProviderName;

    protected override string HttpClientName => ClientName;
}
