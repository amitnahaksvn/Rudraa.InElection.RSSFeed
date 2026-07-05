using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.Logging;
using Application.Abstractions;
using Application.Services;

namespace Infrastructure.Scheduling;

/// <summary>
/// The one thing every <see cref="Domain.Entities.SocialMediaSource"/>-driven Hangfire job
/// actually invokes - never <see cref="ISocialMediaIngestionService"/> directly - same reasoning
/// as <see cref="HangfireDynamicFeedJobExecutor"/>/<see cref="HangfireCrawlJobExecutor"/>: a
/// friendly dashboard name and every log line tagged with this specific job execution's id.
///
/// Tagged onto its own "social" queue (added to <c>HangfireOptions.Queues</c>'s default list)
/// rather than reusing "rss" - a genuinely different pipeline (multi-platform, DB-driven channel
/// list) that may need to scale independently later, the same reasoning "api" got its own queue
/// instead of sharing "rss".
/// </summary>
[Queue("social")]
public sealed class HangfireSocialMediaJobExecutor
{
    private readonly ISocialMediaIngestionService _ingestionService;
    private readonly ILogger<HangfireSocialMediaJobExecutor> _logger;

    public HangfireSocialMediaJobExecutor(ISocialMediaIngestionService ingestionService, ILogger<HangfireSocialMediaJobExecutor> logger)
    {
        _ingestionService = ingestionService;
        _logger = logger;
    }

    [JobDisplayName("Poll social source {0}")]
    public async Task RunAsync(string socialMediaSourceId, PerformContext context, CancellationToken cancellationToken)
    {
        ExecutionContextAccessor.CurrentHangfireJobId = context.BackgroundJob.Id;

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["HangfireJobId"] = context.BackgroundJob.Id,
            ["SocialMediaSourceId"] = socialMediaSourceId
        });

        await _ingestionService.RunAsync(socialMediaSourceId, cancellationToken);
    }
}
