using Domain.Entities;
using Domain.Enums;

namespace Application.Abstractions;

/// <summary>Persistence for <see cref="SocialMediaSource"/> - the "Social" pipeline's counterpart to <see cref="IFeedSourceRepository"/>.</summary>
public interface ISocialMediaSourceRepository
{
    Task<IReadOnlyList<SocialMediaSource>> GetEnabledAsync(CancellationToken cancellationToken);

    Task<SocialMediaSource?> GetByIdAsync(string id, CancellationToken cancellationToken);

    /// <summary>Used by the seeder to stay idempotent - a channel is uniquely identified by (Platform, Identifier), not by Name (display names aren't guaranteed unique).</summary>
    Task<SocialMediaSource?> GetByPlatformAndIdentifierAsync(SocialPlatform platform, string identifier, CancellationToken cancellationToken);

    /// <returns>The generated Id.</returns>
    Task<string> InsertAsync(SocialMediaSource source, CancellationToken cancellationToken);

    Task UpdateLastPolledAtAsync(string id, DateTimeOffset lastPolledAt, CancellationToken cancellationToken);

    Task EnsureIndexesAsync(CancellationToken cancellationToken);
}
