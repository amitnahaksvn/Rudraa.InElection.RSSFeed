using Microsoft.Extensions.Logging;

namespace Infrastructure.RssProviders;

/// <summary>
/// Aaj Tak (आज तक) RSS integration. Phase 1's only provider - feed URLs live entirely in
/// configuration under NewsCrawler:Providers[Name="AajTak"]:Feeds, never hardcoded here.
/// </summary>
public sealed class AajTakRssProvider : BaseRssProvider
{
    public const string ProviderName = "AajTak";
    public const string ClientName = "AajTakRssClient";

    public AajTakRssProvider(IHttpClientFactory httpClientFactory, ILogger<AajTakRssProvider> logger)
        : base(httpClientFactory, logger)
    {
    }

    public override string Name => ProviderName;

    protected override string HttpClientName => ClientName;
}
