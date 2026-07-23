using Mediator;
using Application.Abstractions;
using Application.Crawl.Dtos;
using Application.Models;
using Domain.Entities;
using Domain.Enums;

namespace Application.Crawl.Queries.GetCrawlReport;

/// <summary>Backs the crawl-report page's RSS/API tabs. <paramref name="From"/>/<paramref name="To"/> default to the trailing 7 days when omitted, so the page always has something to show the first time it's opened.</summary>
public sealed record GetCrawlReportQuery(CrawlPipeline Pipeline, DateTimeOffset? From, DateTimeOffset? To) : IRequest<CrawlReportDto>;

public sealed class GetCrawlReportQueryHandler : IRequestHandler<GetCrawlReportQuery, CrawlReportDto>
{
    // Generous ceiling on how many CrawlHistory docs one report aggregates in memory - comfortably
    // above any realistic window for this app's provider count/cron frequency, and a hard stop
    // against an accidentally huge date range turning this into an unbounded Mongo scan.
    private const int MaxRunsConsidered = 20_000;

    private readonly ICrawlHistoryRepository _history;
    private readonly IArticleFingerprintRepository _fingerprints;
    private readonly ICrawlJobStatusReader _statusReader;
    private readonly ICrawlCountryRepository _countryRepository;
    private readonly IProviderScheduleRepository _scheduleRepository;

    public GetCrawlReportQueryHandler(
        ICrawlHistoryRepository history,
        IArticleFingerprintRepository fingerprints,
        ICrawlJobStatusReader statusReader,
        ICrawlCountryRepository countryRepository,
        IProviderScheduleRepository scheduleRepository)
    {
        _history = history;
        _fingerprints = fingerprints;
        _statusReader = statusReader;
        _countryRepository = countryRepository;
        _scheduleRepository = scheduleRepository;
    }

    public async ValueTask<CrawlReportDto> Handle(GetCrawlReportQuery request, CancellationToken cancellationToken)
    {
        var to = request.To ?? DateTimeOffset.UtcNow;
        var from = request.From ?? to.AddDays(-7);

        var runs = await _history.GetFilteredAsync(
            new CrawlHistoryFilter(request.Pipeline, Provider: null, from, to, Skip: 0, Take: MaxRunsConsidered),
            cancellationToken);

        // "New" articles are sourced from ArticleFingerprints' own CrawledAt (a real count of
        // articles actually persisted) rather than trusting each run's self-reported counters.
        // There's no "Duplicate"/"Updated" figure at all - a duplicate is silently skipped at
        // persistence time and an existing article is never modified in place, so neither has
        // anything worth reporting.
        var sourceType = request.Pipeline == CrawlPipeline.Api ? ArticleSourceType.Api : ArticleSourceType.Rss;

        var newCounts = await _fingerprints.GetDailyProviderCountsAsync(sourceType, from, to, cancellationToken);
        var newArticlesByDay = newCounts.GroupBy(c => c.Date).ToDictionary(g => g.Key, g => g.Sum(c => c.Count));
        var newArticlesByProvider = newCounts
            .GroupBy(c => c.Provider, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.Count), StringComparer.OrdinalIgnoreCase);

        // Database-backed (ICrawlCountryRepository/IProviderScheduleRepository), same source of
        // truth the orchestrators and Provider Management page already use - one row per
        // (Country, Provider) schedule, so a provider scheduled under more than one country (e.g.
        // SerpApiGoogleNews) already shows up as its own row per country here, each with its own
        // Cron/TimeZone/next-run pulled from that country's own Hangfire job. NewArticles/run
        // counts/success rate below remain attributed per bare provider name, not per
        // (Provider, Country) - CrawlHistory.Providers and ArticleFingerprint carry no Country
        // dimension today, so a multi-country provider's activity figures stay commingled across
        // its country rows until those get one too; a known, separate follow-up.
        var countries = await _countryRepository.GetAllAsync(request.Pipeline, cancellationToken);
        var enabledCountryNames = new HashSet<string>(
            countries.Where(c => c.Enabled).Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
        var schedules = await _scheduleRepository.GetAllAsync(request.Pipeline, cancellationToken);
        var configured = schedules
            .Where(s => s.Enabled && enabledCountryNames.Contains(s.Country))
            .Select(s => (Country: s.Country, Provider: s.Provider))
            .ToList();

        var summary = BuildSummary(runs, newCounts.Sum(c => c.Count));
        var timeSeries = BuildTimeSeries(runs, from, to, newArticlesByDay);
        var providers = BuildProviderBreakdown(request.Pipeline, configured, runs, newArticlesByProvider);

        return new CrawlReportDto(request.Pipeline.ToString(), from, to, summary, timeSeries, providers);
    }

    private static CrawlReportSummaryDto BuildSummary(IReadOnlyList<CrawlHistory> runs, int newArticles)
    {
        var successful = runs.Count(r => r.Status == CrawlStatus.Completed);
        var withErrors = runs.Count(r => r.Status == CrawlStatus.CompletedWithErrors);
        var failed = runs.Count(r => r.Status == CrawlStatus.Failed);
        var skippedRuns = runs.Count(r => r.Status == CrawlStatus.Skipped);
        var failedFeeds = runs.Sum(r => r.FailedFeeds.Count);
        var totalRuns = runs.Count;
        var successRate = totalRuns == 0 ? 0 : Math.Round(successful * 100.0 / totalRuns, 1);

        return new CrawlReportSummaryDto(
            totalRuns,
            successful,
            withErrors,
            failed,
            skippedRuns,
            successRate,
            newArticles,
            failedFeeds);
    }

    private static IReadOnlyList<CrawlReportDailyPointDto> BuildTimeSeries(
        IReadOnlyList<CrawlHistory> runs,
        DateTimeOffset from,
        DateTimeOffset to,
        IReadOnlyDictionary<DateOnly, int> newArticlesByDay)
    {
        var byDay = runs.ToLookup(r => DateOnly.FromDateTime(r.StartTime.UtcDateTime));

        var points = new List<CrawlReportDailyPointDto>();
        for (var day = DateOnly.FromDateTime(from.UtcDateTime); day <= DateOnly.FromDateTime(to.UtcDateTime); day = day.AddDays(1))
        {
            var dayRuns = byDay[day].ToList();
            points.Add(new CrawlReportDailyPointDto(
                day,
                TotalRuns: dayRuns.Count,
                SuccessfulRuns: dayRuns.Count(r => r.Status == CrawlStatus.Completed),
                RunsWithErrors: dayRuns.Count(r => r.Status == CrawlStatus.CompletedWithErrors),
                FailedRuns: dayRuns.Count(r => r.Status == CrawlStatus.Failed),
                SkippedRuns: dayRuns.Count(r => r.Status == CrawlStatus.Skipped),
                NewArticles: newArticlesByDay.GetValueOrDefault(day),
                FailedFeeds: dayRuns.Sum(r => r.FailedFeeds.Count)));
        }

        return points;
    }

    private IReadOnlyList<CrawlReportProviderDto> BuildProviderBreakdown(
        CrawlPipeline pipeline,
        IReadOnlyList<(string Country, string Provider)> configured,
        IReadOnlyList<CrawlHistory> runs,
        IReadOnlyDictionary<string, int> newArticlesByProvider)
    {
        // Fanned out across many provider-countries concurrently, not sequentially - see
        // ICrawlJobStatusReader.GetStatuses's own doc comment for why that distinction matters at
        // this app's provider counts.
        var statuses = _statusReader.GetStatuses(pipeline, configured.Select(cp => (cp.Provider, cp.Country)).ToList());

        // Exact per-provider article/run attribution only applies to single-provider runs - the
        // normal case, since each provider's own scheduled Hangfire job crawls just that provider.
        // A manual "trigger everything" run (POST /api/crawl/trigger with no filter) can bundle
        // many providers into one CrawlHistory record; that activity still counts toward the
        // overall summary/time-series above, but is deliberately left out of any single
        // provider's row here rather than crediting the whole run's totals to every provider it
        // touched. Keyed by bare provider name, not (Provider, Country) - CrawlHistory.Providers
        // has no Country dimension (see this method's own caller for why that's an accepted,
        // separately-tracked limitation for now).
        var singleProviderRuns = runs
            .Where(r => r.Providers.Count == 1)
            .ToLookup(r => r.Providers[0], StringComparer.OrdinalIgnoreCase);

        var rows = new List<CrawlReportProviderDto>();
        foreach (var (country, providerName) in configured)
        {
            var providerRuns = singleProviderRuns[providerName].ToList();
            statuses.TryGetValue((providerName, country), out var status);

            // Failed-feed attribution stays exact even for multi-provider runs, since each entry
            // is its own "{Provider}/{Feed}" string regardless of how many providers ran together.
            var failedFeedPrefix = providerName + "/";
            var failedFeeds = runs.Sum(r => r.FailedFeeds.Count(f => f.StartsWith(failedFeedPrefix, StringComparison.OrdinalIgnoreCase)));

            var successful = providerRuns.Count(r => r.Status == CrawlStatus.Completed);
            var withErrors = providerRuns.Count(r => r.Status == CrawlStatus.CompletedWithErrors);
            var failed = providerRuns.Count(r => r.Status == CrawlStatus.Failed);
            var skipped = providerRuns.Count(r => r.Status == CrawlStatus.Skipped);
            var totalRuns = providerRuns.Count;

            rows.Add(new CrawlReportProviderDto(
                country,
                providerName,
                HasRun: totalRuns > 0,
                status?.Cron,
                status?.TimeZone,
                status?.NextExecution,
                status?.LastExecution,
                status?.LastJobState,
                status?.LastErrorMessage,
                totalRuns,
                successful,
                withErrors,
                failed,
                skipped,
                SuccessRatePercent: totalRuns == 0 ? 0 : Math.Round(successful * 100.0 / totalRuns, 1),
                NewArticles: newArticlesByProvider.GetValueOrDefault(providerName),
                failedFeeds));
        }

        return rows
            .OrderBy(r => r.Country, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Provider, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
