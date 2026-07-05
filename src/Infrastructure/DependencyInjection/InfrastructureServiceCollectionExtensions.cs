using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Polly;
using Polly.Extensions.Http;
using Resend;
using Application.Abstractions;
using Application.Options;
using Infrastructure.Email;
using Infrastructure.Mongo;
using Infrastructure.NewsApiProviders;
using Infrastructure.Persistence;
using Infrastructure.RSS;
using Infrastructure.RssProviders;
using Infrastructure.Scheduling;
using Infrastructure.Seed;

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
        services.AddSingleton<IFeedSourceRepository, FeedSourceRepository>();
        services.AddSingleton<IFeedErrorLogRepository, FeedErrorLogRepository>();
        services.AddSingleton<IErrorLogRepository, ErrorLogRepository>();

        // Monitoring-alert email, backed by the official Resend SDK. AddResend registers IResend
        // as a typed HttpClient and returns the IHttpClientBuilder, so the same Polly
        // transient-error retry used by every other external HTTP dependency in this method
        // attaches here too - "not raw HttpClient" (Resend's SDK owns the request shape) without
        // giving up the retry behaviour. ResendEmailService (not IResend directly) is what every
        // caller depends on via IEmailService, so swapping providers later never touches a caller.
        services
            .AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // EmailOptions.MaxRetryAttempts drives this retry count directly (read from raw
        // configuration, not the bound EmailOptions instance, since DI registration happens before
        // any options are actually resolved) - previously this was a hardcoded 3 regardless of
        // what MaxRetryAttempts was configured to, making that setting a no-op.
        var emailMaxRetryAttempts = configuration.GetValue($"{EmailOptions.SectionName}:{nameof(EmailOptions.MaxRetryAttempts)}", 3);
        services
            .AddResend(o => o.ApiToken = configuration[$"{EmailOptions.SectionName}:{nameof(EmailOptions.ApiKey)}"] ?? string.Empty)
            .AddPolicyHandler(HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(emailMaxRetryAttempts, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));
        services.AddSingleton<EmailTemplateBuilder>();
        services.AddSingleton<IEmailService, ResendEmailService>();

        AddRssProvider<AajTakRssProvider>(services, AajTakRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<AbpNewsRssProvider>(services, AbpNewsRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<ZeeNewsRssProvider>(services, ZeeNewsRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<IndiaTvRssProvider>(services, IndiaTvRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<NdtvRssProvider>(services, NdtvRssProvider.ClientName, CrawlerUserAgent);
        // IndianExpress's CDN (Akamai) started returning 403 for the crawler UA after previously
        // working fine with it - same WAF signature as News18/OneIndia/PIB below.
        AddRssProvider<IndianExpressRssProvider>(services, IndianExpressRssProvider.ClientName, BrowserUserAgent);
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
        // DeccanChronicle's WAF returns 403 for the declared crawler UA while serving the same
        // feed to browser UAs - same reasoning as News18/OneIndia/PIB.
        AddRssProvider<DeccanChronicleRssProvider>(services, DeccanChronicleRssProvider.ClientName, BrowserUserAgent);
        // OneIndia's CDN returns 403 for crawler-style UAs while serving the same public RSS
        // feeds to browsers - same reasoning as News18.
        AddRssProvider<OneIndiaRssProvider>(services, OneIndiaRssProvider.ClientName, BrowserUserAgent);
        AddRssProvider<NewsXRssProvider>(services, NewsXRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<DnaIndiaRssProvider>(services, DnaIndiaRssProvider.ClientName, CrawlerUserAgent);
        // PIB's WAF returns 403 for the declared crawler UA while serving the same feed to
        // browser UAs - same reasoning as News18/OneIndia.
        AddRssProvider<PibRssProvider>(services, PibRssProvider.ClientName, BrowserUserAgent);
        AddRssProvider<NdmaRssProvider>(services, NdmaRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<MinistryOfPortsShippingRssProvider>(services, MinistryOfPortsShippingRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<MyGovIndiaRssProvider>(services, MyGovIndiaRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<GoogleNewsRssProvider>(services, GoogleNewsRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<YouTubeRssProvider>(services, YouTubeRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<NyTimesRssProvider>(services, NyTimesRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<TechCrunchRssProvider>(services, TechCrunchRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<SportskeedaRssProvider>(services, SportskeedaRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<FrontlineRssProvider>(services, FrontlineRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<HinduBusinessLineRssProvider>(services, HinduBusinessLineRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<TimesNowRssProvider>(services, TimesNowRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<AltNewsRssProvider>(services, AltNewsRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<TheBetterIndiaRssProvider>(services, TheBetterIndiaRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<TheProbeRssProvider>(services, TheProbeRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<IndiaDotComRssProvider>(services, IndiaDotComRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<OnmanoramaRssProvider>(services, OnmanoramaRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<TelanganaTodayRssProvider>(services, TelanganaTodayRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<TheQuintRssProvider>(services, TheQuintRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<FreePressJournalRssProvider>(services, FreePressJournalRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<NationalHeraldRssProvider>(services, NationalHeraldRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<OpIndiaRssProvider>(services, OpIndiaRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<TfiPostRssProvider>(services, TfiPostRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<OrganiserRssProvider>(services, OrganiserRssProvider.ClientName, CrawlerUserAgent);

        // International providers (Feedspot cross-check) - each provider's own doc comment notes
        // its country; NewsCrawler:Providers[...]:Feeds[...]:Country carries that same value
        // through to NormalizedArticle/NewsArticle and, on failure, ErrorLog, so a batch of crawl
        // failures in the error-notification email can be scanned by country, not just provider.
        AddRssProvider<NprRssProvider>(services, NprRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<BbcNewsRssProvider>(services, BbcNewsRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<SkyNewsRssProvider>(services, SkyNewsRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<TheGuardianRssProvider>(services, TheGuardianRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<CbcNewsRssProvider>(services, CbcNewsRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<GlobalNewsRssProvider>(services, GlobalNewsRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<AbcNewsAustraliaRssProvider>(services, AbcNewsAustraliaRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<SbsNewsRssProvider>(services, SbsNewsRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<SydneyMorningHeraldRssProvider>(services, SydneyMorningHeraldRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<DwRssProvider>(services, DwRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<DerSpiegelRssProvider>(services, DerSpiegelRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<France24RssProvider>(services, France24RssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<NhkWorldRssProvider>(services, NhkWorldRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<JapanTimesRssProvider>(services, JapanTimesRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<YonhapRssProvider>(services, YonhapRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<CnaRssProvider>(services, CnaRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<AntaraRssProvider>(services, AntaraRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<BangkokPostRssProvider>(services, BangkokPostRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<AlJazeeraRssProvider>(services, AlJazeeraRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<JerusalemPostRssProvider>(services, JerusalemPostRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<TimesOfIsraelRssProvider>(services, TimesOfIsraelRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<MexicoNewsDailyRssProvider>(services, MexicoNewsDailyRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<AnadoluAgencyRssProvider>(services, AnadoluAgencyRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<TassRssProvider>(services, TassRssProvider.ClientName, CrawlerUserAgent);

        // More United States providers, verified against a user-supplied publisher list.
        AddRssProvider<AbcNewsRssProvider>(services, AbcNewsRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<CbsNewsRssProvider>(services, CbsNewsRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<NbcNewsRssProvider>(services, NbcNewsRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<FoxNewsRssProvider>(services, FoxNewsRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<WashingtonPostRssProvider>(services, WashingtonPostRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<WsjRssProvider>(services, WsjRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<BloombergRssProvider>(services, BloombergRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<TheHillRssProvider>(services, TheHillRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<NewsweekRssProvider>(services, NewsweekRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<TimeMagazineRssProvider>(services, TimeMagazineRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<ForbesRssProvider>(services, ForbesRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<FortuneRssProvider>(services, FortuneRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<BusinessInsiderRssProvider>(services, BusinessInsiderRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<MarketWatchRssProvider>(services, MarketWatchRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<CnbcRssProvider>(services, CnbcRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<LaTimesRssProvider>(services, LaTimesRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<ProPublicaRssProvider>(services, ProPublicaRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<PbsNewsRssProvider>(services, PbsNewsRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<VoxRssProvider>(services, VoxRssProvider.ClientName, CrawlerUserAgent);

        // More United Kingdom providers, verified against a user-supplied publisher list.
        AddRssProvider<BbcSportRssProvider>(services, BbcSportRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<FinancialTimesRssProvider>(services, FinancialTimesRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<TheIndependentRssProvider>(services, TheIndependentRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<TheTelegraphRssProvider>(services, TheTelegraphRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<MetroRssProvider>(services, MetroRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<EveningStandardRssProvider>(services, EveningStandardRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<DailyExpressRssProvider>(services, DailyExpressRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<MirrorRssProvider>(services, MirrorRssProvider.ClientName, CrawlerUserAgent);
        AddRssProvider<PoliticsCoUkRssProvider>(services, PoliticsCoUkRssProvider.ClientName, CrawlerUserAgent);

        // The Mongo-driven FeedSource pipeline (PIB first) - a generic alternative to the
        // file-configured providers above, for feeds that need no publisher-specific quirks.
        // One shared named HttpClient (rather than one per FeedSource, which would need a DI
        // registration - i.e. a code change - per feed, defeating the whole point) with a Polly
        // transient-error retry on top; the real per-feed timeout is enforced by
        // DynamicFeedIngestionService's own linked CancellationTokenSource from
        // FeedSource.TimeoutSeconds, so this HttpClient's own Timeout is just a generous ceiling.
        // Browser UA, not CrawlerUserAgent: PIB (the first and currently only FeedSource) blocks
        // the declared crawler UA with 403 - same WAF behavior as News18/OneIndia above. Revisit
        // if a future FeedSource needs the opposite (unlikely, since a browser UA is accepted
        // more broadly than a declared-bot one across every publisher seen in this codebase so far).
        services.AddHttpClient(DynamicFeedIngestionService.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
            // Same reasoning as AddRssProvider below - a real Chrome tab always sends these
            // alongside its User-Agent, and WAFs like Akamai Bot Manager can still flag a request
            // as bot-like when a "browser" UA shows up without them.
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        }).AddPolicyHandler(HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));
        services.AddSingleton<IDynamicFeedIngestionService, DynamicFeedIngestionService>();
        services.AddSingleton<FeedSourceSeeder>();
        services.AddTransient<HangfireDynamicFeedJobExecutor>();

        // The JSON news-API pipeline (NewsAPI.org, GNews, TheNewsAPI, Currents, Mediastack,
        // NewsData.io, WorldNewsAPI) - one shared named HttpClient (same reasoning as
        // DynamicFeedClient above: one DI registration, not one per provider, so a new provider
        // never needs a code change to its HttpClient) with the same Polly transient-error retry.
        // Per-provider timeout is enforced by BaseNewsApiProvider's own linked
        // CancellationTokenSource from NewsApiProviderOptions.TimeoutSeconds.
        services.AddHttpClient(BaseNewsApiProvider.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromMinutes(2);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
        }).AddPolicyHandler(HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

        services.AddSingleton<INewsApiProvider, NewsApiOrgProvider>();
        services.AddSingleton<INewsApiProvider, GNewsProvider>();
        services.AddSingleton<INewsApiProvider, TheNewsApiProvider>();
        services.AddSingleton<INewsApiProvider, CurrentsApiProvider>();
        services.AddSingleton<INewsApiProvider, MediastackProvider>();
        services.AddSingleton<INewsApiProvider, NewsDataIoProvider>();
        services.AddSingleton<INewsApiProvider, WorldNewsApiProvider>();
        services.AddSingleton<INewsApiProvider, ApiTubeProvider>();
        services.AddSingleton<INewsApiProvider, NewscatcherApiProvider>();
        services.AddSingleton<INewsApiProvider, GdeltProvider>();
        services.AddSingleton<INewsApiProvider, SerpApiGoogleNewsProvider>();
        services.AddSingleton<INewsApiProvider, GuardianProvider>();
        services.AddSingleton<INewsApiProvider, DataGovInProvider>();
        // Custom INewsApiProvider implementation (not BaseNewsApiProvider - see its own doc
        // comments): Event Registry needs a POST+JSON body.
        services.AddSingleton<INewsApiProvider, EventRegistryProvider>();
        services.AddTransient<HangfireNewsApiJobExecutor>();

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
        services.AddTransient<HangfireErrorNotificationDispatchExecutor>();

        return services;
    }

    private static void AddRssProvider<TProvider>(IServiceCollection services, string clientName, string userAgent)
        where TProvider : class, IRssProvider
    {
        services.AddHttpClient(clientName, (sp, client) =>
        {
            client.Timeout = sp.GetRequiredService<IOptions<NewsCrawlerOptions>>().Value.FeedTimeout;
            client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);

            // A real Chrome tab always sends these alongside its User-Agent - some WAFs (Akamai
            // Bot Manager in particular, seen blocking News18/IndianExpress) score a request as
            // more bot-like when a "browser" User-Agent shows up without them, even though the UA
            // string alone matches. Harmless to send for CrawlerUserAgent providers too, so this
            // isn't conditional on which UA was passed in.
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
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
