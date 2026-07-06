using Microsoft.Extensions.Options;
using Moq;
using Application.Abstractions;
using Application.Models;
using Application.Options;
using Application.Providers.Commands.TestApiEndpoint;
using Application.Providers.Commands.TestRssFeed;
using Application.Providers.Queries.GetApiProviders;
using Application.Providers.Queries.GetRssProviders;

namespace PoliticalNews.Tests.Application;

public class ProviderManagementHandlerTests
{
    private static NewsCrawlerOptions BuildRssOptions() => new()
    {
        Countries =
        [
            new CountryOptions
            {
                Name = "India",
                Enabled = true,
                Providers =
                [
                    new RssProviderOptions
                    {
                        Name = "AajTak",
                        Enabled = true,
                        Cron = "*/5 * * * *",
                        Feeds =
                        [
                            new RssFeedOptions { Name = "Home", Url = "https://aajtak.in/rss?id=home", Category = "General", Language = "hi", Enabled = true },
                            new RssFeedOptions { Name = "Cricket", Url = "https://tak.live/cricket-tak/rssfeed.xml", Category = "Sports", Language = "hi", Enabled = false },
                        ],
                    },
                ],
            },
            new CountryOptions
            {
                Name = "United Kingdom",
                Enabled = false,
                Providers =
                [
                    new RssProviderOptions { Name = "BBCNews", Enabled = true, Cron = "0 * * * *", Feeds = [new RssFeedOptions { Name = "TopStories", Url = "https://feeds.bbci.co.uk/top.xml", Category = "General", Language = "en", Enabled = true }] },
                ],
            },
        ],
    };

    private static NewsApiCrawlerOptions BuildApiOptions() => new()
    {
        Countries =
        [
            new NewsApiCountryOptions
            {
                Name = "India",
                Enabled = true,
                Providers =
                [
                    new NewsApiProviderOptions
                    {
                        Name = "NewsApiOrg",
                        Enabled = true,
                        Cron = "0 * * * *",
                        BaseUrl = "https://newsapi.org/v2",
                        AuthType = ApiAuthType.QueryParameter,
                        AuthParamName = "apiKey",
                        TimeoutSeconds = 30,
                        Endpoints =
                        [
                            new NewsApiEndpointOptions { Name = "Everything", Endpoint = "everything", Category = "General", Language = "en", Enabled = true, QueryParameters = new Dictionary<string, string> { ["q"] = "India politics" } },
                            new NewsApiEndpointOptions { Name = "TopHeadlines", Endpoint = "top-headlines", Category = "General", Language = "en", Enabled = false },
                        ],
                    },
                ],
            },
        ],
    };

    [Fact]
    public async Task GetRssProvidersQueryHandler_FlattensCountriesAndFoldsInCountryEnabled()
    {
        var handler = new GetRssProvidersQueryHandler(Options.Create(BuildRssOptions()));

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
        var handler = new GetApiProvidersQueryHandler(Options.Create(BuildApiOptions()));

        var result = await handler.Handle(new GetApiProvidersQuery(), CancellationToken.None);

        var newsApiOrg = Assert.Single(result);
        Assert.Equal("India", newsApiOrg.Country);
        Assert.True(newsApiOrg.Enabled);
        Assert.Equal(2, newsApiOrg.Endpoints.Count);
        Assert.Equal("QueryParameter", newsApiOrg.AuthType);
        Assert.Contains("1 of 2 endpoints enabled", newsApiOrg.Description);
    }

    [Fact]
    public async Task TestRssFeedCommandHandler_Success_MapsFeedFetchResultToDto()
    {
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
                },
            ]);

        var handler = new TestRssFeedCommandHandler([provider.Object], Options.Create(BuildRssOptions()));
        var result = await handler.Handle(new TestRssFeedCommand("India", "AajTak", "https://aajtak.in/rss?id=home"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(200, result.HttpStatusCode);
        Assert.Equal(2, result.ArticleCount);
        Assert.Equal(123, result.ProcessingDurationMs);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task TestRssFeedCommandHandler_UnknownProvider_ReturnsNotFoundResult()
    {
        var handler = new TestRssFeedCommandHandler([], Options.Create(BuildRssOptions()));
        var result = await handler.Handle(new TestRssFeedCommand("India", "DoesNotExist", "https://example.com/rss"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task TestRssFeedCommandHandler_UnknownFeed_ReturnsNotFoundResult()
    {
        var provider = new Mock<IRssProvider>();
        provider.Setup(p => p.Name).Returns("AajTak");

        var handler = new TestRssFeedCommandHandler([provider.Object], Options.Create(BuildRssOptions()));
        var result = await handler.Handle(new TestRssFeedCommand("India", "AajTak", "https://not-configured.example/rss"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        provider.Verify(p => p.FetchAllFeedsAsync(It.IsAny<IReadOnlyList<RssFeedOptions>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TestApiEndpointCommandHandler_Success_MapsApiFetchResultToDtoAndPassesOnlyRequestedEndpoint()
    {
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
                },
            ]);

        var handler = new TestApiEndpointCommandHandler([provider.Object], Options.Create(BuildApiOptions()));
        var result = await handler.Handle(new TestApiEndpointCommand("India", "NewsApiOrg", "Everything"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(200, result.HttpStatusCode);
        Assert.Equal(1, result.ArticleCount);
        Assert.Equal(456, result.ProcessingDurationMs);
    }

    [Fact]
    public async Task TestApiEndpointCommandHandler_UnknownEndpoint_ReturnsNotFoundResult()
    {
        var provider = new Mock<INewsApiProvider>();
        provider.Setup(p => p.Name).Returns("NewsApiOrg");

        var handler = new TestApiEndpointCommandHandler([provider.Object], Options.Create(BuildApiOptions()));
        var result = await handler.Handle(new TestApiEndpointCommand("India", "NewsApiOrg", "DoesNotExist"), CancellationToken.None);

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
