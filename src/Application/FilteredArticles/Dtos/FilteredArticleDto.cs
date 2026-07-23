using Domain.Entities;
using Domain.Enums;

namespace Application.FilteredArticles.Dtos;

/// <summary>Read-model projection of a <see cref="FilteredArticle"/> for the admin page.</summary>
public sealed record FilteredArticleDto(
    string Id,
    string Provider,
    string Title,
    string? Summary,
    string Category,
    ArticleSourceType SourceType,
    DateTimeOffset PulledAt)
{
    public static FilteredArticleDto FromDomain(FilteredArticle article) => new(
        article.Id,
        article.Provider,
        article.Title,
        article.Summary,
        article.Category,
        article.SourceType,
        article.PulledAt);
}
