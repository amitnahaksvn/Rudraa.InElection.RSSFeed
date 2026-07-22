using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Domain.Entities;

namespace Infrastructure.Email;

/// <summary>
/// Builds the HTML bodies for every monitoring-alert email, reading the shared chrome from
/// <c>Templates/Layout.html</c> on disk (see that file's own header comment) and generating only
/// the dynamic inner content (stat badges, context/exception/tracing panels, tables) here in code
/// - the two are split deliberately so branding/style tweaks (colors, footer copy, fonts) never
/// need a C# change, only an edit to the .html file. Lives in Infrastructure (not Application)
/// specifically because "how a notification renders" is a provider/presentation concern, unlike
/// <see cref="ErrorLog"/> itself which is provider-agnostic business data.
/// </summary>
public sealed class EmailTemplateBuilder
{
    private const int MaxFieldLength = 4000;

    private static readonly string LayoutPath = Path.Combine(AppContext.BaseDirectory, "Email", "Templates", "Layout.html");
    private const string FallbackLayout =
        "<!doctype html><html><body style=\"font-family:sans-serif;\">" +
        "<div style=\"border-top:4px solid {{AccentColor}};padding:16px;background:#f9fafb;color:#111827;\">" +
        "<b>{{Icon}} {{HeaderTitle}}</b><br/>{{HeaderSubtitle}}</div>" +
        "<div style=\"padding:16px;\">{{Body}}</div>" +
        "<div style=\"padding:12px;color:#999;font-size:11px;\">{{FooterNote}}</div>" +
        "</body></html>";

    private readonly ILogger<EmailTemplateBuilder> _logger;
    private readonly Lazy<string> _layoutHtml;

    public EmailTemplateBuilder(ILogger<EmailTemplateBuilder> logger)
    {
        _logger = logger;
        _layoutHtml = new Lazy<string>(LoadLayout);
    }

    private static readonly Regex HtmlCommentRegex = new("<!--.*?-->", RegexOptions.Singleline | RegexOptions.Compiled);

    private string LoadLayout()
    {
        try
        {
            // Layout.html's own leading comment documents the {{Placeholder}} tokens for whoever
            // edits the file - stripped here (comments never render in an email client anyway) so
            // that literal "{{...}}" text inside the comment isn't itself replaced below, and so
            // the comment never ships in the actual sent email's HTML source.
            var raw = File.ReadAllText(LayoutPath);
            return HtmlCommentRegex.Replace(raw, string.Empty);
        }
        catch (Exception ex)
        {
            // A missing/unreadable template file must never stop an alert email from going out -
            // fall back to a minimal inline layout instead.
            _logger.LogWarning(ex, "Could not read email layout template at {Path} - using built-in fallback layout", LayoutPath);
            return FallbackLayout;
        }
    }

    // Deliberately soft/muted, not saturated - paired with Layout.html's light header (see that
    // file's own comment) so the whole email reads as clear and prominent without being visually
    // loud ("very high contrast" was flagged explicitly as something to avoid).
    private static readonly (string Icon, string Color, string ColorSoft) Error = ("⚠️", "#C0392B", "#FDECEA");
    private static readonly (string Icon, string Color, string ColorSoft) Warning = ("⚠️", "#B7791F", "#FEF3C7");
    private static readonly (string Icon, string Color, string ColorSoft) Info = ("ℹ️", "#2B6CB0", "#E8F1FB");
    private static readonly (string Icon, string Color, string ColorSoft) Success = ("✅", "#2F855A", "#E6F6ED");

    /// <summary>
    /// One email covering a batch of persisted, not-yet-dispatched <see cref="ErrorLog"/> records -
    /// a summary table (each row carrying its own <see cref="ErrorLog.Id"/>) plus a full detail
    /// section per error, so a burst of failures produces one readable email instead of a flood.
    /// Called only by the error-notification dispatch job, on its own schedule - never at the
    /// moment an error actually occurs.
    /// </summary>
    public (string Subject, string Html) BuildErrorLogBatch(IReadOnlyList<ErrorLog> errors)
    {
        var subject = $"⚠️ InElection Monitoring - {errors.Count} pending error(s)";
        var first = errors[0];
        var sourcesAffected = errors.Select(x => x.Provider ?? x.Source).Distinct().Count();
        var oldest = errors.Min(x => x.CreatedOn);

        var body = new StringBuilder();
        body.Append(StatStrip([
            ("Pending Errors", errors.Count.ToString(), Error.Color),
            ("Sources Affected", sourcesAffected.ToString(), "#374151"),
            ("Oldest Error (UTC)", oldest.UtcDateTime.ToString("yyyy-MM-dd HH:mm"), "#374151")
        ]));

        body.Append(PanelOpen("📋", "Summary"));
        body.Append(ErrorLogSummaryTable(errors));
        body.Append(PanelClose());

        body.Append("""<h3 style="margin:24px 0 4px;font-size:13px;color:#374151;text-transform:uppercase;letter-spacing:0.4px;">Error Details</h3>""");
        for (var i = 0; i < errors.Count; i++)
        {
            var e = errors[i];
            body.Append($"""
                <div style="margin:14px 0 6px;font-size:13px;font-weight:700;color:#111827;">
                #{i + 1} &middot; <span style="color:{Error.Color};">Error ID: {Encode(e.Id)}</span> &middot; {(e.Country is { } c ? $"[{Encode(c)}] " : "")}{Encode(e.Provider ?? e.Source)}{(e.FeedOrApiName is { } f ? $" / {Encode(f)}" : "")}
                </div>
                """);
            body.Append(ErrorLogSections(e));
        }

        var html = Render(Error, "InElection Monitoring Alert", HeaderBadges(first.Environment, first.ApplicationName), body.ToString());
        return (subject, html);
    }

    public (string Subject, string Html) BuildSimple(string kind, string subject, string message)
    {
        var theme = kind switch
        {
            "Warning" => Warning,
            "Information" => Info,
            "Success" => Success,
            _ => Info
        };
        var emojiPrefix = kind switch { "Warning" => "⚠️", "Success" => "✅", _ => "ℹ️" };
        var fullSubject = $"{emojiPrefix} InElection {kind} - {subject}";

        var body = new StringBuilder();
        body.Append(PanelOpen(theme.Icon, Encode(subject)));
        body.Append($"""<p style="margin:0;font-size:14px;color:#374151;line-height:1.7;white-space:pre-wrap;">{Encode(message)}</p>""");
        body.Append(PanelClose());

        var html = Render(theme, $"InElection {kind}", "", body.ToString());
        return (fullSubject, html);
    }

    // ---- layout plumbing ----

    private string Render((string Icon, string Color, string ColorSoft) theme, string headerTitle, string headerSubtitleHtml, string bodyHtml) =>
        _layoutHtml.Value
            .Replace("{{AccentColor}}", theme.Color)
            .Replace("{{AccentColorSoft}}", theme.ColorSoft)
            .Replace("{{Icon}}", theme.Icon)
            .Replace("{{HeaderTitle}}", Encode(headerTitle))
            .Replace("{{HeaderSubtitle}}", headerSubtitleHtml)
            .Replace("{{Body}}", bodyHtml)
            .Replace("{{FooterNote}}", Encode($"Generated {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC"));

    private static string HeaderBadges(string environment, string applicationName) => $"""
        <span style="{BadgeStyle()}">ENV: {Encode(environment.ToUpperInvariant())}</span>
        <span style="{BadgeStyle()}margin-left:6px;">APP: {Encode(applicationName)}</span>
        """;

    private static string BadgeStyle() =>
        "display:inline-block;background:#f1f5f9;color:#475569;font-size:11px;font-weight:600;" +
        "padding:3px 9px;border-radius:20px;letter-spacing:0.3px;border:1px solid #e2e8f0;";

    private static string StatStrip((string Label, string Value, string Color)[] stats)
    {
        var sb = new StringBuilder("""<table role="presentation" style="width:100%;border-collapse:collapse;margin-bottom:20px;"><tr>""");
        foreach (var (label, value, color) in stats)
        {
            sb.Append($"""
                <td style="padding:10px 14px;background:#f9fafb;border:1px solid #eef0f2;border-radius:8px;">
                <div style="font-size:10px;color:#9aa1ab;text-transform:uppercase;letter-spacing:0.4px;">{Encode(label)}</div>
                <div style="font-size:14px;font-weight:700;color:{color};margin-top:2px;">{Encode(value)}</div>
                </td><td style="width:8px;"></td>
                """);
        }
        sb.Append("</tr></table>");
        return sb.ToString();
    }

    private static string PanelOpen(string icon, string title) => $"""
        <div style="margin-bottom:16px;border:1px solid #eef0f2;border-radius:8px;overflow:hidden;">
        <div style="background:#f9fafb;padding:10px 14px;border-bottom:1px solid #eef0f2;font-size:12px;font-weight:700;color:#374151;text-transform:uppercase;letter-spacing:0.4px;">{icon} {title}</div>
        <div style="padding:14px;">
        """;

    private static string PanelClose() => "</div></div>";

    private static string ErrorLogSections(ErrorLog e)
    {
        var sb = new StringBuilder();

        sb.Append(PanelOpen("🧭", "Context"));
        sb.Append(KeyValueGrid([
            ("Error Id", e.Id),
            ("Occurred On (UTC)", e.CreatedOn.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss")),
            ("Environment", e.Environment),
            ("Application", e.ApplicationName),
            ("Service Name", e.ServiceName),
            ("Machine Name", e.MachineName),
            ("Provider", e.Provider),
            ("Feed/API Name", e.FeedOrApiName),
            ("Country", e.Country),
            ("Source", e.Source),
            ("Source URL", e.SourceUrl)
        ]));
        sb.Append(PanelClose());

        sb.Append(PanelOpen("💥", "Exception"));
        sb.Append(KeyValueGrid([
            ("Exception Type", e.ExceptionType),
            ("Message", e.Message),
            ("Inner Exception", e.InnerException),
            ("Error Code", e.ErrorCode)
        ]));
        sb.Append(PanelClose());

        if (e.HttpStatusCode is not null || e.ResponseBody is not null || e.SourceUrl is not null
            || e.RequestPath is not null || e.HttpMethod is not null || e.QueryString is not null)
        {
            sb.Append(PanelOpen("🌐", "HTTP"));
            sb.Append(KeyValueGrid([
                ("HTTP Status Code", e.HttpStatusCode?.ToString()),
                ("HTTP Method", e.HttpMethod),
                ("Request Path", e.RequestPath),
                ("Query String", e.QueryString)
            ]));
            if (!string.IsNullOrWhiteSpace(e.RequestBody))
            {
                sb.Append("""<div style="font-size:11px;color:#9aa1ab;text-transform:uppercase;letter-spacing:0.4px;margin:8px 0 4px;">Request Body</div>""");
                sb.Append(CodeBlock(Truncate(e.RequestBody)!));
            }
            if (!string.IsNullOrWhiteSpace(e.ResponseBody))
            {
                sb.Append("""<div style="font-size:11px;color:#9aa1ab;text-transform:uppercase;letter-spacing:0.4px;margin:8px 0 4px;">Response Body</div>""");
                sb.Append(CodeBlock(Truncate(e.ResponseBody)!));
            }
            sb.Append(PanelClose());
        }

        if (e.UserId is not null || e.UserName is not null || e.IpAddress is not null || e.UserAgent is not null)
        {
            sb.Append(PanelOpen("👤", "Request Context"));
            sb.Append(KeyValueGrid([
                ("User Id", e.UserId),
                ("User Name", e.UserName),
                ("IP Address", e.IpAddress),
                ("User Agent", e.UserAgent)
            ]));
            sb.Append(PanelClose());
        }

        if (!string.IsNullOrWhiteSpace(e.StackTrace))
        {
            sb.Append(PanelOpen("🧵", "Stack Trace"));
            sb.Append(CodeBlock(Truncate(e.StackTrace)!));
            sb.Append(PanelClose());
        }

        sb.Append(PanelOpen("🔗", "Tracing"));
        sb.Append(KeyValueGrid([
            ("Correlation Id", e.CorrelationId),
            ("Hangfire Job Id", e.HangfireJobId),
            ("Trace Id", e.TraceId),
            ("Assembly Version", e.AssemblyVersion),
            ("Execution Duration", e.ExecutionDuration?.ToString("g"))
        ]));
        sb.Append(PanelClose());

        if (!string.IsNullOrWhiteSpace(e.AdditionalData))
        {
            sb.Append(PanelOpen("🧩", "Additional Data"));
            sb.Append(CodeBlock(Truncate(e.AdditionalData)!));
            sb.Append(PanelClose());
        }

        return sb.ToString();
    }

    private static string KeyValueGrid((string Label, string? Value)[] pairs)
    {
        var sb = new StringBuilder("""<table role="presentation" style="width:100%;border-collapse:collapse;">""");
        foreach (var (label, value) in pairs)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            sb.Append("<tr>");
            sb.Append($"""<td style="padding:5px 0;font-size:11px;color:#9aa1ab;width:170px;vertical-align:top;white-space:nowrap;text-transform:uppercase;letter-spacing:0.3px;">{Encode(label)}</td>""");
            sb.Append($"""<td style="padding:5px 0;font-size:13px;color:#111827;word-break:break-word;">{Encode(value)}</td>""");
            sb.Append("</tr>");
        }
        sb.Append("</table>");
        return sb.ToString();
    }

    private static string CodeBlock(string content) => $"""
        <pre style="margin:0;white-space:pre-wrap;word-break:break-word;font-family:'SF Mono',Consolas,Menlo,monospace;font-size:12px;line-height:1.5;background:#f8fafc;color:#334155;padding:12px 14px;border-radius:6px;border:1px solid #e2e8f0;">{Encode(content)}</pre>
        """;

    private static string ErrorLogSummaryTable(IReadOnlyList<ErrorLog> errors)
    {
        var sb = new StringBuilder("""<table role="presentation" style="width:100%;border-collapse:collapse;font-size:12px;">""");
        sb.Append("""<tr style="background:#f9fafb;">""");
        foreach (var col in new[] { "#", "Error Id", "Country", "Provider", "Feed/API", "Exception", "HTTP", "Occurred (UTC)" })
        {
            sb.Append($"""<th style="text-align:left;padding:8px 10px;border-bottom:2px solid #eef0f2;color:#6b7280;text-transform:uppercase;font-size:10px;letter-spacing:0.3px;">{col}</th>""");
        }
        sb.Append("</tr>");

        for (var i = 0; i < errors.Count; i++)
        {
            var e = errors[i];
            var rowBg = i % 2 == 0 ? "#ffffff" : "#fbfbfc";
            sb.Append($"""<tr style="background:{rowBg};">""");
            sb.Append(Cell($"""<span style="display:inline-block;width:7px;height:7px;border-radius:50%;background:{Error.Color};margin-right:6px;"></span>{i + 1}"""));
            sb.Append(Cell($"""<span style="font-family:'SF Mono',Consolas,Menlo,monospace;">{Encode(e.Id)}</span>"""));
            sb.Append(Cell(Encode(e.Country ?? "-")));
            sb.Append(Cell(Encode(e.Provider ?? "-")));
            sb.Append(Cell(Encode(e.FeedOrApiName ?? "-")));
            sb.Append(Cell(Encode(e.ExceptionType)));
            sb.Append(Cell(Encode(e.HttpStatusCode?.ToString() ?? "-")));
            sb.Append(Cell(Encode(e.CreatedOn.UtcDateTime.ToString("yyyy-MM-dd HH:mm"))));
            sb.Append("</tr>");
        }
        sb.Append("</table>");
        return sb.ToString();

        static string Cell(string html) => $"""<td style="padding:8px 10px;border-bottom:1px solid #f1f2f4;color:#374151;">{html}</td>""";
    }

    private static string? Truncate(string? value) =>
        value is null ? null : value.Length > MaxFieldLength ? value[..MaxFieldLength] + "\n... [truncated]" : value;

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
