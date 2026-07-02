using Mediator;
using Application.Abstractions;
using Application.News.Dtos;

namespace Application.News.Queries.GetLatestNews;

public sealed record GetLatestNewsQuery(int Count) : IRequest<IReadOnlyList<NewsArticleDto>>;

public sealed class GetLatestNewsQueryHandler : IRequestHandler<GetLatestNewsQuery, IReadOnlyList<NewsArticleDto>>
{
    private readonly INewsArticleRepository _articles;

    public GetLatestNewsQueryHandler(INewsArticleRepository articles)
    {
        _articles = articles;
    }

    public async ValueTask<IReadOnlyList<NewsArticleDto>> Handle(GetLatestNewsQuery request, CancellationToken cancellationToken)
    {
        var articles = await _articles.GetLatestAsync(request.Count, cancellationToken);
        return articles.Select(NewsArticleDto.FromDomain).ToList();
    }
}
