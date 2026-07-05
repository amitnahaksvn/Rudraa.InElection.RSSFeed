using Microsoft.Extensions.Logging;

namespace Infrastructure.RssProviders;

/// <summary>
/// Financial Times (ft.com, United Kingdom) RSS integration - ft.com/rss/home plus per-section
/// ?format=rss query parameter feeds. Feed URLs live entirely in configuration under
/// NewsCrawler:Countries[Name="United Kingdom"]:Providers[Name="FinancialTimes"]:Feeds, never
/// hardcoded here.
/// </summary>
public sealed class FinancialTimesRssProvider : BaseRssProvider
{
    public const string ProviderName = "FinancialTimes";
    public const string ClientName = "FinancialTimesRssClient";

    public FinancialTimesRssProvider(IHttpClientFactory httpClientFactory, ILogger<FinancialTimesRssProvider> logger)
        : base(httpClientFactory, logger)
    {
    }

    public override string Name => ProviderName;

    protected override string HttpClientName => ClientName;
}
