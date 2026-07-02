using Microsoft.Extensions.Logging.Abstractions;
using Application.Options;
using Infrastructure.RssProviders;
using PoliticalNews.Tests.TestSupport;

namespace PoliticalNews.Tests.Infrastructure;

public class ZeeNewsRssProviderTests
{
    private const string FeedUrl = "https://example.com/rss/india-national-news.xml";
    private const string ArticleUrl = "https://example.com/india/story-1.html";

    /// <summary>
    /// Mirrors Zee News's real quirks exactly: lowercase &lt;pubdate&gt;, the nonstandard
    /// "GMT +5:30" date format, CDATA-wrapped links, and no image tags at all.
    /// </summary>
    private const string SampleRss = $$"""
        <rss version="2.0" >
        <channel>
         <title>Zee News :India National</title>
            <item>
                <title>Test Headline One</title>
                <link><![CDATA[{{ArticleUrl}}]]></link>
                <description>Summary of story one</description>
                <pubdate>Thursday, July 02, 2026, 14:08 GMT +5:30</pubdate>
                <author>Zee News</author>
                <guid isPermaLink="false"><![CDATA[{{ArticleUrl}}]]></guid>
            </item>
        </channel>
        </rss>
        """;

    private const string ArticleHtml = """
        <html><head>
        <meta property="og:image" content="https://example.com/images/story-1.jpg" />
        </head><body>story</body></html>
        """;

    [Fact]
    public async Task FetchAllFeedsAsync_ParsesZeeQuirks_LowercasePubdateAndOgImageFallback()
    {
        var handler = new StubHttpMessageHandler(new Dictionary<string, string>
        {
            [FeedUrl] = SampleRss,
            [ArticleUrl] = ArticleHtml
        });
        var provider = new ZeeNewsRssProvider(new StubHttpClientFactory(handler), NullLogger<ZeeNewsRssProvider>.Instance);

        var feeds = new List<RssFeedOptions>
        {
            new() { Name = "IndiaNational", Url = FeedUrl, Category = "National", Language = "en", Enabled = true }
        };

        var results = await provider.FetchAllFeedsAsync(feeds, CancellationToken.None);

        var result = Assert.Single(results);
        Assert.True(result.Success);
        var article = Assert.Single(result.Articles);

        Assert.Equal("ZeeNews", article.Provider);
        Assert.Equal("Test Headline One", article.Title);
        Assert.Equal(ArticleUrl, article.Url);

        // Lowercase <pubdate> + "GMT +5:30" format must still yield the right instant.
        Assert.NotNull(article.PublishedAt);
        Assert.Equal(new DateTimeOffset(2026, 7, 2, 14, 8, 0, TimeSpan.FromMinutes(330)), article.PublishedAt);

        // No image tags in the feed - image must come from the article page's og:image.
        Assert.Equal("https://example.com/images/story-1.jpg", article.ImageUrl);
    }

    [Fact]
    public async Task FetchAllFeedsAsync_ArticlePageUnreachable_ImageIsNullButArticleStillParsed()
    {
        var handler = new StubHttpMessageHandler(new Dictionary<string, string> { [FeedUrl] = SampleRss });
        var provider = new ZeeNewsRssProvider(new StubHttpClientFactory(handler), NullLogger<ZeeNewsRssProvider>.Instance);

        var feeds = new List<RssFeedOptions>
        {
            new() { Name = "IndiaNational", Url = FeedUrl, Category = "National", Language = "en", Enabled = true }
        };

        var results = await provider.FetchAllFeedsAsync(feeds, CancellationToken.None);

        var article = Assert.Single(Assert.Single(results).Articles);
        Assert.Null(article.ImageUrl);
        Assert.NotNull(article.PublishedAt);
    }
}
