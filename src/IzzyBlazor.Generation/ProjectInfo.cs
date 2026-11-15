using System.Xml.Linq;

namespace IzzyBlazor.Generation;

/// <summary>
/// The app an item is generated into: its name and namespace (the <c>{{ProjectName}}</c> and
/// <c>{{RootNamespace}}</c> tokens), how its projects are laid out, and a way to read its files, for inserts and
/// conflicts. Paths are relative to <see cref="Directory"/>: the project's folder, or with a <c>.Client</c>
/// project the folder holding both (<c>MyApp/Program.cs</c>, <c>MyApp.Client/Pages/Home.razor</c>).
/// </summary>
/// <param name="Name">The server project's name, as given to <c>-n</c> or read from its .csproj file name.</param>
/// <param name="RootNamespace">The server project's root namespace.</param>
public sealed record ProjectInfo(string Name, string RootNamespace)
{
    /// <summary>Reads a file by its path relative to <see cref="Directory"/> (forward slashes); null when there's none.</summary>
    public Func<string, string?> ReadFile { get; init; } = _ => null;

    /// <summary>The folder paths are relative to; null for a project not written yet.</summary>
    public string? Directory { get; init; }

    /// <summary>The server project's folder, from <see cref="Directory"/>, ending in a slash; empty when it is <see cref="Directory"/>.</summary>
    public string Host { get; init; } = "";

    /// <summary>The <c>.Client</c> project's folder (WebAssembly or Auto), ending in a slash; null when there's none.</summary>
    public string? Client { get; init; }

    /// <summary>The <c>.Client</c> project's root namespace.</summary>
    public string? ClientNamespace { get; init; }

    /// <summary>
    /// Whether the router, layout and pages are in the <c>.Client</c> project (every page interactive in the
    /// browser): an item's <c>Components/</c> files then go there, except the ones it marks as server-only.
    /// </summary>
    public bool LayoutInBrowser { get; init; }

    /// <summary>More values for conditions, worked out from the project (init's: what kind of app, whether it links Bootstrap).</summary>
    public IReadOnlyDictionary<string, object> Facts { get; init; } = new Dictionary<string, object>();

    /// <summary>More tokens, worked out from the project (init's: the layout file, Bootstrap's address).</summary>
    public IReadOnlyDictionary<string, string> Tokens { get; init; } = new Dictionary<string, string>();

    /// <summary>A new project: the namespace comes from the name, as <c>dotnet new</c> works it out, and no files exist yet.</summary>
    public static ProjectInfo ForNewProject(string name) => new(name, Names.SafeNamespace(name));

    /// <summary>An existing project, from the folder holding its one .csproj file. See <see cref="FromProject"/>.</summary>
    public static ProjectInfo FromDirectory(string directory)
    {
        var projects = System.IO.Directory.GetFiles(directory, "*.csproj");
        if (projects.Length != 1)
            throw new GenerationException(projects.Length == 0 ? $"No .csproj file in {directory}." : $"More than one .csproj file in {directory}; pick one.");
        return FromProject(projects[0]);
    }

    /// <summary>
    /// An existing app, from either of its projects. A server project's <c>.Client</c> is the WebAssembly project it
    /// references; a WebAssembly project's server is a project near it that references it. The server project
    /// names the app; its <c>RootNamespace</c> (or the name made safe) is the namespace.
    /// </summary>
    public static ProjectInfo FromProject(string csproj)
    {
        csproj = Path.GetFullPath(csproj);
        string? host = csproj, client = null;
        if (IsWebAssembly(csproj))
        {
            client = csproj;
            host = FindHost(csproj);
        }
        else client = References(csproj).FirstOrDefault(IsWebAssembly);

        // A WebAssembly project on its own is the app.
        host ??= csproj;
        if (host == client) client = null;
        var hostDir = Path.GetDirectoryName(host)!;
        var root = client is null ? hostDir : CommonFolder(hostDir, Path.GetDirectoryName(client)!);
        var name = Path.GetFileNameWithoutExtension(host);
        var clientPrefix = client is null ? null : Prefix(root, Path.GetDirectoryName(client)!);
        return new ProjectInfo(name, Namespace(host))
        {
            Directory = root,
            Host = Prefix(root, hostDir),
            Client = clientPrefix,
            ClientNamespace = client is null ? null : Namespace(client),
            LayoutInBrowser = client is not null && HasRouter(Path.GetDirectoryName(client)!),
            ReadFile = Reader(root),
        };
    }

    /// <summary>Reads files under <paramref name="root"/> by relative path, refusing paths that leave it.</summary>
    public static Func<string, string?> Reader(string root)
    {
        root = Path.GetFullPath(root);
        return path =>
        {
            var full = Path.GetFullPath(Path.Combine(root, path));
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new GenerationException($"{path} is outside the project.");
            if (File.Exists(full)) return File.ReadAllText(full);
            // A name in another case (privacy for Privacy.cshtml), as Windows and macOS find it, on Linux too.
            var folder = Path.GetDirectoryName(full)!;
            var name = Path.GetFileName(full);
            var other = System.IO.Directory.Exists(folder)
                ? System.IO.Directory.EnumerateFiles(folder).FirstOrDefault(f => Path.GetFileName(f).Equals(name, StringComparison.OrdinalIgnoreCase))
                : null;
            return other is null ? null : File.ReadAllText(other);
        };
    }

    /// <summary>The path relative to <see cref="Directory"/> of a file in the project (an editor's file, <c>--at</c>).</summary>
    public string Relative(string fullPath)
    {
        var path = Path.GetRelativePath(Directory ?? ".", fullPath).Replace('\\', '/');
        return path.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(path) ? throw new GenerationException($"{fullPath} isn't in the project.") : path;
    }

    static bool IsWebAssembly(string csproj) =>
        File.Exists(csproj) && (XDocument.Load(csproj).Root?.Attribute("Sdk")?.Value ?? "").Contains("BlazorWebAssembly", StringComparison.OrdinalIgnoreCase);

    static IEnumerable<string> References(string csproj) =>
        XDocument.Load(csproj).Descendants("ProjectReference")
            .Select(r => (string?)r.Attribute("Include"))
            .Where(r => r is not null)
            .Select(r => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(csproj)!, r!.Replace('\\', Path.DirectorySeparatorChar))));

    // The server project of a .Client one: a web project beside it (or one folder further down) that references
    // it, so a test project referencing the .Client isn't taken for it. Folders that can't be read are skipped.
    static string? FindHost(string client)
    {
        var parent = Path.GetDirectoryName(Path.GetDirectoryName(client)!);
        if (parent is null) return null;
        var near = new List<string>(Projects(parent));
        foreach (var folder in Safe(() => System.IO.Directory.GetDirectories(parent)) ?? []) near.AddRange(Projects(folder));
        return near.Order(StringComparer.OrdinalIgnoreCase).FirstOrDefault(p => !p.Equals(client, StringComparison.OrdinalIgnoreCase)
            && Safe(() => IsWeb(p) && References(p).Any(r => r.Equals(client, StringComparison.OrdinalIgnoreCase))));

        static string[] Projects(string folder) => Safe(() => System.IO.Directory.GetFiles(folder, "*.csproj")) ?? [];
    }

    static T Safe<T>(Func<T> read)
    {
        try { return read(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Xml.XmlException) { return default!; }
    }

    static bool IsWeb(string csproj) =>
        (XDocument.Load(csproj).Root?.Attribute("Sdk")?.Value ?? "").Contains("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase);

    // Whether a .razor file in the folder holds the app's Router.
    static bool HasRouter(string directory) =>
        System.IO.Directory.EnumerateFiles(directory, "*.razor", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Any(f => File.ReadAllText(f).Contains("<Router ", StringComparison.Ordinal));

    static string Namespace(string csproj)
    {
        var rootNamespace = XDocument.Load(csproj).Descendants("RootNamespace").LastOrDefault()?.Value.Trim();
        return string.IsNullOrEmpty(rootNamespace) ? Names.SafeNamespace(Path.GetFileNameWithoutExtension(csproj)) : rootNamespace;
    }

    static string CommonFolder(string a, string b)
    {
        var x = a.TrimEnd(Path.DirectorySeparatorChar).Split(Path.DirectorySeparatorChar);
        var y = b.TrimEnd(Path.DirectorySeparatorChar).Split(Path.DirectorySeparatorChar);
        var same = 0;
        while (same < x.Length && same < y.Length && x[same].Equals(y[same], StringComparison.OrdinalIgnoreCase)) same++;
        return string.Join(Path.DirectorySeparatorChar, x[..same]);
    }

    static string Prefix(string root, string directory)
    {
        var relative = Path.GetRelativePath(root, directory).Replace('\\', '/');
        return relative == "." ? "" : relative + "/";
    }
}
