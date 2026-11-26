using System.Text.RegularExpressions;
using IzzyBlazor.Generation;

namespace IzzyBlazor.Cli;

/// <summary>
/// What <c>izzy add page</c> and <c>izzy add razor</c> work out from the app before the generator runs: the
/// file, the route, whether the page names its render mode, the nav link, and the <c>@using</c> a new folder
/// needs. Both refuse a file that is already there, and a page refuses a route another page has.
/// </summary>
static class Pages
{
    /// <param name="routed">A page (<c>izzy add page</c>); false for a component with no route.</param>
    /// <param name="notes">What the person should know, such as a nav that wasn't found.</param>
    public static ProjectInfo Prepare(ProjectInfo project, bool routed, Dictionary<string, string> settings, List<string> notes)
    {
        string? Given(string name) => settings.FirstOrDefault(s => s.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value is { Length: > 0 } value ? value : null;
        var command = routed ? "izzy add page --name <Name>" : "izzy add razor --name <Name>";
        var name = Given("Name") ?? throw new UsageException($"Give the {(routed ? "page" : "component")} a name: {command}, such as {(routed ? "Orders" : "OrderSummary")}.");
        if (!Regex.IsMatch(name, @"^[A-Z][A-Za-z0-9_]*$"))
            throw new UsageException($"--name is a component's name, such as {(routed ? "Orders" : "OrderSummary")}, not \"{name}\": it starts with a capital letter and has no spaces.");

        var mode = routed ? Given("Mode") ?? "Static" : "Static";
        // The @rendermode names say the same: InteractiveServer is Server (the VS Code extension sends these).
        if (mode.StartsWith("Interactive", StringComparison.OrdinalIgnoreCase) && mode.Length > 11)
        {
            mode = mode[11..];
            foreach (var key in settings.Keys.Where(k => k.Equals("Mode", StringComparison.OrdinalIgnoreCase)).ToList()) settings[key] = mode;
        }
        var program = project.ReadFile(project.Host + "Program.cs") ?? "";
        // An app whose pages all run in the browser, or one whose Routes are interactive: a page says nothing.
        var app = project.ReadFile(project.Host + "Components/App.razor") ?? "";
        var atRoot = project.LayoutInBrowser || Regex.IsMatch(app, @"<Routes\b[^>]*@rendermode");
        var inBrowser = mode.Equals("WebAssembly", StringComparison.OrdinalIgnoreCase) || mode.Equals("Auto", StringComparison.OrdinalIgnoreCase);
        if (inBrowser && project.Client is null)
            throw new UsageException($"--mode {mode.ToLowerInvariant()} is for an app with a .Client project, whose pages can run in the browser. {project.Name} has none: use --mode server, or leave the page static.");
        if (mode.Equals("Server", StringComparison.OrdinalIgnoreCase) && !Regex.IsMatch(program, @"\bAddInteractiveServerComponents\b"))
            throw new UsageException($"--mode server needs interactive server components, and {project.Host}Program.cs doesn't add them (AddInteractiveServerComponents). Leave the page static, or add them first.");
        if (mode.Equals("Auto", StringComparison.OrdinalIgnoreCase) && !Regex.IsMatch(program, @"\bAddInteractiveServerComponents\b"))
            throw new UsageException($"--mode auto starts on a circuit, and {project.Host}Program.cs doesn't add interactive server components. Use --mode webassembly.");

        var folder = (Given("Folder") ?? (!routed ? "Components" : inBrowser && !project.LayoutInBrowser ? project.Client + "Pages" : "Components/Pages")).Replace('\\', '/').Trim('/');
        if (folder.Split('/').Any(part => part is "" or "." or "..") || Path.IsPathRooted(folder))
            throw new UsageException($"--folder is a folder in the project, such as Components/Pages/Admin, not \"{Given("Folder")}\".");
        if (inBrowser && !project.LayoutInBrowser && !folder.StartsWith(project.Client!, StringComparison.Ordinal))
            throw new UsageException($"A page that runs in the browser is in the .Client project: give --folder a folder under {project.Client}, or leave it out.");
        var file = InApp(project, $"{folder}/{name}.razor");
        if (project.ReadFile(file) is not null) throw new UsageException($"{file} is already there. Pick another --name or --folder.");

        var facts = new Dictionary<string, object>(project.Facts) { ["Routed"] = routed, ["AddNav"] = false, ["NavIcons"] = false, ["HasLayout"] = false, ["SaysRenderMode"] = false, ["AddUsing"] = false };
        var tokens = new Dictionary<string, string>(project.Tokens) { ["File"] = "~/" + file, ["Title"] = ModelReader.Words(name), ["FormName"] = Kebab(name) };
        settings["Name"] = name;

        if (!routed)
        {
            // The folder's namespace, and the _Imports.razor of the project the file is in: a line there makes
            // the component usable on every page.
            var inClient = project.Client is { } client && file.StartsWith(client, StringComparison.Ordinal);
            var root = inClient ? project.Client! : project.Host;
            var imports = inClient ? root + "_Imports.razor" : root + "Components/_Imports.razor";
            var inside = Path.GetDirectoryName(file[root.Length..])!.Replace('\\', '/');
            var ns = (inClient ? project.ClientNamespace ?? project.RootNamespace + ".Client" : project.RootNamespace) + (inside.Length > 0 ? "." + string.Join('.', inside.Split('/').Select(Cli.NamespacePart)) : "");
            tokens["Namespace"] = ns;
            tokens["ImportsFile"] = "~/" + imports;
            if (project.ReadFile(imports) is { } text)
                facts["AddUsing"] = !Regex.IsMatch(text, $@"^\s*@using\s+{Regex.Escape(ns)}\s*;?\s*$", RegexOptions.Multiline);
            else notes.Add($"{imports} wasn't found: to use {name} on a page, add @using {ns} there.");
            return project with { Facts = facts, Tokens = tokens };
        }

        var route = (Given("Route") ?? Kebab(name)).Trim().Trim('/');
        if (route.Contains('"') || route.Contains(' ') || route.Contains('?'))
            throw new UsageException($"--route is the page's address, such as orders or orders/{{id:int}}, not \"{Given("Route")}\".");
        if (RouteOf(project, route) is { } taken) throw new UsageException($"/{route} is already the route of {taken}. Pick another --route.");
        settings["Route"] = route;
        tokens["Route"] = route;
        tokens["NavRoute"] = route;
        // A page in an app that is static unless a page says otherwise says its mode.
        facts["SaysRenderMode"] = !atRoot && !mode.Equals("Static", StringComparison.OrdinalIgnoreCase);
        tokens["RenderMode"] = mode.ToLowerInvariant() switch { "server" => "InteractiveServer", "webassembly" => "InteractiveWebAssembly", "auto" => "InteractiveAuto", _ => "" };
        if (Given("Layout") is { } layout)
        {
            if (!Regex.IsMatch(layout, @"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$"))
                throw new UsageException($"--layout is a layout component's name, such as EmptyLayout, not \"{layout}\".");
            facts["HasLayout"] = true;
        }
        if ("true".Equals(Given("Nav"), StringComparison.OrdinalIgnoreCase))
        {
            var navFile = project.LayoutInBrowser ? project.Client + "Layout/MainLayout.razor" : project.Host + "Components/Layout/MainLayout.razor";
            var nav = project.ReadFile(navFile);
            tokens["LayoutFile"] = "~/" + navFile;
            if (route.Contains('{')) notes.Add($"/{route} has a parameter, so no nav link was added: a link needs a whole address.");
            else if (nav is null || !Regex.IsMatch(nav, @"<NavItem Href="""" Match=""NavLinkMatch\.All"""))
                notes.Add($"No nav was found in {navFile}, so no link was added: add one to /{route} where your app's nav is.");
            else if (!Regex.IsMatch(nav, $@"<NavItem\b[^>]*\bHref=""/?{Regex.Escape(route)}""", RegexOptions.IgnoreCase))
            {
                facts["AddNav"] = true;
                facts["NavIcons"] = Regex.IsMatch(nav, @"<NavItem\b[^>]*\bIcon=");
            }
        }
        return project with { Facts = facts, Tokens = tokens };
    }

    // The path in the app of a path from the project's folder, as the generator places it.
    static string InApp(ProjectInfo project, string path)
    {
        if (project.Client is { } client && path.StartsWith(client, StringComparison.Ordinal)) return path;
        if (project.Host.Length > 0 && path.StartsWith(project.Host, StringComparison.Ordinal)) return path;
        const string components = "Components/";
        if (project.LayoutInBrowser && project.Client is { } inBrowser && path.StartsWith(components, StringComparison.Ordinal)) return inBrowser + path[components.Length..];
        return project.Host + path;
    }

    // The page that has this route already, if one does: its @page line, with or without the first slash.
    static string? RouteOf(ProjectInfo project, string route)
    {
        var root = project.Directory!;
        var line = new Regex($@"^\s*@page\s+""/?{Regex.Escape(route)}/?""\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        return Directory.EnumerateFiles(root, "*.razor", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .Where(f => !Regex.IsMatch(f, @"(^|/)(bin|obj|\.izzy|node_modules|\.git)/"))
            .FirstOrDefault(f => line.IsMatch(File.ReadAllText(Path.Combine(root, f))));
    }

    static string Kebab(string name) => Regex.Replace(name, @"(?<=[a-z0-9])(?=[A-Z])", "-").ToLowerInvariant();
}
