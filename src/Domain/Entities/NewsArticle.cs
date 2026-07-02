namespace Domain.Entities;

/// <summary>
/// A single normalized news article persisted in the NewsArticles collection.
/// Shared across all providers (AajTak today; ANI/NDTV/PIB/etc. in later phases).
/// </summary>
public sealed class NewsArticle
{
    public string Id { get; set; } = string.Empty;

    /// <summary>Provider key, e.g. "AajTak". Drives future multi-provider filtering.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Name of the specific RSS feed the article was crawled from, e.g. "AajTak-India".</summary>
    public string FeedName { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Summary { get; set; }

    public string? Content { get; set; }

    public string Url { get; set; } = string.Empty;

    /// <summary>The RSS &lt;guid&gt; value as published by the source, used for duplicate detection.</summary>
    public string? OriginalGuid { get; set; }

    public string? Author { get; set; }

    public string Language { get; set; } = "hi";

    public string? ImageUrl { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public DateTimeOffset CrawledAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public List<string> Tags { get; set; } = [];

    /// <summary>Human readable origin, e.g. the feed URL, kept for traceability/debugging.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Stable hash of Title + PublishedAt used as a last-resort duplicate signal.</summary>
    public string Hash { get; set; } = string.Empty;

    /// <summary>Free-form extension bag for future enrichment (AI summary, sentiment, entities, etc.).</summary>
    public Dictionary<string, string> Metadata { get; set; } = [];

    public bool IsActive { get; set; } = true;
}
