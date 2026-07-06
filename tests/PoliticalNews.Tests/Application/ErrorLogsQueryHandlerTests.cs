using Moq;
using Application.Abstractions;
using Application.ErrorLogs.Commands.AddErrorLogComment;
using Application.ErrorLogs.Commands.SetErrorLogResolved;
using Application.ErrorLogs.Queries.GetErrorLogById;
using Application.ErrorLogs.Queries.GetErrorLogCounts;
using Application.ErrorLogs.Queries.GetErrorLogs;
using Domain.Entities;

namespace PoliticalNews.Tests.Application;

public class ErrorLogsQueryHandlerTests
{
    private static ErrorLog BuildErrorLog(string id = "1", bool isResolved = false) => new()
    {
        Id = id,
        ExceptionType = "System.Net.Http.HttpRequestException",
        Message = "network timeout",
        Source = "RSS Feed Fetch",
        Provider = "AajTak",
        Country = "India",
        Environment = "Production",
        ApplicationName = "Web",
        CreatedOn = DateTimeOffset.UtcNow,
        IsResolved = isResolved
    };

    [Fact]
    public async Task GetErrorLogsQueryHandler_MapsRepositoryPageIntoSummaryDtos()
    {
        var repo = new Mock<IErrorLogRepository>();
        var logs = new List<ErrorLog> { BuildErrorLog("1"), BuildErrorLog("2") };
        repo
            .Setup(r => r.GetPagedAsync(It.IsAny<ErrorLogFilter>(), 0, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);
        repo
            .Setup(r => r.CountAsync(It.IsAny<ErrorLogFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var handler = new GetErrorLogsQueryHandler(repo.Object);
        var result = await handler.Handle(new GetErrorLogsQuery(1, 20), CancellationToken.None);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.False(result.HasMore);
        Assert.Equal("1", result.Items[0].Id);
    }

    [Fact]
    public async Task GetErrorLogsQueryHandler_Page2_ComputesCorrectSkip()
    {
        var repo = new Mock<IErrorLogRepository>();
        repo
            .Setup(r => r.GetPagedAsync(It.IsAny<ErrorLogFilter>(), 20, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        repo
            .Setup(r => r.CountAsync(It.IsAny<ErrorLogFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(25);

        var handler = new GetErrorLogsQueryHandler(repo.Object);
        var result = await handler.Handle(new GetErrorLogsQuery(2, 20), CancellationToken.None);

        repo.Verify(r => r.GetPagedAsync(It.IsAny<ErrorLogFilter>(), 20, 20, It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(result.HasMore is false); // page 2 * pageSize 20 = 40, not < totalCount 25
    }

    [Fact]
    public async Task GetErrorLogsQueryHandler_PassesFiltersThrough()
    {
        var repo = new Mock<IErrorLogRepository>();
        ErrorLogFilter? capturedFilter = null;
        repo
            .Setup(r => r.GetPagedAsync(It.IsAny<ErrorLogFilter>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<ErrorLogFilter, int, int, CancellationToken>((f, _, _, _) => capturedFilter = f)
            .ReturnsAsync([]);
        repo.Setup(r => r.CountAsync(It.IsAny<ErrorLogFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var handler = new GetErrorLogsQueryHandler(repo.Object);
        await handler.Handle(new GetErrorLogsQuery(1, 20, IsResolved: false, Provider: "AajTak", Country: "India", Search: "timeout"), CancellationToken.None);

        Assert.NotNull(capturedFilter);
        Assert.Equal(false, capturedFilter!.IsResolved);
        Assert.Equal("AajTak", capturedFilter.Provider);
        Assert.Equal("India", capturedFilter.Country);
        Assert.Equal("timeout", capturedFilter.SearchText);
    }

    [Fact]
    public async Task GetErrorLogByIdQueryHandler_Found_ReturnsDetailDto()
    {
        var repo = new Mock<IErrorLogRepository>();
        repo.Setup(r => r.GetByIdAsync("1", It.IsAny<CancellationToken>())).ReturnsAsync(BuildErrorLog("1"));

        var handler = new GetErrorLogByIdQueryHandler(repo.Object);
        var result = await handler.Handle(new GetErrorLogByIdQuery("1"), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("1", result!.Id);
        Assert.Equal("network timeout", result.Message);
    }

    [Fact]
    public async Task GetErrorLogByIdQueryHandler_NotFound_ReturnsNull()
    {
        var repo = new Mock<IErrorLogRepository>();
        repo.Setup(r => r.GetByIdAsync("missing", It.IsAny<CancellationToken>())).ReturnsAsync((ErrorLog?)null);

        var handler = new GetErrorLogByIdQueryHandler(repo.Object);
        var result = await handler.Handle(new GetErrorLogByIdQuery("missing"), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task SetErrorLogResolvedCommandHandler_DelegatesToRepositoryAndReturnsItsResult()
    {
        var repo = new Mock<IErrorLogRepository>();
        repo.Setup(r => r.SetResolvedAsync("1", true, "Fixed the feed URL", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repo.Setup(r => r.SetResolvedAsync("missing", true, "Fixed the feed URL", null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var notifier = new Mock<IErrorLogNotifier>();

        var handler = new SetErrorLogResolvedCommandHandler(repo.Object, notifier.Object);

        Assert.True(await handler.Handle(new SetErrorLogResolvedCommand("1", true, "Fixed the feed URL"), CancellationToken.None));
        Assert.False(await handler.Handle(new SetErrorLogResolvedCommand("missing", true, "Fixed the feed URL"), CancellationToken.None));

        notifier.Verify(
            n => n.NotifyResolvedChangedAsync("1", true, It.IsAny<DateTimeOffset?>(), "Fixed the feed URL", null, It.IsAny<DateTimeOffset>(), null, It.IsAny<CancellationToken>()),
            Times.Once);
        notifier.Verify(
            n => n.NotifyResolvedChangedAsync("missing", It.IsAny<bool>(), It.IsAny<DateTimeOffset?>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTimeOffset>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AddErrorLogCommentCommandHandler_DelegatesToRepositoryAndReturnsItsResult()
    {
        var repo = new Mock<IErrorLogRepository>();
        repo.Setup(r => r.AddCommentAsync("1", "Investigating", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repo.Setup(r => r.AddCommentAsync("missing", "Investigating", null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var notifier = new Mock<IErrorLogNotifier>();

        var handler = new AddErrorLogCommentCommandHandler(repo.Object, notifier.Object);

        Assert.True(await handler.Handle(new AddErrorLogCommentCommand("1", "Investigating"), CancellationToken.None));
        Assert.False(await handler.Handle(new AddErrorLogCommentCommand("missing", "Investigating"), CancellationToken.None));

        notifier.Verify(
            n => n.NotifyCommentAddedAsync("1", "Investigating", null, It.IsAny<DateTimeOffset>(), null, It.IsAny<CancellationToken>()),
            Times.Once);
        notifier.Verify(
            n => n.NotifyCommentAddedAsync("missing", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTimeOffset>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetErrorLogCountsQueryHandler_ReturnsCountsPerStatus()
    {
        var repo = new Mock<IErrorLogRepository>();
        repo.Setup(r => r.CountAsync(It.Is<ErrorLogFilter>(f => f.IsResolved == null && f.Category == null), It.IsAny<CancellationToken>())).ReturnsAsync(10);
        repo.Setup(r => r.CountAsync(It.Is<ErrorLogFilter>(f => f.IsResolved == false), It.IsAny<CancellationToken>())).ReturnsAsync(7);
        repo.Setup(r => r.CountAsync(It.Is<ErrorLogFilter>(f => f.IsResolved == true), It.IsAny<CancellationToken>())).ReturnsAsync(3);
        repo.Setup(r => r.CountAsync(It.Is<ErrorLogFilter>(f => f.Category == ErrorLogCategory.Rss), It.IsAny<CancellationToken>())).ReturnsAsync(4);
        repo.Setup(r => r.CountAsync(It.Is<ErrorLogFilter>(f => f.Category == ErrorLogCategory.Api), It.IsAny<CancellationToken>())).ReturnsAsync(2);
        repo.Setup(r => r.CountAsync(It.Is<ErrorLogFilter>(f => f.Category == ErrorLogCategory.Social), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        repo.Setup(r => r.CountAsync(It.Is<ErrorLogFilter>(f => f.Category == ErrorLogCategory.Http), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        repo.Setup(r => r.CountAsync(It.Is<ErrorLogFilter>(f => f.Category == ErrorLogCategory.Critical), It.IsAny<CancellationToken>())).ReturnsAsync(5);
        repo.Setup(r => r.CountAsync(It.Is<ErrorLogFilter>(f => f.Category == ErrorLogCategory.Warning), It.IsAny<CancellationToken>())).ReturnsAsync(2);

        var handler = new GetErrorLogCountsQueryHandler(repo.Object);
        var result = await handler.Handle(new GetErrorLogCountsQuery(), CancellationToken.None);

        Assert.Equal(10, result.All);
        Assert.Equal(4, result.Rss);
        Assert.Equal(2, result.Api);
        Assert.Equal(1, result.Social);
        Assert.Equal(1, result.Http);
        Assert.Equal(5, result.Critical);
        Assert.Equal(2, result.Warning);
        Assert.Equal(7, result.Unresolved);
        Assert.Equal(3, result.Resolved);
    }
}
