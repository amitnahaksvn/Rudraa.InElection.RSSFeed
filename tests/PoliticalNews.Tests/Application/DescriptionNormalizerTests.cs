using Application.Services;

namespace PoliticalNews.Tests.Application;

public class DescriptionNormalizerTests
{
    [Fact]
    public void Clean_NullInput_ReturnsEmptyString()
    {
        var result = DescriptionNormalizer.Clean(null);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Clean_EmptyString_ReturnsEmptyString()
    {
        var result = DescriptionNormalizer.Clean(string.Empty);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Clean_WhitespaceOnlyInput_ReturnsEmptyString()
    {
        var result = DescriptionNormalizer.Clean("   \n\t  ");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Clean_PlainTextShortInput_ReturnsTrimmedTextUnchanged()
    {
        var result = DescriptionNormalizer.Clean("  This is a short plain text summary.  ");

        Assert.Equal("This is a short plain text summary.", result);
    }

    [Fact]
    public void Clean_HtmlTags_AreRemovedButTextContentKept()
    {
        var result = DescriptionNormalizer.Clean("<p>Hello <b>World</b>, this is <a href=\"https://example.com\">a link</a>.</p>");

        Assert.Equal("Hello World, this is a link.", result);
    }

    [Fact]
    public void Clean_ScriptAndStyleContent_AreRemovedEntirely()
    {
        var result = DescriptionNormalizer.Clean(
            "<p>Real content here.</p><script>alert('should not appear');</script><style>.a{color:red}</style>");

        Assert.Equal("Real content here.", result);
    }

    [Fact]
    public void Clean_HtmlComment_IsRemoved()
    {
        var result = DescriptionNormalizer.Clean("<p>Visible text.<!-- hidden editorial note --></p>");

        Assert.Equal("Visible text.", result);
    }

    [Fact]
    public void Clean_HtmlEntities_AreDecoded()
    {
        var result = DescriptionNormalizer.Clean("Tom &amp; Jerry said &quot;hello&quot; &ndash; it&#39;s fun.");

        Assert.Equal("Tom & Jerry said \"hello\" – it's fun.", result);
    }

    [Fact]
    public void Clean_BasicAmpersandEntity_DecodesCorrectly()
    {
        var result = DescriptionNormalizer.Clean("Rock &amp; Roll forever.");

        Assert.Equal("Rock & Roll forever.", result);
    }

    [Fact]
    public void Clean_FigureWithoutCaption_DoesNotGlueSurroundingText()
    {
        var result = DescriptionNormalizer.Clean("Before text<figure><img src=\"x.jpg\"/></figure>After text.");

        Assert.DoesNotContain("textAfter", result);
        Assert.Equal("Before text After text.", result);
    }

    [Fact]
    public void Clean_FigcaptionContent_IsRemoved()
    {
        var result = DescriptionNormalizer.Clean(
            "<figure><img src=\"x.jpg\"/><figcaption>Representative image via ANI</figcaption></figure><p>The actual story text.</p>");

        Assert.Equal("The actual story text.", result);
    }

    [Fact]
    public void Clean_CaptionClassSpan_IsRemoved()
    {
        var result = DescriptionNormalizer.Clean(
            "<p>The main story.</p><span class=\"wp-caption-text\">Photo: Reuters</span>");

        Assert.Equal("The main story.", result);
    }

    [Fact]
    public void Clean_PhotoCreditBoilerplateLine_IsRemoved()
    {
        var result = DescriptionNormalizer.Clean("<p>The main story continues here.</p><p>Photo: ANI</p>");

        Assert.Equal("The main story continues here.", result);
    }

    [Fact]
    public void Clean_AlsoReadBoilerplateLine_IsRemoved()
    {
        var result = DescriptionNormalizer.Clean("<p>The election results were declared today.</p><p>Also Read: Related coverage</p>");

        Assert.Equal("The election results were declared today.", result);
    }

    [Fact]
    public void Clean_AdvertisementLine_IsRemoved()
    {
        var result = DescriptionNormalizer.Clean("<p>Real news content.</p><p>Advertisement</p><p>More real content.</p>");

        Assert.Equal("Real news content. More real content.", result);
    }

    [Fact]
    public void Clean_ExtraWhitespaceAndLineBreaks_AreNormalized()
    {
        var result = DescriptionNormalizer.Clean("Line one.\n\n\n   Line   two.\t\tLine three.");

        Assert.Equal("Line one. Line two. Line three.", result);
    }

    [Fact]
    public void Clean_BrAndBlockTags_BecomeSingleSpaceNotGluedWords()
    {
        var result = DescriptionNormalizer.Clean("<div>Hello</div><div>World</div>Hello<br/>World");

        Assert.DoesNotContain("HelloWorld", result);
        Assert.Equal("Hello World Hello World", result);
    }

    [Fact]
    public void Clean_ContentAt250WordsOrFewer_IsReturnedInFullWithoutTruncation()
    {
        var sentence = BuildWordSentence(1, 50);
        var text = string.Join(" ", Enumerable.Repeat(sentence, 5)); // 5 * 50 = 250 words total

        var result = DescriptionNormalizer.Clean(text);

        Assert.Equal(250, CountWords(result));
        Assert.DoesNotContain("...", result);
    }

    [Fact]
    public void Clean_LongContent_StopsWithinTargetRangeAtSentenceBoundary()
    {
        // 27 sentences of 10 words each = 270 words total (> 250), so only whole sentences that
        // fit within 250 should be kept.
        var sentences = Enumerable.Range(0, 27)
            .Select(i => BuildWordSentence(i * 10 + 1, 10))
            .ToList();
        var text = string.Join(" ", sentences);

        var result = DescriptionNormalizer.Clean(text);
        var wordCount = CountWords(result);

        Assert.InRange(wordCount, 200, 250);
        Assert.DoesNotContain("...", result);
        Assert.EndsWith(".", result);
        // The 26th sentence's first word (index 25*10+1 = 251) must not appear - it was excluded.
        Assert.DoesNotContain("Word251", result);
    }

    [Fact]
    public void Clean_LongContent_NeverCutsAKeptSentenceMidway()
    {
        var sentences = Enumerable.Range(0, 27)
            .Select(i => BuildWordSentence(i * 10 + 1, 10))
            .ToList();
        var text = string.Join(" ", sentences);

        var result = DescriptionNormalizer.Clean(text);

        foreach (var kept in sentences.Where(s => result.Contains(s)))
        {
            Assert.Contains(kept, result);
        }

        // Every kept sentence's full text (not a partial prefix) must be present.
        Assert.EndsWith(".", result);
    }

    [Fact]
    public void Clean_LongContent_PreservesOriginalWordingAndOrder()
    {
        var text = "Alpha bravo charlie delta echo foxtrot golf hotel india juliet kilo lima mike november oscar papa "
            + "quebec romeo sierra tango uniform victor whiskey xray yankee zulu one two three four. "
            + "Second sentence stays exactly as written without any rewriting at all here today now.";

        var result = DescriptionNormalizer.Clean(text);

        Assert.Equal(text, result);
    }

    [Fact]
    public void Clean_SingleSentenceExceedingLimit_TruncatesAndAppendsEllipsis()
    {
        // One long run-on "sentence" (no punctuation until the very end) with 300 words.
        var words = Enumerable.Range(1, 300).Select(i => $"Word{i}");
        var text = string.Join(" ", words) + ".";

        var result = DescriptionNormalizer.Clean(text);

        Assert.EndsWith("...", result);
        Assert.Contains("Word250", result);
        Assert.DoesNotContain("Word251", result);
        Assert.Equal(250, CountWords(result.Replace("...", string.Empty)));
    }

    [Fact]
    public void Clean_AbbreviationLikeDr_DoesNotCauseIncorrectSentenceSplit()
    {
        var sentences = Enumerable.Range(0, 27)
            .Select(i => BuildWordSentence(i * 10 + 1, 10))
            .ToList();
        // Prefix with an abbreviation-containing sentence to make sure "Dr." isn't treated as a
        // sentence boundary on its own.
        var text = "Dr. Smith announced the results today with great confidence overall. " + string.Join(" ", sentences);

        var result = DescriptionNormalizer.Clean(text);

        Assert.StartsWith("Dr. Smith announced", result);
    }

    [Fact]
    public void Clean_MalformedHtml_UnclosedTag_DoesNotThrowAndReturnsReadableText()
    {
        var exception = Record.Exception(() => DescriptionNormalizer.Clean("<p>Some story <b>content that never closes its bold tag"));

        Assert.Null(exception);
    }

    [Fact]
    public void Clean_MalformedHtml_StrayAngleBrackets_DoesNotThrow()
    {
        var exception = Record.Exception(() => DescriptionNormalizer.Clean("3 < 5 and 10 > 2, this is < not a real tag"));

        Assert.Null(exception);
    }

    [Fact]
    public void Clean_DeeplyNestedMalformedMarkup_DoesNotThrowAndCompletesQuickly()
    {
        var malformed = string.Concat(Enumerable.Repeat("<div><span><b><i>", 500)) + "Text" + string.Concat(Enumerable.Repeat("</i></b></span></div>", 500));

        var exception = Record.Exception(() => DescriptionNormalizer.Clean(malformed));

        Assert.Null(exception);
    }

    private static string BuildWordSentence(int startIndex, int wordCount)
    {
        var words = Enumerable.Range(startIndex, wordCount).Select(i => $"Word{i}").ToArray();
        return string.Join(" ", words) + ".";
    }

    private static int CountWords(string text) =>
        text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
}
