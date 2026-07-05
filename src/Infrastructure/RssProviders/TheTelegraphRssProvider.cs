using Microsoft.Extensions.Logging;

namespace Infrastructure.RssProviders;

/// <summary>
/// The Telegraph (telegraph.co.uk, United Kingdom) RSS integration -
/// telegraph.co.uk/{section}/rss.xml. The Home/Business/Sport feeds update multiple times a day;
/// News and Politics specifically update far less often (newest item ~1-2 weeks old at
/// verification time, not a broken/frozen feed like CNN/Xinhua/Forbes-most-popular - genuinely
/// still-live, just low-frequency, likely reflecting how much of that content sits behind a
/// paywall and never reaches the free RSS feed) - kept as configured, same "keep it, note the low
/// volume" precedent as IndiaTV's politics feed elsewhere in this codebase. Feed URLs live
/// entirely in configuration under
/// NewsCrawler:Countries[Name="United Kingdom"]:Providers[Name="TheTelegraph"]:Feeds, never
/// hardcoded here.
/// </summary>
public sealed class TheTelegraphRssProvider : BaseRssProvider
{
    public const string ProviderName = "TheTelegraph";
    public const string ClientName = "TheTelegraphRssClient";

    public TheTelegraphRssProvider(IHttpClientFactory httpClientFactory, ILogger<TheTelegraphRssProvider> logger)
        : base(httpClientFactory, logger)
    {
    }

    public override string Name => ProviderName;

    protected override string HttpClientName => ClientName;
}
