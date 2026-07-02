using Microsoft.Extensions.Logging;

namespace Infrastructure.RssProviders;

/// <summary>
/// Navbharat Times (navbharattimes.indiatimes.com, Hindi) RSS integration - a different feed-id
/// scheme from its sister Times of India (langapi/sitemap/gstandrssfeed/{id}.xml, not
/// rssfeeds/{id}.cms); only one working feed id could be found publicly. Feed URLs live entirely
/// in configuration under NewsCrawler:Providers[Name="NavbharatTimes"]:Feeds, never hardcoded here.
/// </summary>
public sealed class NavbharatTimesRssProvider : BaseRssProvider
{
    public const string ProviderName = "NavbharatTimes";
    public const string ClientName = "NavbharatTimesRssClient";

    public NavbharatTimesRssProvider(IHttpClientFactory httpClientFactory, ILogger<NavbharatTimesRssProvider> logger)
        : base(httpClientFactory, logger)
    {
    }

    public override string Name => ProviderName;

    protected override string HttpClientName => ClientName;
}
