using Microsoft.Extensions.Logging.Abstractions;
using Application.Options;
using Infrastructure.RssProviders;
using PoliticalNews.Tests.TestSupport;

namespace PoliticalNews.Tests.Infrastructure;

public class TheWeekRssProviderTests
{
    private const string FeedUrl = "https://example.com/news/india.rss";
    private const string ArticleUrl = "https://example.com/news/india/2026/05/28/story-1.html";

    /// <summary>
    /// Mirrors The Week's real quirks exactly: the "EEE MMM dd HH:mm:ss IST yyyy" pubDate format
    /// (the "IST" zone abbreviation, not a numeric offset), and no image tag at all - only an
    /// HTML &lt;img&gt; embedded inside &lt;description&gt;.
    /// </summary>
    private const string SampleRss = $$"""
        <rss version="2.0">
        <channel>
         <title>The Week</title>
            <item>
                <title> story-1-headline-as-a-slug</title>
                <description>
                    &lt;a href="{{ArticleUrl}}"&gt;&lt;img border="0" src="http://img.theweek.in/story-1.jpg" /&gt;  </description>
                <link>{{ArticleUrl}}</link>
                <guid>{{ArticleUrl}}</guid>
                <pubDate>Thu May 28 21:09:41 IST 2026</pubDate>
            </item>
        </channel>
        </rss>
        """;

    private const string ArticleHtml = """
        <html><head>
        <meta property="og:image" content="https://example.com/images/story-1-og.jpg" />
        </head><body>story</body></html>
        """;

    [Fact]
    public async Task FetchAllFeedsAsync_ParsesIstPubDateAndFallsBackToOgImage()
    {
        var handler = new StubHttpMessageHandler(new Dictionary<string, string>
        {
            [FeedUrl] = SampleRss,
            [ArticleUrl] = ArticleHtml
        });
        var provider = new TheWeekRssProvider(new StubHttpClientFactory(handler), NullLogger<TheWeekRssProvider>.Instance);

        var feeds = new List<RssFeedOptions>
        {
            new() { Name = "India", Url = FeedUrl, Category = "National", Language = "en", Enabled = true }
        };

        var results = await provider.FetchAllFeedsAsync(feeds, CancellationToken.None);

        var result = Assert.Single(results);
        Assert.True(result.Success);
        var article = Assert.Single(result.Articles);

        Assert.Equal("TheWeek", article.Provider);
        Assert.Equal(ArticleUrl, article.Url);

        // "IST" (not a numeric offset, not "GMT") must still parse to the right instant.
        Assert.NotNull(article.PublishedAt);
        Assert.Equal(new DateTimeOffset(2026, 5, 28, 21, 9, 41, TimeSpan.FromMinutes(330)), article.PublishedAt);

        // No media:thumbnail/content tag - the <img> is only inside <description> HTML, which
        // BaseRssProvider doesn't scrape, so the image must come from the article's og:image.
        Assert.Equal("https://example.com/images/story-1-og.jpg", article.ImageUrl);
    }
}
