using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Application.Abstractions;
using Application.Options;
using Infrastructure.Mongo;
using Infrastructure.Persistence;
using Infrastructure.RssProviders;
using Infrastructure.Scheduling;

namespace Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    private const string CrawlerUserAgent =
        "Mozilla/5.0 (compatible; PoliticalNewsCrawler/1.0; +https://example.com/bot)";

    /// <summary>Only for providers whose CDN rejects crawler UAs on public feeds (News18).</summary>
    private const string BrowserUserAgent =
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";

    /// <summary>
    /// Registers Mongo, the repository layer, and every <see cref="IRssProvider"/>. Adding a new
    /// provider in a future phase is one line here plus one appsettings.json config block.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        MongoClassMapConfigurator.Configure();

        services
            .AddOptions<MongoDbOptions>()
            .Bind(configuration.GetSection(MongoDbOptions.SectionName))
            // Aspire (and the ASP.NET Core convention in general) injects resource connection
            // strings under ConnectionStrings:<name> - e.g. AppHost.cs's "mongodb" resource
            // becomes ConnectionStrings__mongodb. When present it wins over MongoDb:ConnectionString,
            // so the same code runs unchanged whether launched via the Aspire AppHost, plain
            // `dotnet run`, or docker-compose.
            .PostConfigure(options => options.ConnectionString = ResolveMongoConnectionString(configuration, options.ConnectionString))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<MongoDbContext>();
        services.AddSingleton<IMongoClient>(sp => sp.GetRequiredService<MongoDbContext>().Client);

        services.AddSingleton<INewsArticleRepository, NewsArticleRepository>();
        services.AddSingleton<ICrawlHistoryRepository, CrawlHistoryRepository>();
        services.AddSingleton<ICrawlLockRepository, CrawlLockRepository>();
        services.AddSingleton<IRssRawResponseRepository, RssRawResponseRepository>();

        AddRssProvider<AajTakRssProvider>(services, AajTakRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<AbpNewsRssProvider>(services, AbpNewsRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<ZeeNewsRssProvider>(services, ZeeNewsRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<IndiaTvRssProvider>(services, IndiaTvRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<NdtvRssProvider>(services, NdtvRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<IndianExpressRssProvider>(services, IndianExpressRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<TheHinduRssProvider>(services, TheHinduRssProvider.ClientName, CrawlerUserAgent);
        // News18's CDN (Akamai) returns 403 for crawler-style UAs while serving the same public
        // RSS feeds to browsers - the one provider that needs a browser-style UA.
        AddRssProvider<News18RssProvider>(services, News18RssProvider.ClientName, BrowserUserAgent);
        AddRssProvider<TimesOfIndiaRssProvider>(services, TimesOfIndiaRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<NavbharatTimesRssProvider>(services, NavbharatTimesRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<HindustanTimesRssProvider>(services, HindustanTimesRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<ThePrintRssProvider>(services, ThePrintRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<ScrollInRssProvider>(services, ScrollInRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<MintRssProvider>(services, MintRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<DeccanHeraldRssProvider>(services, DeccanHeraldRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<NewIndianExpressRssProvider>(services, NewIndianExpressRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<AmarUjalaRssProvider>(services, AmarUjalaRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<DainikBhaskarRssProvider>(services, DainikBhaskarRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<LiveHindustanRssProvider>(services, LiveHindustanRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<TheWeekRssProvider>(services, TheWeekRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<IndiaTodayRssProvider>(services, IndiaTodayRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<DeccanChronicleRssProvider>(services, DeccanChronicleRssProvider.ClientName, CrawlerUserAgent);
        // OneIndia's CDN returns 403 for crawler-style UAs while serving the same public RSS
        // feeds to browsers - same reasoning as News18.
        AddRssProvider<OneIndiaRssProvider>(services, OneIndiaRssProvider.ClientName, BrowserUserAgent);
        AddRssProvider<NewsXRssProvider>(services, NewsXRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<DnaIndiaRssProvider>(services, DnaIndiaRssProvider.ClientName, CrawlerUserAgent);

        services.AddHostedService<MongoIndexInitializerHostedService>();

        // Requires the caller to have already called AddHangfire(...) (Web/Worker's Program.cs -
        // connection-string resolution needs the builder before this method runs) so that
        // IRecurringJobManager/JobStorage are resolvable; both only need the client-side API, not
        // a running server, so they work from Web (read/trigger-only) as much as from Worker
        // (which executes).
        services.AddSingleton<ICrawlJobTrigger, HangfireCrawlJobTrigger>();
        services.AddSingleton<ICrawlJobStatusReader, HangfireCrawlJobStatusReader>();
        services.AddTransient<HangfireCrawlJobExecutor>();
        services.AddTransient<HangfireRawResponseCleanupExecutor>();

        return services;
    }

    private static void AddRssProvider<TProvider>(IServiceCollection services, string clientName, string userAgent)
        where TProvider : class, IRssProvider
    {
        services.AddHttpClient(clientName, (sp, client) =>
        {
            client.Timeout = sp.GetRequiredService<IOptions<NewsCrawlerOptions>>().Value.FeedTimeout;
            client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        });
        services.AddSingleton<IRssProvider, TProvider>();
    }

    /// <summary>
    /// Same Aspire-first-else-configured-value resolution used for <see cref="MongoDbOptions"/>
    /// above, exposed for callers (e.g. Hangfire's Mongo storage setup) that need the connection
    /// string before <see cref="IServiceProvider"/> / bound options exist yet.
    /// </summary>
    public static string ResolveMongoConnectionString(IConfiguration configuration, string fallback)
    {
        var aspireConnectionString = configuration.GetConnectionString("mongodb");
        return string.IsNullOrWhiteSpace(aspireConnectionString) ? fallback : aspireConnectionString;
    }
}
