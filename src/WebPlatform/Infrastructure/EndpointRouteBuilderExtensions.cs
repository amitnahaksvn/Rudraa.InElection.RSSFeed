namespace WebPlatform.Infrastructure;

/// <summary>
/// Thin wrappers around the standard Minimal API <c>MapGet</c>/<c>MapPost</c>/etc. that derive
/// <c>WithName</c> from the handler method's own name (e.g. <c>GetLatest</c>), so every endpoint
/// gets a stable OpenAPI operationId for free without repeating the name at the call site. Typed
/// against <see cref="RouteGroupBuilder"/> specifically (rather than <see cref="IEndpointRouteBuilder"/>)
/// so these are picked over the built-in overloads by C#'s more-specific-receiver rule, and the
/// explicit interface cast below is what stops that same rule from making this recurse into itself.
/// </summary>
public static class EndpointRouteBuilderExtensions
{
    public static RouteHandlerBuilder MapGet(this RouteGroupBuilder builder, string pattern, Delegate handler) =>
        ((IEndpointRouteBuilder)builder).MapGet(pattern, handler).WithName(handler.Method.Name);

    public static RouteHandlerBuilder MapPost(this RouteGroupBuilder builder, string pattern, Delegate handler) =>
        ((IEndpointRouteBuilder)builder).MapPost(pattern, handler).WithName(handler.Method.Name);

    public static RouteHandlerBuilder MapPut(this RouteGroupBuilder builder, string pattern, Delegate handler) =>
        ((IEndpointRouteBuilder)builder).MapPut(pattern, handler).WithName(handler.Method.Name);

    public static RouteHandlerBuilder MapPatch(this RouteGroupBuilder builder, string pattern, Delegate handler) =>
        ((IEndpointRouteBuilder)builder).MapPatch(pattern, handler).WithName(handler.Method.Name);

    public static RouteHandlerBuilder MapDelete(this RouteGroupBuilder builder, string pattern, Delegate handler) =>
        ((IEndpointRouteBuilder)builder).MapDelete(pattern, handler).WithName(handler.Method.Name);
}
