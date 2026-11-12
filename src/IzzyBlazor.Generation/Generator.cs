using System.Globalization;
using System.Text;

namespace IzzyBlazor.Generation;

/// <summary>Generates an item: its manifest and template files plus settings in, a <see cref="Changeset"/> out. Writes nothing.</summary>
public static class Generator
{
    static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Generates <paramref name="item"/> into <paramref name="project"/> with the given settings.</summary>
    /// <param name="item">The item.</param>
    /// <param name="settings">Settings by name, as text (<c>true</c>, <c>Auto</c>, <c>3</c>); the rest take their defaults.</param>
    /// <param name="project">The project's name and namespace.</param>
    public static Changeset Generate(ItemDefinition item, IReadOnlyDictionary<string, string> settings, ProjectInfo project)
    {
        var manifest = item.Manifest;
        var values = Resolve(manifest, settings);
        var tokens = Tokens(manifest, values, project);

        var excluded = new List<string>();
        foreach (var rule in manifest.Exclude)
            if (Condition.Evaluate(rule.If, values)) excluded.AddRange(rule.Paths);

        var creates = new List<FileCreate>();
        foreach (var (path, bytes) in item.Files)
        {
            if (excluded.Any(e => Matches(e, path))) continue;
            var target = TemplateText.ReplaceTokens(path, tokens);
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

        var packages = manifest.Packages
            .Where(p => p.If is null || Condition.Evaluate(p.If, values))
            .Select(p => new PackageReference(p.Id, p.Version))
            .ToList();

        return new Changeset { Creates = creates, Packages = packages };
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
        _ => text,
    };

    static Dictionary<string, string> Tokens(ItemManifest manifest, Dictionary<string, object> values, ProjectInfo project)
    {
        var tokens = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ProjectName"] = project.Name,
            ["RootNamespace"] = project.RootNamespace,
        };
        foreach (var p in manifest.Parameters.Where(p => p.Type != ParameterType.Bool))
            tokens[p.Name] = Convert.ToString(values[p.Name], CultureInfo.InvariantCulture)!;
        return tokens;
    }

    /// <summary>An exclude path matches itself, or everything under it when it ends in <c>/**</c>.</summary>
    static bool Matches(string pattern, string path) => pattern.EndsWith("/**", StringComparison.Ordinal)
        ? path.StartsWith(pattern[..^2], StringComparison.Ordinal)
        : string.Equals(pattern, path, StringComparison.Ordinal);
}
