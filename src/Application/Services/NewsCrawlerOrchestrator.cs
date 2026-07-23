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
/// Coordinates a full crawl run: for every enabled country/provider/feed (database-backed - see
/// <see cref="ICrawlCountryRepository"/>/<see cref="IProviderScheduleRepository"/>/
/// <see cref="ICrawlFeedRepository"/>), fetches and normalizes articles via
/// <see cref="IRssProvider"/>, then deduplicates and persists them via
/// <see cref="INewsArticleRepository"/>. Contains no MongoDB or HTTP/XML specifics itself.
/// </summary>
public sealed class NewsCrawlerOrchestrator : INewsCrawlerService
{
    private readonly IEnumerable<IRssProvider> _providers;
    private readonly INewsArticleRepository _articleRepository;
    private readonly IArticleFingerprintRepository _fingerprintRepository;
    private readonly ICrawlHistoryRepository _historyRepository;
    private readonly ICrawlLockRepository _lockRepository;
    private readonly IRssRawResponseRepository _rawResponseRepository;
    private readonly IErrorLogRepository _errorLogRepository;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ICrawlCountryRepository _countryRepository;
    private readonly IProviderScheduleRepository _scheduleRepository;
    private readonly ICrawlFeedRepository _feedRepository;
    private readonly IEnumerable<IArticleNormalizer> _normalizers;
    private readonly NewsCrawlerOptions _options;
    private readonly ILogger<NewsCrawlerOrchestrator> _logger;
    private readonly string _ownerId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    public NewsCrawlerOrchestrator(
        IEnumerable<IRssProvider> providers,
        INewsArticleRepository articleRepository,
        IArticleFingerprintRepository fingerprintRepository,
        ICrawlHistoryRepository historyRepository,
        ICrawlLockRepository lockRepository,
        IRssRawResponseRepository rawResponseRepository,
        IErrorLogRepository errorLogRepository,
        IHostEnvironment hostEnvironment,
        ICrawlCountryRepository countryRepository,
        IProviderScheduleRepository scheduleRepository,
        ICrawlFeedRepository feedRepository,
        IEnumerable<IArticleNormalizer> normalizers,
        IOptions<NewsCrawlerOptions> options,
        ILogger<NewsCrawlerOrchestrator> logger)
    {
        _providers = providers;
        _articleRepository = articleRepository;
        _fingerprintRepository = fingerprintRepository;
        _historyRepository = historyRepository;
        _lockRepository = lockRepository;
        _rawResponseRepository = rawResponseRepository;
        _errorLogRepository = errorLogRepository;
        _hostEnvironment = hostEnvironment;
        _countryRepository = countryRepository;
        _scheduleRepository = scheduleRepository;
        _feedRepository = feedRepository;
        _normalizers = normalizers;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Runs a full crawl. The distributed lock is scoped per provider ("{LockName}:{Provider}"),
    /// not globally: the invariant worth enforcing is that the *same* provider is never crawled
    /// by two runs at once (scheduled tick vs manual trigger vs another instance) - different
    /// providers crawling in parallel is fine and, with every provider's Hangfire job firing on
    /// the same cron tick, necessary; a single global lock would let exactly one provider win
    /// per tick and starve the rest. Providers whose lock is held are skipped individually;
    /// only when every requested provider is lock-skipped is a non-persisted
    /// <see cref="CrawlStatus.Skipped"/> result returned.
    /// </summary>
    public Task<CrawlHistory> RunCrawlAsync(CancellationToken cancellationToken) =>
        RunCrawlAsync(providerFilter: null, cancellationToken);

    /// <inheritdoc cref="INewsCrawlerService.RunCrawlAsync(IReadOnlyCollection{string}, CancellationToken)" />
    public Task<CrawlHistory> RunCrawlAsync(IReadOnlyCollection<string> providerNames, CancellationToken cancellationToken) =>
        RunCrawlAsync(p => providerNames.Contains(p.Name, StringComparer.OrdinalIgnoreCase), cancellationToken);

    /// <summary>One provider together with the country group it belongs to - the database (<see cref="ICrawlCountryRepository"/>/<see cref="IProviderScheduleRepository"/>/<see cref="ICrawlFeedRepository"/>) is the source of truth, this is just the flattened-for-iteration shape.</summary>
    private readonly record struct CountryProvider(string Country, RssProviderOptions Provider);

    private async Task<CrawlHistory> RunCrawlAsync(Func<RssProviderOptions, bool>? providerFilter, CancellationToken cancellationToken)
    {
        var countries = await _countryRepository.GetAllAsync(CrawlPipeline.Rss, cancellationToken);
        var enabledCountryNames = new HashSet<string>(
            countries.Where(c => c.Enabled).Select(c => c.Name), StringComparer.OrdinalIgnoreCase);

        var schedules = await _scheduleRepository.GetAllAsync(CrawlPipeline.Rss, cancellationToken);
        var feeds = await _feedRepository.GetAllAsync(CrawlPipeline.Rss, cancellationToken);
        var feedsByProvider = feeds
            .GroupBy(f => f.Provider, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<CrawlFeed>)g.ToList(), StringComparer.OrdinalIgnoreCase);

        var candidates = schedules
            .Where(s => s.Enabled && enabledCountryNames.Contains(s.Country))
            .Select(s => new CountryProvider(s.Country, BuildProviderOptions(s, feedsByProvider)))
            .Where(cp => providerFilter is null || providerFilter(cp.Provider))
            .ToList();

        var lockedProviders = await ProviderLockCoordinator.AcquireAsync(
            candidates,
            cp => cp.Provider.Name,
            ProviderLockName,
            _lockRepository,
            _ownerId,
            _options.LockTtl,
            _logger,
            "Crawl skipped for provider {Provider} - lock '{Lock}' is held by another run",
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

    private static RssProviderOptions BuildProviderOptions(ProviderSchedule schedule, IReadOnlyDictionary<string, IReadOnlyList<CrawlFeed>> feedsByProvider)
    {
        feedsByProvider.TryGetValue(schedule.Provider, out var providerFeeds);

        return new RssProviderOptions
        {
            Name = schedule.Provider,
            Enabled = schedule.Enabled,
            Cron = schedule.Cron,
            SaveRawResponses = schedule.SaveRawResponses,
            Feeds = (providerFeeds ?? [])
                .Where(f => f.Enabled)
                .Select(f => new RssFeedOptions
                {
                    Name = f.Name,
                    Url = f.Url,
                    Category = f.Category,
                    Language = f.Language,
                    Enabled = f.Enabled,
                    DefaultImageUrl = f.DefaultImageUrl
                })
                .ToList()
        };
    }

    private async Task<CrawlHistory> RunLockedAsync(IReadOnlyList<CountryProvider> lockedProviders, CancellationToken cancellationToken)
    {
        var history = new CrawlHistory
        {
            Pipeline = CrawlPipeline.Rss,
            Providers = lockedProviders.Select(cp => cp.Provider.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            StartTime = DateTimeOffset.UtcNow,
            Status = CrawlStatus.Running
        };
        history.Id = await _historyRepository.InsertAsync(history, cancellationToken);

        var failedFeeds = new List<string>();
        var errors = new List<ErrorNotification>();
        var newCount = 0;
        var feedCount = 0;
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
                        "No IRssProvider registered for configured provider '{Provider}' - skipping",
                        providerOptions.Name);
                    continue;
                }

                var enabledFeeds = providerOptions.Feeds.Where(f => f.Enabled).ToList();
                if (enabledFeeds.Count == 0)
                {
                    continue;
                }

                _logger.LogInformation("[{RunId}] Started: {Provider} ({FeedCount} feeds)", history.Id, provider.Name, enabledFeeds.Count);
                var providerStopwatch = Stopwatch.StartNew();
                var providerNewCount = 0;
                var providerFailedCount = 0;

                var results = await provider.FetchAllFeedsAsync(enabledFeeds, cancellationToken);
                feedCount += results.Count;

                var saveRawResponses = _options.SaveRawResponses && providerOptions.SaveRawResponses;

                foreach (var result in results)
                {
                    if (saveRawResponses)
                    {
                        await _rawResponseRepository.InsertAsync(
                            new RssRawResponse
                            {
                                Provider = provider.Name,
                                FeedName = result.FeedName,
                                FeedUrl = result.FeedUrl,
                                FetchedAt = result.FetchedAt,
                                HttpStatusCode = result.HttpStatusCode,
                                RawXml = result.RawXml,
                                ContentHash = result.ContentHash,
                                ParseSucceeded = result.Success,
                                ParseError = result.Error,
                                ProcessingDurationMs = result.ProcessingDurationMs,
                                CreatedAt = DateTimeOffset.UtcNow
                            },
                            cancellationToken);
                    }

                    if (!result.Success)
                    {
                        failedFeeds.Add($"{provider.Name}/{result.FeedName}");
                        providerFailedCount++;
                        _logger.LogError("Feed failed: {Provider}/{Feed} - {Error}", provider.Name, result.FeedName, result.Error);
                        errors.Add(new ErrorNotification
                        {
                            Environment = _hostEnvironment.EnvironmentName,
                            ApplicationName = _hostEnvironment.ApplicationName,
                            Provider = provider.Name,
                            FeedOrApiName = result.FeedName,
                            Country = country,
                            SourceUrl = result.FeedUrl,
                            Operation = "RSS Feed Fetch",
                            ExceptionType = result.ExceptionType ?? "Unknown",
                            ErrorMessage = result.Error ?? "Unknown error",
                            StackTrace = result.StackTrace,
                            InnerException = result.InnerException,
                            HttpStatusCode = result.HttpStatusCode,
                            RequestUrl = result.FeedUrl,
                            ResponseBody = result.RawXml,
                            CorrelationId = history.Id,
                            HangfireJobId = ExecutionContextAccessor.CurrentHangfireJobId,
                            ExecutionDuration = TimeSpan.FromMilliseconds(result.ProcessingDurationMs)
                        });
                        continue;
                    }

                    var inserted = await PersistArticlesAsync(
                        result.Articles.Take(_options.BatchSize).Select(a => a with { Country = country }),
                        cancellationToken);

                    newCount += inserted;
                    providerNewCount += inserted;

                    _logger.LogDebug(
                        "Feed completed: {Provider}/{Feed} - {New} new",
                        provider.Name, result.FeedName, inserted);
                }

                providerStopwatch.Stop();
                _logger.LogInformation(
                    "[{RunId}] Completed: {Provider} - {New} new, {Failed} failed ({Elapsed})",
                    history.Id, provider.Name, providerNewCount, providerFailedCount, providerStopwatch.Elapsed);
            }

            history.Status = failedFeeds.Count == 0 ? CrawlStatus.Completed : CrawlStatus.CompletedWithErrors;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Same reasoning as BaseRssProvider: a stray OperationCanceledException from some
            // inner timeout (not our own token) must be recorded as a failed run, not crash the
            // caller. Only propagates uncaught when cancellationToken itself was cancelled.
            runError = ex.Message;
            history.Status = CrawlStatus.Failed;
            _logger.LogError(ex, "[{RunId}] Crawl run failed unexpectedly", history.Id);
            errors.Add(ErrorNotification.FromException(
                ex,
                _hostEnvironment.EnvironmentName,
                _hostEnvironment.ApplicationName,
                operation: "Crawl Run",
                correlationId: history.Id,
                hangfireJobId: ExecutionContextAccessor.CurrentHangfireJobId));
        }

        history.EndTime = DateTimeOffset.UtcNow;
        history.Duration = history.EndTime - history.StartTime;
        history.FeedCount = feedCount;
        history.NewArticles = newCount;
        history.FailedFeeds = failedFeeds;
        history.Error = runError;

        await _historyRepository.UpdateAsync(history, cancellationToken);

        _logger.LogInformation(
            "[{RunId}] Crawl completed: {Status} - {New} new, {Failed} failed ({Duration})",
            history.Id, history.Status, newCount, failedFeeds.Count, history.Duration);

        await ErrorLogRecorder.RecordIfAnyAsync(_errorLogRepository, errors, _logger, history.Id, cancellationToken);

        return history;
    }

    private Task<int> PersistArticlesAsync(
        IEnumerable<NormalizedArticle> articles,
        CancellationToken cancellationToken) =>
        ArticlePersister.PersistAsync(_articleRepository, _fingerprintRepository, articles, _normalizers, _logger, cancellationToken);
}
