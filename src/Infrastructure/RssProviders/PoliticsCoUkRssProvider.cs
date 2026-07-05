using Microsoft.Extensions.Logging;

namespace Infrastructure.RssProviders;

/// <summary>
/// Politics.co.uk (politics.co.uk, United Kingdom - political news/analysis) RSS integration -
/// standard WordPress /feed/. A genuinely low-frequency-publishing site (newest item ~2 weeks old
/// at verification time) rather than a broken/frozen feed - kept as configured, same "keep it,
/// note the low volume" precedent as IndiaTV's politics feed elsewhere in this codebase. Feed URL
/// lives entirely in configuration under
/// NewsCrawler:Countries[Name="United Kingdom"]:Providers[Name="PoliticsCoUk"]:Feeds, never
/// hardcoded here.
/// </summary>
public sealed class PoliticsCoUkRssProvider : BaseRssProvider
{
    public const string ProviderName = "PoliticsCoUk";
    public const string ClientName = "PoliticsCoUkRssClient";

    public PoliticsCoUkRssProvider(IHttpClientFactory httpClientFactory, ILogger<PoliticsCoUkRssProvider> logger)
        : base(httpClientFactory, logger)
    {
    }

    public override string Name => ProviderName;

    protected override string HttpClientName => ClientName;
}
