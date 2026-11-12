namespace IzzyBlazor.Generation;

/// <summary>The project an item is generated into: its name and namespace, the <c>{{ProjectName}}</c> and <c>{{RootNamespace}}</c> tokens.</summary>
/// <param name="Name">The project's name, as given to <c>-n</c> or read from its .csproj file name.</param>
/// <param name="RootNamespace">Its root namespace.</param>
public sealed record ProjectInfo(string Name, string RootNamespace)
{
    /// <summary>A new project: the namespace comes from the name, as <c>dotnet new</c> works it out.</summary>
    public static ProjectInfo ForNewProject(string name) => new(name, Names.SafeNamespace(name));
}
