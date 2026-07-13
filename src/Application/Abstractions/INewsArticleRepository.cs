using Application.Models;
using Domain.Entities;
using Domain.Enums;

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
    /// <summary>
    /// Inserts a brand new article, updates an existing one whose content changed, or reports
    /// a duplicate skip when the incoming article matches an existing one with no changes.
    /// Duplicate detection order: Url, then OriginalGuid, then Hash - resolved entirely against
    /// the lean <see cref="Domain.Entities.ArticleFingerprint"/> collection
    /// (<see cref="IArticleFingerprintRepository"/>), so a duplicate/no-change skip never loads
    /// the full article.
    /// </summary>
    Task<ArticleUpsertOutcome> UpsertAsync(NewsArticle article, CancellationToken cancellationToken);

    Task<IReadOnlyList<NewsArticle>> GetLatestAsync(int count, CancellationToken cancellationToken);

    Task<IReadOnlyList<NewsArticle>> GetByProviderAsync(string provider, int count, CancellationToken cancellationToken);

    Task<IReadOnlyList<NewsArticle>> GetByCategoryAsync(string category, int count, CancellationToken cancellationToken);

    Task<IReadOnlyList<NewsArticle>> SearchAsync(string query, int count, CancellationToken cancellationToken);

    /// <summary>Newest-first, optionally narrowed to one pipeline (RSS/API) and/or one country - backs the News Feed page's infinite scroll.</summary>
    Task<IReadOnlyList<NewsArticle>> GetFeedAsync(NewsArticleFeedFilter filter, CancellationToken cancellationToken);

    /// <summary>Total articles matching <paramref name="filter"/>'s pipeline/country narrowing (its Skip/Take are ignored) - backs the News Feed page's total-count header.</summary>
    Task<long> CountFeedAsync(NewsArticleFeedFilter filter, CancellationToken cancellationToken);

    /// <summary>Every distinct, non-empty country currently represented among active articles (optionally narrowed to one pipeline) - backs the News Feed page's country filter.</summary>
    Task<IReadOnlyList<string>> GetDistinctCountriesAsync(ArticleSourceType? sourceType, CancellationToken cancellationToken);

    /// <summary>Ensures the PublishedAt, Provider and Category indexes exist - Url/OriginalGuid/Hash uniqueness now lives on <see cref="IArticleFingerprintRepository"/> instead.</summary>
    Task EnsureIndexesAsync(CancellationToken cancellationToken);
}
