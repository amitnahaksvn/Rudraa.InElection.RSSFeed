using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.Logging;
using Application.Abstractions;

namespace Infrastructure.Scheduling;

/// <summary>
/// The one thing every Hangfire-scheduled crawl job actually invokes - never
/// <see cref="INewsCrawlerService"/> directly - so that:
/// (1) the Hangfire dashboard's job list shows "Crawl AajTak"/"Crawl ABPNews" instead of a raw
///     method signature (<see cref="JobDisplayNameAttribute"/> below), and
/// (2) every log line the crawl emits is tagged with that specific Hangfire job execution's own
///     id (via <see cref="PerformContext"/>, which Hangfire auto-injects - never serialized/passed
///     by the caller), so a run seen in the dashboard can be traced back to its exact log output.
/// </summary>
public sealed class HangfireCrawlJobExecutor
{
    private readonly INewsCrawlerService _crawlerService;
    private readonly ILogger<HangfireCrawlJobExecutor> _logger;

    public HangfireCrawlJobExecutor(INewsCrawlerService crawlerService, ILogger<HangfireCrawlJobExecutor> logger)
    {
        _crawlerService = crawlerService;
        _logger = logger;
    }

    [JobDisplayName("Crawl {0}")]
    public async Task RunAsync(string providerName, PerformContext context, CancellationToken cancellationToken)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["HangfireJobId"] = context.BackgroundJob.Id,
            ["Provider"] = providerName
        });

        await _crawlerService.RunCrawlAsync(new[] { providerName }, cancellationToken);
    }
}
