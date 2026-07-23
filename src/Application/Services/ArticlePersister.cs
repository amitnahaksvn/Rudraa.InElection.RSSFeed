using Microsoft.Extensions.Logging;
using Application.Abstractions;
using Application.Models;
using Domain.Entities;

namespace Application.Services;

/// <summary>
/// Shared normalized-article persistence logic (hash computation + upsert + outcome logging) used
/// by every crawler orchestrator - <see cref="NewsCrawlerOrchestrator"/> (RSS) and
/// <see cref="NewsApiCrawlerOrchestrator"/> (JSON APIs) alike - so the dedup/upsert path only
/// exists once regardless of how an article was fetched. Also the one place
/// <see cref="IArticleNormalizer"/> gets applied (by <see cref="NormalizedArticle.Provider"/>,
/// at most one match per article) - being the single choke point both pipelines already share
/// means a provider's normalizer runs the same way regardless of which pipeline fetched it.
/// </summary>
internal static class ArticlePersister
{
    public static async Task<int> PersistAsync(
        INewsArticleRepository articleRepository,
        IEnumerable<NormalizedArticle> articles,
        IEnumerable<IArticleNormalizer> normalizers,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var normalizersByProvider = normalizers.ToDictionary(n => n.Provider, StringComparer.OrdinalIgnoreCase);

        var inserted = 0;

        foreach (var rawNormalized in articles)
        {
            var normalized = normalizersByProvider.TryGetValue(rawNormalized.Provider, out var normalizer)
                ? normalizer.Normalize(rawNormalized)
                : rawNormalized;

            var now = DateTimeOffset.UtcNow;

            // A source's own PublishedAt is occasionally ahead of real time - a publisher's CMS
            // clock running fast, or content pre-scheduled/staged before its nominal publish time
            // (confirmed live against AmarUjala's own feed: an item dated ~11.5 hours into the
            // future). A story can never be validly recorded as published after the moment this
            // crawl actually saw it, so that's the clamp - not an attempt to guess the "true"
            // publish time, which isn't recoverable from a wrong source timestamp. Hash below
            // deliberately uses the raw, unclamped normalized.PublishedAt - clamping against `now`
            // would make the hash drift on every crawl of the same still-future-dated story instead
            // of staying a stable dedup signature.
            var publishedAt = normalized.PublishedAt is { } claimedPublishedAt && claimedPublishedAt > now
                ? now
                : normalized.PublishedAt;

            var article = new NewsArticle
            {
                Provider = normalized.Provider,
                SourceType = normalized.SourceType,
                FeedName = normalized.FeedName,
                Category = normalized.Category,
                Title = normalized.Title,
                Summary = DescriptionNormalizer.Clean(normalized.Summary),
                Content = normalized.Content,
                Url = normalized.Url,
                OriginalGuid = normalized.OriginalGuid,
                Author = normalized.Author,
                Language = normalized.Language,
                Country = normalized.Country,
                ImageUrl = normalized.ImageUrl,
                PublishedAt = publishedAt,
                CrawledAt = now,
                UpdatedAt = now,
                Tags = normalized.Tags,
                Source = normalized.Source,
                Hash = ArticleHasher.ComputeHash(normalized.Title, normalized.PublishedAt),
                IsActive = true
            };

            var outcome = await articleRepository.UpsertAsync(article, cancellationToken);
            switch (outcome)
            {
                case ArticleUpsertOutcome.Inserted:
                    inserted++;
                    logger.LogDebug("New article inserted: {Title} ({Url})", article.Title, article.Url);
                    break;
                case ArticleUpsertOutcome.DuplicateSkipped:
                    logger.LogDebug("Duplicate skipped: {Title} ({Url})", article.Title, article.Url);
                    break;
            }
        }

        return inserted;
    }
}
