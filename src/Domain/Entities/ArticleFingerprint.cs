namespace Domain.Entities;

/// <summary>
/// The lean duplicate-detection record for one <see cref="NewsArticle"/> - just enough (Url,
/// OriginalGuid, the two hashes, CrawledAt) to answer "have we seen this?" and "did it change?"
/// without loading the full article (Title/Summary/Content/ImageUrl/Tags/Metadata/...). Shares its
/// Id 1:1 with the NewsArticle it fingerprints. See
/// Infrastructure/Persistence/NewsArticleRepository.UpsertAsync for how this replaced querying
/// NewsArticles directly for every dedup check.
/// </summary>
public sealed class ArticleFingerprint
{
    public string Id { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    /// <summary>The RSS &lt;guid&gt; value as published by the source, mirrored from NewsArticle.OriginalGuid.</summary>
    public string? OriginalGuid { get; set; }

    /// <summary>Hash of Title + PublishedAt - see <see cref="Application.Services.ArticleHasher.ComputeHash"/>.</summary>
    public string Hash { get; set; } = string.Empty;

    /// <summary>Hash of Title + Summary + Content + ImageUrl - lets an in-place content change be detected (or ruled out) without loading the full article. See <see cref="Application.Services.ArticleHasher.ComputeContentHash"/>.</summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>The article's original CrawledAt, preserved across updates the same way NewsArticle.CrawledAt is.</summary>
    public DateTimeOffset CrawledAt { get; set; }
}
