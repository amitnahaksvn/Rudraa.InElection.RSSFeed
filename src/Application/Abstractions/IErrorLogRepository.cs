using Domain.Entities;

namespace Application.Abstractions;

/// <summary>Optional filters for <see cref="IErrorLogRepository.GetPagedAsync"/>/<see cref="IErrorLogRepository.CountAsync"/> - a null field means "don't filter on this". <paramref name="Category"/> is the error-monitor sidebar's quick-filter shortcut (see <see cref="ErrorLogCategory"/>) - a derived grouping on top of <see cref="Domain.Entities.ErrorLog.Source"/>/<see cref="Domain.Entities.ErrorLog.HttpStatusCode"/>, not a stored field of its own.</summary>
public sealed record ErrorLogFilter(
    bool? IsResolved = null,
    string? Provider = null,
    string? Country = null,
    string? Source = null,
    string? SearchText = null,
    ErrorLogCategory? Category = null);

/// <summary>
/// The error-monitor sidebar's quick-filter categories, shown alongside All/Unresolved/Resolved -
/// <see cref="Rss"/>/<see cref="Api"/>/<see cref="Social"/>/<see cref="Http"/> map onto
/// <see cref="Domain.Entities.ErrorLog.Source"/> (the literal strings every <c>ErrorNotification.Operation</c>
/// call site uses - "RSS Feed Fetch"/"Dynamic Feed Fetch", "News API Fetch", "Social Media Fetch",
/// "HTTP Request"), while <see cref="Critical"/>/<see cref="Warning"/> are a severity derived from
/// <see cref="Domain.Entities.ErrorLog.HttpStatusCode"/> since there's no stored severity field:
/// null or 5xx is treated as Critical (an unhandled exception or a server-side failure), 4xx as
/// Warning (a client-facing but non-fatal response).
/// </summary>
public enum ErrorLogCategory
{
    Rss,
    Api,
    Social,
    Http,
    Critical,
    Warning,
}

/// <summary>Persistence for <see cref="ErrorLog"/> - the general app-wide exception log.</summary>
public interface IErrorLogRepository
{
    Task InsertAsync(ErrorLog errorLog, CancellationToken cancellationToken);

    /// <summary>Oldest-first, up to <paramref name="limit"/> rows with <see cref="ErrorLog.IsSent"/> still false.</summary>
    Task<IReadOnlyList<ErrorLog>> GetUnsentAsync(int limit, CancellationToken cancellationToken);

    /// <summary>Flips <see cref="ErrorLog.IsSent"/> to true and stamps <see cref="ErrorLog.SentOn"/> for exactly these ids - called only after a dispatch email has actually been sent successfully.</summary>
    Task MarkAsSentAsync(IReadOnlyList<string> ids, DateTimeOffset sentOn, CancellationToken cancellationToken);

    /// <summary>Unresolved rows first, then newest first within each group - matches the error-monitor UI's default sort so a fresh unresolved failure always surfaces at the top.</summary>
    Task<IReadOnlyList<ErrorLog>> GetPagedAsync(ErrorLogFilter filter, int skip, int limit, CancellationToken cancellationToken);

    Task<long> CountAsync(ErrorLogFilter filter, CancellationToken cancellationToken);

    Task<ErrorLog?> GetByIdAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// Sets <see cref="ErrorLog.IsResolved"/> (and <see cref="ErrorLog.ResolvedOn"/> when
    /// resolving, clearing it when un-resolving) and appends a required-comment
    /// <see cref="ErrorLogHistoryEntry"/>. Returns false when no row with that id exists.
    /// </summary>
    Task<bool> SetResolvedAsync(string id, bool resolved, string comment, string? description, CancellationToken cancellationToken);

    /// <summary>Appends a standalone comment - <see cref="ErrorLog.IsResolved"/>/<see cref="ErrorLog.ResolvedOn"/> are left unchanged, the new <see cref="ErrorLogHistoryEntry"/> just records the row's current status alongside the comment. Returns false when no row with that id exists.</summary>
    Task<bool> AddCommentAsync(string id, string comment, string? description, CancellationToken cancellationToken);

    Task EnsureIndexesAsync(CancellationToken cancellationToken);
}
