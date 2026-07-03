namespace Application.Abstractions;

/// <summary>
/// Creates/updates and triggers recurring crawl jobs. Every job this manages does exactly one
/// fixed, safe thing - crawl a specific, already-configured provider - parameterized only by
/// schedule (never by arbitrary code), so this is not a general-purpose job execution API.
/// </summary>
public interface ICrawlJobTrigger
{
    /// <returns>The id of the job that was triggered.</returns>
    string TriggerNow(string providerName);

    /// <summary>
    /// Registers (or updates, if it already exists) a provider's recurring crawl job. This is a
    /// live override of whatever <c>NewsCrawler.appsettings.json</c> currently has for that
    /// provider's <c>Cron</c> - it does not persist back to that file, so it only lasts until
    /// this process next restarts and re-registers every provider's job from config.
    /// </summary>
    /// <returns>The id of the recurring job that was created/updated.</returns>
    string CreateOrUpdate(string providerName, string cronExpression, string timeZoneId);
}
