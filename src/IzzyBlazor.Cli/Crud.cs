using System.Globalization;
using System.Text.RegularExpressions;
using IzzyBlazor.Generation;

namespace IzzyBlazor.Cli;

/// <summary>
/// izzy add crud &lt;Model&gt;: reads the model from the app's source and gives the crud item what it's made from.
/// The settings it leaves out get their defaults here, from the model and the app, so .izzy/items.json
/// records what was made: Name the plural, Route its kebab case, Store Ef when one DbContext has a DbSet
/// of the model (else Memory), Authorize when the app signs people in. The fields go to the engine as
/// lists (Fields, FormFields, ListFields, SearchFields), the rest as facts and tokens.
/// </summary>
static partial class Crud
{
    const int ListColumns = 6;
    const int SearchColumns = 3;

    public static ProjectInfo Prepare(ProjectInfo project, string model, Dictionary<string, string> settings, List<string> notes)
    {
        if (project.LayoutInBrowser)
            throw new UsageException($"{project.Name}'s pages run in the browser (its Routes are in {project.Client}), and crud's pages read the database on the server. Add them to a Blazor Web App whose pages are in the server project.");
        var root = project.Directory!;
        var files = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .Where(f => !Regex.IsMatch(f, @"(^|/)(bin|obj|\.izzy|node_modules|\.git)/"))
            .Select(f => (f, File.ReadAllText(Path.Combine(root, f))))
            .ToList();
        var csproj = File.ReadAllText(Path.Combine(root, project.Host, project.Name + ".csproj"));
        var nullable = Regex.IsMatch(csproj, @"<Nullable>\s*enable\s*</Nullable>", RegexOptions.IgnoreCase)
            || DirectoryBuildProps(root).Any(p => Regex.IsMatch(p, @"<Nullable>\s*enable\s*</Nullable>", RegexOptions.IgnoreCase));
        var info = ModelReader.Read(files, model, nullable);
        foreach (var skipped in info.Skipped) notes.Add($"{info.Name}.{skipped}: left out.");

        string? Given(string name) => settings.FirstOrDefault(s => s.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;

        var name = Given("Name") ?? Plural(info.Name);
        if (!Regex.IsMatch(name, @"^[A-Za-z_][A-Za-z0-9_]*$"))
            throw new UsageException($"--name is a C# name, such as Customers, not \"{name}\": it names the folder and the components.");
        var route = (Given("Route") ?? Kebab(name)).Trim('/');
        if (route.Length == 0 || route.Contains('{') || route.Contains('?'))
            throw new UsageException($"--route is the address the pages start at, such as customers or admin/customers, not \"{Given("Route")}\".");

        var program = project.ReadFile(project.Host + "Program.cs") ?? "";
        var contexts = info.Contexts;
        if (Given("Context") is { } wanted)
        {
            contexts = [.. contexts.Where(c => c.Name == wanted)];
            if (contexts.Count == 0) throw new UsageException($"{wanted} has no DbSet<{info.Name}>. Add one, or leave out --context.");
        }
        var store = Given("Store") ?? (contexts.Count > 0 ? "Ef" : "Memory");
        ContextInfo? context = null;
        if (store.Equals("Ef", StringComparison.OrdinalIgnoreCase))
        {
            if (contexts.Count == 0)
                throw new UsageException($"No DbContext in {project.Name} has a DbSet<{info.Name}>. Add one (public DbSet<{info.Name}> {Plural(info.Name)} => Set<{info.Name}>();) and a migration, or use --store memory.");
            if (contexts.Count > 1)
                throw new UsageException($"{string.Join(" and ", contexts.Select(c => c.Name))} each have a DbSet<{info.Name}>: pick one with --context.");
            context = contexts[0];
            settings["Context"] = context.Name;
        }
        else notes.Add($"The {Words(name).ToLowerInvariant()} are kept in memory until the app stops: Memory{info.Name}Store. When there's a DbSet<{info.Name}>, run again with --store ef --overwrite.");
        var authorize = Given("Authorize") ?? (Regex.IsMatch(program, @"\b(AddIdentityCore|AddDefaultIdentity|AddIdentity|AddAuthentication)\b") ? "true" : "false");

        settings["Model"] = info.Name;
        settings["Name"] = name;
        settings["Route"] = route;
        settings["Store"] = store;
        settings["Authorize"] = authorize;

        var shown = info.Fields;
        var form = shown.Where(f => !f.ReadOnly).ToList();
        if (form.Count == 0) throw new UsageException($"{info.Name} has nothing to edit: every property crud shows is read-only.");
        var title = shown.FirstOrDefault(f => f.Kind == FieldKind.Text);
        var subtitle = shown.FirstOrDefault(f => f.Kind == FieldKind.Email);
        var search = shown.Where(f => f.Kind is FieldKind.Text or FieldKind.Email).Take(SearchColumns).ToList();
        var status = shown.FirstOrDefault(f => f.Kind == FieldKind.Enum && f.Name == "Status") ?? shown.FirstOrDefault(f => f.Kind == FieldKind.Enum);
        // The list's columns after the title (with the email under it): the status first, as the design has it.
        // Then what a list compares: choices, numbers and dates, then other required text. Contact details
        // and optional text (phone, website, notes) are for the detail page.
        int Rank(ModelField f) => f.Kind switch
        {
            FieldKind.Enum or FieldKind.Bool => 0,
            FieldKind.Integer or FieldKind.Decimal or FieldKind.Money => 1,
            FieldKind.Date or FieldKind.DateTime => 2,
            FieldKind.Text when f.Required => 3,
            _ => 9,
        };
        var list = (status is null ? [] : new[] { status })
            .Concat(shown.Where(f => f != status && f != title && f != subtitle && Rank(f) < 9).OrderBy(Rank).ThenBy(f => shown.ToList().IndexOf(f)))
            .Take(ListColumns - 1)
            // In the model's order, the status first.
            .OrderBy(f => f == status ? -1 : shown.ToList().IndexOf(f))
            .ToList();
        // The grid page shows the same columns as the list (title, then these); the rest only in its dialog.
        IReadOnlyCollection<ModelField> columns = [.. new[] { title, subtitle }.OfType<ModelField>(), .. list];
        // Newest first by the last date shown, else by the title.
        var sortDate = list.LastOrDefault(f => f.Kind is FieldKind.Date or FieldKind.DateTime);
        var titleKey = (title ?? info.Key).Name.ToLowerInvariant();

        var facts = new Dictionary<string, object>(project.Facts)
        {
            ["Fields"] = Records(shown, status, columns),
            ["FormFields"] = Records(form, status, columns),
            ["ListFields"] = Records(list, status, columns),
            ["SearchFields"] = Records(search, status, columns),
            ["StatusValues"] = status is null ? new List<IReadOnlyDictionary<string, object>>() : Values(status),
            ["HasTitle"] = title is not null,
            ["HasSubtitle"] = subtitle is not null,
            ["HasSearch"] = search.Count > 0,
            ["HasStatus"] = status is not null,
            ["HasModelNamespace"] = info.Namespace.Length > 0 && info.Namespace != project.RootNamespace + ".Data",
            ["KeyGenerated"] = info.KeyGenerated,
            ["KeyIsText"] = info.Key.Kind == FieldKind.Text,
            ["KeyIsGuid"] = info.Key.Kind == FieldKind.Guid,
            ["HasFactory"] = context is not null && Regex.IsMatch(program, $@"AddDbContextFactory\s*<\s*{Regex.Escape(context.Name)}\b"),
            ["HasContextNamespace"] = context is not null && context.Namespace.Length > 0 && context.Namespace != project.RootNamespace + ".Data" && context.Namespace != info.Namespace,
            ["NavIcons"] = NavHasIcons(project),
            // A run with the other store replaces its registration rather than adding a second.
            ["HasStoreRegistration"] = Regex.IsMatch(program, $@"\bI{Regex.Escape(info.Name)}Store\s*,"),
        };
        var keyType = info.Key.Type.TrimEnd('?');
        var tokens = new Dictionary<string, string>(project.Tokens)
        {
            ["ModelNamespace"] = info.Namespace,
            ["Singular"] = Words(info.Name),
            ["SingularLower"] = Lower(Words(info.Name)),
            ["Article"] = Article(Lower(Words(info.Name))),
            ["Plural"] = Words(name),
            ["PluralLower"] = Lower(Words(name)),
            // The store is the model's, whatever --name the pages have, so a second run with another name matches it.
            ["ModelPlural"] = Words(Plural(info.Name)),
            ["ModelPluralLower"] = Lower(Words(Plural(info.Name))),
            ["Key"] = info.Key.Name,
            ["KeyType"] = keyType,
            ["KeyParse"] = info.Key.Kind switch
            {
                FieldKind.Text => "(string?)key",
                FieldKind.Guid => "(Guid.TryParse(key, out var parsed) ? parsed : (Guid?)null)",
                _ => $"({keyType}.TryParse(key, out var parsed) ? parsed : ({keyType}?)null)",
            },
            ["IdValue"] = info.Key.Kind == FieldKind.Text ? "Id!" : "Id!.Value",
            ["KeyConstraint"] = info.Key.Kind switch
            {
                FieldKind.Guid => ":guid",
                FieldKind.Text => "",
                _ => keyType is "long" or "Int64" ? ":long" : ":int",
            },
            ["TitleField"] = title?.Name ?? info.Key.Name,
            ["TitleLabel"] = title?.ShortLabel ?? Words(info.Name),
            ["TitleSortKey"] = titleKey,
            ["InitialSort"] = sortDate is not null ? "-" + sortDate.Name.ToLowerInvariant() : titleKey,
            ["SubtitleField"] = subtitle?.Name ?? "",
            ["StatusField"] = status?.Name ?? "",
            ["StatusType"] = status?.Type.TrimEnd('?') ?? "",
            ["StatusLabel"] = status?.Label ?? "",
            ["StatusPlural"] = status is null ? "" : Lower(Plural(status.Label)),
            ["SearchPlaceholder"] = search.Count == 0 ? "" : "Search " + Join(search.Select(f => Lower(f.Label))),
            ["NoMatchHint"] = "Try another " + Join(search.Select(f => Lower(f.Label)).Take(1).Concat(status is null ? [] : [Lower(status.Label)])) + ".",
            ["StoreClass"] = (context is null ? "Memory" : "Ef") + info.Name + "Store",
            ["StoreLifetime"] = context is null ? "AddSingleton" : "AddScoped",
            ["Context"] = context?.Name ?? "",
            ["ContextNamespace"] = context?.Namespace ?? "",
            ["Set"] = context?.SetName ?? "",
        };
        return project with { Facts = facts, Tokens = tokens };
    }

    static List<IReadOnlyDictionary<string, object>> Records(IEnumerable<ModelField> fields, ModelField? status, IReadOnlyCollection<ModelField> columns) =>
        [.. fields.Select(f => (IReadOnlyDictionary<string, object>)new Dictionary<string, object>
        {
            ["Name"] = f.Name,
            ["Label"] = f.Label,
            ["Column"] = f.ShortLabel,
            ["InList"] = columns.Contains(f),
            ["LabelLower"] = Lower(f.Label),
            ["Article"] = Article(Lower(f.Label)),
            ["Kind"] = f.Kind.ToString(),
            ["Key"] = f.Name.ToLowerInvariant(),
            ["IsStatus"] = f == status,
            ["PriorityAttr"] = f == status ? " Priority=\"1\"" : "",
            ["Format"] = Format(f.Kind),
            ["HasFormat"] = Format(f.Kind).Length > 0,
            ["FormatAttr"] = Format(f.Kind) is { Length: > 0 } format ? $" Format=\"{format}\"" : "",
            ["EditableAttr"] = f.ReadOnly ? " Editable=\"false\"" : "",
            ["WidthAttr"] = Width(f.Kind, f.ShortLabel) is { } rem ? $" Width=\"Length.Rem({rem.ToString(CultureInfo.InvariantCulture)})\"" : "",
            ["Type"] = f.Type,
            ["EnumType"] = f.Kind == FieldKind.Enum ? f.Type.TrimEnd('?') : "",
            ["Required"] = f.Required,
            // Required by the model but not by an attribute the form reads, such as a string that isn't
            // nullable: the page checks it itself.
            ["CheckRequired"] = f.Required && f.Kind is FieldKind.Text or FieldKind.LongText or FieldKind.Email or FieldKind.Phone or FieldKind.Url,
            // Marked required in the form: a value type always has a value, so it isn't "(optional)".
            ["RequiredAttr"] = f.Required && f.Kind != FieldKind.Bool ? " Required" : "",
            ["Nullable"] = f.Nullable,
            ["ReadOnly"] = f.ReadOnly,
            ["Numeric"] = f.Numeric,
            ["Wide"] = f.Kind == FieldKind.LongText,
            ["MaxLength"] = (long)f.MaxLength,
            ["Radio"] = f.Kind == FieldKind.Enum && f.Values.Count is >= 2 and <= 4,
            ["Values"] = Values(f),
        })];

    static List<IReadOnlyDictionary<string, object>> Values(ModelField field) =>
        [.. field.Values.Select(v => (IReadOnlyDictionary<string, object>)new Dictionary<string, object>
        {
            ["Name"] = v.Name, ["Label"] = v.Label, ["Tone"] = Tone(v.Name),
        })];

    // How a value is shown in the list and on the detail page.
    static string Format(FieldKind kind) => kind switch
    {
        FieldKind.Money => "C",
        FieldKind.Date => "MMM d, yyyy",
        FieldKind.DateTime => "MMM d, yyyy h:mm tt",
        _ => "",
    };

    // A list column's width by what it holds, as the design sizes them (Status 120px, Orders 90px, Joined 130px),
    // so the grid fits them before hiding any; text takes what's left.
    static double? Width(FieldKind kind, string title) => ByKind(kind) is { } width
        // Wide enough for the title and its sort arrow too: about 0.55rem a letter, 2.75rem of padding and arrow.
        ? Math.Max(width, Math.Ceiling((title.Length * 0.55 + 2.75) * 2) / 2)
        : null;

    static double? ByKind(FieldKind kind) => kind switch
    {
        FieldKind.Enum => 7.5,
        FieldKind.Bool => 6,
        FieldKind.Integer => 6.5,
        FieldKind.Decimal => 7,
        FieldKind.Money => 9,
        FieldKind.Date or FieldKind.Time => 8.5,
        FieldKind.DateTime => 11,
        _ => null,
    };

    // A badge's color from what the value usually means; anything else is neutral. Yours to change in the page.
    static string Tone(string value) => value switch
    {
        "Active" or "Paid" or "Done" or "Completed" or "Complete" or "Approved" or "Published" or "Open" or "Enabled" or "Shipped" or "Delivered" => "Success",
        "Trial" or "New" or "Draft" or "Scheduled" or "Invited" or "Sent" => "Info",
        "Pending" or "OnHold" or "Paused" or "Review" or "InReview" or "Waiting" or "Late" => "Warning",
        "Failed" or "Cancelled" or "Canceled" or "Overdue" or "Rejected" or "Blocked" or "Suspended" or "Banned" => "Danger",
        _ => "Neutral",
    };

    // Whether the app's layout gives its nav items icons (the starter's sidebar does, its top nav doesn't).
    static bool NavHasIcons(ProjectInfo project) =>
        project.ReadFile(project.Host + "Components/Layout/MainLayout.razor") is { } layout
        && Regex.IsMatch(layout, @"<NavItem\b[^>]*\bIcon=");

    static IEnumerable<string> DirectoryBuildProps(string root)
    {
        for (var dir = new DirectoryInfo(root); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Directory.Build.props"))) yield return File.ReadAllText(Path.Combine(dir.FullName, "Directory.Build.props"));
    }

    /// <summary>The plural of a C# name: Customer is Customers, Category Categories, Box Boxes, Person People.</summary>
    public static string Plural(string name)
    {
        var words = Regex.Split(name, @"(?<=[a-z])(?=[A-Z])|(?<= )");
        var last = words[^1];
        var head = name[..^last.Length];
        string plural = last switch
        {
            "Person" => "People", "person" => "people",
            "Child" => "Children", "child" => "children",
            _ when Regex.IsMatch(last, "[^aeiou]y$") => last[..^1] + "ies",
            _ when Regex.IsMatch(last, "(s|x|z|ch|sh)$") => last + "es",
            _ => last + "s",
        };
        return head + plural;
    }

    static string Kebab(string name) => Regex.Replace(name, @"(?<=[a-z0-9])(?=[A-Z])", "-").ToLowerInvariant();

    static string Words(string name) => ModelReader.Words(name);

    // Lowercase but for acronyms: "Email" is "email", "URL" stays.
    static string Lower(string words) => string.Join(' ', words.Split(' ').Select(w => w.Length > 1 && w.All(char.IsUpper) ? w : w.ToLower(CultureInfo.InvariantCulture)));

    static string Article(string word) => Regex.IsMatch(word, "^(?:[aeio]|u(?!ni|se|su|r)|hour|honest)", RegexOptions.IgnoreCase) ? "an" : "a";

    static string Join(IEnumerable<string> words)
    {
        var list = words.ToList();
        return list.Count switch { 0 => "", 1 => list[0], _ => string.Join(", ", list[..^1]) + " or " + list[^1] };
    }
}
