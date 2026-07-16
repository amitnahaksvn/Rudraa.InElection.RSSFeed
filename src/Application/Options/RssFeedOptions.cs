namespace Application.Options;

/// <summary>
/// A single RSS feed URL belonging to a provider, as declared in configuration.
/// </summary>
public sealed class RssFeedOptions
{
    /// <summary>Human readable feed name, e.g. "India", "Cricket", "Bollywood".</summary>
    public string Name { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    /// <summary>Category assigned to every article pulled from this feed.</summary>
    public string Category { get; set; } = string.Empty;

    public string Language { get; set; } = "hi";

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Fallback image URL used when an item carries no image of its own (no media/enclosure tag,
    /// and the og:image HTML fallback comes up empty too - e.g. PIB's feeds, which have neither).
    /// Purely a config knob: adding a fallback image for a future feed/provider that never ships
    /// its own images is a one-line JSON addition, no code change.
    /// </summary>
    public string? DefaultImageUrl { get; set; }
}
