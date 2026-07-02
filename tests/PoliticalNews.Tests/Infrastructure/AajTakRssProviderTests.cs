using Microsoft.Extensions.Logging.Abstractions;
using Application.Options;
using Infrastructure.RssProviders;
using PoliticalNews.Tests.TestSupport;

namespace PoliticalNews.Tests.Infrastructure;

public class AajTakRssProviderTests
{
    private const string FeedUrl = "https://example.com/rssfeeds/?id=home";

    private const string SampleRss = """
        <?xml version="1.0" encoding="UTF-8"?>
        <rss version="2.0" xmlns:media="http://search.yahoo.com/mrss/" xmlns:content="http://purl.org/rss/1.0/modules/content/">
          <channel>
            <title>Aaj Tak</title>
            <item>
              <title>Test Headline One</title>
              <link>https://example.com/story-1</link>
              <guid>guid-1</guid>
              <pubDate>Wed, 01 Jul 2026 10:00:00 +0530</pubDate>
              <description>Summary of story one</description>
              <category>Politics</category>
              <media:thumbnail url="https://example.com/image1.jpg" />
            </item>
            <item>
              <title>Test Headline Two</title>
              <link>https://example.com/story-2</link>
              <guid>guid-2</guid>
              <pubDate>Wed, 01 Jul 2026 11:00:00 +0530</pubDate>
              <description>Summary of story two</description>
              <enclosure url="https://example.com/image2.jpg" type="image/jpeg" />
            </item>
          </channel>
        </rss>
        """;

    [Fact]
    public async Task FetchAllFeedsAsync_ParsesAndNormalizesItems()
    {
        var handler = new StubHttpMessageHandler(new Dictionary<string, string> { [FeedUrl] = SampleRss });
        var provider = new AajTakRssProvider(new StubHttpClientFactory(handler), NullLogger<AajTakRssProvider>.Instance);

        var feeds = new List<RssFeedOptions>
        {
            new() { Name = "Home", Url = FeedUrl, Category = "General", Language = "hi", Enabled = true }
        };

        var results = await provider.FetchAllFeedsAsync(feeds, CancellationToken.None);

        var result = Assert.Single(results);
        Assert.True(result.Success);
        Assert.Equal(2, result.Articles.Count);

        var first = result.Articles[0];
        Assert.Equal("AajTak", first.Provider);
        Assert.Equal("Home", first.FeedName);
        Assert.Equal("General", first.Category);
        Assert.Equal("Test Headline One", first.Title);
        Assert.Equal("https://example.com/story-1", first.Url);
        Assert.Equal("guid-1", first.OriginalGuid);
        Assert.Equal("https://example.com/image1.jpg", first.ImageUrl);
        Assert.Contains("Politics", first.Tags);
        Assert.NotNull(first.PublishedAt);

        var second = result.Articles[1];
        Assert.Equal("https://example.com/image2.jpg", second.ImageUrl);
    }

    [Fact]
    public async Task FetchAllFeedsAsync_HttpFailure_ReturnsUnsuccessfulResultInsteadOfThrowing()
    {
        var handler = new StubHttpMessageHandler(new Dictionary<string, string>());
        var provider = new AajTakRssProvider(new StubHttpClientFactory(handler), NullLogger<AajTakRssProvider>.Instance);

        var feeds = new List<RssFeedOptions>
        {
            new() { Name = "Home", Url = FeedUrl, Category = "General", Enabled = true }
        };

        var results = await provider.FetchAllFeedsAsync(feeds, CancellationToken.None);

        var result = Assert.Single(results);
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task FetchAllFeedsAsync_FeedHangsPastHttpClientTimeout_ReturnsUnsuccessfulResultInsteadOfCrashing()
    {
        // Regression test: a dead/hanging AajTak connection makes HttpClient's own Timeout fire a
        // TaskCanceledException that has nothing to do with the caller's cancellationToken (which
        // stays uncancelled here). BaseRssProvider must catch that and report a failed feed rather
        // than let it propagate and crash the whole host.
        var handler = new HangingHttpMessageHandler();
        var provider = new AajTakRssProvider(
            new StubHttpClientFactory(handler, timeout: TimeSpan.FromMilliseconds(100)),
            NullLogger<AajTakRssProvider>.Instance);

        var feeds = new List<RssFeedOptions>
        {
            new() { Name = "Home", Url = FeedUrl, Category = "General", Enabled = true }
        };

        var results = await provider.FetchAllFeedsAsync(feeds, CancellationToken.None);

        var result = Assert.Single(results);
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }
}
