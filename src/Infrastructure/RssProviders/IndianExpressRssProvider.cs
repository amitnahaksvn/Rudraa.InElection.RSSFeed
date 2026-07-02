using Microsoft.Extensions.Logging;

namespace Infrastructure.RssProviders;

/// <summary>
/// The Indian Express (indianexpress.com) RSS integration - standard WordPress feeds
/// (/section/{name}/feed/). Feed URLs live entirely in configuration under
/// NewsCrawler:Providers[Name="IndianExpress"]:Feeds, never hardcoded here.
/// </summary>
public sealed class IndianExpressRssProvider : BaseRssProvider
{
    public const string ProviderName = "IndianExpress";
    public const string ClientName = "IndianExpressRssClient";

    public IndianExpressRssProvider(IHttpClientFactory httpClientFactory, ILogger<IndianExpressRssProvider> logger)
        : base(httpClientFactory, logger)
    {
    }

    public override string Name => ProviderName;

    protected override string HttpClientName => ClientName;
}
