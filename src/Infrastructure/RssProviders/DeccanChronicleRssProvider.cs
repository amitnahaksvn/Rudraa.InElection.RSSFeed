using Microsoft.Extensions.Logging;

namespace Infrastructure.RssProviders;

/// <summary>
/// Deccan Chronicle (deccanchronicle.com) RSS integration - only the single bare /feed endpoint
/// could be found publicly; every guessed section-specific feed (e.g. /nation/feed) 404s. Feed
/// URLs live entirely in configuration under NewsCrawler:Providers[Name="DeccanChronicle"]:Feeds,
/// never hardcoded here.
/// </summary>
public sealed class DeccanChronicleRssProvider : BaseRssProvider
{
    public const string ProviderName = "DeccanChronicle";
    public const string ClientName = "DeccanChronicleRssClient";

    public DeccanChronicleRssProvider(IHttpClientFactory httpClientFactory, ILogger<DeccanChronicleRssProvider> logger)
        : base(httpClientFactory, logger)
    {
    }

    public override string Name => ProviderName;

    protected override string HttpClientName => ClientName;
}
