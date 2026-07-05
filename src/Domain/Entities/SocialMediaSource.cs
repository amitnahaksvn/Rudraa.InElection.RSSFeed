using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// One social-media channel/page/account to poll, stored in the <c>SocialMediaSources</c>
/// collection - the "Social" pipeline's counterpart to <see cref="FeedSource"/> (Mongo-driven, no
/// code/config change needed to add a new one) except spanning multiple platforms instead of just
/// RSS 2.0. Only <see cref="Platform"/> = <see cref="SocialPlatform.YouTube"/> has a working
/// <c>ISocialPlatformFetcher</c> today; the other platform values are recognized but unimplemented
/// - a document for one of them is simply never polled (no matching fetcher, logged and skipped),
/// the same "not wired up yet" behavior every other unimplemented option in this codebase has.
/// </summary>
public sealed class SocialMediaSource
{
    public string Id { get; set; } = string.Empty;

    public SocialPlatform Platform { get; set; }

    /// <summary>What kind of entity this channel belongs to - lets the source list be scanned by "every politician" vs "every party" at a glance.</summary>
    public SourceEntityType SourceType { get; set; }

    public string Country { get; set; } = string.Empty;

    /// <summary>State/province, when the source is a state-level (not national) entity. Null for national-level sources.</summary>
    public string? State { get; set; }

    /// <summary>Display name, e.g. "Narendra Modi", "BJP" - also becomes <c>NewsArticle.FeedName</c> for articles pulled from this source.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Platform-specific identifier the fetcher actually polls with - a YouTube channel id (<c>UC...</c>) today; would be an RSS URL, a Telegram channel username, a Facebook page id, etc. once those platforms get a fetcher.</summary>
    public string Identifier { get; set; } = string.Empty;

    /// <summary>Public @handle, where the platform has one - display/reference only, never used to build the actual fetch request (see <see cref="Identifier"/> for that).</summary>
    public string? Handle { get; set; }

    /// <summary>Public profile/channel URL - display/reference only, same reasoning as <see cref="Handle"/>.</summary>
    public string? Url { get; set; }

    public bool Enabled { get; set; } = true;

    public int Priority { get; set; } = 1;

    /// <summary>How often this source is polled - converted to a Hangfire cron expression once, at recurring-job registration time (see <c>HangfireRecurringJobRegistrar</c>), the same way <see cref="FeedSource.FetchIntervalMinutes"/> already works.</summary>
    public int PollIntervalMinutes { get; set; } = 30;

    public int TimeoutSeconds { get; set; } = 60;

    public string Language { get; set; } = "en";

    /// <summary>Stamped onto every <c>NewsArticle</c> pulled from this source when its own content has no better category - "Video" for YouTube, matching the convention the existing file-configured <c>YouTubeRssProvider</c> feeds already use.</summary>
    public string Category { get; set; } = "Video";

    public DateTimeOffset? LastPolledAt { get; set; }

    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedOn { get; set; } = DateTimeOffset.UtcNow;
}
