using Microsoft.Extensions.Logging;

namespace Infrastructure.RssProviders;

/// <summary>
/// The Week (theweek.in) RSS integration - {section}/{sub}.rss, discoverable only via the
/// &lt;link rel="alternate"&gt; tag on an *article* page (absent from section/homepage HTML). Items
/// carry no image tag at all - the image is embedded as an HTML &lt;img&gt; inside &lt;description&gt;
/// instead, so (like Zee News) every article's image comes from the og:image HTML fallback.
/// Feed URLs live entirely in configuration under NewsCrawler:Providers[Name="TheWeek"]:Feeds,
/// never hardcoded here.
/// </summary>
public sealed class TheWeekRssProvider : BaseRssProvider
{
    public const string ProviderName = "TheWeek";
    public const string ClientName = "TheWeekRssClient";

    public TheWeekRssProvider(IHttpClientFactory httpClientFactory, ILogger<TheWeekRssProvider> logger)
        : base(httpClientFactory, logger)
    {
    }

    public override string Name => ProviderName;

    protected override string HttpClientName => ClientName;
}
