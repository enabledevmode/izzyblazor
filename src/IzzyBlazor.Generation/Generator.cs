using System.Globalization;
using System.Text;

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
    public static Changeset Generate(ItemDefinition item, IReadOnlyDictionary<string, string> settings, ProjectInfo project)
    {
        var manifest = item.Manifest;
        var values = Resolve(manifest, settings);
        if (!item.IncludesPro) RefusePro(manifest, values);
        var tokens = Tokens(manifest, values, project);

        var excluded = new List<Glob>();
        foreach (var rule in manifest.Exclude)
            if (Condition.Evaluate(rule.If, values)) excluded.AddRange(rule.Paths.Select(p => new Glob(p)));
        var renames = manifest.Rename.Where(r => Condition.Evaluate(r.If, values)).SelectMany(r => r.Paths).ToList();

        var creates = new List<FileCreate>();
        foreach (var (path, bytes) in item.Files)
        {
            if (excluded.Any(e => e.Matches(path))) continue;
            var target = TemplateText.ReplaceTokens(Renamed(path, renames), tokens);
            if (TemplateText.IsBinary(path))
            {
                creates.Add(new FileCreate(target, null, bytes));
                continue;
            }
            if (bytes is [0xEF, 0xBB, 0xBF, ..])
                throw new GenerationException($"{manifest.Id}: {path} starts with a byte order mark; save it as UTF-8 without one.");
            string text;
            try { text = StrictUtf8.GetString(bytes); }
            catch (DecoderFallbackException) { throw new GenerationException($"{manifest.Id}: {path} is not UTF-8 text; add its extension to the binary list or re-save it."); }
            creates.Add(new FileCreate(target, TemplateText.Apply(path, text, values, tokens), null));
        }
        creates.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));

        var warnings = new List<string>();
        var inserts = new List<TextInsert>();
        foreach (var written in manifest.Inserts)
        {
            if (written.If is not null && !Condition.Evaluate(written.If, values)) continue;
            var path = TemplateText.ReplaceTokens(written.File, tokens);
            var text = TemplateText.ReplaceTokens(written.Text, tokens);
            var spec = written with
            {
                Anchor = written.Anchor is null ? null : TemplateText.ReplaceTokens(written.Anchor, tokens),
                Pattern = written.Pattern is null ? null : TemplateText.ReplaceTokens(written.Pattern, tokens),
            };
            var created = creates.FindIndex(c => c.Path == path && c.Text is not null);
            if (created >= 0)
            {
                var file = creates[created].Text!;
                if (Inserts.Place(file, spec, text) is { } place) creates[created] = creates[created] with { Text = file.Insert(place.Offset, place.Text) };
                else if (!Inserts.AlreadyThere(file, text)) warnings.Add(Inserts.NoAnchor(manifest.Id, path, spec));
                continue;
            }
            var existing = project.ReadFile(path);
            if (existing is null)
            {
                warnings.Add($"{manifest.Id}: {path} isn't in the project, so this wasn't added to it:\n{text}");
                continue;
            }
            if (Inserts.Place(existing, spec, text) is { } at) inserts.Add(new TextInsert(path, at.Offset, at.Text, Inserts.Hash(existing)));
            else if (!Inserts.AlreadyThere(existing, text)) warnings.Add(Inserts.NoAnchor(manifest.Id, path, spec));
        }

        // Checks read the project as it is, before anything here is applied.
        foreach (var check in manifest.Checks)
        {
            if (check.If is not null && !Condition.Evaluate(check.If, values)) continue;
            var text = project.ReadFile(TemplateText.ReplaceTokens(check.File, tokens));
            if (text is not null && System.Text.RegularExpressions.Regex.IsMatch(text, check.Pattern, System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1)))
                warnings.Add($"{manifest.Id}: {TemplateText.ReplaceTokens(check.Message, tokens)}");
        }

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
            .Select(p => new PackageReference(p.Id, p.Version))
            .ToList();

        return new Changeset
        {
            Creates = creates, Inserts = inserts, Packages = packages, Conflicts = conflicts, Warnings = warnings,
            Restore = manifest.Restore is not null && Condition.Evaluate(manifest.Restore, values),
        };
    }

    static void RefusePro(ItemManifest manifest, Dictionary<string, object> values)
    {
        if (manifest.Tier == ItemTier.Pro)
            throw new ProRequiredException($"{manifest.Id} is a Pro item.");
        foreach (var p in manifest.Parameters)
            if (p.ProChoices.Contains(values[p.Name] as string))
                throw new ProRequiredException($"{p.Name} {values[p.Name]} is a Pro choice of {manifest.Id}. Free choices: {string.Join(", ", p.Choices.Except(p.ProChoices))}.");
    }

    /// <summary>The settings as typed values (bool, string, long), defaults filled in, then the computed values.</summary>
    internal static Dictionary<string, object> Resolve(ItemManifest manifest, IReadOnlyDictionary<string, string> settings)
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
        foreach (var (name, expression) in manifest.Computed)
            values[name] = Condition.Evaluate(expression, values);
        return values;
    }

    static object Typed(string id, ItemParameter p, string text) => p.Type switch
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
        };
        foreach (var p in manifest.Parameters.Where(p => p.Type != ParameterType.Bool))
            tokens[p.Name] = Convert.ToString(values[p.Name], CultureInfo.InvariantCulture)!;
        return tokens;
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
