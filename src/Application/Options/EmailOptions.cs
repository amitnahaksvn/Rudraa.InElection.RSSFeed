namespace Application.Options;

/// <summary>
/// Root configuration section ("Email") controlling the monitoring-alert email service. Lives in
/// Web's own appsettings.json (the one host running the crawl orchestrators that raise these
/// alerts). Provider-agnostic by design - nothing here is Resend-specific - so swapping the
/// concrete <see cref="Abstractions.IEmailService"/> implementation for SendGrid/SES/SMTP/etc.
/// later never requires touching this class or any caller.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>Master switch - when false, every <see cref="Abstractions.IEmailService"/> method is a logged no-op.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Provider API key/token. A development-tier credential kept directly in appsettings.json by deliberate choice - see CLAUDE.md.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string From { get; set; } = string.Empty;

    public List<string> To { get; set; } = [];

    /// <summary>Retry attempts for transient failures when calling the underlying email provider's API.</summary>
    public int MaxRetryAttempts { get; set; } = 3;
}
