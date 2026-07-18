using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Application.Options;

namespace WebApp;

/// <summary>
/// Self-ping loop that keeps a free-tier host (Render/Azure App Service Free, both of which spin
/// down an app after a stretch of no inbound HTTP traffic) from sleeping. WebApp's counterpart to
/// <c>Infrastructure.Scheduling.HangfireKeepAliveExecutor</c> - deliberately a plain
/// <see cref="BackgroundService"/> instead of a Hangfire recurring job, since WebApp never calls
/// <c>AddHangfireServer()</c> (it only enqueues/manages jobs against the shared storage, it never
/// executes one) and so has no Hangfire server in-process that could ever run one. No-ops
/// (never pings) if neither <see cref="KeepAliveOptions.PingUrl"/> nor Render's own
/// <c>RENDER_EXTERNAL_URL</c> is set - there's nothing meaningful to ping in local dev/the Aspire
/// AppHost.
/// </summary>
public sealed class KeepAliveBackgroundService : BackgroundService
{
    private static readonly TimeSpan PingInterval = TimeSpan.FromMinutes(5);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly KeepAliveOptions _options;
    private readonly ILogger<KeepAliveBackgroundService> _logger;

    public KeepAliveBackgroundService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IOptions<KeepAliveOptions> options,
        ILogger<KeepAliveBackgroundService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pingUrl = _options.PingUrl;
        if (string.IsNullOrWhiteSpace(pingUrl))
        {
            var externalUrl = _configuration["RENDER_EXTERNAL_URL"];
            if (string.IsNullOrWhiteSpace(externalUrl))
            {
                _logger.LogDebug(
                    "No self-ping URL configured (KeepAlive:PingUrl / RENDER_EXTERNAL_URL) - keep-alive loop not started");
                return;
            }

            pingUrl = $"{externalUrl.TrimEnd('/')}/alive";
        }

        using var timer = new PeriodicTimer(PingInterval);
        do
        {
            try
            {
                var client = _httpClientFactory.CreateClient("SelfPing");
                using var response = await client.GetAsync(pingUrl, stoppingToken);
                response.EnsureSuccessStatusCode();
                _logger.LogDebug("Self-ping to {Url} returned {StatusCode}", pingUrl, (int)response.StatusCode);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Self-ping to {Url} failed", pingUrl);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
