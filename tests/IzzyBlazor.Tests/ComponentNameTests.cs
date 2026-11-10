using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace IzzyBlazor.Tests;

// Our components have plain names (Button, not IzButton), as do the framework's, and an app's
// _Imports brings in both namespaces: a framework release that adds a component by one of our names
// makes every use of that tag ambiguous. .NET 10 added Label and DisplayName, .NET 11 CacheView.
public class ComponentNameTests
{
    [Fact]
    public void No_component_shares_a_name_with_one_of_the_frameworks()
    {
        static string Plain(Type t) => t.Name.Split('`')[0];
        static IEnumerable<Type> Components(Assembly a) => a.GetExportedTypes().Where(t => !t.IsAbstract && typeof(IComponent).IsAssignableFrom(t));

        var folder = Path.GetDirectoryName(typeof(ComponentBase).Assembly.Location)!;
        var framework = Directory.GetFiles(folder, "Microsoft.AspNetCore.Components*.dll")
            .Select(Assembly.LoadFrom)
            .SelectMany(Components)
            .Select(Plain)
            .ToHashSet();
        Assert.Contains("NavLink", framework);

        var ours = Components(typeof(Button).Assembly).Select(Plain).ToHashSet();
        Assert.Empty(ours.Intersect(framework));
    }
}
