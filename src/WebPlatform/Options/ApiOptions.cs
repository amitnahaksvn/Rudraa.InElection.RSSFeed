namespace WebPlatform.Options;

/// <summary>Root configuration section ("Api") controlling read-endpoint defaults and limits.</summary>
public sealed class ApiOptions
{
    public const string SectionName = "Api";

    public int DefaultPageSize { get; set; } = 20;

    public int MaxPageSize { get; set; } = 100;

    public bool EnableSwagger { get; set; } = true;

    /// <summary>
    /// Hangfire's dashboard has no built-in auth and none is applied here - anyone who reaches
    /// this URL can view job internals and trigger/delete jobs, not just view them. Off by
    /// default; enabling this on a public deployment is a deliberate convenience-over-security
    /// trade-off, made knowingly, not a default to flip on casually.
    /// </summary>
    public bool EnableHangfireDashboard { get; set; }

    /// <summary>
    /// The error-monitor page (/errors) reads via the authenticated-nothing api/errors endpoints -
    /// same "no built-in auth, off by default" trade-off as <see cref="EnableHangfireDashboard"/>,
    /// since it surfaces stack traces and raw request/response bodies that may contain sensitive
    /// data. Enabling this on a public deployment is a deliberate choice, not a default to flip on
    /// casually.
    /// </summary>
    public bool EnableErrorDashboard { get; set; }

    /// <summary>
    /// The Provider Management page (/providers) reads via the authenticated-nothing api/providers
    /// endpoints - same "no built-in auth, off by default" trade-off as
    /// <see cref="EnableErrorDashboard"/>. Its Test action also makes a real outbound HTTP request
    /// to whichever third-party feed/API is selected on demand, so anyone who can reach it can
    /// trigger arbitrary configured-feed fetches at will (not arbitrary URLs - only feeds/endpoints
    /// already present in configuration - but still real, repeatable outbound traffic and, for
    /// metered API keys, real quota usage). Enabling this on a public deployment is a deliberate
    /// choice, not a default to flip on casually.
    /// </summary>
    public bool EnableProviderDashboard { get; set; }

    /// <summary>
    /// The Crawl Report page (/reports) reads via the authenticated-nothing api/crawl/report and
    /// api/crawl/history endpoints - same "no built-in auth, off by default" trade-off as
    /// <see cref="EnableProviderDashboard"/>. Read-only (no test/trigger action), so lower-risk than
    /// that one, but still off by default for consistency with every other admin dashboard in this
    /// app; enabling it on a public deployment is a deliberate choice, not a default to flip on
    /// casually.
    /// </summary>
    public bool EnableCrawlReportDashboard { get; set; }

    /// <summary>
    /// The Job Report page (/job-reports) reads via the authenticated-nothing api/job-reports
    /// endpoint - same "no built-in auth, off by default" trade-off as
    /// <see cref="EnableCrawlReportDashboard"/>. Read-only, same lower-risk category; still off by
    /// default for consistency with every other admin dashboard in this app.
    /// </summary>
    public bool EnableJobReportDashboard { get; set; }
}
