using Hangfire.Dashboard;

namespace WebPlatform.Hangfire;

/// <summary>
/// Wires <see cref="FilteredRecurringJobsDispatcher"/> into one process's Hangfire dashboard: a new
/// "/service-jobs" route plus its own left-nav entry (Hangfire's own supported customization point,
/// <see cref="NavigationMenu"/>.Items - the same mechanism used for the built-in nav entries).
/// </summary>
public static class ServiceScopedDashboardExtensions
{
    private const string RoutePath = "/service-jobs";

    /// <summary>Call once, before the process starts serving requests - typically right next to <c>app.UseHangfireDashboard(...)</c>.</summary>
    public static void RegisterServiceScopedRecurringJobsPage(string[] ownedQueues, string navLabel)
    {
        DashboardRoutes.Routes.Add(RoutePath, new FilteredRecurringJobsDispatcher(ownedQueues, navLabel));

        NavigationMenu.Items.Add(page => new MenuItem(navLabel, page.Url.To(RoutePath))
        {
            Active = page.RequestPath == RoutePath,
        });
    }
}
