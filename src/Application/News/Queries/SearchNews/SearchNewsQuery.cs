using Mediator;
using Application.Abstractions;
using Application.News.Dtos;

namespace Application.News.Queries.SearchNews;

public sealed record SearchNewsQuery(string Query, int Count) : IRequest<IReadOnlyList<NewsArticleDto>>;

public sealed class SearchNewsQueryHandler : IRequestHandler<SearchNewsQuery, IReadOnlyList<NewsArticleDto>>
{
    private readonly INewsArticleRepository _articles;

    public SearchNewsQueryHandler(INewsArticleRepository articles)
    {
        _articles = articles;
    }

    public async ValueTask<IReadOnlyList<NewsArticleDto>> Handle(SearchNewsQuery request, CancellationToken cancellationToken)
    {
        var articles = await _articles.SearchAsync(request.Query, request.Count, cancellationToken);
        return articles.Select(NewsArticleDto.FromDomain).ToList();
    }
}
