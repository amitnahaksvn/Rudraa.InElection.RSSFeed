using Microsoft.Extensions.Logging;

namespace Infrastructure.RssProviders;

/// <summary>
/// Amar Ujala (amarujala.com, Hindi) RSS integration - rss/{slug}.xml, with hundreds of
/// district-level feeds alongside the topical ones actually used here. Feed URLs live entirely in
/// configuration under NewsCrawler:Providers[Name="AmarUjala"]:Feeds, never hardcoded here.
/// </summary>
public sealed class AmarUjalaRssProvider : BaseRssProvider
{
    public const string ProviderName = "AmarUjala";
    public const string ClientName = "AmarUjalaRssClient";

    public AmarUjalaRssProvider(IHttpClientFactory httpClientFactory, ILogger<AmarUjalaRssProvider> logger)
        : base(httpClientFactory, logger)
    {
    }

    public override string Name => ProviderName;

    protected override string HttpClientName => ClientName;
}
