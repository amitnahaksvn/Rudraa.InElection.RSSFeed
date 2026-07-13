using Domain.Enums;

namespace Application.Models;

/// <summary>Which timestamp the News Feed page's infinite scroll is ordered by - see <see cref="NewsArticleFeedFilter.SortBy"/>.</summary>
public enum NewsFeedSortBy
{
    /// <summary>When the source published the article - falls back to <see cref="CrawledAt"/> for the minority of articles/feeds with no publish date at all.</summary>
    PublishedAt,

    /// <summary>When this app fetched the article - always populated, so no fallback needed.</summary>
    CrawledAt
}

/// <summary>Query shape for <see cref="Abstractions.INewsArticleRepository.GetFeedAsync"/> - backs the News Feed page's infinite scroll: narrow by pipeline (RSS/API) and/or country, page via Skip/Take, newest first by <see cref="SortBy"/>.</summary>
public sealed record NewsArticleFeedFilter(
    ArticleSourceType? SourceType = null,
    string? Country = null,
    int Skip = 0,
    int Take = 20,
    NewsFeedSortBy SortBy = NewsFeedSortBy.PublishedAt);
