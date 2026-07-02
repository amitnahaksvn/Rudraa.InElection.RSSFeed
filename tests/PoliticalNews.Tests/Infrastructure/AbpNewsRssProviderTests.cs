using Microsoft.Extensions.Logging.Abstractions;
using Application.Options;
using Infrastructure.RssProviders;
using PoliticalNews.Tests.TestSupport;

namespace PoliticalNews.Tests.Infrastructure;

public class AbpNewsRssProviderTests
{
    private const string FeedUrl = "https://example.com/news/india/feed";

    private const string SampleRss = """
        <?xml version="1.0" encoding="UTF-8"?>
        <rss version="2.0" xmlns:media="http://search.yahoo.com/mrss/" xmlns:content="http://purl.org/rss/1.0/modules/content/">
          <channel>
            <title>ABP Live</title>
            <item>
              <title>Test Headline One</title>
              <link>https://example.com/story-1</link>
              <guid>guid-1</guid>
              <pubDate>Wed, 01 Jul 2026 10:00:00 +0530</pubDate>
              <description>Summary of story one</description>
              <category>Politics</category>
              <media:thumbnail url="https://example.com/image1.jpg" />
            </item>
          </channel>
        </rss>
        """;

    [Fact]
    public async Task FetchAllFeedsAsync_ParsesAndNormalizesItems()
    {
        var handler = new StubHttpMessageHandler(new Dictionary<string, string> { [FeedUrl] = SampleRss });
        var provider = new AbpNewsRssProvider(new StubHttpClientFactory(handler), NullLogger<AbpNewsRssProvider>.Instance);

        var feeds = new List<RssFeedOptions>
        {
            new() { Name = "India", Url = FeedUrl, Category = "National", Language = "hi", Enabled = true }
        };

        var results = await provider.FetchAllFeedsAsync(feeds, CancellationToken.None);

        var result = Assert.Single(results);
        Assert.True(result.Success);
        var article = Assert.Single(result.Articles);
        Assert.Equal("ABPNews", article.Provider);
        Assert.Equal("India", article.FeedName);
        Assert.Equal("National", article.Category);
        Assert.Equal("Test Headline One", article.Title);
        Assert.Equal("https://example.com/story-1", article.Url);
        Assert.Equal("guid-1", article.OriginalGuid);
    }

    [Fact]
    public async Task FetchAllFeedsAsync_HttpFailure_ReturnsUnsuccessfulResultInsteadOfThrowing()
    {
        var handler = new StubHttpMessageHandler(new Dictionary<string, string>());
        var provider = new AbpNewsRssProvider(new StubHttpClientFactory(handler), NullLogger<AbpNewsRssProvider>.Instance);

        var feeds = new List<RssFeedOptions>
        {
            new() { Name = "India", Url = FeedUrl, Category = "National", Enabled = true }
        };

        var results = await provider.FetchAllFeedsAsync(feeds, CancellationToken.None);

        var result = Assert.Single(results);
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }
}
