namespace WebPlatform.Infrastructure;

/// <summary>
/// Marker interface for a Minimal API endpoint group. Implementers expose a
/// <c>public static void Map(RouteGroupBuilder groupBuilder)</c> method, discovered and invoked
/// via reflection by <see cref="WebApplicationExtensions.MapEndpoints"/> - there is no instance
/// to construct and no virtual dispatch.
/// </summary>
public interface IEndpointGroup;
