using Domain.Entities;

namespace Application.Abstractions;

/// <summary>Optional filters for <see cref="IErrorLogRepository.GetPagedAsync"/>/<see cref="IErrorLogRepository.CountAsync"/> - a null field means "don't filter on this".</summary>
public sealed record ErrorLogFilter(
    bool? IsResolved = null,
    string? Provider = null,
    string? Country = null,
    string? Source = null,
    string? SearchText = null);

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
    Task<bool> SetResolvedAsync(string id, bool resolved, string comment, CancellationToken cancellationToken);

    /// <summary>Appends a standalone comment - <see cref="ErrorLog.IsResolved"/>/<see cref="ErrorLog.ResolvedOn"/> are left unchanged, the new <see cref="ErrorLogHistoryEntry"/> just records the row's current status alongside the comment. Returns false when no row with that id exists.</summary>
    Task<bool> AddCommentAsync(string id, string comment, CancellationToken cancellationToken);

    Task EnsureIndexesAsync(CancellationToken cancellationToken);
}
