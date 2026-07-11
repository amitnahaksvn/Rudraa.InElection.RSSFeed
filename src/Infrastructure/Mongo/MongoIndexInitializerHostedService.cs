using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Application.Abstractions;
using Application.Options;

namespace Infrastructure.Mongo;

/// <summary>
/// Ensures every Mongo index (and, implicitly, every collection) exists before the host starts
/// serving requests/ticks. Registered by <c>AddInfrastructure</c>, so it runs automatically
/// against a brand new database, creating the required collections/indexes with no manual step.
/// </summary>
public sealed class MongoIndexInitializerHostedService : IHostedService
{
    private readonly INewsArticleRepository _articles;
    private readonly IArticleFingerprintRepository _articleFingerprints;
    private readonly ICrawlHistoryRepository _history;
    private readonly ICrawlLockRepository _locks;
    private readonly IRssRawResponseRepository _rawResponses;
    private readonly IFeedSourceRepository _feedSources;
    private readonly IFeedErrorLogRepository _feedErrorLogs;
    private readonly IErrorLogRepository _errorLogs;
    private readonly ISocialMediaSourceRepository _socialMediaSources;
    private readonly IProviderScheduleRepository _providerSchedules;
    private readonly NewsCrawlerOptions _options;
    private readonly ILogger<MongoIndexInitializerHostedService> _logger;

    public MongoIndexInitializerHostedService(
        INewsArticleRepository articles,
        IArticleFingerprintRepository articleFingerprints,
        ICrawlHistoryRepository history,
        ICrawlLockRepository locks,
        IRssRawResponseRepository rawResponses,
        IFeedSourceRepository feedSources,
        IFeedErrorLogRepository feedErrorLogs,
        IErrorLogRepository errorLogs,
        ISocialMediaSourceRepository socialMediaSources,
        IProviderScheduleRepository providerSchedules,
        IOptions<NewsCrawlerOptions> options,
        ILogger<MongoIndexInitializerHostedService> logger)
    {
        _articles = articles;
        _articleFingerprints = articleFingerprints;
        _history = history;
        _locks = locks;
        _rawResponses = rawResponses;
        _feedSources = feedSources;
        _feedErrorLogs = feedErrorLogs;
        _errorLogs = errorLogs;
        _socialMediaSources = socialMediaSources;
        _providerSchedules = providerSchedules;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Ensuring MongoDB indexes");
        await _articles.EnsureIndexesAsync(cancellationToken);
        await _articleFingerprints.EnsureIndexesAsync(cancellationToken);
        await _history.EnsureIndexesAsync(cancellationToken);
        await _locks.EnsureIndexesAsync(cancellationToken);
        await _rawResponses.EnsureIndexesAsync(_options.RawResponseRetention, cancellationToken);
        await _feedSources.EnsureIndexesAsync(cancellationToken);
        await _feedErrorLogs.EnsureIndexesAsync(cancellationToken);
        await _errorLogs.EnsureIndexesAsync(cancellationToken);
        await _socialMediaSources.EnsureIndexesAsync(cancellationToken);
        await _providerSchedules.EnsureIndexesAsync(cancellationToken);
        _logger.LogInformation("MongoDB indexes ready");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
