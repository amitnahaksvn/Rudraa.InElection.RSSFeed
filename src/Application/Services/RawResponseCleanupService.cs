using Microsoft.Extensions.Logging;
using Application.Abstractions;

namespace Application.Services;

public sealed class RawResponseCleanupService : IRawResponseCleanupService
{
    private readonly IRssRawResponseRepository _rawResponseRepository;
    private readonly ILogger<RawResponseCleanupService> _logger;

    public RawResponseCleanupService(IRssRawResponseRepository rawResponseRepository, ILogger<RawResponseCleanupService> logger)
    {
        _rawResponseRepository = rawResponseRepository;
        _logger = logger;
    }

    public async Task<long> CleanupAsync(TimeSpan retention, CancellationToken cancellationToken)
    {
        var olderThan = DateTimeOffset.UtcNow - retention;
        var deletedCount = await _rawResponseRepository.DeleteOlderThanAsync(olderThan, cancellationToken);

        _logger.LogInformation(
            "Raw response cleanup: deleted {DeletedCount} records older than {OlderThan:O} (retention {Retention})",
            deletedCount, olderThan, retention);

        return deletedCount;
    }
}
