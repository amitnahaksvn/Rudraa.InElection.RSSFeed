using Microsoft.Extensions.Logging;

namespace Infrastructure.RssProviders;

/// <summary>
/// Deccan Herald (deccanherald.com) RSS integration - only the single main /feed endpoint could
/// be found publicly; every guessed section-specific feed (e.g. /india/feed) 404s. Feed URLs live
/// entirely in configuration under NewsCrawler:Providers[Name="DeccanHerald"]:Feeds, never
/// hardcoded here.
/// </summary>
public sealed class DeccanHeraldRssProvider : BaseRssProvider
{
    public const string ProviderName = "DeccanHerald";
    public const string ClientName = "DeccanHeraldRssClient";

    public DeccanHeraldRssProvider(IHttpClientFactory httpClientFactory, ILogger<DeccanHeraldRssProvider> logger)
        : base(httpClientFactory, logger)
    {
    }

    public override string Name => ProviderName;

    protected override string HttpClientName => ClientName;
}
