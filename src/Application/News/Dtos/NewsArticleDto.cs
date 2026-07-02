using Domain.Entities;

namespace Application.News.Dtos;

/// <summary>Read projection of a <see cref="NewsArticle"/>, returned by every News query.</summary>
public sealed record NewsArticleDto(
    string Id,
    string Provider,
    string FeedName,
    string Category,
    string Title,
    string? Summary,
    string? Content,
    string Url,
    string? Author,
    string Language,
    string? ImageUrl,
    DateTimeOffset? PublishedAt,
    DateTimeOffset CrawledAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<string> Tags)
{
    public static NewsArticleDto FromDomain(NewsArticle article) => new(
        article.Id,
        article.Provider,
        article.FeedName,
        article.Category,
        article.Title,
        article.Summary,
        article.Content,
        article.Url,
        article.Author,
        article.Language,
        article.ImageUrl,
        article.PublishedAt,
        article.CrawledAt,
        article.UpdatedAt,
        article.Tags);
}
