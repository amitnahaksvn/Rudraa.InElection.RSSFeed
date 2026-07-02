using Domain.Entities;

namespace Application.Abstractions;

/// <summary>Upsert result reported back to the crawler orchestrator for logging/metrics.</summary>
public enum ArticleUpsertOutcome
{
    Inserted,
    Updated,
    DuplicateSkipped
}

public interface INewsArticleRepository
{
    Task<NewsArticle?> FindByUrlAsync(string url, CancellationToken cancellationToken);

    Task<NewsArticle?> FindByOriginalGuidAsync(string originalGuid, CancellationToken cancellationToken);

    Task<NewsArticle?> FindByHashAsync(string hash, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts a brand new article, updates an existing one whose content changed, or reports
    /// a duplicate skip when the incoming article matches an existing one with no changes.
    /// Duplicate detection order: Url, then OriginalGuid, then Hash.
    /// </summary>
    Task<ArticleUpsertOutcome> UpsertAsync(NewsArticle article, CancellationToken cancellationToken);

    Task<IReadOnlyList<NewsArticle>> GetLatestAsync(int count, CancellationToken cancellationToken);

    Task<IReadOnlyList<NewsArticle>> GetByProviderAsync(string provider, int count, CancellationToken cancellationToken);

    Task<IReadOnlyList<NewsArticle>> GetByCategoryAsync(string category, int count, CancellationToken cancellationToken);

    Task<IReadOnlyList<NewsArticle>> SearchAsync(string query, int count, CancellationToken cancellationToken);

    /// <summary>Ensures the Url (unique), OriginalGuid, Hash, PublishedAt, Provider and Category indexes exist.</summary>
    Task EnsureIndexesAsync(CancellationToken cancellationToken);
}
