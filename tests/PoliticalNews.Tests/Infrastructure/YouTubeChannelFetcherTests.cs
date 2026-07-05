using Microsoft.Extensions.Logging.Abstractions;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Social;
using PoliticalNews.Tests.TestSupport;

namespace PoliticalNews.Tests.Infrastructure;

public class YouTubeChannelFetcherTests
{
    private static SocialMediaSource BuildSource() => new()
    {
        Id = "1",
        Platform = SocialPlatform.YouTube,
        SourceType = SourceEntityType.Politician,
        Country = "India",
        Name = "Narendra Modi",
        Identifier = "UC1NF71EwP41VdjAU1iXdLkw",
        Language = "en",
        Category = "Video",
        Enabled = true,
        PollIntervalMinutes = 30,
        TimeoutSeconds = 60
    };

    [Fact]
    public async Task FetchAsync_ParsesAtomEntries_IntoNormalizedArticles()
    {
        const string url = "https://www.youtube.com/feeds/videos.xml?channel_id=UC1NF71EwP41VdjAU1iXdLkw";
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <feed xmlns:yt="http://www.youtube.com/xml/schemas/2015" xmlns:media="http://search.yahoo.com/mrss/" xmlns="http://www.w3.org/2005/Atom">
              <entry>
                <id>yt:video:abc123</id>
                <yt:videoId>abc123</yt:videoId>
                <title>PM addresses the nation</title>
                <link rel="alternate" href="https://www.youtube.com/watch?v=abc123"/>
                <author><name>Narendra Modi</name></author>
                <published>2026-07-01T10:15:00+00:00</published>
                <media:group>
                  <media:description>Full address to the nation on the latest policy announcement.</media:description>
                  <media:thumbnail url="https://i.ytimg.com/vi/abc123/hqdefault.jpg"/>
                </media:group>
              </entry>
            </feed>
            """;

        var fetcher = new YouTubeChannelFetcher(
            new StubHttpClientFactory(new StubHttpMessageHandler(new Dictionary<string, string> { [url] = xml })),
            NullLogger<YouTubeChannelFetcher>.Instance);

        var articles = await fetcher.FetchAsync(BuildSource(), CancellationToken.None);

        var article = Assert.Single(articles);
        Assert.Equal("PM addresses the nation", article.Title);
        Assert.Equal("https://www.youtube.com/watch?v=abc123", article.Url);
        Assert.Equal("abc123", article.OriginalGuid);
        Assert.Equal("Narendra Modi", article.Author);
        Assert.Equal("Narendra Modi", article.FeedName);
        Assert.Equal("YouTube", article.Provider);
        Assert.Equal("India", article.Country);
        Assert.Equal("https://i.ytimg.com/vi/abc123/hqdefault.jpg", article.ImageUrl);
        Assert.Equal(new DateTimeOffset(2026, 7, 1, 10, 15, 0, TimeSpan.Zero), article.PublishedAt);
        Assert.Contains("video", article.Tags);
    }

    [Fact]
    public async Task FetchAsync_EntryMissingTitleOrLink_IsSkipped()
    {
        const string url = "https://www.youtube.com/feeds/videos.xml?channel_id=UC1NF71EwP41VdjAU1iXdLkw";
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <feed xmlns:yt="http://www.youtube.com/xml/schemas/2015" xmlns:media="http://search.yahoo.com/mrss/" xmlns="http://www.w3.org/2005/Atom">
              <entry>
                <yt:videoId>abc123</yt:videoId>
                <author><name>Narendra Modi</name></author>
              </entry>
            </feed>
            """;

        var fetcher = new YouTubeChannelFetcher(
            new StubHttpClientFactory(new StubHttpMessageHandler(new Dictionary<string, string> { [url] = xml })),
            NullLogger<YouTubeChannelFetcher>.Instance);

        var articles = await fetcher.FetchAsync(BuildSource(), CancellationToken.None);

        Assert.Empty(articles);
    }

    [Fact]
    public void Platform_IsYouTube() =>
        Assert.Equal(SocialPlatform.YouTube, new YouTubeChannelFetcher(
            new StubHttpClientFactory(new StubHttpMessageHandler(new Dictionary<string, string>())),
            NullLogger<YouTubeChannelFetcher>.Instance).Platform);
}
