using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace IzzyBlazor.Generation;

/// <summary>Generates an item: its manifest and template files plus settings in, a <see cref="Changeset"/> out. Writes nothing.</summary>
public static class Generator
{
    static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// The IzzyBlazor version items reference (the <c>{{IzzyBlazorVersion}}</c> token): this engine's own, since
    /// the engine, the templates and the components are released together.
    /// </summary>
    public static string IzzyBlazorVersion { get; } =
        (typeof(Generator).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion ?? "0.0.0")
        .Split('+')[0];

    /// <summary>Generates <paramref name="item"/> into <paramref name="project"/> with the given settings.</summary>
    /// <param name="item">The item.</param>
    /// <param name="settings">Settings by name, as text (<c>true</c>, <c>Auto</c>, <c>3</c>); the rest take their defaults.</param>
    /// <param name="project">The project's name and namespace.</param>
    /// <param name="at">A line to put the item at instead of its own place; null for its own place.</param>
    public static Changeset Generate(ItemDefinition item, IReadOnlyDictionary<string, string> settings, ProjectInfo project, Placement? at = null)
    {
        var manifest = item.Manifest;
        var place = manifest.Inserts.FirstOrDefault(i => i.Place);
        if (at is not null)
        {
            if (place is null) throw new GenerationException($"{manifest.Id} isn't placed on a page, so it can't go at a line.");
            // A place in a file named by a setting ({{Into}}): the setting becomes that file, so checks read it.
            if (Regex.Match(place.File, @"^\{\{(\w+)\}\}$") is { Success: true } token
                && manifest.Parameters.Any(p => p.Name == token.Groups[1].Value)
                && !settings.Keys.Any(k => k.Equals(token.Groups[1].Value, StringComparison.OrdinalIgnoreCase)))
                settings = new Dictionary<string, string>(settings) { [token.Groups[1].Value] = at.Path };
        }
        var values = Resolve(manifest, settings, Facts(project));
        Refuse(item, values);
        var tokens = Tokens(manifest, values, project);
        // What a person wrote, as opposed to a name we check or a value the item fixes: escaped where it lands.
        var typed = manifest.Parameters
            .Where(p => p.Type == ParameterType.Text && p.Pattern is null && !manifest.Fixed.ContainsKey(p.Name))
            .Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        var server = manifest.Server.Select(g => new Glob(TemplateText.ReplaceTokens(g, tokens))).ToList();
        string InApp(string path) => ToApp(path, project, server);

        var excluded = new List<Glob>();
        foreach (var rule in manifest.Exclude)
            if (Condition.Evaluate(rule.If, values)) excluded.AddRange(rule.Paths.Select(p => new Glob(p)));
        var renames = manifest.Rename.Where(r => Condition.Evaluate(r.If, values)).SelectMany(r => r.Paths).ToList();

        // Files an insert takes its text from are inserted, not written.
        var snippets = manifest.Inserts.Where(i => i.TextFrom is not null).Select(i => i.TextFrom!).ToHashSet(StringComparer.Ordinal);
        foreach (var snippet in snippets)
            if (!item.Files.ContainsKey(snippet)) throw new GenerationException($"{manifest.Id}: an insert's textFrom names {snippet}, which isn't in its files.");

        var creates = new List<FileCreate>();
        foreach (var (path, bytes) in item.Files)
        {
            if (excluded.Any(e => e.Matches(path)) || snippets.Contains(path)) continue;
            var target = InApp(TemplateText.ReplaceTokens(Renamed(path, renames), tokens));
            if (TemplateText.IsBinary(path))
            {
                creates.Add(new FileCreate(target, null, bytes));
                continue;
            }
            creates.Add(new FileCreate(target, TemplateText.Apply(path, Text(path, bytes), values, tokens, typed), null));
        }
        creates.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));

        // A template's text: UTF-8 with no byte order mark, which would land in the file it's written into.
        string Text(string path, byte[] bytes)
        {
            if (bytes is [0xEF, 0xBB, 0xBF, ..])
                throw new GenerationException($"{manifest.Id}: {path} starts with a byte order mark; save it as UTF-8 without one.");
            try { return StrictUtf8.GetString(bytes); }
            catch (DecoderFallbackException) { throw new GenerationException($"{manifest.Id}: {path} is not UTF-8 text; add its extension to the binary list or re-save it."); }
        }

        var warnings = new List<string>();
        var inserts = new List<TextInsert>();
        foreach (var written in manifest.Inserts)
        {
            if (written.If is not null && !Condition.Evaluate(written.If, values)) continue;
            var path = TemplateText.ReplaceTokens(written.File, tokens);
            if (path.Length > 0) path = InApp(path);
            var text = written.TextFrom is { } from
                ? TemplateText.Apply(from, Text(from, item.Files[from]), values, tokens, typed)
                : TemplateText.ReplaceTokens(path, written.Text, tokens, typed);
            // A block that belongs to a layout (a footer) has no page of its own unless one is named.
            if (written.Place && at is null && path.Length == 0)
            {
                warnings.Add(text.Trim().Contains('\n')
                    ? $"{manifest.Id} isn't placed on a page: add this where it goes, or place it with --at <file>:<line>.\n{text.TrimEnd()}"
                    : $"{manifest.Id} isn't placed on a page: add {text.Trim()} where it goes, or place it with --at <file>:<line>.");
                continue;
            }
            if (at is not null && written.Place)
            {
                path = at.Path;
                // No check for it already being there: the line was given, and a recipe can go on a page twice.
                var target = project.ReadFile(path) ?? throw new GenerationException($"{path} isn't in the project.");
                var (offset, body) = Inserts.AtLine(target, path, at.Line, text);
                inserts.Add(new TextInsert(path, offset, body, Inserts.Hash(target)));
                continue;
            }
            var unless = written.Unless is null ? null : TemplateText.ReplaceTokens(written.Unless, tokens);
            var spec = written with
            {
                Text = text,
                Anchor = written.Anchor is null ? null : TemplateText.ReplaceTokens(written.Anchor, tokens),
                Pattern = written.Pattern is null ? null : TemplateText.ReplaceTokens(written.Pattern, tokens),
            };
            var created = creates.FindIndex(c => c.Path == path && c.Text is not null);
            if (created >= 0)
            {
                var file = creates[created].Text!;
                if (unless is not null && file.Contains(unless, StringComparison.Ordinal)) continue;
                if (Inserts.Place(file, spec, text) is { } spot) creates[created] = creates[created] with { Text = file.Remove(spot.Offset, spot.Length).Insert(spot.Offset, spot.Text) };
                else if (spec.Position != InsertPosition.Replace && !Inserts.AlreadyThere(file, text)) warnings.Add(Inserts.NoAnchor(manifest.Id, path, spec));
                continue;
            }
            var existing = project.ReadFile(path);
            if (existing is null)
            {
                warnings.Add($"{manifest.Id}: {path} isn't in the project, so this wasn't added to it:\n{text}");
                continue;
            }
            // Made already, and edited since: the file holds what the insert names, not its text.
            if (unless is not null && existing.Contains(unless, StringComparison.Ordinal)) continue;
            if (Inserts.Place(existing, spec, text) is { } found)
            {
                // A replacement with the text that's there already changes nothing.
                if (found.Length > 0 && found.Length == found.Text.Length && string.CompareOrdinal(existing, found.Offset, found.Text, 0, found.Length) == 0) continue;
                inserts.Add(new TextInsert(path, found.Offset, found.Text, Inserts.Hash(existing)) { Length = found.Length });
            }
            // A replacement with nothing to replace is made already.
            else if (spec.Position != InsertPosition.Replace && !Inserts.AlreadyThere(existing, text)) warnings.Add(Inserts.NoAnchor(manifest.Id, path, spec));
        }

        // Checks read the project as it is, before anything here is applied.
        foreach (var check in manifest.Checks)
        {
            if (check.If is not null && !Condition.Evaluate(check.If, values)) continue;
            var file = TemplateText.ReplaceTokens(check.File, tokens);
            var text = project.ReadFile(InApp(file));
            // The message names the file as the app has it (MyApp.Client/Pages/Home.razor).
            if (text is not null && System.Text.RegularExpressions.Regex.IsMatch(text, check.Pattern, System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1)))
                warnings.Add($"{manifest.Id}: {TemplateText.ReplaceTokens(check.Message, tokens).Replace(file, InApp(file), StringComparison.Ordinal)}");
        }

        // A component in the server project can't be used from a page in the .Client project, which doesn't
        // reference it. Before files already there are dropped, so adding the item a second time is checked too.
        if (project.Client is { } client)
            foreach (var placed in inserts.Where(i => i.Path.StartsWith(client, StringComparison.Ordinal)))
                if (creates.FirstOrDefault(c => c.Path.EndsWith(".razor", StringComparison.Ordinal) && !c.Path.StartsWith(client, StringComparison.Ordinal)
                    && Regex.IsMatch(placed.Text, $@"<{Regex.Escape(Path.GetFileNameWithoutExtension(c.Path))}(?=[\s/>])")) is { } component)
                    throw new GenerationException($"{manifest.Id}'s {component.Path} is in the server project, and {placed.Path} is in {client}, which can't use it. Place it on a page in the server project.");

        // A file already in the project with the same text needs nothing; with other text it's a conflict.
        var conflicts = new List<string>();
        creates.RemoveAll(c =>
        {
            var existing = project.ReadFile(c.Path);
            if (existing is null) return false;
            if (c.Text is not null && existing == c.Text) return true;
            conflicts.Add(c.Path);
            return false;
        });

        var packages = manifest.Packages
            .Where(p => p.If is null || Condition.Evaluate(p.If, values))
            .Select(p => new PackageReference(p.Id, TemplateText.ReplaceTokens(p.Version, tokens)) { Project = TemplateText.ReplaceTokens(p.Project, tokens) is { Length: > 0 } folder ? folder.TrimStart('~', '/') : project.Host })
            .ToList();


        return new Changeset
        {
            Creates = creates, Inserts = inserts, Packages = packages, Conflicts = conflicts, Warnings = warnings,
            Settings = manifest.Parameters.ToDictionary(p => p.Name, p => values[p.Name] is bool b ? (b ? "true" : "false") : Convert.ToString(values[p.Name], CultureInfo.InvariantCulture)!),
            Generated = [.. manifest.Parameters.Where(p => (p.Port is not null || p.Guid) && !settings.Keys.Any(k => k.Equals(p.Name, StringComparison.OrdinalIgnoreCase))).Select(p => p.Name)],
            Restore = manifest.Restore is not null && Condition.Evaluate(manifest.Restore, values),
        };
    }

    // What the item's access doesn't reach: an account item without its files, a Pro item or a Pro choice.
    static void Refuse(ItemDefinition item, Dictionary<string, object> values)
    {
        var manifest = item.Manifest;
        if (item.Access == ItemTier.Pro) return;
        if (manifest.Tier == ItemTier.Pro)
            throw new ProRequiredException($"{manifest.Id} is a Pro item.");
        if (manifest.Tier == ItemTier.Account && item.Access != ItemTier.Account)
            throw new AccountRequiredException($"{manifest.Name} ({manifest.Id}) comes with a free IzzyBlazor account.");
        foreach (var p in manifest.Parameters)
            if (p.ProChoices.Contains(values[p.Name] as string))
                throw new ProRequiredException($"{p.Name} {values[p.Name]} is a Pro choice of {manifest.Id}. Free choices: {string.Join(", ", p.Choices.Except(p.ProChoices))}.");
    }

    /// <summary>
    /// The settings as typed values (bool, string, long), defaults filled in, then what the project is like
    /// (a setting of the same name wins), then the computed values.
    /// </summary>
    internal static Dictionary<string, object> Resolve(ItemManifest manifest, IReadOnlyDictionary<string, string> settings, IEnumerable<KeyValuePair<string, object>>? facts = null)
    {
        foreach (var name in settings.Keys)
            if (!manifest.Parameters.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                throw new GenerationException($"{manifest.Id} has no setting \"{name}\". Settings: {string.Join(", ", manifest.Parameters.Select(p => p.Name))}.");

        var values = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var p in manifest.Parameters)
        {
            var given = settings.FirstOrDefault(s => s.Key.Equals(p.Name, StringComparison.OrdinalIgnoreCase)).Value;
            // A port with none given is picked from its range, as dotnet new's port generator does.
            if (given is null && p.Port is var (low, high)) given = Random.Shared.Next(low, high + 1).ToString(CultureInfo.InvariantCulture);
            if (given is null && p.Guid) given = System.Guid.NewGuid().ToString("D");
            values[p.Name] = Typed(manifest.Id, p, given ?? p.Default);
        }
        foreach (var (name, value) in manifest.Fixed) values[name] = value;
        foreach (var (name, fact) in facts ?? [])
            values.TryAdd(name, fact);
        foreach (var (name, expression) in manifest.Computed)
            values[name] = Condition.Evaluate(expression, values);
        return values;
    }

    internal static object Typed(string id, ItemParameter p, string text) => p.Type switch
    {
        ParameterType.Bool => bool.TryParse(text, out var b) ? b : throw new GenerationException($"{id}: \"{p.Name}\" is true or false, not \"{text}\"."),
        ParameterType.Int => long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n) ? n : throw new GenerationException($"{id}: \"{p.Name}\" is a whole number, not \"{text}\"."),
        ParameterType.Choice => p.Choices.FirstOrDefault(c => c.Equals(text, StringComparison.OrdinalIgnoreCase))
            ?? throw new GenerationException($"{id}: \"{p.Name}\" is one of {string.Join(", ", p.Choices)}, not \"{text}\"."),
        _ when p.Pattern is not null && !System.Text.RegularExpressions.Regex.IsMatch(text, p.Pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1))
            => throw new GenerationException($"{id}: \"{text}\" isn't a valid {p.Name}. {p.Description}"),
        _ => text,
    };

    static Dictionary<string, string> Tokens(ItemManifest manifest, Dictionary<string, object> values, ProjectInfo project)
    {
        var tokens = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ProjectName"] = project.Name,
            ["RootNamespace"] = project.RootNamespace,
            ["HostName"] = Names.HostName(project.Name),
            ["IzzyBlazorVersion"] = IzzyBlazorVersion,
            // Where an item's Components/ files go: the .Client project when the pages run in the browser.
            ["UiNamespace"] = project.LayoutInBrowser && project.ClientNamespace is { } ui ? ui : project.RootNamespace + ".Components",
            // A project's folder from the app's (~/MyApp.Client/), so the path stays in that project even when it's
            // the app's own folder: {{Host}}Components/_Imports.razor is the server's, though the .Client is under it.
            ["Client"] = project.Client is { } inClient ? "~/" + inClient : "",
            ["Host"] = "~/" + project.Host,
            ["ClientNamespace"] = project.ClientNamespace ?? "",
        };
        foreach (var (name, value) in project.Tokens) tokens.TryAdd(name, value);
        // A setting's value can name the project ("{{ProjectName}} gives your team..."), not other settings.
        var named = new Dictionary<string, string>(tokens, StringComparer.Ordinal);
        foreach (var p in manifest.Parameters.Where(p => p.Type != ParameterType.Bool))
            tokens[p.Name] = TemplateText.ReplaceTokens(Convert.ToString(values[p.Name], CultureInfo.InvariantCulture)!, named);
        foreach (var (name, value) in manifest.Fixed)
            if (value is string text) tokens[name] = text;
        return tokens;
    }

    /// <summary>What conditions can ask about the project, besides the item's settings.</summary>
    static IEnumerable<KeyValuePair<string, object>> Facts(ProjectInfo project)
    {
        yield return new("LayoutInBrowser", project.LayoutInBrowser);
        yield return new("HasClient", project.Client is not null);
        foreach (var fact in project.Facts) yield return fact;
    }

    /// <summary>
    /// An item's path (written for one project: Program.cs, Components/Pages/Home.razor) as a path in the app;
    /// <c>~/</c> starts at the app's folder.
    /// A path already in one of its projects stays. With the pages in the .Client project, Components/ files go
    /// there (Components/Pages/Home.razor is MyApp.Client/Pages/Home.razor), except App.razor, the Identity pages
    /// and what the item marks as server-only; everything else is the server project's.
    /// </summary>
    static string ToApp(string path, ProjectInfo project, List<Glob> server)
    {
        // ~/ is the app's own folder: the one holding both projects (.mcp.json).
        if (path.StartsWith("~/", StringComparison.Ordinal)) return path[2..];
        if (project.Client is { } client && path.StartsWith(client, StringComparison.Ordinal)) return path;
        if (project.Host.Length > 0 && path.StartsWith(project.Host, StringComparison.Ordinal)) return path;
        const string components = "Components/";
        if (project.LayoutInBrowser && project.Client is { } inBrowser && path.StartsWith(components, StringComparison.Ordinal)
            && path != "Components/App.razor" && !path.StartsWith("Components/Account/", StringComparison.Ordinal)
            && !server.Any(g => g.Matches(path)))
            return inBrowser + path[components.Length..];
        return project.Host + path;
    }

    /// <summary>
    /// A template path after the renames that hold: the first rule whose source is the path, or a folder (ending
    /// in <c>/</c>) holding it, moves it. <c>./</c> is the project folder.
    /// </summary>
    static string Renamed(string path, List<KeyValuePair<string, string>> renames)
    {
        foreach (var (from, to) in renames)
        {
            string? moved = null;
            if (from.EndsWith('/') && path.StartsWith(from, StringComparison.Ordinal)) moved = to + path[from.Length..];
            else if (path == from) moved = to;
            if (moved is null) continue;
            while (moved.StartsWith("./", StringComparison.Ordinal)) moved = moved[2..];
            return moved;
        }
        return path;
    }
}
