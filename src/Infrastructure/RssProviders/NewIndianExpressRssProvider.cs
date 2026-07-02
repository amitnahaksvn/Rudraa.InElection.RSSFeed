using Microsoft.Extensions.Logging;

namespace Infrastructure.RssProviders;

/// <summary>
/// The New Indian Express (newindianexpress.com) RSS integration - only the single main /feed
/// endpoint could be found publicly; every guessed section-specific feed (e.g. /nation/feed)
/// 404s. Feed URLs live entirely in configuration under
/// NewsCrawler:Providers[Name="NewIndianExpress"]:Feeds, never hardcoded here.
/// </summary>
public sealed class NewIndianExpressRssProvider : BaseRssProvider
{
    public const string ProviderName = "NewIndianExpress";
    public const string ClientName = "NewIndianExpressRssClient";

    public NewIndianExpressRssProvider(IHttpClientFactory httpClientFactory, ILogger<NewIndianExpressRssProvider> logger)
        : base(httpClientFactory, logger)
    {
    }

    public override string Name => ProviderName;

    protected override string HttpClientName => ClientName;
}
