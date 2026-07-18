using System.Net;
using System.Text;
using Hangfire.Dashboard;
using Hangfire.Storage;

namespace WebPlatform.Hangfire;

/// <summary>
/// A "/service-jobs" dashboard page scoped to one process's own <see cref="Application.Options.HangfireOptions.Queues"/> -
/// RssService and ApiService each mount their own <c>/hangfire</c> dashboard against the same shared
/// Mongo storage as WebApp (see <c>HangfireStorageSetup</c>), so the built-in "Recurring Jobs" page
/// (at <c>/recurring</c>) shows every job from all three processes there, not just the ones this
/// particular process actually executes - confusing on RssService's dashboard when it lists JSON-API
/// jobs it never runs, and vice versa on ApiService. This page reuses the exact same per-job
/// <see cref="RecurringJobDto.Queue"/> that already drives which process executes a job (resolved
/// from each executor's own <c>[Queue("...")]</c> attribute) to filter the list down to just the
/// queues this process's own Hangfire server was configured with - so it's always in sync with what
/// actually runs here, never a second hardcoded list that could drift.
///
/// Deliberately a brand-new route rather than an attempt to override the built-in "/recurring" page
/// in place: <see cref="Hangfire.Dashboard.DashboardRoutes"/>.Routes resolves the *first* registered
/// match for a path, and the built-in route is already registered by the time any application code
/// runs, so a second registration for the same path would never actually be reached. The original
/// unfiltered "Recurring Jobs" page is left exactly as-is (still reachable at /recurring for anyone
/// who wants the raw cross-process view) - this is purely additive, not a replacement.
/// </summary>
public sealed class FilteredRecurringJobsDispatcher : IDashboardDispatcher
{
    private readonly IReadOnlyCollection<string> _ownedQueues;
    private readonly string _serviceLabel;

    public FilteredRecurringJobsDispatcher(IReadOnlyCollection<string> ownedQueues, string serviceLabel)
    {
        _ownedQueues = ownedQueues;
        _serviceLabel = serviceLabel;
    }

    public Task Dispatch(DashboardContext context)
    {
        using var connection = context.Storage.GetConnection();
        var jobs = FilterToOwnedQueues(connection.GetRecurringJobs(), _ownedQueues);

        context.Response.ContentType = "text/html; charset=utf-8";
        return context.Response.WriteAsync(BuildHtml(jobs, _ownedQueues, _serviceLabel, context.Request.PathBase));
    }

    /// <summary>Extracted from <see cref="Dispatch"/> so the actual filtering rule (not just the HTML rendering) has direct test coverage without needing a real Hangfire storage connection.</summary>
    public static List<RecurringJobDto> FilterToOwnedQueues(IEnumerable<RecurringJobDto> allJobs, IReadOnlyCollection<string> ownedQueues) =>
        allJobs
            .Where(job => job.Queue is not null && ownedQueues.Contains(job.Queue, StringComparer.OrdinalIgnoreCase))
            .OrderBy(job => job.Queue, StringComparer.OrdinalIgnoreCase)
            .ThenBy(job => job.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string BuildHtml(
        IReadOnlyList<RecurringJobDto> jobs, IReadOnlyCollection<string> ownedQueues, string serviceLabel, string pathBase)
    {
        var html = new StringBuilder();
        html.Append("""
            <!doctype html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <title>Service jobs</title>
            <style>
              body { font-family: -apple-system, "Segoe UI", Arial, sans-serif; margin: 24px; color: #1b1f2a; background: #f5f6f9; }
              h1 { font-size: 20px; margin: 0 0 4px; }
              p.sub { color: #5c6472; font-size: 13.5px; margin: 0 0 20px; }
              a.back { font-size: 13px; color: #46529e; text-decoration: none; }
              table { border-collapse: collapse; width: 100%; background: #fff; border: 1px solid #e2e5ec; border-radius: 8px; overflow: hidden; }
              th, td { text-align: left; padding: 8px 12px; border-bottom: 1px solid #e2e5ec; font-size: 13px; }
              th { background: #eef0f5; text-transform: uppercase; font-size: 11px; letter-spacing: .03em; color: #5c6472; }
              tr:last-child td { border-bottom: none; }
              code { background: #eef0f5; border-radius: 4px; padding: 1px 5px; font-family: ui-monospace, "SF Mono", Consolas, monospace; }
              .queue-pill { display: inline-block; font-size: 11px; font-weight: 700; padding: 1px 7px; border-radius: 999px; background: #e8eaf7; color: #2e3878; }
              .empty { padding: 24px; text-align: center; color: #8991a0; }
              .state-succeeded { color: #16794f; font-weight: 600; }
              .state-failed { color: #a3550a; font-weight: 600; }
            </style>
            </head>
            <body>
            """);

        html.Append($"<p><a class=\"back\" href=\"{WebUtility.HtmlEncode(pathBase)}/\">&larr; Back to dashboard</a></p>");
        html.Append($"<h1>{WebUtility.HtmlEncode(serviceLabel)}</h1>");
        html.Append($"<p class=\"sub\">Recurring jobs in this process's own queue(s): {WebUtility.HtmlEncode(string.Join(", ", ownedQueues))}. " +
                     "The default \"Recurring Jobs\" page shows every job from every process sharing this storage; this page shows only what actually runs here.</p>");

        if (jobs.Count == 0)
        {
            html.Append("<div class=\"empty\">No recurring jobs registered in these queues.</div>");
        }
        else
        {
            html.Append("<table><thead><tr><th>Id</th><th>Queue</th><th>Cron</th><th>Next execution (UTC)</th><th>Last execution (UTC)</th><th>Last state</th></tr></thead><tbody>");
            foreach (var job in jobs)
            {
                var stateClass = job.LastJobState switch
                {
                    "Succeeded" => "state-succeeded",
                    "Failed" => "state-failed",
                    _ => "",
                };
                html.Append("<tr>");
                html.Append($"<td><code>{WebUtility.HtmlEncode(job.Id)}</code></td>");
                html.Append($"<td><span class=\"queue-pill\">{WebUtility.HtmlEncode(job.Queue)}</span></td>");
                html.Append($"<td><code>{WebUtility.HtmlEncode(job.Cron)}</code></td>");
                html.Append($"<td>{job.NextExecution?.ToString("u") ?? "-"}</td>");
                html.Append($"<td>{job.LastExecution?.ToString("u") ?? "-"}</td>");
                html.Append($"<td class=\"{stateClass}\">{WebUtility.HtmlEncode(job.LastJobState ?? "-")}</td>");
                html.Append("</tr>");
            }
            html.Append("</tbody></table>");
        }

        html.Append("</body></html>");
        return html.ToString();
    }
}
