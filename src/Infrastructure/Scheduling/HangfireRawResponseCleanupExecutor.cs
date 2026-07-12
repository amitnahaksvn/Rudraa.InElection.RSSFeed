using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.Logging;
using Application.Abstractions;
using Application.Services;

namespace Infrastructure.Scheduling;

/// <summary>
/// What the daily raw-response-cleanup recurring job actually invokes - same reasoning as
/// <see cref="HangfireCrawlJobExecutor"/>: a friendly dashboard name and every log line tagged
/// with this specific job execution's id via <see cref="PerformContext"/>.
/// </summary>
public sealed class HangfireRawResponseCleanupExecutor
{
    private readonly IRawResponseCleanupService _cleanupService;
    private readonly JobExecutionLogger _executionLogger;
    private readonly ILogger<HangfireRawResponseCleanupExecutor> _logger;

    public HangfireRawResponseCleanupExecutor(
        IRawResponseCleanupService cleanupService, JobExecutionLogger executionLogger, ILogger<HangfireRawResponseCleanupExecutor> logger)
    {
        _cleanupService = cleanupService;
        _executionLogger = executionLogger;
        _logger = logger;
    }

    [JobDisplayName("Cleanup raw responses older than {0}")]
    public async Task RunAsync(TimeSpan retention, PerformContext context, CancellationToken cancellationToken)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object> { ["HangfireJobId"] = context.BackgroundJob.Id });

        await _executionLogger.RunAsync(
            HangfireJobIds.RawResponseCleanup,
            "Cleanup raw responses",
            context.BackgroundJob.Id,
            () => _cleanupService.CleanupAsync(retention, cancellationToken),
            cancellationToken);
    }
}
