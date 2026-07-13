using Application.Abstractions;
using Application.Models;
using Infrastructure.RssProviders;

namespace Infrastructure.ArticleNormalizers;

/// <summary>
/// FreePressJournal-specific cleanup, confirmed live against its own feed
/// (prod-qt-images.s3.amazonaws.com/production/freepressjournal/feed.xml): every single entry
/// (20/20 sampled) emits a genuinely empty &lt;summary&gt;&lt;/summary&gt; and there is no
/// &lt;content&gt; element at all - this publisher never provides article body text via RSS,
/// not a parsing gap (there is nothing to extract, unlike TheHindu which has real text roughly
/// 60% of the time). The one real bug: <see cref="BaseAtomRssProvider"/>'s Atom parsing assigns
/// &lt;summary&gt;'s value straight to <see cref="NormalizedArticle.Summary"/>/<see cref="NormalizedArticle.Content"/>
/// with no empty-string-to-null check (unlike the RSS 2.0 pipeline's <c>StripHtml</c>, which already
/// has one) - so an empty tag was being stored as <c>""</c> instead of <c>null</c>. Fixed here rather
/// than in the shared base, since only FreePressJournal has actually been confirmed to hit it.
/// </summary>
public sealed class FreePressJournalArticleNormalizer : IArticleNormalizer
{
    public string Provider => FreePressJournalRssProvider.ProviderName;

    public NormalizedArticle Normalize(NormalizedArticle article) => article with
    {
        Summary = NullIfEmpty(article.Summary),
        Content = NullIfEmpty(article.Content),
    };

    private static string? NullIfEmpty(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text;
}
