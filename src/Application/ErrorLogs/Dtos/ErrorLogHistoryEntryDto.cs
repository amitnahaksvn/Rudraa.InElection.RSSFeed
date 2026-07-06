using Domain.Entities;

namespace Application.ErrorLogs.Dtos;

public sealed record ErrorLogHistoryEntryDto(string Comment, string? Description, bool IsResolved, DateTimeOffset CreatedOn)
{
    public static ErrorLogHistoryEntryDto FromDomain(ErrorLogHistoryEntry entry) =>
        new(entry.Comment, entry.Description, entry.IsResolved, entry.CreatedOn);
}
