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
/// Coordinates a full news-API crawl run: for every enabled country/provider/endpoint
/// (database-backed - see <see cref="ICrawlCountryRepository"/>/<see cref="IProviderScheduleRepository"/>/
/// <see cref="ICrawlFeedRepository"/>), calls its endpoint via <see cref="INewsApiProvider"/>, then
/// deduplicates and persists the normalized articles via <see cref="INewsArticleRepository"/> (the
/// same repository/dedup path RSS uses). The <see cref="NewsCrawlerOrchestrator"/> counterpart for
/// JSON APIs - deliberately a separate orchestrator/lock-namespace/options-section rather than
/// folded into the RSS one, since the two fetch shapes (a list of feeds per provider vs a single
/// rate-limited endpoint per provider) don't share a request loop even though they share
/// everything downstream of "normalized article".
/// </summary>
public sealed class NewsApiCrawlerOrchestrator : INewsApiCrawlerService
{
    private readonly IEnumerable<INewsApiProvider> _providers;
    private readonly INewsArticleRepository _articleRepository;
    private readonly IArticleFingerprintRepository _fingerprintRepository;
    private readonly ICrawlHistoryRepository _historyRepository;
    private readonly ICrawlLockRepository _lockRepository;
    private readonly IErrorLogRepository _errorLogRepository;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ICrawlCountryRepository _countryRepository;
    private readonly IProviderScheduleRepository _scheduleRepository;
    private readonly ICrawlFeedRepository _feedRepository;
    private readonly IEnumerable<IArticleNormalizer> _normalizers;
    private readonly NewsApiCrawlerOptions _options;
    private readonly ILogger<NewsApiCrawlerOrchestrator> _logger;
    private readonly string _ownerId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    public NewsApiCrawlerOrchestrator(
        IEnumerable<INewsApiProvider> providers,
        INewsArticleRepository articleRepository,
        IArticleFingerprintRepository fingerprintRepository,
        ICrawlHistoryRepository historyRepository,
        ICrawlLockRepository lockRepository,
        IErrorLogRepository errorLogRepository,
        IHostEnvironment hostEnvironment,
        ICrawlCountryRepository countryRepository,
        IProviderScheduleRepository scheduleRepository,
        ICrawlFeedRepository feedRepository,
        IEnumerable<IArticleNormalizer> normalizers,
        IOptions<NewsApiCrawlerOptions> options,
        ILogger<NewsApiCrawlerOrchestrator> logger)
    {
        _providers = providers;
        _articleRepository = articleRepository;
        _fingerprintRepository = fingerprintRepository;
        _historyRepository = historyRepository;
        _lockRepository = lockRepository;
        _errorLogRepository = errorLogRepository;
        _hostEnvironment = hostEnvironment;
        _countryRepository = countryRepository;
        _scheduleRepository = scheduleRepository;
        _feedRepository = feedRepository;
        _normalizers = normalizers;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Runs every enabled provider. Locking mirrors <see cref="NewsCrawlerOrchestrator"/>: per (provider, country), not global and not per-provider-alone, so two country-schedules of the same provider fetch in parallel across Hangfire ticks without starving each other.</summary>
    public Task<CrawlHistory> RunCrawlAsync(CancellationToken cancellationToken) =>
        RunCrawlAsync(candidateFilter: null, cancellationToken);

    /// <inheritdoc cref="INewsApiCrawlerService.RunCrawlAsync(string, string, CancellationToken)" />
    public Task<CrawlHistory> RunCrawlAsync(string provider, string country, CancellationToken cancellationToken) =>
        RunCrawlAsync(
            cp => string.Equals(cp.Provider.Name, provider, StringComparison.OrdinalIgnoreCase) && string.Equals(cp.Country, country, StringComparison.OrdinalIgnoreCase),
            cancellationToken);

    /// <summary>One provider together with the country its schedule belongs to - the database is the source of truth, this is just the flattened-for-iteration shape, same pattern as <see cref="NewsCrawlerOrchestrator"/>'s own <c>CountryProvider</c>.</summary>
    private readonly record struct CountryProvider(string Country, NewsApiProviderOptions Provider);

    private async Task<CrawlHistory> RunCrawlAsync(Func<CountryProvider, bool>? candidateFilter, CancellationToken cancellationToken)
    {
        var countries = await _countryRepository.GetAllAsync(CrawlPipeline.Api, cancellationToken);
        var enabledCountryNames = new HashSet<string>(
            countries.Where(c => c.Enabled).Select(c => c.Name), StringComparer.OrdinalIgnoreCase);

        var schedules = await _scheduleRepository.GetAllAsync(CrawlPipeline.Api, cancellationToken);
        var endpoints = await _feedRepository.GetAllAsync(CrawlPipeline.Api, cancellationToken);
        var endpointsByProviderCountry = endpoints
            .GroupBy(e => NormalizeKey(e.Provider, e.Country))
            .ToDictionary(g => g.Key, g => (IReadOnlyList<CrawlFeed>)g.ToList());
        var endpointsByProviderOnly = endpoints
            .GroupBy(e => e.Provider, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<CrawlFeed>)g.ToList(), StringComparer.OrdinalIgnoreCase);

        var candidates = schedules
            .Where(s => s.Enabled && enabledCountryNames.Contains(s.Country))
            .Select(s => new CountryProvider(s.Country, BuildProviderOptions(s, endpointsByProviderCountry, endpointsByProviderOnly)))
            .Where(cp => candidateFilter is null || candidateFilter(cp))
            .ToList();

        var lockedProviders = await ProviderLockCoordinator.AcquireAsync(
            candidates,
            cp => $"{cp.Provider.Name}::{cp.Country}",
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
                lockedProviders, cp => $"{cp.Provider.Name}::{cp.Country}", ProviderLockName, _lockRepository, _ownerId, CancellationToken.None);
        }
    }

    private static (string Provider, string Country) NormalizeKey(string provider, string country) =>
        (provider.ToUpperInvariant(), country.ToUpperInvariant());

    private string ProviderLockName(string providerCountryKey) => $"{_options.LockName}:{providerCountryKey}";

    // Same Provider+Country-first-with-Provider-only-fallback reasoning as
    // NewsCrawlerOrchestrator.BuildProviderOptions - see that method's own doc comment.
    private static NewsApiProviderOptions BuildProviderOptions(
        ProviderSchedule schedule,
        IReadOnlyDictionary<(string Provider, string Country), IReadOnlyList<CrawlFeed>> endpointsByProviderCountry,
        IReadOnlyDictionary<string, IReadOnlyList<CrawlFeed>> endpointsByProviderOnly)
    {
        if (!endpointsByProviderCountry.TryGetValue(NormalizeKey(schedule.Provider, schedule.Country), out var providerEndpoints))
        {
            endpointsByProviderOnly.TryGetValue(schedule.Provider, out providerEndpoints);
        }

        return new NewsApiProviderOptions
        {
            Name = schedule.Provider,
            Enabled = schedule.Enabled,
            Cron = schedule.Cron,
            BaseUrl = schedule.BaseUrl ?? string.Empty,
            AuthType = schedule.AuthType ?? Domain.Enums.ApiAuthType.QueryParameter,
            AuthParamName = schedule.AuthParamName ?? "apiKey",
            TimeoutSeconds = schedule.TimeoutSeconds ?? 120,
            Endpoints = (providerEndpoints ?? [])
                .Where(e => e.Enabled)
                .Select(e => new NewsApiEndpointOptions
                {
                    Name = e.Name,
                    Endpoint = e.Url,
                    QueryParameters = e.QueryParameters ?? [],
                    Category = e.Category,
                    Language = e.Language,
                    Enabled = e.Enabled
                })
                .ToList()
        };
    }

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
                        _fingerprintRepository,
                        result.Articles.Take(_options.BatchSize).Select(a => a with { Country = country }),
                        _normalizers,
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
