using Microsoft.Extensions.Logging;

namespace Infrastructure.RssProviders;

/// <summary>
/// Times of India (timesofindia.indiatimes.com) RSS integration - numeric feed ids
/// (rssfeeds/{id}.cms). Feed URLs live entirely in configuration under
/// NewsCrawler:Providers[Name="TimesOfIndia"]:Feeds, never hardcoded here.
/// </summary>
public sealed class TimesOfIndiaRssProvider : BaseRssProvider
{
    public const string ProviderName = "TimesOfIndia";
    public const string ClientName = "TimesOfIndiaRssClient";

    public TimesOfIndiaRssProvider(IHttpClientFactory httpClientFactory, ILogger<TimesOfIndiaRssProvider> logger)
        : base(httpClientFactory, logger)
    {
    }

    public override string Name => ProviderName;

    protected override string HttpClientName => ClientName;
}
