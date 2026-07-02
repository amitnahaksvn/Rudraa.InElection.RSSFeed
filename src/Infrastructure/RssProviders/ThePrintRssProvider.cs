using Microsoft.Extensions.Logging;

namespace Infrastructure.RssProviders;

/// <summary>
/// ThePrint (theprint.in) RSS integration - standard WordPress category feeds
/// (/category/{name}/feed/; the bare /feed/ itself returns no items, so only category feeds are
/// used). Feed URLs live entirely in configuration under
/// NewsCrawler:Providers[Name="ThePrint"]:Feeds, never hardcoded here.
/// </summary>
public sealed class ThePrintRssProvider : BaseRssProvider
{
    public const string ProviderName = "ThePrint";
    public const string ClientName = "ThePrintRssClient";

    public ThePrintRssProvider(IHttpClientFactory httpClientFactory, ILogger<ThePrintRssProvider> logger)
        : base(httpClientFactory, logger)
    {
    }

    public override string Name => ProviderName;

    protected override string HttpClientName => ClientName;
}
