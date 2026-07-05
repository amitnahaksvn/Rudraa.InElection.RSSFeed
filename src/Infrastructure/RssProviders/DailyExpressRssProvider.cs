using Microsoft.Extensions.Logging;

namespace Infrastructure.RssProviders;

/// <summary>
/// Daily Express (express.co.uk, United Kingdom) RSS integration -
/// express.co.uk/posts/rss/1/news. Feed URL lives entirely in configuration under
/// NewsCrawler:Countries[Name="United Kingdom"]:Providers[Name="DailyExpress"]:Feeds, never
/// hardcoded here.
/// </summary>
public sealed class DailyExpressRssProvider : BaseRssProvider
{
    public const string ProviderName = "DailyExpress";
    public const string ClientName = "DailyExpressRssClient";

    public DailyExpressRssProvider(IHttpClientFactory httpClientFactory, ILogger<DailyExpressRssProvider> logger)
        : base(httpClientFactory, logger)
    {
    }

    public override string Name => ProviderName;

    protected override string HttpClientName => ClientName;
}
