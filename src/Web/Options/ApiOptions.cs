namespace Web.Options;

/// <summary>Root configuration section ("Api") controlling read-endpoint defaults and limits.</summary>
public sealed class ApiOptions
{
    public const string SectionName = "Api";

    public int DefaultPageSize { get; set; } = 20;

    public int MaxPageSize { get; set; } = 100;

    public bool EnableSwagger { get; set; } = true;

    /// <summary>
    /// Hangfire's dashboard has no built-in auth - off by default so it's never accidentally
    /// exposed unauthenticated in production. Enable explicitly per environment (see
    /// appsettings.Development.json) once you've decided who should be able to reach it.
    /// </summary>
    public bool EnableHangfireDashboard { get; set; }
}
