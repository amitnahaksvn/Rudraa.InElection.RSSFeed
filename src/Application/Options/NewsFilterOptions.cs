namespace Application.Options;

/// <summary>
/// Root configuration section ("NewsFilter") restricting persisted articles to a curated set of
/// political categories, with no AI/ML involved - a plain allowlist match against
/// <see cref="Models.NormalizedArticle.Category"/> (the same free-form string every RSS
/// feed/API endpoint is already tagged with in configuration). Applied once, centrally, in
/// <see cref="Services.ArticlePersister"/> - every pipeline that already funnels through it
/// (RSS, JSON-API, Social) is covered automatically. An article whose Category doesn't match is
/// never discarded outright: it's recorded in <c>FilteredArticles</c> instead (see
/// <see cref="Abstractions.IFilteredArticleRepository"/>) so what's being excluded stays visible
/// and reversible - turning <see cref="Enabled"/> off (or widening <see cref="Categories"/>)
/// takes effect on the very next crawl, no backfill needed since already-filtered rows were never
/// deleted, only diverted.
/// </summary>
public sealed class NewsFilterOptions
{
    public const string SectionName = "NewsFilter";

    /// <summary>Master switch - false means every article is persisted as before, regardless of <see cref="Categories"/>.</summary>
    public bool Enabled { get; set; }

    /// <summary>Category allowlist, matched case-insensitively against <see cref="Models.NormalizedArticle.Category"/>.</summary>
    public List<string> Categories { get; set; } = [];
}
