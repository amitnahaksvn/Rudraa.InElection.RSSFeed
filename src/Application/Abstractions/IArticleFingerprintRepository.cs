using Application.Models;
using Domain.Entities;
using Domain.Enums;

namespace Application.Abstractions;

/// <summary>
/// The lean duplicate-detection lookup <c>INewsArticleRepository.UpsertAsync</c> checks before
/// ever touching the full NewsArticles collection - see <see cref="ArticleFingerprint"/> for why
/// this exists as its own collection instead of querying NewsArticles directly.
/// </summary>
public interface IArticleFingerprintRepository
{
    Task<ArticleFingerprint?> FindByUrlAsync(string url, CancellationToken cancellationToken);

    Task<ArticleFingerprint?> FindByOriginalGuidAsync(string originalGuid, CancellationToken cancellationToken);

    Task<ArticleFingerprint?> FindByHashAsync(string hash, CancellationToken cancellationToken);

    /// <summary>
    /// The same Url -&gt; OriginalGuid -&gt; Hash duplicate-lookup order <c>NewsArticleRepository.UpsertAsync</c>
    /// uses internally, exposed here as a single shared call.
    /// </summary>
    Task<ArticleFingerprint?> FindDuplicateAsync(string url, string? originalGuid, string hash, CancellationToken cancellationToken);

    Task InsertAsync(ArticleFingerprint fingerprint, CancellationToken cancellationToken);

    /// <summary>
    /// Real per-(day, provider) article counts within a date range, grouped straight out of this
    /// lean collection's own CrawledAt - backs the crawl-report page's "New articles" figures.
    /// </summary>
    Task<IReadOnlyList<ArticleCrawlCount>> GetDailyProviderCountsAsync(
        ArticleSourceType sourceType, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);

    Task EnsureIndexesAsync(CancellationToken cancellationToken);
}
