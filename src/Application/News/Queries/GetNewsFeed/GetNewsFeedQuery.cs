using Mediator;
using Application.Abstractions;
using Application.Models;
using Application.News.Dtos;
using Domain.Enums;

namespace Application.News.Queries.GetNewsFeed;

/// <summary>Backs the News Feed page's infinite scroll - newest first by <paramref name="SortBy"/> (PublishedAt by default), optionally narrowed to one pipeline (RSS/API tab) and/or one country.</summary>
public sealed record GetNewsFeedQuery(
    ArticleSourceType? SourceType,
    string? Country,
    int Skip,
    int Count,
    NewsFeedSortBy SortBy = NewsFeedSortBy.PublishedAt) : IRequest<IReadOnlyList<NewsArticleDto>>;

public sealed class GetNewsFeedQueryHandler : IRequestHandler<GetNewsFeedQuery, IReadOnlyList<NewsArticleDto>>
{
    private readonly INewsArticleRepository _articles;

    public GetNewsFeedQueryHandler(INewsArticleRepository articles)
    {
        _articles = articles;
    }

    public async ValueTask<IReadOnlyList<NewsArticleDto>> Handle(GetNewsFeedQuery request, CancellationToken cancellationToken)
    {
        var filter = new NewsArticleFeedFilter(request.SourceType, request.Country, request.Skip, request.Count, request.SortBy);
        var articles = await _articles.GetFeedAsync(filter, cancellationToken);
        return articles.Select(NewsArticleDto.FromDomain).ToList();
    }
}
