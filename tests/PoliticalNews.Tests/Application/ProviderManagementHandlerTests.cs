using Moq;
using Application.Abstractions;
using Application.Models;
using Application.Options;
using Application.Providers.Commands.TestApiEndpoint;
using Application.Providers.Commands.TestRssFeed;
using Application.Providers.Queries.GetApiProviders;
using Application.Providers.Queries.GetRssProviders;
using Domain.Entities;
using Domain.Enums;

namespace PoliticalNews.Tests.Application;

public class ProviderManagementHandlerTests
{
    private static Mock<ICrawlCountryRepository> BuildCountryRepo(CrawlPipeline pipeline, params CrawlCountry[] countries)
    {
        var repo = new Mock<ICrawlCountryRepository>();
        repo.Setup(c => c.GetAllAsync(pipeline, It.IsAny<CancellationToken>())).ReturnsAsync(countries);
        return repo;
    }

    private static Mock<ICrawlFeedRepository> BuildFeedRepo(CrawlPipeline pipeline, params CrawlFeed[] feeds)
    {
        var repo = new Mock<ICrawlFeedRepository>();
        repo.Setup(f => f.GetAllAsync(pipeline, It.IsAny<CancellationToken>())).ReturnsAsync(feeds);
        return repo;
    }

    private static Mock<IProviderScheduleRepository> BuildScheduleRepo(CrawlPipeline pipeline, params ProviderSchedule[] schedules)
    {
        var repo = new Mock<IProviderScheduleRepository>();
        repo.Setup(s => s.GetAllAsync(pipeline, It.IsAny<CancellationToken>())).ReturnsAsync(schedules);
        return repo;
    }

    [Fact]
    public async Task GetRssProvidersQueryHandler_FlattensCountriesAndFoldsInCountryEnabled()
    {
        var countries = BuildCountryRepo(
            CrawlPipeline.Rss,
            new CrawlCountry { Name = "India", Enabled = true, Pipeline = CrawlPipeline.Rss },
            new CrawlCountry { Name = "United Kingdom", Enabled = false, Pipeline = CrawlPipeline.Rss });

        var schedules = BuildScheduleRepo(
            CrawlPipeline.Rss,
            new ProviderSchedule { Provider = "AajTak", Country = "India", Enabled = true, Cron = "*/5 * * * *", TimeZone = "UTC" },
            new ProviderSchedule { Provider = "BBCNews", Country = "United Kingdom", Enabled = true, Cron = "0 * * * *", TimeZone = "UTC" });

        var feeds = BuildFeedRepo(
            CrawlPipeline.Rss,
            new CrawlFeed { Id = "feed-home", Provider = "AajTak", Name = "Home", Url = "https://aajtak.in/rss?id=home", Category = "General", Language = "hi", Enabled = true },
            new CrawlFeed { Id = "feed-cricket", Provider = "AajTak", Name = "Cricket", Url = "https://tak.live/cricket-tak/rssfeed.xml", Category = "Sports", Language = "hi", Enabled = false },
            new CrawlFeed { Id = "feed-top", Provider = "BBCNews", Name = "TopStories", Url = "https://feeds.bbci.co.uk/top.xml", Category = "General", Language = "en", Enabled = true });

        var handler = new GetRssProvidersQueryHandler(countries.Object, schedules.Object, feeds.Object);

        var result = await handler.Handle(new GetRssProvidersQuery(), CancellationToken.None);

        Assert.Equal(2, result.Count);

        var aajTak = result.Single(p => p.Name == "AajTak");
        Assert.Equal("India", aajTak.Country);
        Assert.True(aajTak.Enabled);
        Assert.Equal(2, aajTak.Feeds.Count);
        Assert.Contains("1 of 2 feeds enabled", aajTak.Description);

        // United Kingdom's country-level Enabled=false must fold into the provider's own combined
        // Enabled flag, even though BBCNews's own Enabled is true.
        var bbc = result.Single(p => p.Name == "BBCNews");
        Assert.False(bbc.Enabled);
    }

    [Fact]
    public async Task GetApiProvidersQueryHandler_FlattensCountriesAndFoldsInCountryEnabled()
    {
        var countries = BuildCountryRepo(
            CrawlPipeline.Api,
            new CrawlCountry { Name = "India", Enabled = true, Pipeline = CrawlPipeline.Api });

        var schedules = BuildScheduleRepo(
            CrawlPipeline.Api,
            new ProviderSchedule
            {
                Provider = "NewsApiOrg",
                Country = "India",
                Enabled = true,
                Cron = "0 * * * *",
                TimeZone = "UTC",
                BaseUrl = "https://newsapi.org/v2",
                AuthType = ApiAuthType.QueryParameter,
                AuthParamName = "apiKey",
                TimeoutSeconds = 30,
            });

        var feeds = BuildFeedRepo(
            CrawlPipeline.Api,
            new CrawlFeed { Id = "ep-everything", Provider = "NewsApiOrg", Name = "Everything", Url = "everything", Category = "General", Language = "en", Enabled = true, QueryParameters = new Dictionary<string, string> { ["q"] = "India politics" } },
            new CrawlFeed { Id = "ep-top", Provider = "NewsApiOrg", Name = "TopHeadlines", Url = "top-headlines", Category = "General", Language = "en", Enabled = false });

        var handler = new GetApiProvidersQueryHandler(countries.Object, schedules.Object, feeds.Object);

        var result = await handler.Handle(new GetApiProvidersQuery(), CancellationToken.None);

        var newsApiOrg = Assert.Single(result);
        Assert.Equal("India", newsApiOrg.Country);
        Assert.True(newsApiOrg.Enabled);
        Assert.Equal(2, newsApiOrg.Endpoints.Count);
        Assert.Equal("QueryParameter", newsApiOrg.AuthType);
        Assert.Contains("1 of 2 endpoints enabled", newsApiOrg.Description);

        var everything = newsApiOrg.Endpoints.Single(e => e.Name == "Everything");
        Assert.Equal("https://newsapi.org/v2/everything", everything.Url);
    }

    [Fact]
    public async Task TestRssFeedCommandHandler_Success_MapsFeedFetchResultToDto()
    {
        var feeds = new Mock<ICrawlFeedRepository>();
        feeds
            .Setup(f => f.GetByIdAsync("feed-home", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CrawlFeed { Id = "feed-home", Provider = "AajTak", Name = "Home", Url = "https://aajtak.in/rss?id=home", Category = "General", Language = "hi", Enabled = true });

        var provider = new Mock<IRssProvider>();
        provider.Setup(p => p.Name).Returns("AajTak");
        provider
            .Setup(p => p.FetchAllFeedsAsync(It.Is<IReadOnlyList<RssFeedOptions>>(f => f.Count == 1 && f[0].Url == "https://aajtak.in/rss?id=home"), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new FeedFetchResult
                {
                    FeedName = "Home",
                    FeedUrl = "https://aajtak.in/rss?id=home",
                    Success = true,
                    HttpStatusCode = 200,
                    Articles = [BuildArticle(), BuildArticle()],
                    FetchedAt = DateTimeOffset.UtcNow,
                    ProcessingDurationMs = 123,
                    RawXml = "<rss><channel><item><title>Test</title></item></channel></rss>",
                },
            ]);

        var handler = new TestRssFeedCommandHandler([provider.Object], feeds.Object);
        var result = await handler.Handle(new TestRssFeedCommand("feed-home"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(200, result.HttpStatusCode);
        Assert.Equal(2, result.ArticleCount);
        Assert.Equal(123, result.ProcessingDurationMs);
        Assert.Null(result.Error);
        Assert.Equal("<rss><channel><item><title>Test</title></item></channel></rss>", result.RawResponseBody);
    }

    [Fact]
    public async Task TestRssFeedCommandHandler_UnknownProvider_ReturnsNotFoundResult()
    {
        var feeds = new Mock<ICrawlFeedRepository>();
        feeds
            .Setup(f => f.GetByIdAsync("feed-orphan", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CrawlFeed { Id = "feed-orphan", Provider = "DoesNotExist", Name = "Home", Url = "https://example.com/rss", Category = "General", Language = "en", Enabled = true });

        var handler = new TestRssFeedCommandHandler([], feeds.Object);
        var result = await handler.Handle(new TestRssFeedCommand("feed-orphan"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task TestRssFeedCommandHandler_UnknownFeed_ReturnsNotFoundResult()
    {
        var feeds = new Mock<ICrawlFeedRepository>();
        feeds
            .Setup(f => f.GetByIdAsync("does-not-exist", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CrawlFeed?)null);

        var provider = new Mock<IRssProvider>();
        provider.Setup(p => p.Name).Returns("AajTak");

        var handler = new TestRssFeedCommandHandler([provider.Object], feeds.Object);
        var result = await handler.Handle(new TestRssFeedCommand("does-not-exist"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        provider.Verify(p => p.FetchAllFeedsAsync(It.IsAny<IReadOnlyList<RssFeedOptions>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TestApiEndpointCommandHandler_Success_MapsApiFetchResultToDtoAndPassesOnlyRequestedEndpoint()
    {
        var feeds = new Mock<ICrawlFeedRepository>();
        feeds
            .Setup(f => f.GetByIdAsync("ep-everything", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CrawlFeed { Id = "ep-everything", Provider = "NewsApiOrg", Country = "India", Name = "Everything", Url = "everything", Category = "General", Language = "en", Enabled = true, QueryParameters = new Dictionary<string, string> { ["q"] = "India politics" } });

        var schedules = new Mock<IProviderScheduleRepository>();
        schedules
            .Setup(s => s.GetAsync(CrawlPipeline.Api, "NewsApiOrg", "India", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderSchedule
            {
                Provider = "NewsApiOrg",
                Country = "India",
                Enabled = true,
                Cron = "0 * * * *",
                TimeZone = "UTC",
                BaseUrl = "https://newsapi.org/v2",
                AuthType = ApiAuthType.QueryParameter,
                AuthParamName = "apiKey",
                TimeoutSeconds = 30,
            });

        var provider = new Mock<INewsApiProvider>();
        provider.Setup(p => p.Name).Returns("NewsApiOrg");
        provider
            .Setup(p => p.FetchAllEndpointsAsync(It.Is<NewsApiProviderOptions>(o => o.Endpoints.Count == 1 && o.Endpoints[0].Name == "Everything"), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ApiFetchResult
                {
                    EndpointName = "Everything",
                    EndpointUrl = "https://newsapi.org/v2/everything",
                    Success = true,
                    HttpStatusCode = 200,
                    Articles = [BuildArticle()],
                    FetchedAt = DateTimeOffset.UtcNow,
                    ProcessingDurationMs = 456,
                    ResponseBody = """{"status":"ok","articles":[]}""",
                },
            ]);

        var handler = new TestApiEndpointCommandHandler([provider.Object], feeds.Object, schedules.Object);
        var result = await handler.Handle(new TestApiEndpointCommand("ep-everything"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(200, result.HttpStatusCode);
        Assert.Equal(1, result.ArticleCount);
        Assert.Equal(456, result.ProcessingDurationMs);
        Assert.Equal("""{"status":"ok","articles":[]}""", result.RawResponseBody);
    }

    [Fact]
    public async Task TestApiEndpointCommandHandler_UnknownEndpoint_ReturnsNotFoundResult()
    {
        var feeds = new Mock<ICrawlFeedRepository>();
        feeds
            .Setup(f => f.GetByIdAsync("does-not-exist", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CrawlFeed?)null);

        var schedules = new Mock<IProviderScheduleRepository>();

        var provider = new Mock<INewsApiProvider>();
        provider.Setup(p => p.Name).Returns("NewsApiOrg");

        var handler = new TestApiEndpointCommandHandler([provider.Object], feeds.Object, schedules.Object);
        var result = await handler.Handle(new TestApiEndpointCommand("does-not-exist"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        provider.Verify(p => p.FetchAllEndpointsAsync(It.IsAny<NewsApiProviderOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static NormalizedArticle BuildArticle() => new()
    {
        Provider = "TestProvider",
        FeedName = "TestFeed",
        Category = "General",
        Title = "Test Article",
        Url = "https://example.com/article",
        Source = "Test Source",
        PublishedAt = DateTimeOffset.UtcNow,
    };
}
