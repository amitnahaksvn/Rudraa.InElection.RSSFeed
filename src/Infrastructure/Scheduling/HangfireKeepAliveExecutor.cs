using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Application.Options;
using Application.Services;

namespace Infrastructure.Scheduling;

/// <summary>
/// Self-ping job that keeps a free-tier host (Render's Web Service free tier spins down after
/// ~15 minutes with no inbound HTTP traffic) from sleeping, without needing a third-party uptime
/// monitor. Pings this same process's own public URL - an outbound request that round-trips back
/// in through Render's edge as ordinary inbound traffic, resetting the inactivity timer - rather
/// than doing anything purely internal, since a container that's already spun down can't run its
/// own timer to wake itself; only real inbound traffic prevents the spin-down in the first place.
/// No-ops outside Render (local dev, docker-compose, the Aspire AppHost) where neither
/// <see cref="KeepAliveOptions.PingUrl"/> nor Render's own <c>RENDER_EXTERNAL_URL</c> environment
/// variable is set - there's nothing meaningful to ping.
///
/// Tagged onto its own dedicated "keepalive" queue - listed first in
/// <see cref="Application.Options.HangfireOptions.Queues"/>'s priority order, ahead of every
/// content-crawling queue - rather than sharing "default" with the raw-response-cleanup/error-
/// notification jobs. Confirmed in production: a burst of 100+ freshly-due rss/api crawl jobs
/// (the normal shape right after any wake-up) can occupy this app's small WorkerCount for
/// minutes; sharing a lower-priority queue let this job's own once-a-minute deadline get missed
/// under that load, letting the free-tier host spin back down anyway despite the self-ping
/// itself working correctly every time it did get a turn.
/// </summary>
[Queue("keepalive")]
public sealed class HangfireKeepAliveExecutor
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly KeepAliveOptions _options;
    private readonly JobExecutionLogger _executionLogger;
    private readonly ILogger<HangfireKeepAliveExecutor> _logger;

    public HangfireKeepAliveExecutor(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IOptions<KeepAliveOptions> options,
        JobExecutionLogger executionLogger,
        ILogger<HangfireKeepAliveExecutor> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _options = options.Value;
        _executionLogger = executionLogger;
        _logger = logger;
    }

    [JobDisplayName("Keep-alive self-ping")]
    public async Task RunAsync(PerformContext context, CancellationToken cancellationToken)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object> { ["HangfireJobId"] = context.BackgroundJob.Id });

        var pingUrl = _options.PingUrl;
        if (string.IsNullOrWhiteSpace(pingUrl))
        {
            var externalUrl = _configuration["RENDER_EXTERNAL_URL"];
            if (string.IsNullOrWhiteSpace(externalUrl))
            {
                _logger.LogDebug(
                    "No self-ping URL configured (KeepAlive:PingUrl / RENDER_EXTERNAL_URL) - skipping, not running on Render");
                return;
            }

            pingUrl = $"{externalUrl.TrimEnd('/')}/alive";
        }

        // Only a real ping attempt gets a JobExecutionLog row - the "not on Render" no-op above
        // would otherwise write one every single minute forever in local dev/docker-compose, for
        // no diagnostic value.
        await _executionLogger.RunAsync(
            HangfireJobIds.KeepAlivePing(_options.AppName),
            "Keep-alive self-ping",
            context.BackgroundJob.Id,
            async () =>
            {
                var client = _httpClientFactory.CreateClient("SelfPing");
                using var response = await client.GetAsync(pingUrl, cancellationToken);
                response.EnsureSuccessStatusCode();

                _logger.LogDebug("Self-ping to {Url} returned {StatusCode}", pingUrl, (int)response.StatusCode);
            },
            cancellationToken);
    }
}
