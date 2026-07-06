namespace Application.ErrorLogs.Dtos;

/// <summary>Per-status and per-category counts for the error-monitor's left-hand sidebar (All/Unresolved/Resolved, plus Rss/Api/Social/Http/Critical/Warning - see <c>Application.Abstractions.ErrorLogCategory</c>), computed under whatever non-status/non-category filters (provider/country/source/search) are currently active.</summary>
public sealed record ErrorLogCountsDto(
    long All,
    long Unresolved,
    long Resolved,
    long Rss,
    long Api,
    long Social,
    long Http,
    long Critical,
    long Warning);
