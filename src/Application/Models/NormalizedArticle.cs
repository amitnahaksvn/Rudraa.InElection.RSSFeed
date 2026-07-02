namespace Application.Models;

/// <summary>
/// Common shape every <see cref="Abstractions.IRssProvider"/> must normalize its feed items into,
/// before the crawler orchestrator persists them. Providers never touch MongoDB directly.
/// </summary>
public sealed class NormalizedArticle
{
    public required string Provider { get; init; }

    public required string FeedName { get; init; }

    public required string Category { get; init; }

    public required string Title { get; init; }

    public string? Summary { get; init; }

    public string? Content { get; init; }

    public required string Url { get; init; }

    public string? OriginalGuid { get; init; }

    public string? Author { get; init; }

    public string Language { get; init; } = "hi";

    public string? ImageUrl { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    public List<string> Tags { get; init; } = [];

    public required string Source { get; init; }
}
