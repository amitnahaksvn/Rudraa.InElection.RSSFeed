using Microsoft.Extensions.Logging;

namespace Infrastructure.RssProviders;

/// <summary>
/// DNA India (dnaindia.com) RSS integration - feeds live under /feeds/{slug}.xml. Feed URLs live
/// entirely in configuration under NewsCrawler:Providers[Name="DnaIndia"]:Feeds, never hardcoded
/// here.
/// </summary>
public sealed class DnaIndiaRssProvider : BaseRssProvider
{
    public const string ProviderName = "DnaIndia";
    public const string ClientName = "DnaIndiaRssClient";

    public DnaIndiaRssProvider(IHttpClientFactory httpClientFactory, ILogger<DnaIndiaRssProvider> logger)
        : base(httpClientFactory, logger)
    {
    }

    public override string Name => ProviderName;

    protected override string HttpClientName => ClientName;
}
