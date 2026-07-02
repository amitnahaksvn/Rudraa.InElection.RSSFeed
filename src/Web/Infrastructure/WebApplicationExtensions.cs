using System.Reflection;

namespace Web.Infrastructure;

public static class WebApplicationExtensions
{
    /// <summary>
    /// Reflection-discovers every <see cref="IEndpointGroup"/> in <paramref name="assembly"/> and
    /// invokes its static <c>Map(RouteGroupBuilder)</c> method. Adding a new feature's endpoints is
    /// just adding the class - nothing to register by hand here.
    /// </summary>
    public static void MapEndpoints(this WebApplication app, Assembly assembly)
    {
        var groupTypes = assembly.GetTypes()
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
