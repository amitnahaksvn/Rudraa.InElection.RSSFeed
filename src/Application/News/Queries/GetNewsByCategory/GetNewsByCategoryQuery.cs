using Mediator;
using Application.Abstractions;
using Application.News.Dtos;

namespace Application.News.Queries.GetNewsByCategory;

public sealed record GetNewsByCategoryQuery(string Category, int Count) : IRequest<IReadOnlyList<NewsArticleDto>>;

public sealed class GetNewsByCategoryQueryHandler : IRequestHandler<GetNewsByCategoryQuery, IReadOnlyList<NewsArticleDto>>
{
    private readonly INewsArticleRepository _articles;

    public GetNewsByCategoryQueryHandler(INewsArticleRepository articles)
    {
        _articles = articles;
    }

    public async ValueTask<IReadOnlyList<NewsArticleDto>> Handle(GetNewsByCategoryQuery request, CancellationToken cancellationToken)
    {
        var articles = await _articles.GetByCategoryAsync(request.Category, request.Count, cancellationToken);
        return articles.Select(NewsArticleDto.FromDomain).ToList();
    }
}
