using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Application.Abstractions;
using Application.Models;
using Application.Services;
using Domain.Entities;
using Domain.Enums;

namespace PoliticalNews.Tests.Application;

public class SocialMediaIngestionServiceTests
{
    private static SocialMediaSource BuildSource(bool enabled = true) => new()
    {
        Id = "source-1",
        Platform = SocialPlatform.YouTube,
        SourceType = SourceEntityType.Politician,
        Country = "India",
        Name = "Narendra Modi",
        Identifier = "UC1NF71EwP41VdjAU1iXdLkw",
        Language = "en",
        Category = "Video",
        Enabled = enabled,
        PollIntervalMinutes = 30,
        TimeoutSeconds = 60
    };

    private static NormalizedArticle BuildArticle() => new()
    {
        Provider = "YouTube",
        FeedName = "Narendra Modi",
        Category = "Video",
        Title = "PM addresses the nation",
        Url = "https://www.youtube.com/watch?v=abc123",
        Source = "https://www.youtube.com/feeds/videos.xml?channel_id=UC1NF71EwP41VdjAU1iXdLkw"
    };

    private static Mock<IHostEnvironment> BuildHostEnvironment()
    {
        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName).Returns("Development");
        env.SetupGet(e => e.ApplicationName).Returns("Web");
        return env;
    }

    [Fact]
    public async Task RunAsync_SourceNotFound_DoesNothing()
    {
        var sourceRepository = new Mock<ISocialMediaSourceRepository>();
        sourceRepository.Setup(r => r.GetByIdAsync("missing", It.IsAny<CancellationToken>())).ReturnsAsync((SocialMediaSource?)null);

        var historyRepository = new Mock<ICrawlHistoryRepository>();
        var service = new SocialMediaIngestionService(
            sourceRepository.Object,
            [],
            Mock.Of<INewsArticleRepository>(),
            historyRepository.Object,
            Mock.Of<IErrorLogRepository>(),
            BuildHostEnvironment().Object,
            NullLogger<SocialMediaIngestionService>.Instance);

        await service.RunAsync("missing", CancellationToken.None);

        historyRepository.Verify(r => r.InsertAsync(It.IsAny<CrawlHistory>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_SourceDisabled_DoesNothing()
    {
        var sourceRepository = new Mock<ISocialMediaSourceRepository>();
        sourceRepository.Setup(r => r.GetByIdAsync("source-1", It.IsAny<CancellationToken>())).ReturnsAsync(BuildSource(enabled: false));

        var historyRepository = new Mock<ICrawlHistoryRepository>();
        var service = new SocialMediaIngestionService(
            sourceRepository.Object,
            [],
            Mock.Of<INewsArticleRepository>(),
            historyRepository.Object,
            Mock.Of<IErrorLogRepository>(),
            BuildHostEnvironment().Object,
            NullLogger<SocialMediaIngestionService>.Instance);

        await service.RunAsync("source-1", CancellationToken.None);

        historyRepository.Verify(r => r.InsertAsync(It.IsAny<CrawlHistory>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_NoMatchingFetcher_LogsAndSkipsWithoutThrowing()
    {
        var sourceRepository = new Mock<ISocialMediaSourceRepository>();
        sourceRepository.Setup(r => r.GetByIdAsync("source-1", It.IsAny<CancellationToken>())).ReturnsAsync(BuildSource());

        var historyRepository = new Mock<ICrawlHistoryRepository>();
        var service = new SocialMediaIngestionService(
            sourceRepository.Object,
            [], // no ISocialPlatformFetcher registered for YouTube
            Mock.Of<INewsArticleRepository>(),
            historyRepository.Object,
            Mock.Of<IErrorLogRepository>(),
            BuildHostEnvironment().Object,
            NullLogger<SocialMediaIngestionService>.Instance);

        await service.RunAsync("source-1", CancellationToken.None);

        historyRepository.Verify(r => r.InsertAsync(It.IsAny<CrawlHistory>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_Success_PersistsArticlesAndUpdatesLastPolledAtAndHistory()
    {
        var source = BuildSource();
        var sourceRepository = new Mock<ISocialMediaSourceRepository>();
        sourceRepository.Setup(r => r.GetByIdAsync("source-1", It.IsAny<CancellationToken>())).ReturnsAsync(source);

        var fetcher = new Mock<ISocialPlatformFetcher>();
        fetcher.SetupGet(f => f.Platform).Returns(SocialPlatform.YouTube);
        fetcher.Setup(f => f.FetchAsync(source, It.IsAny<CancellationToken>())).ReturnsAsync([BuildArticle()]);

        var articleRepository = new Mock<INewsArticleRepository>();
        articleRepository
            .Setup(r => r.UpsertAsync(It.IsAny<NewsArticle>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ArticleUpsertOutcome.Inserted);

        string? historyId = null;
        var historyRepository = new Mock<ICrawlHistoryRepository>();
        historyRepository
            .Setup(r => r.InsertAsync(It.IsAny<CrawlHistory>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("history-1")
            .Callback<CrawlHistory, CancellationToken>((h, _) => historyId = h.Id);
        CrawlHistory? updatedHistory = null;
        historyRepository
            .Setup(r => r.UpdateAsync(It.IsAny<CrawlHistory>(), It.IsAny<CancellationToken>()))
            .Callback<CrawlHistory, CancellationToken>((h, _) => updatedHistory = h)
            .Returns(Task.CompletedTask);

        var service = new SocialMediaIngestionService(
            sourceRepository.Object,
            [fetcher.Object],
            articleRepository.Object,
            historyRepository.Object,
            Mock.Of<IErrorLogRepository>(),
            BuildHostEnvironment().Object,
            NullLogger<SocialMediaIngestionService>.Instance);

        await service.RunAsync("source-1", CancellationToken.None);

        sourceRepository.Verify(r => r.UpdateLastPolledAtAsync("source-1", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(updatedHistory);
        Assert.Equal(Domain.Enums.CrawlStatus.Completed, updatedHistory!.Status);
        Assert.Equal(1, updatedHistory.NewArticles);
    }

    [Fact]
    public async Task RunAsync_FetcherThrows_RecordsFailedHistoryAndErrorLog()
    {
        var source = BuildSource();
        var sourceRepository = new Mock<ISocialMediaSourceRepository>();
        sourceRepository.Setup(r => r.GetByIdAsync("source-1", It.IsAny<CancellationToken>())).ReturnsAsync(source);

        var fetcher = new Mock<ISocialPlatformFetcher>();
        fetcher.SetupGet(f => f.Platform).Returns(SocialPlatform.YouTube);
        fetcher.Setup(f => f.FetchAsync(source, It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("boom"));

        CrawlHistory? updatedHistory = null;
        var historyRepository = new Mock<ICrawlHistoryRepository>();
        historyRepository.Setup(r => r.InsertAsync(It.IsAny<CrawlHistory>(), It.IsAny<CancellationToken>())).ReturnsAsync("history-1");
        historyRepository
            .Setup(r => r.UpdateAsync(It.IsAny<CrawlHistory>(), It.IsAny<CancellationToken>()))
            .Callback<CrawlHistory, CancellationToken>((h, _) => updatedHistory = h)
            .Returns(Task.CompletedTask);

        var errorLogRepository = new Mock<IErrorLogRepository>();

        var service = new SocialMediaIngestionService(
            sourceRepository.Object,
            [fetcher.Object],
            Mock.Of<INewsArticleRepository>(),
            historyRepository.Object,
            errorLogRepository.Object,
            BuildHostEnvironment().Object,
            NullLogger<SocialMediaIngestionService>.Instance);

        await service.RunAsync("source-1", CancellationToken.None);

        Assert.NotNull(updatedHistory);
        Assert.Equal(Domain.Enums.CrawlStatus.Failed, updatedHistory!.Status);
        Assert.Equal("boom", updatedHistory.Error);
        errorLogRepository.Verify(r => r.InsertAsync(It.IsAny<ErrorLog>(), It.IsAny<CancellationToken>()), Times.Once);
        sourceRepository.Verify(r => r.UpdateLastPolledAtAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
