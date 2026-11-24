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

    public static Components Load(string path) =>
        File.Exists(path)
            ? new Components(JsonNode.Parse(File.ReadAllText(path))!.AsObject())
            : throw new UsageException("This izzy has no component data (components.json is missing beside it). Reinstall the tool.");

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

    static JsonObject Property(JsonObject p, string? group)
    {
        var kind = (string)p["kind"]!;
        var type = (string)p["type"]!;
        var schema = new JsonObject
        {
            ["type"] = kind switch
            {
                "bool" => "boolean",
                "number" => type.TrimEnd('?') is "int" or "long" ? "integer" : "number",
                _ => "string",
            },
        };
        if (p["values"] is { } values) schema["enum"] = values.DeepClone();
        if ((string?)p["default"] is { } written)
        {
            if (Default(kind, written) is { } value) schema["default"] = value;
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

    /// <summary>The names of a base's options, for the text form of describe.</summary>
    public IEnumerable<string> BaseOptions(string name) =>
        _bases.TryGetValue(name, out var shared) ? shared["parameters"]!.AsArray().Select(p => (string)p!["name"]!) : [];
}
