namespace Domain.Entities;

/// <summary>
/// The complete RSS response exactly as received for a single feed fetch, kept for audit/replay.
/// Written once per fetch and never modified afterward - not even <see cref="RawXml"/> when a
/// parse fails, so the original bytes are always available to diagnose why.
/// </summary>
public sealed class RssRawResponse
{
    public string Id { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;

    public string FeedName { get; set; } = string.Empty;

    public string FeedUrl { get; set; } = string.Empty;

    public DateTimeOffset FetchedAt { get; set; }

    /// <summary>Null when the request never completed (e.g. DNS/connection failure).</summary>
    public int? HttpStatusCode { get; set; }

    /// <summary>Null when no response body was ever received.</summary>
    public string? RawXml { get; set; }

    /// <summary>SHA-256 of <see cref="RawXml"/>, used to detect an unchanged feed without re-parsing it.</summary>
    public string? ContentHash { get; set; }

    public bool ParseSucceeded { get; set; }

    public string? ParseError { get; set; }

    public long ProcessingDurationMs { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
