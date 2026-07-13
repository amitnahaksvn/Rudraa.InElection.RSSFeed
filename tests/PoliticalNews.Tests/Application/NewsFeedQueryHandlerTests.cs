using Moq;
using Application.Abstractions;
using Application.Models;
using Application.News.Queries.GetNewsFeed;
using Application.News.Queries.GetNewsFeedCount;
using Domain.Entities;
using Domain.Enums;

namespace PoliticalNews.Tests.Application;

public class NewsFeedQueryHandlerTests
{
    [Fact]
    public async Task GetNewsFeedQueryHandler_DefaultsToPublishedAtSort()
    {
        var repo = new Mock<INewsArticleRepository>();
        NewsArticleFeedFilter? captured = null;
        repo
            .Setup(r => r.GetFeedAsync(It.IsAny<NewsArticleFeedFilter>(), It.IsAny<CancellationToken>()))
            .Callback<NewsArticleFeedFilter, CancellationToken>((f, _) => captured = f)
            .ReturnsAsync([]);

        var handler = new GetNewsFeedQueryHandler(repo.Object);
        await handler.Handle(new GetNewsFeedQuery(ArticleSourceType.Rss, null, 0, 20), CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(NewsFeedSortBy.PublishedAt, captured!.SortBy);
    }

    [Fact]
    public async Task GetNewsFeedQueryHandler_PassesRequestedCrawledAtSortThrough()
    {
        var repo = new Mock<INewsArticleRepository>();
        NewsArticleFeedFilter? captured = null;
        repo
            .Setup(r => r.GetFeedAsync(It.IsAny<NewsArticleFeedFilter>(), It.IsAny<CancellationToken>()))
            .Callback<NewsArticleFeedFilter, CancellationToken>((f, _) => captured = f)
            .ReturnsAsync([new NewsArticle { Id = "1" }]);

        var handler = new GetNewsFeedQueryHandler(repo.Object);
        var result = await handler.Handle(new GetNewsFeedQuery(ArticleSourceType.Rss, null, 0, 20, NewsFeedSortBy.CrawledAt), CancellationToken.None);

        Assert.Single(result);
        Assert.NotNull(captured);
        Assert.Equal(NewsFeedSortBy.CrawledAt, captured!.SortBy);
    }

    [Fact]
    public async Task GetNewsFeedCountQueryHandler_PassesSourceTypeAndCountryThroughToRepository()
    {
        var repo = new Mock<INewsArticleRepository>();
        NewsArticleFeedFilter? captured = null;
        repo
            .Setup(r => r.CountFeedAsync(It.IsAny<NewsArticleFeedFilter>(), It.IsAny<CancellationToken>()))
            .Callback<NewsArticleFeedFilter, CancellationToken>((f, _) => captured = f)
            .ReturnsAsync(42);

        var handler = new GetNewsFeedCountQueryHandler(repo.Object);
        var result = await handler.Handle(new GetNewsFeedCountQuery(ArticleSourceType.Rss, "India"), CancellationToken.None);

        Assert.Equal(42, result);
        Assert.NotNull(captured);
        Assert.Equal(ArticleSourceType.Rss, captured!.SourceType);
        Assert.Equal("India", captured.Country);
    }

    [Fact]
    public async Task GetNewsFeedCountQueryHandler_NullFilters_PassesNullsThrough()
    {
        var repo = new Mock<INewsArticleRepository>();
        repo
            .Setup(r => r.CountFeedAsync(It.IsAny<NewsArticleFeedFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var handler = new GetNewsFeedCountQueryHandler(repo.Object);
        await handler.Handle(new GetNewsFeedCountQuery(null, null), CancellationToken.None);

        repo.Verify(
            r => r.CountFeedAsync(It.Is<NewsArticleFeedFilter>(f => f.SourceType == null && f.Country == null), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
