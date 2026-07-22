using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Application.Abstractions;
using Application.Options;
using Application.Services;
using Domain.Entities;

namespace PoliticalNews.Tests.Application;

public class ErrorNotificationDispatchServiceTests
{
    private static ErrorLog BuildError(string id) => new()
    {
        Id = id,
        ExceptionType = "System.Exception",
        Message = "boom",
        Source = "RSS Feed Fetch",
        Environment = "Test",
        ApplicationName = "PoliticalNews.Tests",
        IsSent = false
    };

    private static ErrorNotificationDispatchService BuildService(
        Mock<IErrorLogRepository> errorLogRepo, Mock<IEmailService> emailService, int maxBatchSize = 100) =>
        new(
            errorLogRepo.Object,
            emailService.Object,
            Options.Create(new ErrorNotificationOptions { DispatchCron = "*/5 * * * *", MaxBatchSize = maxBatchSize }),
            NullLogger<ErrorNotificationDispatchService>.Instance);

    [Fact]
    public async Task DispatchPendingAsync_NoPendingErrors_ReturnsZeroAndNeverSendsEmail()
    {
        var errorLogRepo = new Mock<IErrorLogRepository>();
        errorLogRepo
            .Setup(r => r.GetUnsentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var emailService = new Mock<IEmailService>();
        var service = BuildService(errorLogRepo, emailService);

        var count = await service.DispatchPendingAsync(CancellationToken.None);

        Assert.Equal(0, count);
        emailService.Verify(e => e.SendErrorLogBatchAsync(It.IsAny<IReadOnlyList<ErrorLog>>(), It.IsAny<CancellationToken>()), Times.Never);
        errorLogRepo.Verify(r => r.MarkAsSentAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DispatchPendingAsync_PendingErrors_SendsBatchEmailAndMarksThemSent()
    {
        var pending = new List<ErrorLog> { BuildError("err-1"), BuildError("err-2") };

        var errorLogRepo = new Mock<IErrorLogRepository>();
        errorLogRepo
            .Setup(r => r.GetUnsentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pending);

        var emailService = new Mock<IEmailService>();
        emailService
            .Setup(e => e.SendErrorLogBatchAsync(It.IsAny<IReadOnlyList<ErrorLog>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = BuildService(errorLogRepo, emailService);

        var count = await service.DispatchPendingAsync(CancellationToken.None);

        Assert.Equal(2, count);
        emailService.Verify(
            e => e.SendErrorLogBatchAsync(It.Is<IReadOnlyList<ErrorLog>>(l => l.Count == 2), It.IsAny<CancellationToken>()),
            Times.Once);
        errorLogRepo.Verify(
            r => r.MarkAsSentAsync(
                It.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(new[] { "err-1", "err-2" })),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchPendingAsync_EmailSendFails_LeavesErrorsUnsent()
    {
        var pending = new List<ErrorLog> { BuildError("err-1") };

        var errorLogRepo = new Mock<IErrorLogRepository>();
        errorLogRepo
            .Setup(r => r.GetUnsentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pending);

        var emailService = new Mock<IEmailService>();
        emailService
            .Setup(e => e.SendErrorLogBatchAsync(It.IsAny<IReadOnlyList<ErrorLog>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = BuildService(errorLogRepo, emailService);

        var count = await service.DispatchPendingAsync(CancellationToken.None);

        Assert.Equal(0, count);
        errorLogRepo.Verify(r => r.MarkAsSentAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DispatchPendingAsync_PassesConfiguredMaxBatchSizeToRepository()
    {
        var errorLogRepo = new Mock<IErrorLogRepository>();
        errorLogRepo
            .Setup(r => r.GetUnsentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var emailService = new Mock<IEmailService>();
        var service = BuildService(errorLogRepo, emailService, maxBatchSize: 42);

        await service.DispatchPendingAsync(CancellationToken.None);

        errorLogRepo.Verify(r => r.GetUnsentAsync(42, It.IsAny<CancellationToken>()), Times.Once);
    }
}
