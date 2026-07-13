using Application.Models;

namespace Application.Abstractions;

/// <summary>
/// Optional per-provider content cleanup applied to a freshly parsed <see cref="NormalizedArticle"/>
/// right before it's persisted - for provider-specific text quirks (stray whitespace, HTML entity
/// artifacts, etc.) confirmed against that provider's real feed/response, that don't belong in the
/// generic RSS/API parsing pipeline shared by every other provider. Applied inside
/// <see cref="Services.ArticlePersister"/>, the one shared persistence path both the RSS and
/// JSON-API pipelines already go through, so a single implementation covers a provider regardless
/// of which pipeline it's fetched by. Most providers need none of this - only register one once a
/// concrete cleanup need is confirmed live against that provider's actual output, not speculatively.
/// </summary>
public interface IArticleNormalizer
{
    /// <summary>The provider name this applies to, e.g. "TheHindu" - matched against <see cref="NormalizedArticle.Provider"/>, case-insensitive.</summary>
    string Provider { get; }

    NormalizedArticle Normalize(NormalizedArticle article);
}
