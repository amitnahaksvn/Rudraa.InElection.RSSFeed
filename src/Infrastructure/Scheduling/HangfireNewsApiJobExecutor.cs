using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.Logging;
using Application.Abstractions;
using Application.Services;

namespace Infrastructure.Scheduling;

/// <summary>
/// The <see cref="HangfireCrawlJobExecutor"/> counterpart for JSON news-API providers - the one
/// thing every Hangfire-scheduled API fetch job actually invokes, never
/// <see cref="INewsApiCrawlerService"/> directly, for the same two reasons: a friendly dashboard
/// name ("Fetch news API NewsApiOrg (India)") and a <see cref="PerformContext"/>-scoped log tag
/// per run.
///
/// Tagged onto its own "api" queue (distinct from RSS's "rss" queue) so a production deployment
/// can scale API-fetching replicas independently from RSS-crawling replicas - the exact scenario
/// <see cref="Application.Options.HangfireOptions.Queues"/>'s doc comment already anticipated.
/// </summary>
[Queue("api")]
public sealed class HangfireNewsApiJobExecutor
{
    private readonly INewsApiCrawlerService _crawlerService;
    private readonly ILogger<HangfireNewsApiJobExecutor> _logger;

    public HangfireNewsApiJobExecutor(INewsApiCrawlerService crawlerService, ILogger<HangfireNewsApiJobExecutor> logger)
    {
        _crawlerService = crawlerService;
        _logger = logger;
    }

    [JobDisplayName("Fetch news API {0} ({1})")]
    public async Task RunAsync(string providerName, string country, PerformContext context, CancellationToken cancellationToken)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["HangfireJobId"] = context.BackgroundJob.Id,
            ["Provider"] = providerName,
            ["Country"] = country
        });

        ExecutionContextAccessor.CurrentHangfireJobId = context.BackgroundJob.Id;
        await _crawlerService.RunCrawlAsync(providerName, country, cancellationToken);
    }
}
