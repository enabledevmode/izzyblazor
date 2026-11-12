namespace IzzyBlazor.Generation;

/// <summary>
/// What generating an item changes, as data: nothing is written until a front end applies it. The CLI writes it,
/// <c>--dry-run --json</c> prints it, the IDE plugins diff and apply it through the editor.
/// </summary>
public sealed class Changeset
{
    /// <summary>Files to create, by path relative to the project folder, in path order.</summary>
    public required IReadOnlyList<FileCreate> Creates { get; init; }

    /// <summary>NuGet packages to reference.</summary>
    public required IReadOnlyList<PackageReference> Packages { get; init; }
}

/// <summary>A file to create.</summary>
/// <param name="Path">Relative to the project folder, with forward slashes.</param>
/// <param name="Text">Its text, or null when it's binary.</param>
/// <param name="Bytes">Its bytes, when it's binary.</param>
public sealed record FileCreate(string Path, string? Text, byte[]? Bytes);

/// <summary>A NuGet package to reference.</summary>
/// <param name="Id">The package id.</param>
/// <param name="Version">The version.</param>
public sealed record PackageReference(string Id, string Version);
