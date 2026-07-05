namespace Domain.Enums;

/// <summary>Which platform a <see cref="Entities.SocialMediaSource"/> is polled from. Only <see cref="YouTube"/> has a working <c>ISocialPlatformFetcher</c> today - the rest are recognized values with no implementation yet.</summary>
public enum SocialPlatform
{
    YouTube,
    Rss,
    Website,
    Facebook,
    Telegram
}
