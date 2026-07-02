using System.Diagnostics;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Application.Common.Behaviours;

/// <summary>Logs a warning for any request that takes longer than <see cref="SlowRequestThreshold"/> to handle.</summary>
public sealed class PerformanceBehaviour<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private static readonly TimeSpan SlowRequestThreshold = TimeSpan.FromSeconds(3);

    private readonly ILogger<PerformanceBehaviour<TMessage, TResponse>> _logger;

    public PerformanceBehaviour(ILogger<PerformanceBehaviour<TMessage, TResponse>> logger)
    {
        _logger = logger;
    }

    public async ValueTask<TResponse> Handle(
        TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await next(message, cancellationToken);
        stopwatch.Stop();

        if (stopwatch.Elapsed > SlowRequestThreshold)
        {
            _logger.LogWarning(
                "Slow request: {RequestName} took {Elapsed}ms", typeof(TMessage).Name, stopwatch.ElapsedMilliseconds);
        }

        return response;
    }
}
