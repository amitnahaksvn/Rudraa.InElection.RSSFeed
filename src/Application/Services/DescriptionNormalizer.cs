using System.Net;
using System.Text.RegularExpressions;

namespace Application.Services;

/// <summary>
/// Turns a raw, possibly-HTML Description/Summary field from any RSS/API provider into clean,
/// readable plain text before persistence - strips markup, decodes entities, drops caption/
/// boilerplate lines, and caps length at whole-sentence boundaries around 200-250 words.
/// Deliberately no AI/NLP: every step is a plain, deterministic string transformation (regex +
/// built-in HTML-entity decoding), so the same input always produces the same output and nothing
/// here paraphrases, rewrites, or reorders the source text - it only removes noise and shortens
/// by dropping trailing sentences.
/// </summary>
public static class DescriptionNormalizer
{
    private const int MaxWords = 250;

    private static readonly Regex HtmlCommentRegex = new(
        "<!--.*?-->", RegexOptions.Singleline | RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    private static readonly Regex ScriptOrStyleRegex = new(
        @"<(script|style)\b[^>]*>.*?</\1\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    /// <summary>Whole caption elements including their text - a caption is noise, not article content, so it's dropped entirely rather than just having its tags stripped.</summary>
    private static readonly Regex FigCaptionRegex = new(
        @"<figcaption\b[^>]*>.*?</figcaption\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    /// <summary>Common WordPress-style "wp-caption-text"/"caption" wrapper elements many RSS feeds embed around an image credit line.</summary>
    private static readonly Regex CaptionClassRegex = new(
        @"<(?<tag>\w+)\b[^>]*\bclass\s*=\s*[""'][^""']*\bcaption\b[^""']*[""'][^>]*>.*?</\k<tag>\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    /// <summary>Block-level tags become a line break (not deleted outright) so words don't glue together once the tag itself is stripped, e.g. "Hello&lt;br&gt;World" -&gt; "Hello World", not "HelloWorld".</summary>
    private static readonly Regex BlockBreakRegex = new(
        @"</?(br|p|div|li|ul|ol|tr|table|h[1-6]|blockquote|figure)\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    private static readonly Regex HtmlTagRegex = new(
        "<[^>]+>", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    /// <summary>
    /// Curated, best-effort boilerplate/caption-line patterns commonly left over in RSS
    /// descriptions once tags are stripped (image credits, "Also Read"/"Read More" cross-links,
    /// social-follow prompts, standalone "Advertisement" markers). Matched per block-level line,
    /// not per sentence, so it only ever drops a line that is *entirely* one of these patterns -
    /// not exhaustive, extend as new junk patterns turn up in real feeds.
    /// </summary>
    private static readonly Regex JunkLineRegex = new(
        @"^\s*(\(?(photo|image|picture|pic|file photo|representative image|image credit|photo credit|courtesy|source)\s*[:\-–].*|also read\s*[:\-–]?.*|read (also|more)\b.*|click here.*|follow us on.*|for more (updates|news)\b.*|subscribe (to|now)\b.*|advertisement\s*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    private static readonly Regex WhitespaceRegex = new(
        @"\s+", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    /// <summary>
    /// Splits after a sentence-ending ./!/? followed by whitespace and a capital letter/digit/
    /// quote/paren, but not right after a common abbreviation (Mr./Dr./U.S./etc.) - a plain
    /// heuristic, not a real NLP sentence tokenizer, since no AI/ML is used here by design.
    /// </summary>
    private static readonly Regex SentenceBoundaryRegex = new(
        @"(?<!\b(?:Mr|Mrs|Ms|Dr|Prof|Sr|Jr|St|vs|etc|Inc|Ltd|Co|No|U\.S|U\.K)\.)(?<=[.!?])\s+(?=[A-Z0-9""'(])",
        RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    /// <summary>
    /// Cleans a raw Description/Summary into plain text and caps it at ~200-250 words, never
    /// cutting a sentence in the middle (a single oversized sentence is truncated to 250 words and
    /// suffixed with "..." instead). Null/empty/whitespace-only input returns <see cref="string.Empty"/>.
    /// </summary>
    public static string Clean(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        string text;
        try
        {
            text = StripMarkup(input);
        }
        catch (RegexMatchTimeoutException)
        {
            // Pathological/adversarial markup that could cause runaway regex backtracking - fall
            // back to a simple, always-safe character strip rather than let one bad article break
            // batch processing of thousands of others.
            text = SafeFallbackStrip(input);
        }

        return text.Length == 0 ? string.Empty : LimitLength(text);
    }

    private static string StripMarkup(string input)
    {
        var text = input.Replace("\r\n", "\n").Replace('\r', '\n');

        text = HtmlCommentRegex.Replace(text, " ");
        text = ScriptOrStyleRegex.Replace(text, " ");
        text = FigCaptionRegex.Replace(text, " ");
        text = CaptionClassRegex.Replace(text, " ");
        text = BlockBreakRegex.Replace(text, "\n");
        text = HtmlTagRegex.Replace(text, string.Empty);

        text = WebUtility.HtmlDecode(text);

        var keptLines = new List<string>();
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length > 0 && !JunkLineRegex.IsMatch(line))
            {
                keptLines.Add(line);
            }
        }

        return WhitespaceRegex.Replace(string.Join(" ", keptLines), " ").Trim();
    }

    private static string SafeFallbackStrip(string input)
    {
        var withoutTags = new string(input.Where(c => c != '<' && c != '>').ToArray());
        var decoded = WebUtility.HtmlDecode(withoutTags);
        return decoded.Replace('\n', ' ').Replace('\r', ' ').Trim();
    }

    private static string LimitLength(string text)
    {
        if (SplitWords(text).Length <= MaxWords)
        {
            return text;
        }

        var sentences = SentenceBoundaryRegex.Split(text)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

        var kept = new List<string>();
        var wordCount = 0;

        foreach (var sentence in sentences)
        {
            var sentenceWords = SplitWords(sentence);

            if (kept.Count == 0 && sentenceWords.Length > MaxWords)
            {
                // The very first sentence alone is already over the limit - truncate just this
                // one sentence rather than ever spilling words into a second sentence.
                return string.Join(" ", sentenceWords.Take(MaxWords)) + "...";
            }

            if (wordCount + sentenceWords.Length > MaxWords)
            {
                break;
            }

            kept.Add(sentence);
            wordCount += sentenceWords.Length;
        }

        // kept is never empty here: the first sentence is always <= MaxWords (the oversized-
        // first-sentence case returns early above), so it always fits on the first iteration.
        return string.Join(" ", kept);
    }

    private static string[] SplitWords(string text) =>
        WhitespaceRegex.Split(text).Where(w => w.Length > 0).ToArray();
}
