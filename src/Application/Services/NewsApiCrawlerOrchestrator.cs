using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Application.Abstractions;
using Application.Models;
using Application.Options;
using Domain.Entities;
using Domain.Enums;

namespace Application.Services;

/// <summary>
/// Coordinates a full news-API crawl run: for every enabled provider in configuration, calls its
/// endpoint via <see cref="INewsApiProvider"/>, then deduplicates and persists the normalized
/// articles via <see cref="INewsArticleRepository"/> (the same repository/dedup path RSS uses).
/// The <see cref="NewsCrawlerOrchestrator"/> counterpart for JSON APIs - deliberately a separate
/// orchestrator/lock-namespace/options-section rather than folded into the RSS one, since the two
/// fetch shapes (a list of feeds per provider vs a single rate-limited endpoint per provider)
/// don't share a request loop even though they share everything downstream of "normalized article".
/// </summary>
public sealed class NewsApiCrawlerOrchestrator : INewsApiCrawlerService
{
    private readonly IEnumerable<INewsApiProvider> _providers;
    private readonly INewsArticleRepository _articleRepository;
    private readonly IFilteredArticleRepository _filteredArticleRepository;
    private readonly ICrawlHistoryRepository _historyRepository;
    private readonly ICrawlLockRepository _lockRepository;
    private readonly IErrorLogRepository _errorLogRepository;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IProviderScheduleRepository _scheduleRepository;
    private readonly IEnumerable<IArticleNormalizer> _normalizers;
    private readonly NewsApiCrawlerOptions _options;
    private readonly NewsFilterOptions _newsFilterOptions;
    private readonly ILogger<NewsApiCrawlerOrchestrator> _logger;
    private readonly string _ownerId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    public NewsApiCrawlerOrchestrator(
        IEnumerable<INewsApiProvider> providers,
        INewsArticleRepository articleRepository,
        IFilteredArticleRepository filteredArticleRepository,
        ICrawlHistoryRepository historyRepository,
        ICrawlLockRepository lockRepository,
        IErrorLogRepository errorLogRepository,
        IHostEnvironment hostEnvironment,
        IProviderScheduleRepository scheduleRepository,
        IEnumerable<IArticleNormalizer> normalizers,
        IOptions<NewsApiCrawlerOptions> options,
        IOptions<NewsFilterOptions> newsFilterOptions,
        ILogger<NewsApiCrawlerOrchestrator> logger)
    {
        _providers = providers;
        _articleRepository = articleRepository;
        _filteredArticleRepository = filteredArticleRepository;
        _historyRepository = historyRepository;
        _lockRepository = lockRepository;
        _errorLogRepository = errorLogRepository;
        _hostEnvironment = hostEnvironment;
        _scheduleRepository = scheduleRepository;
        _normalizers = normalizers;
        _options = options.Value;
        _newsFilterOptions = newsFilterOptions.Value;
        _logger = logger;
    }

    /// <summary>Runs every enabled provider. Locking mirrors <see cref="NewsCrawlerOrchestrator"/>: per-provider, not global, so providers fetch in parallel across Hangfire ticks without starving each other.</summary>
    public Task<CrawlHistory> RunCrawlAsync(CancellationToken cancellationToken) =>
        RunCrawlAsync(providerFilter: null, cancellationToken);

    /// <inheritdoc cref="INewsApiCrawlerService.RunCrawlAsync(IReadOnlyCollection{string}, CancellationToken)" />
    public Task<CrawlHistory> RunCrawlAsync(IReadOnlyCollection<string> providerNames, CancellationToken cancellationToken) =>
        RunCrawlAsync(p => providerNames.Contains(p.Name, StringComparer.OrdinalIgnoreCase), cancellationToken);

    /// <summary>One provider together with the country group it was configured under - <see cref="NewsApiCrawlerOptions.Countries"/> is the source of truth, this is just the flattened-for-iteration shape, same pattern as <see cref="NewsCrawlerOrchestrator"/>'s own <c>CountryProvider</c>.</summary>
    private readonly record struct CountryProvider(string Country, NewsApiProviderOptions Provider);

    private async Task<CrawlHistory> RunCrawlAsync(Func<NewsApiProviderOptions, bool>? providerFilter, CancellationToken cancellationToken)
    {
        // ProviderSchedule (database) is the live source of truth for whether a provider is
        // enabled - see NewsCrawlerOrchestrator's own identical comment for why the file's Enabled
        // is only a fallback now, not the source of truth.
        var schedules = await _scheduleRepository.GetAllAsync(CrawlPipeline.Api, cancellationToken);
        var scheduleByProvider = schedules.ToDictionary(s => s.Provider, StringComparer.OrdinalIgnoreCase);

        var candidates = _options.Countries
            .Where(c => c.Enabled)
            .SelectMany(c => c.Providers.Select(p => new CountryProvider(c.Name, p)))
            .Where(cp => IsEnabled(cp.Provider, scheduleByProvider) && (providerFilter is null || providerFilter(cp.Provider)))
            .ToList();

        var lockedProviders = await ProviderLockCoordinator.AcquireAsync(
            candidates,
            cp => cp.Provider.Name,
            ProviderLockName,
            _lockRepository,
            _ownerId,
            _options.LockTtl,
            _logger,
            "News API crawl skipped for provider {Provider} - lock '{Lock}' is held by another run",
            cancellationToken);

        if (lockedProviders.Count == 0)
        {
            var now = DateTimeOffset.UtcNow;
            return new CrawlHistory { StartTime = now, EndTime = now, Duration = TimeSpan.Zero, Status = CrawlStatus.Skipped };
        }

        try
        {
            return await RunLockedAsync(lockedProviders, cancellationToken);
        }
        finally
        {
            await ProviderLockCoordinator.ReleaseAsync(
                lockedProviders, cp => cp.Provider.Name, ProviderLockName, _lockRepository, _ownerId, CancellationToken.None);
        }
    }

    private string ProviderLockName(string providerName) => $"{_options.LockName}:{providerName}";

    private static bool IsEnabled(NewsApiProviderOptions provider, IReadOnlyDictionary<string, ProviderSchedule> schedules) =>
        schedules.TryGetValue(provider.Name, out var schedule) ? schedule.Enabled : provider.Enabled;

    private async Task<CrawlHistory> RunLockedAsync(IReadOnlyList<CountryProvider> lockedProviders, CancellationToken cancellationToken)
    {
        var history = new CrawlHistory
        {
            Pipeline = CrawlPipeline.Api,
            Providers = lockedProviders.Select(cp => cp.Provider.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            StartTime = DateTimeOffset.UtcNow,
            Status = CrawlStatus.Running
        };
        history.Id = await _historyRepository.InsertAsync(history, cancellationToken);

        var failedEndpoints = new List<string>();
        var errors = new List<ErrorNotification>();
        var newCount = 0;
        var endpointCount = 0;
        string? runError = null;

        try
        {
            foreach (var (country, providerOptions) in lockedProviders)
            {
                var provider = _providers.FirstOrDefault(p =>
                    string.Equals(p.Name, providerOptions.Name, StringComparison.OrdinalIgnoreCase));

                if (provider is null)
                {
                    _logger.LogWarning(
                        "No INewsApiProvider registered for configured provider '{Provider}' - skipping",
                        providerOptions.Name);
                    continue;
                }

                var enabledEndpoints = providerOptions.Endpoints.Count(e => e.Enabled);
                if (enabledEndpoints == 0)
                {
                    continue;
                }

                _logger.LogInformation("[{RunId}] Started: {Provider} ({EndpointCount} endpoints)", history.Id, provider.Name, enabledEndpoints);
                var providerStopwatch = Stopwatch.StartNew();
                var providerNewCount = 0;
                var providerFailedCount = 0;

                var results = await provider.FetchAllEndpointsAsync(providerOptions, cancellationToken);
                endpointCount += results.Count;

                foreach (var result in results)
                {
                    if (!result.Success)
                    {
                        failedEndpoints.Add($"{provider.Name}/{result.EndpointName}");
                        providerFailedCount++;
                        _logger.LogError("News API endpoint failed: {Provider}/{Endpoint} - {Error}", provider.Name, result.EndpointName, result.Error);
                        errors.Add(new ErrorNotification
                        {
                            Environment = _hostEnvironment.EnvironmentName,
                            ApplicationName = _hostEnvironment.ApplicationName,
                            Provider = provider.Name,
                            FeedOrApiName = result.EndpointName,
                            Country = country,
                            SourceUrl = result.EndpointUrl,
                            Operation = "News API Fetch",
                            ExceptionType = result.ExceptionType ?? "Unknown",
                            ErrorMessage = result.Error ?? "Unknown error",
                            StackTrace = result.StackTrace,
                            InnerException = result.InnerException,
                            HttpStatusCode = result.HttpStatusCode,
                            RequestUrl = result.EndpointUrl,
                            ResponseBody = result.ResponseBody,
                            CorrelationId = history.Id,
                            HangfireJobId = ExecutionContextAccessor.CurrentHangfireJobId,
                            ExecutionDuration = TimeSpan.FromMilliseconds(result.ProcessingDurationMs)
                        });
                        continue;
                    }

                    var inserted = await ArticlePersister.PersistAsync(
                        _articleRepository,
                        _filteredArticleRepository,
                        result.Articles.Take(_options.BatchSize).Select(a => a with { Country = country }),
                        _normalizers,
                        _newsFilterOptions,
                        _logger,
                        cancellationToken);

                    newCount += inserted;
                    providerNewCount += inserted;

                    _logger.LogDebug(
                        "News API endpoint completed: {Provider}/{Endpoint} - {New} new",
                        provider.Name, result.EndpointName, inserted);
                }

                providerStopwatch.Stop();
                _logger.LogInformation(
                    "[{RunId}] Completed: {Provider} - {New} new, {Failed} failed ({Elapsed})",
                    history.Id, provider.Name, providerNewCount, providerFailedCount, providerStopwatch.Elapsed);
            }

            history.Status = failedEndpoints.Count == 0 ? CrawlStatus.Completed : CrawlStatus.CompletedWithErrors;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            runError = ex.Message;
            history.Status = CrawlStatus.Failed;
            _logger.LogError(ex, "[{RunId}] News API crawl run failed unexpectedly", history.Id);
            errors.Add(ErrorNotification.FromException(
                ex,
                _hostEnvironment.EnvironmentName,
                _hostEnvironment.ApplicationName,
                operation: "News API Crawl Run",
                correlationId: history.Id,
                hangfireJobId: ExecutionContextAccessor.CurrentHangfireJobId));
        }

        history.EndTime = DateTimeOffset.UtcNow;
        history.Duration = history.EndTime - history.StartTime;
        history.FeedCount = endpointCount;
        history.NewArticles = newCount;
        history.FailedFeeds = failedEndpoints;
        history.Error = runError;

        await _historyRepository.UpdateAsync(history, cancellationToken);

        _logger.LogInformation(
            "[{RunId}] Crawl completed: {Status} - {New} new, {Failed} failed ({Duration})",
            history.Id, history.Status, newCount, failedEndpoints.Count, history.Duration);

        await ErrorLogRecorder.RecordIfAnyAsync(_errorLogRepository, errors, _logger, history.Id, cancellationToken);

        return history;
    }
}
