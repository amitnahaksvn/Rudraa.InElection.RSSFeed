using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Application.Abstractions;
using Application.Models;
using Application.Options;
using Application.Services;
using Domain.Entities;
using Domain.Enums;

namespace PoliticalNews.Tests.Application;

/// <summary>
/// Mirrors <see cref="NewsCrawlerOrchestratorTests"/>'s own Countries-behavior coverage, for the
/// <see cref="NewsApiCrawlerOrchestrator"/> counterpart added when the API pipeline gained the
/// same database-backed <see cref="ICrawlCountryRepository"/>/<see cref="ICrawlFeedRepository"/>
/// grouping RSS already had.
/// </summary>
public class NewsApiCrawlerOrchestratorTests
{
    private static NewsApiCrawlerOptions BuildScalarOptions() => new() { BatchSize = 100 };

    private static CrawlCountry BuildCountry(bool enabled = true) =>
        new() { Name = "United States", Enabled = enabled, Pipeline = CrawlPipeline.Api };

    private static ProviderSchedule BuildSchedule(bool enabled = true) => new()
    {
        Provider = "FEC",
        Country = "United States",
        Enabled = enabled,
        Cron = "0 * * * *",
        TimeZone = "UTC",
        BaseUrl = "https://api.open.fec.gov/v1",
        AuthType = ApiAuthType.QueryParameter,
        AuthParamName = "api_key",
    };

    private static CrawlFeed BuildEndpointFeed() => new()
    {
        Id = "feed-fec-candidates",
        Provider = "FEC",
        Name = "Candidates",
        Url = "candidates/",
        Category = "Politics",
        Language = "en",
        Enabled = true,
    };

    private static NormalizedArticle BuildArticle(string url) => new()
    {
        Provider = "FEC",
        FeedName = "FEC",
        Category = "Politics",
        Title = "Example Candidate",
        Url = url,
        Source = "FEC",
        PublishedAt = DateTimeOffset.UtcNow
    };

    private static ApiFetchResult BuildEndpointResult(bool success, IReadOnlyList<NormalizedArticle>? articles = null, string? error = null) => new()
    {
        EndpointName = "Candidates",
        EndpointUrl = "https://api.open.fec.gov/v1/candidates/",
        Success = success,
        Error = error,
        Articles = articles ?? [],
        FetchedAt = DateTimeOffset.UtcNow,
        HttpStatusCode = success ? 200 : null,
        ProcessingDurationMs = 10
    };

    private static Mock<ICrawlLockRepository> BuildAcquiredLockRepo()
    {
        var lockRepo = new Mock<ICrawlLockRepository>();
        lockRepo
            .Setup(l => l.TryAcquireAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return lockRepo;
    }

    private static Mock<ICrawlCountryRepository> BuildCountryRepo(CrawlCountry country)
    {
        var repo = new Mock<ICrawlCountryRepository>();
        repo.Setup(c => c.GetAllAsync(CrawlPipeline.Api, It.IsAny<CancellationToken>())).ReturnsAsync([country]);
        return repo;
    }

    private static Mock<IProviderScheduleRepository> BuildScheduleRepo(ProviderSchedule schedule)
    {
        var repo = new Mock<IProviderScheduleRepository>();
        repo.Setup(s => s.GetAllAsync(CrawlPipeline.Api, It.IsAny<CancellationToken>())).ReturnsAsync([schedule]);
        return repo;
    }

    private static Mock<ICrawlFeedRepository> BuildFeedRepo(CrawlFeed feed)
    {
        var repo = new Mock<ICrawlFeedRepository>();
        repo.Setup(f => f.GetAllAsync(CrawlPipeline.Api, It.IsAny<CancellationToken>())).ReturnsAsync([feed]);
        return repo;
    }

    [Fact]
    public async Task RunCrawlAsync_PersistsArticlesAndRecordsSuccess()
    {
        var provider = new Mock<INewsApiProvider>();
        provider.Setup(p => p.Name).Returns("FEC");
        provider.Setup(p => p.FetchAllEndpointsAsync(It.IsAny<NewsApiProviderOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([BuildEndpointResult(true, [BuildArticle("https://www.fec.gov/data/candidate/1"), BuildArticle("https://www.fec.gov/data/candidate/2")])]);

        var articleRepo = new Mock<INewsArticleRepository>();
        articleRepo
            .Setup(r => r.UpsertAsync(It.IsAny<NewsArticle>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ArticleUpsertOutcome.Inserted);

        var historyRepo = new Mock<ICrawlHistoryRepository>();
        historyRepo
            .Setup(r => r.InsertAsync(It.IsAny<CrawlHistory>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("history-1");

        var orchestrator = new NewsApiCrawlerOrchestrator(
            [provider.Object],
            articleRepo.Object,
            Mock.Of<IArticleFingerprintRepository>(),
            historyRepo.Object,
            BuildAcquiredLockRepo().Object,
            Mock.Of<IErrorLogRepository>(),
            new PoliticalNews.Tests.TestSupport.FakeHostEnvironment(),
            BuildCountryRepo(BuildCountry()).Object,
            BuildScheduleRepo(BuildSchedule()).Object,
            BuildFeedRepo(BuildEndpointFeed()).Object,
            [],
            Options.Create(BuildScalarOptions()),
            NullLogger<NewsApiCrawlerOrchestrator>.Instance);

        var history = await orchestrator.RunCrawlAsync(CancellationToken.None);

        Assert.Equal(CrawlStatus.Completed, history.Status);
        Assert.Equal(2, history.NewArticles);
        Assert.Equal(CrawlPipeline.Api, history.Pipeline);
        Assert.Equal(["FEC"], history.Providers);
        articleRepo.Verify(r => r.UpsertAsync(It.Is<NewsArticle>(a => a.Country == "United States"), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RunCrawlAsync_CountryDisabled_SkipsEveryProviderUnderIt()
    {
        var provider = new Mock<INewsApiProvider>();
        provider.Setup(p => p.Name).Returns("FEC");

        var articleRepo = new Mock<INewsArticleRepository>();
        var historyRepo = new Mock<ICrawlHistoryRepository>();

        var orchestrator = new NewsApiCrawlerOrchestrator(
            [provider.Object],
            articleRepo.Object,
            Mock.Of<IArticleFingerprintRepository>(),
            historyRepo.Object,
            BuildAcquiredLockRepo().Object,
            Mock.Of<IErrorLogRepository>(),
            new PoliticalNews.Tests.TestSupport.FakeHostEnvironment(),
            BuildCountryRepo(BuildCountry(enabled: false)).Object,
            BuildScheduleRepo(BuildSchedule()).Object,
            BuildFeedRepo(BuildEndpointFeed()).Object,
            [],
            Options.Create(BuildScalarOptions()),
            NullLogger<NewsApiCrawlerOrchestrator>.Instance);

        var history = await orchestrator.RunCrawlAsync(CancellationToken.None);

        // A country-disabled provider is skipped exactly like a lock-held one - no persisted run.
        Assert.Equal(CrawlStatus.Skipped, history.Status);
        provider.Verify(p => p.FetchAllEndpointsAsync(It.IsAny<NewsApiProviderOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        historyRepo.Verify(r => r.InsertAsync(It.IsAny<CrawlHistory>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
