using Mediator;
using Microsoft.Extensions.Logging;

namespace Application.Common.Behaviours;

/// <summary>Logs the request name/payload for any exception that escapes a handler, then rethrows.</summary>
public sealed class UnhandledExceptionBehaviour<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly ILogger<UnhandledExceptionBehaviour<TMessage, TResponse>> _logger;

    public UnhandledExceptionBehaviour(ILogger<UnhandledExceptionBehaviour<TMessage, TResponse>> logger)
    {
        _logger = logger;
    }

    public async ValueTask<TResponse> Handle(
        TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next(message, cancellationToken);
        }
        catch (Exception ex) when (ex is not Common.Exceptions.ValidationException and not OperationCanceledException)
        {
            _logger.LogError(ex, "Unhandled exception for request {RequestName} {@Request}", typeof(TMessage).Name, message);
            throw;
        }
    }
}
