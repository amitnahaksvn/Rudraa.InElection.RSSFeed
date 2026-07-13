using System.Text.RegularExpressions;
using Application.Abstractions;
using Application.Models;
using Infrastructure.RssProviders;

namespace Infrastructure.ArticleNormalizers;

/// <summary>
/// TheHindu-specific cleanup, confirmed live against its own feed (thehindu.com/news/national/feeder/default.rss):
/// roughly 40% of items have a genuinely empty &lt;description&gt; at the source (nothing to
/// recover - Summary correctly stays null for those) but the rest carry real text with embedded
/// U+00A0 non-breaking-space characters mid-word (from an unescaped &amp;nbsp; in the source HTML,
/// e.g. "the&amp;nbsp;e-Sevai&amp;nbsp;(e-Service) centre") and occasional runs of newlines/extra
/// whitespace from the source's own formatting. Neither is a parsing bug - BaseRssProvider.StripHtml
/// already strips real HTML tags and CDATA is handled transparently by XElement.Value - this is
/// purely mid-text character/whitespace cleanup specific to what this one publisher's feed emits.
/// </summary>
public sealed partial class TheHinduArticleNormalizer : IArticleNormalizer
{
    public string Provider => TheHinduRssProvider.ProviderName;

    public NormalizedArticle Normalize(NormalizedArticle article) => article with
    {
        Summary = Clean(article.Summary),
        Content = Clean(article.Content),
    };

    private static string? Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var withoutNbsp = text.Replace('\u00A0', ' ');
        var collapsed = WhitespaceRegex().Replace(withoutNbsp, " ").Trim();
        return collapsed.Length == 0 ? null : collapsed;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
