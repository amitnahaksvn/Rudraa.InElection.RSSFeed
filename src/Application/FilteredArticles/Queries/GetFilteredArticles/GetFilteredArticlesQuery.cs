using Mediator;
using Application.Abstractions;
using Application.ErrorLogs.Dtos;
using Application.FilteredArticles.Dtos;

namespace Application.FilteredArticles.Queries.GetFilteredArticles;

/// <summary>Newest-first page of <see cref="Domain.Entities.FilteredArticle"/> rows for the admin page. <paramref name="Page"/> is 1-based.</summary>
public sealed record GetFilteredArticlesQuery(int Page, int PageSize) : IRequest<PagedResult<FilteredArticleDto>>;

public sealed class GetFilteredArticlesQueryHandler : IRequestHandler<GetFilteredArticlesQuery, PagedResult<FilteredArticleDto>>
{
    private readonly IFilteredArticleRepository _filteredArticles;

    public GetFilteredArticlesQueryHandler(IFilteredArticleRepository filteredArticles)
    {
        _filteredArticles = filteredArticles;
    }

    public async ValueTask<PagedResult<FilteredArticleDto>> Handle(GetFilteredArticlesQuery request, CancellationToken cancellationToken)
    {
        var skip = (request.Page - 1) * request.PageSize;

        var articles = await _filteredArticles.GetPagedAsync(skip, request.PageSize, cancellationToken);
        var totalCount = await _filteredArticles.CountAsync(cancellationToken);

        return new PagedResult<FilteredArticleDto>(
            articles.Select(FilteredArticleDto.FromDomain).ToList(),
            totalCount,
            request.Page,
            request.PageSize);
    }
}
