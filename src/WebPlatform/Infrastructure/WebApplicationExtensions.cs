using System.Reflection;

namespace WebPlatform.Infrastructure;

public static class WebApplicationExtensions
{
    /// <summary>
    /// Reflection-discovers every <see cref="IEndpointGroup"/> across <paramref name="assemblies"/>
    /// (e.g. a host's own executing assembly plus the shared <c>WebPlatform</c> assembly that holds
    /// the pipeline-agnostic endpoint groups) and invokes its static <c>Map(RouteGroupBuilder)</c>
    /// method. Adding a new feature's endpoints is just adding the class - nothing to register by
    /// hand here.
    /// </summary>
    public static void MapEndpoints(this WebApplication app, params Assembly[] assemblies)
    {
        var groupTypes = assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IEndpointGroup).IsAssignableFrom(t));

        foreach (var groupType in groupTypes)
        {
            var mapMethod = groupType.GetMethod("Map", BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException(
                    $"{groupType.Name} implements {nameof(IEndpointGroup)} but has no public static Map(RouteGroupBuilder) method.");

            var routeGroup = app.MapGroup(string.Empty).WithTags(groupType.Name);
            mapMethod.Invoke(null, [routeGroup]);
        }
    }
}
