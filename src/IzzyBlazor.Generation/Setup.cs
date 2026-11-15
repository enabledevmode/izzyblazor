using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace IzzyBlazor.Generation;

/// <summary>
/// What <c>izzy init</c> reads from an app before setting up IzzyBlazor in it: whether IzzyBlazor can join it,
/// what kind of app it is, and the parts of it the setup changes. The catalogue's <c>init</c> item does the
/// rest with these as facts and tokens.
/// </summary>
public static partial class Setup
{
    /// <summary>
    /// The app with the setup's facts (<c>Blazor</c>, <c>AddsBlazor</c>, <c>BootstrapLinked</c>, <c>AppCss</c>,
    /// <c>HasLayout</c>) and tokens (<c>Layout</c>, <c>BootstrapHref</c>). Throws, saying why, for an app
    /// IzzyBlazor can't join: one that isn't an ASP.NET Core app, a standalone WebAssembly app, a Blazor Server
    /// app with a _Host page, or one below .NET 10.
    /// </summary>
    public static ProjectInfo Inspect(ProjectInfo project)
    {
        var root = project.Directory ?? throw new GenerationException("izzy init needs an app on disk.");
        var csproj = Path.Combine(root, project.Host, project.Name + ".csproj");
        var xml = XDocument.Load(csproj);
        var sdk = xml.Root?.Attribute("Sdk")?.Value ?? "";
        if (sdk.Contains("BlazorWebAssembly", StringComparison.OrdinalIgnoreCase))
            throw new GenerationException($"{project.Name} is a standalone Blazor WebAssembly app. IzzyBlazor needs a server, where the theme and the components that read cookies render: make a Blazor Web App with WebAssembly pages instead (izzy new izzy-blazor --interactivity WebAssembly).");
        if (!sdk.Contains("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase))
            throw new GenerationException($"{project.Name} isn't an ASP.NET Core app (its project's SDK is {(sdk.Length > 0 ? sdk : "none")}). IzzyBlazor goes in a Blazor Web App, or an MVC or Razor Pages app.");
        var frameworks = string.Join(";", xml.Descendants().Where(e => e.Name.LocalName is "TargetFramework" or "TargetFrameworks").Select(e => e.Value));
        if (Regex.Matches(frameworks, @"net(\d+)\.\d").Select(m => int.Parse(m.Groups[1].Value)).DefaultIfEmpty(0).Max() is var version && version < 10)
            throw new GenerationException($"{project.Name} targets {(frameworks.Length > 0 ? frameworks : "no framework")}; IzzyBlazor needs .NET 10 (net10.0).");

        var program = project.ReadFile(project.Host + "Program.cs")
            ?? throw new GenerationException($"{project.Name} has no Program.cs, where izzy init registers IzzyBlazor's services.");
        var blazor = program.Contains("MapRazorComponents<", StringComparison.Ordinal);
        if (!blazor && (program.Contains("MapBlazorHub", StringComparison.Ordinal) || project.ReadFile(project.Host + "Pages/_Host.cshtml") is not null))
            throw new GenerationException($"{project.Name} is a Blazor Server app with a _Host page, from before .NET 8. Move it to a Blazor Web App (App.razor and MapRazorComponents) first: https://learn.microsoft.com/aspnet/core/migration/70-to-80");

        var app = blazor ? project.ReadFile(project.Host + "Components/App.razor") ?? "" : "";
        var bootstrap = LinkedBootstrap().Match(app);
        var appCss = app.Contains("@Assets[\"app.css\"]", StringComparison.Ordinal) && project.ReadFile(project.Host + "wwwroot/app.css") is not null;
        var layout = blazor ? FindLayout(project) : null;
        return project with
        {
            Facts = new Dictionary<string, object>(project.Facts)
            {
                ["Blazor"] = blazor,
                ["AddsBlazor"] = !blazor,
                ["BootstrapLinked"] = bootstrap.Success,
                ["AppCss"] = appCss,
                ["HasLayout"] = layout is not null,
            },
            Tokens = new Dictionary<string, string>(project.Tokens)
            {
                ["Layout"] = layout ?? "",
                ["BootstrapHref"] = bootstrap.Success ? bootstrap.Groups["href"].Value : "",
            },
        };
    }

    /// <summary>
    /// Edits that write the full name of each of the app's components named like one of IzzyBlazor's
    /// (<c>&lt;MyApp.Components.Layout.NavMenu /&gt;</c>) where a .razor file uses it, so <c>@using IzzyBlazor</c>
    /// doesn't make the tag ambiguous (RZ9985).
    /// </summary>
    public static IReadOnlyList<TextInsert> QualifyClashes(ProjectInfo project)
    {
        if (project.Directory is null) throw new GenerationException("izzy init needs an app on disk.");
        var files = RazorFiles(project).ToList();
        var clashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in files)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (!ComponentNames.Contains(name) || clashes.ContainsKey(name)) continue;
            var declared = Namespace().Match(project.ReadFile(path) ?? "");
            clashes[name] = (declared.Success ? declared.Groups[1].Value : FolderNamespace(project, path)) + "." + name;
        }
        if (clashes.Count == 0) return [];

        var tag = new Regex($@"<(/?)({string.Join("|", clashes.Keys.Select(Regex.Escape))})(?=[\s/>])", RegexOptions.None, TimeSpan.FromSeconds(1));
        var edits = new List<TextInsert>();
        foreach (var path in files)
        {
            var text = project.ReadFile(path)!;
            var hash = Inserts.Hash(text);
            foreach (Match m in tag.Matches(text))
                edits.Add(new TextInsert(path, m.Groups[2].Index, clashes[m.Groups[2].Value], hash) { Length = m.Groups[2].Length });
        }
        return edits;
    }

    /// <summary>
    /// The rules in the server project's wwwroot/app.css that style bare elements outside a cascade layer
    /// (<c>a { color: … }</c>, <c>html, body { font-family: … }</c>): they win over IzzyBlazor's components.
    /// Microsoft's own <c>h1:focus</c>, which goes with FocusOnNavigate, isn't one.
    /// </summary>
    public static IReadOnlyList<string> UnlayeredElementRules(ProjectInfo project)
    {
        if (project.ReadFile(project.Host + "wwwroot/app.css") is not { } css) return [];
        css = Comment().Replace(css, "");
        var rules = new List<string>();
        var depth = 0;
        var layered = new Stack<bool>();
        var selector = new StringBuilder();
        foreach (var c in css)
        {
            if (c == '{')
            {
                var head = selector.ToString().Trim();
                selector.Clear();
                var inLayer = layered.Any(l => l);
                layered.Push(head.StartsWith("@layer", StringComparison.Ordinal));
                depth++;
                if (!inLayer && !head.StartsWith('@') && head.Split(',').Select(s => s.Trim()).Any(IsElement) && head != "h1:focus")
                    rules.Add(Regex.Replace(head, @"\s+", " "));
            }
            else if (c == '}')
            {
                if (depth > 0) { depth--; layered.Pop(); }
                selector.Clear();
            }
            else if (c == ';' && depth == 0) selector.Clear();
            else selector.Append(c);
        }
        return rules;
    }

    static bool IsElement(string selector) =>
        Regex.IsMatch(selector, @"^(html|body|a|h[1-6]|p|button|input|select|textarea|label|table|th|td|ul|ol|li|img)(?![\w-])");

    /// <summary>The names of IzzyBlazor's components and their parts, from the library (kept in step by a test).</summary>
    internal static IReadOnlySet<string> ComponentNames { get; } = LoadNames();

    static HashSet<string> LoadNames()
    {
        using var stream = typeof(Setup).Assembly.GetManifestResourceStream("IzzyBlazor.Generation.components.txt")
            ?? throw new InvalidOperationException("components.txt isn't embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
    }

    // The .razor files of the app's projects, by path from its folder.
    static IEnumerable<string> RazorFiles(ProjectInfo project)
    {
        var root = Path.GetFullPath(project.Directory!);
        foreach (var folder in new[] { project.Host, project.Client }.Where(f => f is not null).Distinct())
        {
            var start = Path.Combine(root, folder!);
            foreach (var file in Directory.EnumerateFiles(start, "*.razor", SearchOption.AllDirectories))
            {
                var path = Path.GetRelativePath(root, file).Replace('\\', '/');
                var inProject = path[folder!.Length..];
                if (inProject.StartsWith("bin/", StringComparison.Ordinal) || inProject.StartsWith("obj/", StringComparison.Ordinal)) continue;
                // A project folder holding the other one (the .Client under the server's): each file once.
                if (folder == project.Host && project.Client is { } client && client.StartsWith(project.Host, StringComparison.Ordinal) && client != project.Host && path.StartsWith(client, StringComparison.Ordinal)) continue;
                yield return path;
            }
        }
    }

    static string FolderNamespace(ProjectInfo project, string path)
    {
        var inClient = project.Client is { } client && path.StartsWith(client, StringComparison.Ordinal);
        var folder = Path.GetDirectoryName(path[(inClient ? project.Client! : project.Host).Length..])?.Replace('\\', '/') ?? "";
        var start = inClient ? project.ClientNamespace! : project.RootNamespace;
        return folder.Length == 0 ? start : start + "." + string.Join(".", folder.Split('/').Select(Names.SafeNamespace));
    }

    // The layout the router gives pages: DefaultLayout="typeof(Layout.MainLayout)" names MainLayout.razor.
    static string? FindLayout(ProjectInfo project)
    {
        var files = RazorFiles(project).ToList();
        var ui = project.LayoutInBrowser ? project.Client! : project.Host;
        foreach (var path in files.Where(f => f.StartsWith(ui, StringComparison.Ordinal)))
        {
            if (DefaultLayout().Match(project.ReadFile(path) ?? "") is not { Success: true } m) continue;
            var name = m.Groups[1].Value.Split('.')[^1];
            return files.FirstOrDefault(f => f.StartsWith(ui, StringComparison.Ordinal) && Path.GetFileName(f) == name + ".razor");
        }
        return null;
    }

    [GeneratedRegex(@"<link[^>]*href=""(?:@Assets\[""(?<href>[^""]*bootstrap[^""]*)""\]|(?<href>[^""]*bootstrap[^""]*))""[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex LinkedBootstrap();

    [GeneratedRegex(@"^\s*@namespace\s+([\w.]+)", RegexOptions.Multiline)]
    private static partial Regex Namespace();

    [GeneratedRegex(@"DefaultLayout=""typeof\(([\w.]+)\)""")]
    private static partial Regex DefaultLayout();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex Comment();
}
