namespace Application.Models;

/// <summary>
/// Everything captured about a single failure, from wherever it happened (an RSS feed fetch, a
/// JSON news-API endpoint call, MongoDB persistence, or any other unexpected exception) through
/// to what <see cref="Abstractions.IEmailService"/> renders into an alert email. Deliberately a
/// flat record of optional fields rather than a class hierarchy per error source, since most
/// fields (ExceptionType/ErrorMessage/StackTrace) apply everywhere and the source-specific ones
/// (HttpStatusCode, ResponseBody, ...) are simply left null when not applicable - the email
/// template already renders "if available" for exactly that reason.
/// </summary>
public sealed record ErrorNotification
{
    public required string Environment { get; init; }

    public required string ApplicationName { get; init; }

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Provider key, e.g. "AajTak"/"NewsApiOrg". Null for errors not tied to a specific provider (e.g. MongoDB startup).</summary>
    public string? Provider { get; init; }

    /// <summary>Feed name (RSS) or endpoint name (JSON API), e.g. "India" or "LatestHeadlines".</summary>
    public string? FeedOrApiName { get; init; }

    /// <summary>Country the failing feed's publisher is based in - see <see cref="Options.RssFeedOptions.Country"/>. Null for errors not tied to a specific feed.</summary>
    public string? Country { get; init; }

    public string? SourceUrl { get; init; }

    /// <summary>What was being attempted, e.g. "RSS Feed Fetch", "News API Fetch", "MongoDB Persist", "Crawl Run".</summary>
    public required string Operation { get; init; }

    public required string ExceptionType { get; init; }

    public required string ErrorMessage { get; init; }

    public string? StackTrace { get; init; }

    /// <summary>Type + message of the innermost exception, formatted as one string; null when there was no inner exception.</summary>
    public string? InnerException { get; init; }

    public int? HttpStatusCode { get; init; }

    public string? RequestUrl { get; init; }

    /// <summary>Truncated at the template layer to keep the email a reasonable size - see <c>EmailTemplateBuilder</c>.</summary>
    public string? ResponseBody { get; init; }

    public string? CorrelationId { get; init; }

    public string? HangfireJobId { get; init; }

    public TimeSpan? ExecutionDuration { get; init; }

    /// <summary>
    /// Builds an <see cref="ErrorNotification"/> from a live <see cref="Exception"/>, extracting
    /// type/message/stack trace/inner-exception consistently so every call site doesn't repeat
    /// that boilerplate.
    /// </summary>
    public static ErrorNotification FromException(
        Exception exception,
        string environment,
        string applicationName,
        string operation,
        string? provider = null,
        string? feedOrApiName = null,
        string? country = null,
        string? sourceUrl = null,
        int? httpStatusCode = null,
        string? requestUrl = null,
        string? responseBody = null,
        string? correlationId = null,
        string? hangfireJobId = null,
        TimeSpan? executionDuration = null) => new()
    {
        Environment = environment,
        ApplicationName = applicationName,
        Provider = provider,
        FeedOrApiName = feedOrApiName,
        Country = country,
        SourceUrl = sourceUrl,
        Operation = operation,
        ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
        ErrorMessage = exception.Message,
        StackTrace = exception.StackTrace,
        InnerException = exception.InnerException is { } inner ? $"{inner.GetType().FullName}: {inner.Message}" : null,
        HttpStatusCode = httpStatusCode,
        RequestUrl = requestUrl ?? sourceUrl,
        ResponseBody = responseBody,
        CorrelationId = correlationId,
        HangfireJobId = hangfireJobId,
        ExecutionDuration = executionDuration
    };
}
