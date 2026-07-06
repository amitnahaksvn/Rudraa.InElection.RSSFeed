namespace Domain.Entities;

/// <summary>
/// One entry in an <see cref="ErrorLog.History"/> audit trail - appended whenever someone changes
/// <see cref="ErrorLog.IsResolved"/> (comment required) or adds a standalone comment via the
/// error-monitor UI (in which case <see cref="IsResolved"/> here just reflects the row's status
/// at that moment, unchanged by the comment itself).
/// </summary>
public sealed class ErrorLogHistoryEntry
{
    public string Comment { get; set; } = string.Empty;

    /// <summary>Optional longer-form write-up alongside <see cref="Comment"/> - both are rich text (HTML) as authored in the error-monitor UI's editor.</summary>
    public string? Description { get; set; }

    public bool IsResolved { get; set; }

    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
}
