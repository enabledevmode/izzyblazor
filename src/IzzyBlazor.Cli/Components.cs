using System.Globalization;
using System.Text.Json.Nodes;

namespace IzzyBlazor.Cli;

/// <summary>
/// The library's components as izzy knows them: components.json beside the tool, which the build makes from
/// the library's API, the docs' registry and the docs' examples (DocsGen's CliData). A component has no
/// manifest of its own. It describes this izzy's version of the library, which may be newer than an app's.
/// </summary>
sealed class Components
{
    readonly JsonObject _file;
    readonly Dictionary<string, JsonObject> _byName = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, JsonObject> _bases = new(StringComparer.Ordinal);

    Components(JsonObject file)
    {
        _file = file;
        foreach (var c in All)
        {
            _byName[(string)c["name"]!] = c;
            // data-grid as well as DataGrid.
            if ((string?)c["id"] is { } id) _byName.TryAdd(id, c);
        }
        foreach (var b in file["bases"]!.AsArray()) _bases[(string)b!["name"]!] = b.AsObject();
    }

    /// <summary>The newest shape of the file this izzy reads.</summary>
    public const int Format = 1;

    /// <summary>
    /// The data in an app's own IzzyBlazor.Core package, when the file there is one this izzy reads; null
    /// when it's missing, damaged or of a newer shape.
    /// </summary>
    public static Components? FromPackage(string path)
    {
        try
        {
            if (!File.Exists(path) || JsonNode.Parse(File.ReadAllText(path)) is not JsonObject file) return null;
            return ((int?)file["format"] ?? 1) <= Format ? new Components(file) { Source = "package" } : null;
        }
        catch (Exception e) when (e is IOException or System.Text.Json.JsonException or InvalidOperationException or NullReferenceException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Where the data came from: the app's package, or this tool.</summary>
    public string Source { get; private init; } = "tool";

    public static Components Load(string path) =>
        File.Exists(path)
            ? new Components(JsonNode.Parse(File.ReadAllText(path))!.AsObject())
            : throw new UsageException("This izzy has no component data (components.json is missing beside it). Reinstall the tool.");

    /// <summary>Whether the data says what marks each component's outermost element (alpha.30 on), which a trace needs.</summary>
    public bool HasMarks => All.Any(c => c["root"] is not null);

    /// <summary>The library version the data describes.</summary>
    public string Version => (string)_file["version"]!;

    /// <summary>Every component and part, by name.</summary>
    public IEnumerable<JsonObject> All => _file["components"]!.AsArray().Select(c => c!.AsObject());

    /// <summary>The components with a docs page of their own, in the docs' groups and then by name.</summary>
    public IEnumerable<JsonObject> Documented
    {
        get
        {
            var order = _file["categories"]!.AsArray().Select(c => (string)c!).ToList();
            return All.Where(c => c["id"] is not null).OrderBy(c => order.IndexOf((string)c["category"]!)).ThenBy(c => (string)c["name"]!, StringComparer.Ordinal);
        }
    }

    public JsonObject Find(string name) =>
        _byName.TryGetValue(name, out var found) ? found
            : throw new UsageException($"No component named \"{name}\". izzy list component shows them.");

    /// <summary>An example's source and the files beside it; null when the data has none by that id.</summary>
    public JsonObject? Example(string id) => _file["examples"]![id]?.AsObject();

    /// <summary>A row of <c>izzy list --json</c>, in the shape a catalog item has.</summary>
    public static JsonObject ListEntry(JsonObject c) => new()
    {
        ["id"] = (string)c["id"]!, ["name"] = (string)c["name"]!, ["kind"] = "component", ["category"] = (string?)c["category"],
        ["tier"] = "free", ["summary"] = (string?)c["summary"], ["docs"] = (string?)c["docs"], ["picture"] = null,
        ["proChoices"] = new JsonObject(),
    };

    /// <summary>
    /// A component for <c>izzy describe component --json</c>: its options as the JSON Schema a block's settings
    /// use, so one form draws both, with what a form can't edit (content, events) listed by name beside it.
    /// </summary>
    public JsonObject Describe(JsonObject c)
    {
        var properties = new JsonObject();
        var required = new JsonArray();
        var bindable = new JsonArray();
        void Take(JsonArray parameters, string? group)
        {
            foreach (var p in parameters.Select(p => p!.AsObject()))
            {
                var name = (string)p["name"]!;
                if (properties.ContainsKey(name)) continue;
                properties[name] = Property(p, group);
                if ((bool?)p["required"] == true) required.Add(name);
                if ((bool?)p["bindable"] == true) bindable.Add(name);
            }
        }
        Take(c["parameters"]!.AsArray(), null);
        // The options it shares with others (style props, a field's label and help) come after its own.
        foreach (var b in c["bases"]?.AsArray() ?? [])
            if (_bases.TryGetValue((string)b!, out var shared)) Take(shared["parameters"]!.AsArray(), (string)b!);
        foreach (var name in Important(c, required.Select(r => (string)r!), properties)) properties[name]!["x-priority"] = "important";

        return new JsonObject
        {
            ["name"] = (string)c["name"]!, ["tag"] = (string)c["name"]!, ["displayName"] = (string)c["displayName"]!,
            ["id"] = (string?)c["id"], ["partOf"] = (string?)c["partOf"], ["kind"] = "component", ["tier"] = "free",
            ["category"] = (string?)c["category"], ["summary"] = (string?)c["summary"], ["docs"] = (string?)c["docs"],
            ["modes"] = c["modes"]?.DeepClone(),
            ["typeParameters"] = c["typeParameters"]?.DeepClone() ?? new JsonArray(),
            ["parameters"] = new JsonObject { ["type"] = "object", ["properties"] = properties, ["required"] = required },
            ["bindable"] = bindable,
            ["slots"] = c["slots"]?.DeepClone() ?? new JsonArray(),
            ["events"] = c["events"]?.DeepClone() ?? new JsonArray(),
            ["parts"] = c["parts"]?.DeepClone() ?? new JsonArray(),
            ["examples"] = new JsonArray((c["examples"]?.AsArray() ?? []).Select(e => (JsonNode)new JsonObject
            {
                ["name"] = (string)e!["name"]!, ["id"] = (string)e["id"]!, ["title"] = (string)e["title"]!,
                ["interactiveOnly"] = (bool?)e["interactiveOnly"] ?? false,
            }).ToArray()),
        };
    }

    /// <summary>The most options a component marks important.</summary>
    public const int MostImportant = 8;

    // Options chosen beside the hero example's, for the large components whose first example is kept small.
    static readonly Dictionary<string, string[]> Chosen = new(StringComparer.Ordinal)
    {
        ["DataGrid"] = ["Searchable", "SelectionMode", "EditMode", "FilterRow"],
        ["Chart"] = ["Height", "Legend", "Orientation"],
        ["Form"] = ["Columns", "Layout", "ConfirmLeave"],
    };

    /// <summary>
    /// The options a form shows first: the required ones; a field's Label and Hint, which its examples leave to
    /// the model's [Display]; those chosen for it; then those its hero example (the first on its docs page, or
    /// its parent's for a part with none) sets on its tag, in the order written. At most <see cref="MostImportant"/>.
    /// </summary>
    IEnumerable<string> Important(JsonObject c, IEnumerable<string> required, JsonObject properties)
    {
        var name = (string)c["name"]!;
        string[] field = (c["bases"]?.AsArray() ?? []).Any(b => (string?)b == "FieldBase") ? ["Label", "Hint"] : [];
        required = required.Concat(field).Concat(Chosen.GetValueOrDefault(name) ?? []);
        var hero = (c["examples"]?.AsArray() ?? []).FirstOrDefault()
            ?? ((string?)c["partOf"] is { } parent && _byName.TryGetValue(parent, out var whole) ? (whole["examples"]?.AsArray() ?? []).FirstOrDefault() : null);
        var source = hero is null ? null : (string?)Example((string)hero["id"]!)?["source"];
        var set = source is null ? [] : RazorPage.Tags(source).Where(t => t.Name == name)
            .SelectMany(t => t.Attributes).Select(a => a.Name.StartsWith("@bind-", StringComparison.Ordinal) ? a.Name["@bind-".Length..].Split(':')[0] : a.Name);
        // Where the example sits on its docs page (Grow in a Row, a MaxWidth) isn't a choice about the component.
        return required.Concat(set.Where(o => !Placement(o, properties))).Where(properties.ContainsKey).Distinct(StringComparer.Ordinal).Take(MostImportant);

        static bool Placement(string option, JsonObject properties) =>
            option is "Grow" or "Basis" or "MaxWidth" ||(string?)properties[option]?["x-group"] == "LayoutItemBase";
    }

    static JsonObject Property(JsonObject p, string? group)
    {
        var kind = (string)p["kind"]!;
        var type = (string)p["type"]!;
        var schema = new JsonObject
        {
            ["type"] = kind switch
            {
                "bool" => "boolean",
                // Responsive<int> holds whole numbers too.
                "number" => (type.StartsWith("Responsive<", StringComparison.Ordinal) ? type["Responsive<".Length..].TrimEnd('>') : type).TrimEnd('?') is "int" or "long" ? "integer" : "number",
                _ => "string",
            },
        };
        if (p["values"] is { } values) schema["enum"] = values.DeepClone();
        if ((string?)p["default"] is { } written)
        {
            // An enum's default is sometimes written by its member alone (Center).
            if (Default(kind, written) is { } value) schema["default"] = value;
            else if (kind == "enum" && p["values"]!.AsArray().Any(v => (string)v! == written)) schema["default"] = written;
            else schema["x-default"] = written;
        }
        if ((string?)p["summary"] is { } summary) schema["description"] = summary;
        schema["title"] = Titles.For((string)p["name"]!);
        // What a panel draws: bool, enum, string, number, or other (shown with its type, edited as text).
        schema["x-kind"] = kind;
        schema["x-type"] = type;
        if ((string?)p["write"] is { } write) schema["x-write"] = write;
        if ((bool?)p["responsive"] == true) schema["x-responsive"] = true;
        if (group is not null) schema["x-group"] = group;
        return schema;
    }

    // A default as C# writes it (false, ButtonVariant.Solid, "Close", 12), as the value a form holds.
    static JsonNode? Default(string kind, string written) => kind switch
    {
        "bool" when written is "true" or "false" => written == "true",
        "enum" when written.LastIndexOf('.') is var dot and >= 0 && written[(dot + 1)..].All(char.IsLetterOrDigit) => written[(dot + 1)..],
        "string" when written.Length >= 2 && written[0] == '"' && written[^1] == '"' && !written[1..^1].Contains('"') && !written.Contains('\\') => written[1..^1],
        "number" when double.TryParse(written, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) => number,
        _ => null,
    };

    /// <summary>
    /// A tag's options by name, each with whether it takes text (a string) rather than C#; null for a tag that
    /// isn't a component's.
    /// </summary>
    public Dictionary<string, bool>? Options(string tag)
    {
        if (!_byName.TryGetValue(tag, out var c) || !string.Equals((string)c["name"]!, tag, StringComparison.Ordinal)) return null;
        var options = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var p in Parameters(c)) options.TryAdd((string)p["name"]!, (string)p["kind"]! == "string");
        return options;
    }

    /// <summary>A component's options, its own and then its bases'.</summary>
    public IEnumerable<JsonObject> Parameters(JsonObject c) =>
        c["parameters"]!.AsArray().Select(p => p!.AsObject())
            .Concat((c["bases"]?.AsArray() ?? []).SelectMany(b => _bases.TryGetValue((string)b!, out var shared) ? shared["parameters"]!.AsArray().Select(p => p!.AsObject()) : []));

    /// <summary>Whether a tag is a component on this base.</summary>
    public bool HasBase(string tag, string name) =>
        _byName.TryGetValue(tag, out var c) && (c["bases"]?.AsArray() ?? []).Any(b => (string?)b == name);

    /// <summary>The names of a base's options, for the text form of describe.</summary>
    public IEnumerable<string> BaseOptions(string name) =>
        _bases.TryGetValue(name, out var shared) ? shared["parameters"]!.AsArray().Select(p => (string)p!["name"]!) : [];
}
