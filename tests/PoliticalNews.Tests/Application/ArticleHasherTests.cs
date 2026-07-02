using Application.Services;

namespace PoliticalNews.Tests.Application;

public class ArticleHasherTests
{
    [Fact]
    public void ComputeHash_SameTitleAndDate_ProducesSameHash()
    {
        var publishedAt = new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero);

        var hash1 = ArticleHasher.ComputeHash("Some Headline", publishedAt);
        var hash2 = ArticleHasher.ComputeHash("Some Headline", publishedAt);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_IsCaseAndWhitespaceInsensitive()
    {
        var publishedAt = new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero);

        var hash1 = ArticleHasher.ComputeHash("Some Headline", publishedAt);
        var hash2 = ArticleHasher.ComputeHash("  SOME headline  ", publishedAt);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_DifferentTitle_ProducesDifferentHash()
    {
        var publishedAt = new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero);

        var hash1 = ArticleHasher.ComputeHash("Headline A", publishedAt);
        var hash2 = ArticleHasher.ComputeHash("Headline B", publishedAt);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_DifferentPublishedAt_ProducesDifferentHash()
    {
        var hash1 = ArticleHasher.ComputeHash("Same Headline", new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero));
        var hash2 = ArticleHasher.ComputeHash("Same Headline", new DateTimeOffset(2026, 7, 2, 10, 0, 0, TimeSpan.Zero));

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_NullPublishedAt_DoesNotThrow()
    {
        var hash = ArticleHasher.ComputeHash("Headline", null);

        Assert.False(string.IsNullOrWhiteSpace(hash));
    }
}
