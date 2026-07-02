using Mediator;
using Microsoft.Extensions.Logging;

namespace Application.Common.Behaviours;

/// <summary>Logs every command/query as it enters the mediator pipeline.</summary>
public sealed class LoggingBehaviour<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly ILogger<LoggingBehaviour<TMessage, TResponse>> _logger;

    public LoggingBehaviour(ILogger<LoggingBehaviour<TMessage, TResponse>> logger)
    {
        _logger = logger;
    }

    public ValueTask<TResponse> Handle(
        TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling {RequestName}", typeof(TMessage).Name);
        return next(message, cancellationToken);
    }
}
