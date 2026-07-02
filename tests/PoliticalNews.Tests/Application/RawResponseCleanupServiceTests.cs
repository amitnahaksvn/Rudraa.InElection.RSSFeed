using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Application.Abstractions;
using Application.Services;

namespace PoliticalNews.Tests.Application;

public class RawResponseCleanupServiceTests
{
    [Fact]
    public async Task CleanupAsync_DeletesResponsesOlderThanRetention_AndReturnsDeletedCount()
    {
        DateTimeOffset? capturedOlderThan = null;

        var repository = new Mock<IRssRawResponseRepository>();
        repository
            .Setup(r => r.DeleteOlderThanAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Callback<DateTimeOffset, CancellationToken>((olderThan, _) => capturedOlderThan = olderThan)
            .ReturnsAsync(42);

        var service = new RawResponseCleanupService(repository.Object, NullLogger<RawResponseCleanupService>.Instance);
        var retention = TimeSpan.FromDays(7);

        var before = DateTimeOffset.UtcNow;
        var deletedCount = await service.CleanupAsync(retention, CancellationToken.None);
        var after = DateTimeOffset.UtcNow;

        Assert.Equal(42, deletedCount);
        Assert.NotNull(capturedOlderThan);
        // olderThan should be ~7 days before "now" at the moment of the call.
        Assert.InRange(capturedOlderThan.Value, before - retention, after - retention);
        repository.Verify(r => r.DeleteOlderThanAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
