using Mediator;
using Application.Abstractions;
using Application.Models;
using Domain.Enums;

namespace Application.News.Queries.GetNewsFeedCount;

/// <summary>Total articles matching the News Feed page's current pipeline (RSS/API) tab and optional country filter - backs its total-count header.</summary>
public sealed record GetNewsFeedCountQuery(ArticleSourceType? SourceType, string? Country) : IRequest<long>;

public sealed class GetNewsFeedCountQueryHandler : IRequestHandler<GetNewsFeedCountQuery, long>
{
    private readonly INewsArticleRepository _articles;

    public GetNewsFeedCountQueryHandler(INewsArticleRepository articles)
    {
        _articles = articles;
    }

    public async ValueTask<long> Handle(GetNewsFeedCountQuery request, CancellationToken cancellationToken) =>
        await _articles.CountFeedAsync(new NewsArticleFeedFilter(request.SourceType, request.Country), cancellationToken);
}
