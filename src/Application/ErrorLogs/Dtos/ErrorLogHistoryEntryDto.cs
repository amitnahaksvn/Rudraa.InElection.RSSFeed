using Domain.Entities;

namespace Application.ErrorLogs.Dtos;

public sealed record ErrorLogHistoryEntryDto(string Comment, bool IsResolved, DateTimeOffset CreatedOn)
{
    public static ErrorLogHistoryEntryDto FromDomain(ErrorLogHistoryEntry entry) => new(entry.Comment, entry.IsResolved, entry.CreatedOn);
}
