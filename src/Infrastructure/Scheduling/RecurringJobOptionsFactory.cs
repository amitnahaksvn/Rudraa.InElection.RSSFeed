using Hangfire;

namespace Infrastructure.Scheduling;

public static class RecurringJobOptionsFactory
{
    public static RecurringJobOptions Create(TimeZoneInfo timeZone) => new()
    {
        TimeZone = timeZone,
        // A restart or deployment can leave recurring jobs overdue. Ignore misfires so the worker
        // does not replay a backlog immediately and get stuck in a startup crawl storm.
        MisfireHandling = MisfireHandlingMode.Ignorable
    };
}
