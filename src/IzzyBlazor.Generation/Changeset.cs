namespace IzzyBlazor.Generation;

/// <summary>
/// What generating an item changes, as data: nothing is written until a front end applies it. The CLI writes it
/// with <see cref="ChangesetWriter.Apply"/>, <c>--dry-run --json</c> prints it, the IDE plugins diff and apply it
/// through the editor.
/// </summary>
public sealed class Changeset
{
    /// <summary>Files to create, by path relative to the project folder, in path order. Inserts into them are already made.</summary>
    public required IReadOnlyList<FileCreate> Creates { get; init; }

    /// <summary>Text to add to files already in the project.</summary>
    public required IReadOnlyList<TextInsert> Inserts { get; init; }

    /// <summary>NuGet packages to reference.</summary>
    public required IReadOnlyList<PackageReference> Packages { get; init; }

    /// <summary>Files to create that already exist with other content; they're written only when asked to overwrite.</summary>
    public required IReadOnlyList<string> Conflicts { get; init; }

    /// <summary>What couldn't be done and needs a person, such as an insert whose anchor isn't in the file.</summary>
    public required IReadOnlyList<string> Warnings { get; init; }

    /// <summary>
    /// Every setting's value as used, by name (<c>true</c>, <c>Split</c>, <c>5123</c>), defaults and generated
    /// ports and GUIDs included: what <c>.izzy/items.json</c> records, so the same item can be made again.
    /// </summary>
    public IReadOnlyDictionary<string, string> Settings { get; init; } = new Dictionary<string, string>();

    /// <summary>Whether to restore packages once the files are written (a new starter, unless asked not to).</summary>
    public bool Restore { get; init; }
}

/// <summary>A file to create.</summary>
/// <param name="Path">Relative to the project folder, with forward slashes.</param>
/// <param name="Text">Its text, or null when it's binary.</param>
/// <param name="Bytes">Its bytes, when it's binary.</param>
public sealed record FileCreate(string Path, string? Text, byte[]? Bytes);

/// <summary>Text to add to a file already in the project.</summary>
/// <param name="Path">Relative to the project folder, with forward slashes.</param>
/// <param name="Offset">Where it goes, in characters from the start of the file as it was read.</param>
/// <param name="Text">The text, in the file's line endings.</param>
/// <param name="OriginalHash">The SHA-256 of the file as it was read (hex); applying refuses when the file has changed since.</param>
public sealed record TextInsert(string Path, int Offset, string Text, string OriginalHash);

/// <summary>A NuGet package to reference.</summary>
/// <param name="Id">The package id.</param>
/// <param name="Version">The version.</param>
public sealed record PackageReference(string Id, string Version);
