using Domain.Enums;

namespace Application.Abstractions;

/// <summary>
/// Creates/updates/removes and triggers recurring crawl jobs, for either pipeline (RSS or JSON
/// API - the two register under different job-id schemes, see <c>HangfireJobIds</c>). Every job
/// this manages does exactly one fixed, safe thing - crawl a specific, already-configured
/// provider-country schedule - parameterized only by schedule (never by arbitrary code), so this
/// is not a general-purpose job execution API. Every method is keyed by (Pipeline, Provider,
/// Country) together, not Provider alone, since the same provider class can be scheduled
/// independently for more than one country.
/// </summary>
public interface ICrawlJobTrigger
{
    /// <returns>The id of the job that was triggered.</returns>
    string TriggerNow(CrawlPipeline pipeline, string providerName, string country);

    /// <summary>
    /// Registers (or updates, if it already exists) a provider-country's recurring crawl job
    /// against Hangfire directly - the live half of a schedule edit; callers persisting the change
    /// (e.g. <c>ProviderSchedule</c>) do so separately.
    /// </summary>
    /// <returns>The id of the recurring job that was created/updated.</returns>
    string CreateOrUpdate(CrawlPipeline pipeline, string providerName, string country, string cronExpression, string timeZoneId);

    /// <summary>Removes a provider-country's recurring job if one exists - the live counterpart to disabling a schedule. A no-op if no job is currently registered for that provider-country.</summary>
    void Remove(CrawlPipeline pipeline, string providerName, string country);
}
