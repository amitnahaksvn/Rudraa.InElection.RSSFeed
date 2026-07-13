using Application.Abstractions;
using Application.Models;
using Infrastructure.RssProviders;

namespace Infrastructure.ArticleNormalizers;

/// <summary>
/// IndianExpress-specific cleanup, confirmed live against its own feed (indianexpress.com/feed/,
/// 200 items sampled): &lt;description&gt; and &lt;content:encoded&gt; are both always empty
/// CDATA blocks (0/200 non-empty either way) - this publisher never provides article body text
/// via RSS, not a parsing gap (there is nothing to extract). The one real bug: in
/// <see cref="BaseRssProvider"/>'s shared parsing, <c>Content = encodedContent ?? description</c>
/// only falls back to <c>description</c> when <c>encodedContent</c> is null - but an empty-but-present
/// &lt;content:encoded&gt; tag parses to <c>""</c> (empty, not null), so <c>??</c> never triggers and
/// <c>""</c> gets stored instead of <c>null</c> (and, for any other provider hitting this same shape,
/// instead of a real, non-empty description that should have been the fallback). Fixed here rather
/// than in the shared base, since only IndianExpress has actually been confirmed to hit it - the
/// underlying <c>??</c> gap in <c>BaseRssProvider</c> itself is still there for whichever provider
/// trips it next.
/// </summary>
public sealed class IndianExpressArticleNormalizer : IArticleNormalizer
{
    public string Provider => IndianExpressRssProvider.ProviderName;

    public NormalizedArticle Normalize(NormalizedArticle article) => article with
    {
        Summary = NullIfEmpty(article.Summary),
        Content = NullIfEmpty(article.Content),
    };

    private static string? NullIfEmpty(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text;
}
