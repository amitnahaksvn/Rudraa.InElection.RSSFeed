using Hangfire.Storage;
using WebPlatform.Hangfire;

namespace PoliticalNews.Tests.Infrastructure;

public class FilteredRecurringJobsDispatcherTests
{
    private static RecurringJobDto Job(string id, string queue) => new() { Id = id, Queue = queue, Cron = "* * * * *" };

    [Fact]
    public void FilterToOwnedQueues_KeepsOnlyJobsInOwnedQueues()
    {
        var allJobs = new[]
        {
            Job("news-crawl-aajtak", "rss"),
            Job("news-api-newsapiorg", "api"),
            Job("social-media-modi", "social"),
            Job("keep-alive-ping", "keepalive"),
            Job("cleanup-raw-responses", "default"),
        };

        var result = FilteredRecurringJobsDispatcher.FilterToOwnedQueues(allJobs, ["keepalive", "rss", "default"]);

        Assert.Equal(3, result.Count);
        Assert.Contains(result, j => j.Id == "news-crawl-aajtak");
        Assert.Contains(result, j => j.Id == "keep-alive-ping");
        Assert.Contains(result, j => j.Id == "cleanup-raw-responses");
        Assert.DoesNotContain(result, j => j.Id == "news-api-newsapiorg");
        Assert.DoesNotContain(result, j => j.Id == "social-media-modi");
    }

    [Fact]
    public void FilterToOwnedQueues_QueueMatchIsCaseInsensitive()
    {
        var allJobs = new[] { Job("news-api-gnews", "API") };

        var result = FilteredRecurringJobsDispatcher.FilterToOwnedQueues(allJobs, ["api", "social"]);

        Assert.Single(result);
    }

    [Fact]
    public void FilterToOwnedQueues_JobWithNoQueue_IsExcluded()
    {
        var allJobs = new[] { new RecurringJobDto { Id = "orphan", Queue = null, Cron = "* * * * *" } };

        var result = FilteredRecurringJobsDispatcher.FilterToOwnedQueues(allJobs, ["rss", "default"]);

        Assert.Empty(result);
    }

    [Fact]
    public void FilterToOwnedQueues_SortsByQueueThenId()
    {
        var allJobs = new[]
        {
            Job("zeta", "rss"),
            Job("alpha", "rss"),
            Job("beta", "default"),
        };

        var result = FilteredRecurringJobsDispatcher.FilterToOwnedQueues(allJobs, ["rss", "default"]);

        Assert.Equal(["beta", "alpha", "zeta"], result.Select(j => j.Id));
    }
}
