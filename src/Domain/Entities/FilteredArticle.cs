using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// A lightweight record of one article that was fetched but not persisted as a real
/// <see cref="NewsArticle"/>, because its Category didn't match the political allowlist (see
/// <c>Application.Options.NewsFilterOptions</c>). Deliberately minimal - just enough for an admin
/// to recognize what got excluded and why (Provider/Title/Summary/Category/pipeline Type/when it
/// was pulled), not a second copy of the full article (no Content/Url/Tags/ImageUrl).
/// </summary>
public sealed class FilteredArticle
{
    public string Id { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Summary { get; set; }

    /// <summary>The article's actual Category - shown so an admin can see at a glance why it didn't match the allowlist.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Which pipeline pulled it - Rss or Api (Social-sourced articles are also Rss, same as <see cref="NewsArticle.SourceType"/>'s own convention).</summary>
    public ArticleSourceType SourceType { get; set; } = ArticleSourceType.Rss;

    public DateTimeOffset PulledAt { get; set; }
}
