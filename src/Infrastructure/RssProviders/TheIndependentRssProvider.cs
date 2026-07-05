using Microsoft.Extensions.Logging;

namespace Infrastructure.RssProviders;

/// <summary>
/// The Independent (independent.co.uk, United Kingdom) RSS integration -
/// independent.co.uk/news/{section}/rss. Feed URLs live entirely in configuration under
/// NewsCrawler:Countries[Name="United Kingdom"]:Providers[Name="TheIndependent"]:Feeds, never
/// hardcoded here.
/// </summary>
public sealed class TheIndependentRssProvider : BaseRssProvider
{
    public const string ProviderName = "TheIndependent";
    public const string ClientName = "TheIndependentRssClient";

    public TheIndependentRssProvider(IHttpClientFactory httpClientFactory, ILogger<TheIndependentRssProvider> logger)
        : base(httpClientFactory, logger)
    {
    }

    public override string Name => ProviderName;

    protected override string HttpClientName => ClientName;
}
