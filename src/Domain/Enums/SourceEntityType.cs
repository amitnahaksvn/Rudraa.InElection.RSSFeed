namespace Domain.Enums;

/// <summary>What kind of real-world entity a <see cref="Entities.SocialMediaSource"/> represents - lets the source list (and, later, articles derived from it) be scanned/filtered by "every politician" vs "every party" vs official government/news accounts, independent of which platform it's polled from.</summary>
public enum SourceEntityType
{
    Politician,
    Party,
    Government,
    News
}
