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
}
