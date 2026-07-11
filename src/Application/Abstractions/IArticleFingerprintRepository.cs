using Domain.Entities;

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

    Task InsertAsync(ArticleFingerprint fingerprint, CancellationToken cancellationToken);

    Task ReplaceAsync(ArticleFingerprint fingerprint, CancellationToken cancellationToken);

    Task EnsureIndexesAsync(CancellationToken cancellationToken);
}
