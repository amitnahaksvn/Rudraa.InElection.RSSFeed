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

public class NewsCrawlerOrchestratorTests
{
    private static NewsCrawlerOptions BuildOptions(params string[] feedNames) => new()
    {
        BatchSize = 100,
        Providers =
        [
            new RssProviderOptions
            {
                Name = "AajTak",
                Enabled = true,
                Feeds = feedNames
                    .Select(name => new RssFeedOptions { Name = name, Url = $"https://example.com/{name}", Category = "General", Enabled = true })
                    .ToList()
            }
        ]
    };

    private static NewsCrawlerOptions BuildTwoProviderOptions() => new()
    {
        BatchSize = 100,
        Providers =
        [
            new RssProviderOptions
            {
                Name = "AajTak",
                Enabled = true,
                Feeds = [new RssFeedOptions { Name = "Home", Url = "https://example.com/Home", Category = "General", Enabled = true }]
            },
            new RssProviderOptions
            {
                Name = "ABPNews",
                Enabled = true,
                Feeds = [new RssFeedOptions { Name = "Home", Url = "https://example.com/ABPHome", Category = "General", Enabled = true }]
            }
        ]
    };

    private static NormalizedArticle BuildArticle(string url, string title = "Headline") => new()
    {
        Provider = "AajTak",
        FeedName = "Home",
        Category = "General",
        Title = title,
        Url = url,
        Source = "https://example.com/home",
        PublishedAt = DateTimeOffset.UtcNow
    };

    private static FeedFetchResult BuildFeedResult(bool success, IReadOnlyList<NormalizedArticle>? articles = null, string? error = null) => new()
    {
        FeedName = "Home",
        FeedUrl = "https://example.com/Home",
        Success = success,
        Error = error,
        Articles = articles ?? [],
        FetchedAt = DateTimeOffset.UtcNow,
        HttpStatusCode = success ? 200 : null,
        RawXml = success ? "<rss></rss>" : null,
        ContentHash = success ? "hash" : null,
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

    private static Mock<IRssRawResponseRepository> BuildRawResponseRepo() => new();

    [Fact]
    public async Task RunCrawlAsync_PersistsArticlesAndRecordsSuccess()
    {
        var provider = new Mock<IRssProvider>();
        provider.Setup(p => p.Name).Returns("AajTak");
        provider.Setup(p => p.FetchAllFeedsAsync(It.IsAny<IReadOnlyList<RssFeedOptions>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([BuildFeedResult(true, [BuildArticle("https://example.com/a1"), BuildArticle("https://example.com/a2")])]);

        var articleRepo = new Mock<INewsArticleRepository>();
        articleRepo
            .Setup(r => r.UpsertAsync(It.IsAny<NewsArticle>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ArticleUpsertOutcome.Inserted);

        var historyRepo = new Mock<ICrawlHistoryRepository>();
        historyRepo
            .Setup(r => r.InsertAsync(It.IsAny<CrawlHistory>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("history-1");

        var rawResponseRepo = BuildRawResponseRepo();

        var orchestrator = new NewsCrawlerOrchestrator(
            [provider.Object],
            articleRepo.Object,
            historyRepo.Object,
            BuildAcquiredLockRepo().Object,
            rawResponseRepo.Object,
            Options.Create(BuildOptions("Home")),
            NullLogger<NewsCrawlerOrchestrator>.Instance);

        var history = await orchestrator.RunCrawlAsync(CancellationToken.None);

        Assert.Equal(CrawlStatus.Completed, history.Status);
        Assert.Equal(2, history.NewArticles);
        Assert.Equal(0, history.UpdatedArticles);
        Assert.Equal(0, history.DuplicateArticles);
        Assert.Empty(history.FailedFeeds);
        articleRepo.Verify(r => r.UpsertAsync(It.IsAny<NewsArticle>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        historyRepo.Verify(r => r.UpdateAsync(It.Is<CrawlHistory>(h => h.Status == CrawlStatus.Completed), It.IsAny<CancellationToken>()), Times.Once);
        rawResponseRepo.Verify(r => r.InsertAsync(It.Is<RssRawResponse>(x => x.ParseSucceeded), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunCrawlAsync_FeedFailure_RecordsCompletedWithErrorsAndFailedFeedName()
    {
        var provider = new Mock<IRssProvider>();
        provider.Setup(p => p.Name).Returns("AajTak");
        provider.Setup(p => p.FetchAllFeedsAsync(It.IsAny<IReadOnlyList<RssFeedOptions>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([BuildFeedResult(false, error: "network timeout")]);

        var articleRepo = new Mock<INewsArticleRepository>();
        var historyRepo = new Mock<ICrawlHistoryRepository>();
        historyRepo
            .Setup(r => r.InsertAsync(It.IsAny<CrawlHistory>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("history-2");

        var rawResponseRepo = BuildRawResponseRepo();

        var orchestrator = new NewsCrawlerOrchestrator(
            [provider.Object],
            articleRepo.Object,
            historyRepo.Object,
            BuildAcquiredLockRepo().Object,
            rawResponseRepo.Object,
            Options.Create(BuildOptions("Home")),
            NullLogger<NewsCrawlerOrchestrator>.Instance);

        var history = await orchestrator.RunCrawlAsync(CancellationToken.None);

        Assert.Equal(CrawlStatus.CompletedWithErrors, history.Status);
        Assert.Contains("AajTak/Home", history.FailedFeeds);
        articleRepo.Verify(r => r.UpsertAsync(It.IsAny<NewsArticle>(), It.IsAny<CancellationToken>()), Times.Never);
        rawResponseRepo.Verify(r => r.InsertAsync(It.Is<RssRawResponse>(x => !x.ParseSucceeded), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunCrawlAsync_DuplicateArticle_IsCountedAsDuplicateNotNew()
    {
        var provider = new Mock<IRssProvider>();
        provider.Setup(p => p.Name).Returns("AajTak");
        provider.Setup(p => p.FetchAllFeedsAsync(It.IsAny<IReadOnlyList<RssFeedOptions>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([BuildFeedResult(true, [BuildArticle("https://example.com/dup")])]);

        var articleRepo = new Mock<INewsArticleRepository>();
        articleRepo
            .Setup(r => r.UpsertAsync(It.IsAny<NewsArticle>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ArticleUpsertOutcome.DuplicateSkipped);

        var historyRepo = new Mock<ICrawlHistoryRepository>();
        historyRepo
            .Setup(r => r.InsertAsync(It.IsAny<CrawlHistory>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("history-3");

        var orchestrator = new NewsCrawlerOrchestrator(
            [provider.Object],
            articleRepo.Object,
            historyRepo.Object,
            BuildAcquiredLockRepo().Object,
            BuildRawResponseRepo().Object,
            Options.Create(BuildOptions("Home")),
            NullLogger<NewsCrawlerOrchestrator>.Instance);

        var history = await orchestrator.RunCrawlAsync(CancellationToken.None);

        Assert.Equal(0, history.NewArticles);
        Assert.Equal(1, history.DuplicateArticles);
    }

    [Fact]
    public async Task RunCrawlAsync_LockAlreadyHeld_ReturnsSkippedWithoutFetchingFeeds()
    {
        var provider = new Mock<IRssProvider>();
        provider.Setup(p => p.Name).Returns("AajTak");

        var lockRepo = new Mock<ICrawlLockRepository>();
        lockRepo
            .Setup(l => l.TryAcquireAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var articleRepo = new Mock<INewsArticleRepository>();
        var historyRepo = new Mock<ICrawlHistoryRepository>();

        var orchestrator = new NewsCrawlerOrchestrator(
            [provider.Object],
            articleRepo.Object,
            historyRepo.Object,
            lockRepo.Object,
            BuildRawResponseRepo().Object,
            Options.Create(BuildOptions("Home")),
            NullLogger<NewsCrawlerOrchestrator>.Instance);

        var history = await orchestrator.RunCrawlAsync(CancellationToken.None);

        Assert.Equal(CrawlStatus.Skipped, history.Status);
        provider.Verify(p => p.FetchAllFeedsAsync(It.IsAny<IReadOnlyList<RssFeedOptions>>(), It.IsAny<CancellationToken>()), Times.Never);
        historyRepo.Verify(r => r.InsertAsync(It.IsAny<CrawlHistory>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunCrawlAsync_WithProviderNames_OnlyCrawlsNamedProviders()
    {
        var aajTak = new Mock<IRssProvider>();
        aajTak.Setup(p => p.Name).Returns("AajTak");
        aajTak.Setup(p => p.FetchAllFeedsAsync(It.IsAny<IReadOnlyList<RssFeedOptions>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([BuildFeedResult(true, [BuildArticle("https://example.com/a1")])]);

        var abpNews = new Mock<IRssProvider>();
        abpNews.Setup(p => p.Name).Returns("ABPNews");

        var articleRepo = new Mock<INewsArticleRepository>();
        articleRepo
            .Setup(r => r.UpsertAsync(It.IsAny<NewsArticle>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ArticleUpsertOutcome.Inserted);

        var historyRepo = new Mock<ICrawlHistoryRepository>();
        historyRepo
            .Setup(r => r.InsertAsync(It.IsAny<CrawlHistory>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("history-4");

        var orchestrator = new NewsCrawlerOrchestrator(
            [aajTak.Object, abpNews.Object],
            articleRepo.Object,
            historyRepo.Object,
            BuildAcquiredLockRepo().Object,
            BuildRawResponseRepo().Object,
            Options.Create(BuildTwoProviderOptions()),
            NullLogger<NewsCrawlerOrchestrator>.Instance);

        var history = await orchestrator.RunCrawlAsync(["AajTak"], CancellationToken.None);

        Assert.Equal(CrawlStatus.Completed, history.Status);
        Assert.Equal(1, history.NewArticles);
        aajTak.Verify(p => p.FetchAllFeedsAsync(It.IsAny<IReadOnlyList<RssFeedOptions>>(), It.IsAny<CancellationToken>()), Times.Once);
        abpNews.Verify(p => p.FetchAllFeedsAsync(It.IsAny<IReadOnlyList<RssFeedOptions>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunCrawlAsync_OneProviderLockHeld_OthersStillCrawl()
    {
        var aajTak = new Mock<IRssProvider>();
        aajTak.Setup(p => p.Name).Returns("AajTak");
        aajTak.Setup(p => p.FetchAllFeedsAsync(It.IsAny<IReadOnlyList<RssFeedOptions>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([BuildFeedResult(true, [BuildArticle("https://example.com/a1")])]);

        var abpNews = new Mock<IRssProvider>();
        abpNews.Setup(p => p.Name).Returns("ABPNews");

        // Locks are per provider ("news-crawler:{Provider}"): ABPNews's is held elsewhere,
        // AajTak's is free - AajTak must still crawl rather than the whole run being skipped.
        var lockRepo = new Mock<ICrawlLockRepository>();
        lockRepo
            .Setup(l => l.TryAcquireAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string name, string _, TimeSpan _, CancellationToken _) => !name.EndsWith(":ABPNews"));

        var articleRepo = new Mock<INewsArticleRepository>();
        articleRepo
            .Setup(r => r.UpsertAsync(It.IsAny<NewsArticle>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ArticleUpsertOutcome.Inserted);

        var historyRepo = new Mock<ICrawlHistoryRepository>();
        historyRepo
            .Setup(r => r.InsertAsync(It.IsAny<CrawlHistory>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("history-6");

        var orchestrator = new NewsCrawlerOrchestrator(
            [aajTak.Object, abpNews.Object],
            articleRepo.Object,
            historyRepo.Object,
            lockRepo.Object,
            BuildRawResponseRepo().Object,
            Options.Create(BuildTwoProviderOptions()),
            NullLogger<NewsCrawlerOrchestrator>.Instance);

        var history = await orchestrator.RunCrawlAsync(CancellationToken.None);

        Assert.Equal(CrawlStatus.Completed, history.Status);
        Assert.Equal(1, history.NewArticles);
        aajTak.Verify(p => p.FetchAllFeedsAsync(It.IsAny<IReadOnlyList<RssFeedOptions>>(), It.IsAny<CancellationToken>()), Times.Once);
        abpNews.Verify(p => p.FetchAllFeedsAsync(It.IsAny<IReadOnlyList<RssFeedOptions>>(), It.IsAny<CancellationToken>()), Times.Never);
        lockRepo.Verify(l => l.ReleaseAsync("news-crawler:AajTak", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public async Task RunCrawlAsync_RawResponseSavedOnlyWhenBothGlobalAndProviderToggleAreTrue(
        bool globalSave, bool providerSave, bool expectSaved)
    {
        var provider = new Mock<IRssProvider>();
        provider.Setup(p => p.Name).Returns("AajTak");
        provider.Setup(p => p.FetchAllFeedsAsync(It.IsAny<IReadOnlyList<RssFeedOptions>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([BuildFeedResult(true, [BuildArticle("https://example.com/a1")])]);

        var articleRepo = new Mock<INewsArticleRepository>();
        articleRepo
            .Setup(r => r.UpsertAsync(It.IsAny<NewsArticle>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ArticleUpsertOutcome.Inserted);

        var historyRepo = new Mock<ICrawlHistoryRepository>();
        historyRepo
            .Setup(r => r.InsertAsync(It.IsAny<CrawlHistory>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("history-5");

        var rawResponseRepo = BuildRawResponseRepo();

        var options = BuildOptions("Home");
        options.SaveRawResponses = globalSave;
        options.Providers[0].SaveRawResponses = providerSave;

        var orchestrator = new NewsCrawlerOrchestrator(
            [provider.Object],
            articleRepo.Object,
            historyRepo.Object,
            BuildAcquiredLockRepo().Object,
            rawResponseRepo.Object,
            Options.Create(options),
            NullLogger<NewsCrawlerOrchestrator>.Instance);

        await orchestrator.RunCrawlAsync(CancellationToken.None);

        rawResponseRepo.Verify(
            r => r.InsertAsync(It.IsAny<RssRawResponse>(), It.IsAny<CancellationToken>()),
            expectSaved ? Times.Once : Times.Never);
    }
}
