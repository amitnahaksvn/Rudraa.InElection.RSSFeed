using Mediator;
using Application.Abstractions;
using Application.News.Dtos;

namespace Application.News.Queries.GetNewsByProvider;

public sealed record GetNewsByProviderQuery(string Provider, int Count) : IRequest<IReadOnlyList<NewsArticleDto>>;

public sealed class GetNewsByProviderQueryHandler : IRequestHandler<GetNewsByProviderQuery, IReadOnlyList<NewsArticleDto>>
{
    private readonly INewsArticleRepository _articles;

    public GetNewsByProviderQueryHandler(INewsArticleRepository articles)
    {
        _articles = articles;
    }

    public async ValueTask<IReadOnlyList<NewsArticleDto>> Handle(GetNewsByProviderQuery request, CancellationToken cancellationToken)
    {
        var articles = await _articles.GetByProviderAsync(request.Provider, request.Count, cancellationToken);
        return articles.Select(NewsArticleDto.FromDomain).ToList();
    }
}
