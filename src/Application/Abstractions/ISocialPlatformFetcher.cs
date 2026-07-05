using Application.Models;
using Domain.Entities;
using Domain.Enums;

namespace Application.Abstractions;

/// <summary>
/// Fetches every new post/video from one <see cref="SocialMediaSource"/> - one implementation per
/// <see cref="SocialPlatform"/>, matched by <see cref="Platform"/> at ingestion time (see
/// <see cref="ISocialMediaIngestionService"/>). A source whose <c>Platform</c> has no matching
/// fetcher registered is simply skipped and logged, the same "recognized but not implemented"
/// behavior every other unwired option in this codebase already has - not an error.
/// </summary>
public interface ISocialPlatformFetcher
{
    SocialPlatform Platform { get; }

    Task<IReadOnlyList<NormalizedArticle>> FetchAsync(SocialMediaSource source, CancellationToken cancellationToken);
}
