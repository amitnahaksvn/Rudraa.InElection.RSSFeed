namespace Application.Abstractions;

/// <summary>
/// Downloads, deduplicates, and persists new posts for one <see cref="Domain.Entities.SocialMediaSource"/> -
/// the Social pipeline's counterpart to <see cref="IDynamicFeedIngestionService"/>, invoked per
/// source rather than per platform since a <see cref="Domain.Entities.SocialMediaSource"/>
/// document is itself both (one Hangfire recurring job per source, see
/// <c>HangfireRecurringJobRegistrar</c>).
/// </summary>
public interface ISocialMediaIngestionService
{
    Task RunAsync(string socialMediaSourceId, CancellationToken cancellationToken);
}
