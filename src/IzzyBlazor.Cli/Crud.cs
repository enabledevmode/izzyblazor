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

    /// <param name="notes">Said on every run: what is in the way, or needs a hand.</param>
    /// <param name="written">Said only by a run that writes: what was made, such as where the records are kept.</param>
    public static ProjectInfo Prepare(ProjectInfo project, string model, Dictionary<string, string> settings, List<string> notes, List<string> written)
    {
        string? Given(string name) => settings.FirstOrDefault(s => s.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;

        // Http: the pages in the .Client project, calling an API crud adds to the server.
        var http = "Http".Equals(Given("Store"), StringComparison.OrdinalIgnoreCase);
        if (http && project.Client is null)
            throw new UsageException($"--store http is for an app with a .Client project, whose pages run in the browser. {project.Name}'s pages run on the server and read the store there: use --store ef or --store memory.");
        if (project.LayoutInBrowser && !http)
            throw new UsageException($"{project.Name}'s pages run in the browser (its Routes are in {project.Client}), and crud's pages read the database on the server. Use --store http (Pro), which adds an API for them to call, or add them to a Blazor Web App whose pages are in the server project.");
        var root = project.Directory!;
        var files = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .Where(f => !Regex.IsMatch(f, @"(^|/)(bin|obj|\.izzy|node_modules|\.git)/"))
            .Select(f => (f, File.ReadAllText(Path.Combine(root, f))))
            .ToList();
        // Whether a string that isn't nullable is required is the setting of the project the model is in.
        var csproj = http && Directory.EnumerateFiles(Path.Combine(root, project.Client!), "*.csproj").FirstOrDefault() is { } clientProject
            ? File.ReadAllText(clientProject) : File.ReadAllText(Path.Combine(root, project.Host, project.Name + ".csproj"));
        var nullable = Regex.IsMatch(csproj, @"<Nullable>\s*enable\s*</Nullable>", RegexOptions.IgnoreCase)
            || DirectoryBuildProps(root).Any(p => Regex.IsMatch(p, @"<Nullable>\s*enable\s*</Nullable>", RegexOptions.IgnoreCase));
        var info = ModelReader.Read(files, model, nullable);
        foreach (var skipped in info.Skipped) written.Add($"{info.Name}.{skipped}: left out.");

        // The pages in the .Client project can't see a class in the server project; the server sees the client's.
        if (http && !info.File.StartsWith(project.Client!, StringComparison.Ordinal) && (project.Host.Length == 0 || info.File.StartsWith(project.Host, StringComparison.Ordinal)))
            throw new UsageException($"{info.Name} is in the server project ({info.File}), and the pages --store http writes are in {project.Client!.TrimEnd('/')}, which can't see it. Move {info.Name} and the enums it uses to {project.Client!.TrimEnd('/')}: the server project sees them there too.");

        var name = Given("Name") ?? Plural(info.Name);
        // The words people see, when the class's name isn't them ("team member" for Member); the plural follows.
        var singular = Display(Given("Singular")) ?? Words(info.Name);
        var plural = Display(Given("Plural")) ?? (Given("Singular") is not null ? Plural(singular) : Words(name));
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
        var store = http ? "Http" : Given("Store") ?? (contexts.Count > 0 ? "Ef" : "Memory");
        ContextInfo? context = null;
        // Behind the API, the store the server's pages would have: the DbContext with a DbSet of the model, else memory.
        if (http && contexts.Count > 1)
            throw new UsageException($"{string.Join(" and ", contexts.Select(c => c.Name))} each have a DbSet<{info.Name}>: pick one with --context.");
        if (http && contexts.Count == 1)
        {
            context = contexts[0];
            settings["Context"] = context.Name;
        }
        else if (http) written.Add($"No DbContext has a DbSet<{info.Name}>, so behind the API the {Lower(plural)} are kept in memory until the app stops: Memory{info.Name}Store. When there's one, run again with --overwrite.");
        else if (store.Equals("Ef", StringComparison.OrdinalIgnoreCase))
        {
            if (contexts.Count == 0)
                throw new UsageException($"No DbContext in {project.Name} has a DbSet<{info.Name}>. Add one (public DbSet<{info.Name}> {Plural(info.Name)} => Set<{info.Name}>();) and a migration, or use --store memory.");
            if (contexts.Count > 1)
                throw new UsageException($"{string.Join(" and ", contexts.Select(c => c.Name))} each have a DbSet<{info.Name}>: pick one with --context.");
            context = contexts[0];
            settings["Context"] = context.Name;
        }
        else written.Add($"The {Lower(plural)} are kept in memory until the app stops: Memory{info.Name}Store. When there's a DbSet<{info.Name}>, run again with --store ef --overwrite.");
        // A key the database doesn't make (an invoice number, or an int with [DatabaseGenerated(None)]) is typed on New.
        var keyInput = !info.KeyGenerated && info.Key.Kind != FieldKind.Guid;
        if (keyInput && (Given("Mode") ?? "Grid").Equals("Grid", StringComparison.OrdinalIgnoreCase))
            throw new UsageException($"{info.Name}'s key {info.Key.Name} is typed by people, and the grid can't ask for a key only when adding. Let the database make the key, or use --mode pages (Pro).");
        // The line that registers the store, as crud writes it; a second run with the other store replaces it.
        var registration = new Regex($@"builder\.Services\.Add\w+<[\w.]*\bI{Regex.Escape(info.Name)}Store,\s*[\w.]+>\(\);");
        if (!registration.IsMatch(program) && Regex.IsMatch(program, $@"\bI{Regex.Escape(info.Name)}Store\b"))
            notes.Add($"Program.cs registers I{info.Name}Store in its own way, so it wasn't changed: check it uses the store you want.");
        // A run with the other store leaves the first one's file in the project, where it breaks the build once the interface changes.
        var other = $"{project.Host}Data/{(context is not null ? "Memory" : "Ef")}{info.Name}Store.cs";
        if (project.ReadFile(other) is not null)
            notes.Add($"{other} is from a run with the other store and isn't used now: delete it.");
        // The same between the server's pages and the browser's: what the other way wrote breaks the build or doubles the routes.
        var stale = new List<string>();
        void Stale(string path) { if (project.ReadFile(path) is not null) stale.Add(path); }
        if (http)
        {
            Stale($"{project.Host}Data/I{info.Name}Store.cs");
            if (!project.LayoutInBrowser) foreach (var page in (string[])["Index", "Details", "Edit"]) Stale($"{project.Host}Components/Pages/{name}/{page}.razor");
        }
        else if (project.Client is { } browser && project.ReadFile($"{browser}Data/Http{info.Name}Store.cs") is not null)
        {
            Stale($"{browser}Data/Http{info.Name}Store.cs");
            Stale($"{browser}Data/I{info.Name}Store.cs");
            foreach (var page in (string[])["Index", "Details", "Edit"]) Stale($"{browser}Pages/{name}/{page}.razor");
            Stale($"{project.Host}Data/{info.Name}Api.cs");
            notes.Add($"{browser}Program.cs registers Http{info.Name}Store, and {project.Host}Program.cs maps {info.Name}Api: remove both lines.");
        }
        if (stale.Count > 0)
            notes.Add($"From a run with {(http ? "the store on the server" : "--store http")}, and in the way now: delete {string.Join(", ", stale)}.");
        // The pages' own files, in the project whose pages they are.
        var layoutFile = project.LayoutInBrowser ? "~/" + project.Client + "Layout/MainLayout.razor" : "Components/Layout/MainLayout.razor";
        var layout = project.ReadFile(project.LayoutInBrowser ? project.Client + "Layout/MainLayout.razor" : project.Host + "Components/Layout/MainLayout.razor");
        var clientProgram = http ? project.ReadFile(project.Client + "Program.cs") ?? "" : "";
        var authorize = Given("Authorize") ?? (Regex.IsMatch(program, @"\b(AddIdentityCore|AddDefaultIdentity|AddIdentity|AddAuthentication)\b") ? "true" : "false");

        if (http)
        {
            // The pages ask who the person is in the browser too, and the browser's app has its own setup.
            var policies = new[] { settings.GetValueOrDefault("ViewPolicy"), settings.GetValueOrDefault("EditPolicy") }.Where(p => p is { Length: > 0 }).Distinct().ToList();
            if (policies.Count > 0)
                notes.Add($"The pages check {string.Join(" and ", policies)} in the browser as well as on the server: define {(policies.Count == 1 ? "it" : "them")} in {project.Client}Program.cs too (AddAuthorizationCore), or the pages fail there.");
            else if (authorize == "true" && !Regex.IsMatch(clientProgram, @"\bAddAuthorizationCore\b"))
                notes.Add($"The pages are behind sign-in, and {project.Client}Program.cs doesn't call AddAuthorizationCore: add it with AddCascadingAuthenticationState and AddAuthenticationStateDeserialization, or the pages fail in the browser.");
        }

        // The nav entry crud wrote on an earlier run, when the words have changed since (--singular, --plural):
        // only the entry that still reads as crud wrote it, so one you renamed, or a kit's, stays.
        var named = Recorded(project, info.Name, "Plural");
        var navRenamed = named is not null && named != plural && layout is not null
            && Regex.IsMatch(layout, NavEntry(route, named), RegexOptions.IgnoreCase);

        // Where the pages run: static with a store on the server, in the browser with --store http. --render
        // puts a server store's pages on a circuit, or picks between the browser's two modes.
        var circuits = Regex.IsMatch(program, @"\bAddInteractiveServerComponents\b");
        var render = Given("Render") is { Length: > 0 } asked && !asked.Equals("Default", StringComparison.OrdinalIgnoreCase) && !asked.Equals("Static", StringComparison.OrdinalIgnoreCase)
            ? (asked.StartsWith("Interactive", StringComparison.OrdinalIgnoreCase) ? asked["Interactive".Length..] : asked).ToLowerInvariant() : null;
        if ("Static".Equals(Given("Render"), StringComparison.OrdinalIgnoreCase) && http)
            throw new UsageException("--store http writes pages that run in the browser: use --render webassembly or auto, or a store on the server.");
        switch (render)
        {
            case null: break;
            case "server":
                if (http) throw new UsageException("--store http writes pages that run in the browser: use --render webassembly or auto, or a store on the server.");
                if (!circuits) throw new UsageException($"--render server needs interactive server components, and {project.Host}Program.cs doesn't add them (AddInteractiveServerComponents). Leave the pages static, or add them first.");
                break;
            case "webassembly" or "auto":
                if (!http) throw new UsageException($"--render {render} runs the pages in the browser, where they can't read a store on the server. Add --store http (Pro), which gives them an API to call.");
                if (render == "auto" && !circuits) throw new UsageException($"--render auto starts on a circuit, and {project.Host}Program.cs doesn't add interactive server components. Use --render webassembly.");
                break;
            default: throw new UsageException($"--render is Server, WebAssembly or Auto, not \"{Given("Render")}\".");
        }
        if (Given("Render") is not null) settings[settings.Keys.First(k => k.Equals("Render", StringComparison.OrdinalIgnoreCase))] = render switch { "server" => "Server", "webassembly" => "WebAssembly", "auto" => "Auto", _ => "Default" };
        // An app whose Routes are interactive: a page says nothing and takes the app's mode.
        var appWide = project.LayoutInBrowser || Regex.IsMatch(project.ReadFile(project.Host + "Components/App.razor") ?? "", @"<Routes\b[^>]*@rendermode");

        // The folder the pages' own folder goes in, when it isn't the usual one.
        string? pagesFolder = null;
        if (Given("Folder") is { Length: > 0 } into)
        {
            var folder = into.Replace('\\', '/').Trim('/');
            if (folder.Split('/').Any(part => part is "" or "." or "..") || Path.IsPathRooted(folder))
                throw new UsageException($"--folder is a folder in the project, such as Components/Pages/Admin, not \"{into}\".");
            var inClient = project.Client is { } clientFolder && folder.StartsWith(clientFolder, StringComparison.Ordinal);
            // In an app whose layout is in the browser, Components/… is the .Client project's own folder (Pages.InApp).
            if (http && !inClient && !(project.LayoutInBrowser && folder.StartsWith("Components/", StringComparison.Ordinal)))
                throw new UsageException($"With --store http the pages are in the .Client project: give --folder a folder under {project.Client}, or leave it out.");
            if (!http && inClient)
                throw new UsageException($"{folder} is in the .Client project, and these pages read the store on the server. Give --folder a folder of the server project, or use --store http (Pro).");
            pagesFolder = "~/" + Pages.InApp(project, folder);
        }
        var pageLayout = Given("Layout") is { Length: > 0 } named2 ? named2 : null;
        if (pageLayout is not null && !Regex.IsMatch(pageLayout, @"^[A-Za-z_]\w*(\.[A-Za-z_]\w*)*$"))
            throw new UsageException($"--layout is a layout component's name, such as EmptyLayout, not \"{pageLayout}\".");
        var noNav = "false".Equals(Given("Nav"), StringComparison.OrdinalIgnoreCase);

        settings["Model"] = info.Name;
        settings["Name"] = name;
        settings["Singular"] = singular;
        settings["Plural"] = plural;
        settings["Route"] = route;
        settings["Store"] = store;
        settings["Authorize"] = authorize;

        var shown = info.Fields;
        // A Guid other than the key is made by code, not typed: shown, not edited.
        var form = shown.Where(f => !f.ReadOnly && f.Kind != FieldKind.Guid).ToList();
        if (form.Count == 0) throw new UsageException($"{info.Name} has nothing to edit: every property crud shows is read-only.");
        // A required text field names a record best; an optional one can be empty, and then the name falls back.
        var title = shown.FirstOrDefault(f => f.Kind == FieldKind.Text && f.Required) ?? shown.FirstOrDefault(f => f.Kind == FieldKind.Text);
        // A first and last name ahead of any other text are the title together ("Ada Lindqvist"), sorted by the
        // last; after a Subject, they name the requester, not the record.
        var first = shown.FirstOrDefault(f => f.Kind == FieldKind.Text && f.Name is "FirstName" or "GivenName" or "Forename");
        var last = shown.FirstOrDefault(f => f.Kind == FieldKind.Text && f.Name is "LastName" or "Surname" or "FamilyName");
        if (first is not null && last is not null && first == shown.FirstOrDefault(f => f.Kind == FieldKind.Text)) title = first;
        else last = null;
        var subtitle = shown.FirstOrDefault(f => f.Kind == FieldKind.Email);
        var search = shown.Where(f => f.Kind is FieldKind.Text or FieldKind.Email && !f.ReadOnly).Take(SearchColumns).ToList();
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
            .Concat(shown.Where(f => f != status && f != title && f != last && f != subtitle && Rank(f) < 9).OrderBy(Rank).ThenBy(f => shown.ToList().IndexOf(f)))
            .Take(ListColumns - 1)
            // In the model's order, the status first.
            .OrderBy(f => f == status ? -1 : shown.ToList().IndexOf(f))
            .ToList();
        // The grid page shows the same columns as the list (title, then these); the rest only in its dialog.
        IReadOnlyCollection<ModelField> columns = [.. new[] { title, last, subtitle }.OfType<ModelField>(), .. list];
        // Newest first by the last date shown, else by the title.
        var sortDate = list.LastOrDefault(f => f.Kind is FieldKind.Date or FieldKind.DateTime);
        var titleKey = (last ?? title ?? info.Key).Name.ToLowerInvariant();

        // The model and its enums by aliases in each file (using Customer = global::Shop.Models.Customer), not
        // their namespace: a model named Task or Tag would clash with what the pages use, and importing its
        // namespace would bring the clash to every page. A name the pages use already gets a suffix.
        var modelType = Alias(info.Name, "Item");
        var aliases = new Dictionary<string, string> { [modelType] = Global(info.Namespace, info.Name) };
        // Each enum by its full name (a nested Ticket.Kind, or two Status enums in different namespaces).
        var enumTypes = new Dictionary<string, string>();
        foreach (var e in shown.Where(f => f.Kind == FieldKind.Enum))
        {
            if (enumTypes.ContainsKey(e.EnumFullName)) continue;
            var enumName = Alias(e.EnumFullName[(e.EnumFullName.LastIndexOf('.') + 1)..], "Value");
            for (var n = 2; aliases.ContainsKey(enumName); n++) enumName = Alias(e.EnumFullName[(e.EnumFullName.LastIndexOf('.') + 1)..], "Value") + n;
            enumTypes[e.EnumFullName] = enumName;
            aliases[enumName] = "global::" + e.EnumFullName;
        }
        string TypeOf(ModelField f) => f.Kind == FieldKind.Enum ? enumTypes[f.EnumFullName] : f.Type.TrimEnd('?');

        // The DbContext too, for the EF store, unless it's in the store's own namespace.
        if (context is not null && context.Namespace != project.RootNamespace + ".Data")
            aliases[context.Name] = Global(context.Namespace, context.Name);
        // A type in the global namespace is in scope everywhere, and an alias of its own name would clash with it.
        foreach (var plain in aliases.Where(a => a.Value == "global::" + a.Key).Select(a => a.Key).ToList()) aliases.Remove(plain);

        var facts = new Dictionary<string, object>(project.Facts)
        {
            ["Aliases"] = aliases.Select(a => (IReadOnlyDictionary<string, object>)new Dictionary<string, object> { ["Name"] = a.Key, ["Type"] = a.Value }).ToList(),
            ["Fields"] = Records(shown, status, columns, TypeOf),
            ["FormFields"] = Records(form, status, columns, TypeOf),
            ["ListFields"] = Records(list, status, columns, TypeOf),
            ["SearchFields"] = Records(search, status, columns, TypeOf),
            ["StatusValues"] = status is null ? new List<IReadOnlyDictionary<string, object>>() : Values(status),
            ["HasTitle"] = title is not null,
            ["HasSubtitle"] = subtitle is not null,
            ["HasSearch"] = search.Count > 0,
            ["HasStatus"] = status is not null,
            ["KeyGenerated"] = info.KeyGenerated,
            ["KeyIsText"] = info.Key.Kind == FieldKind.Text,
            ["KeyIsGuid"] = info.Key.Kind == FieldKind.Guid,
            // A key people choose (an invoice number): typed on New, fixed after.
            ["KeyInput"] = keyInput,
            ["HasFactory"] = context is not null && Regex.IsMatch(program, $@"AddDbContextFactory\s*<\s*{Regex.Escape(context.Name)}\b"),
            ["NavIcons"] = layout is not null && Regex.IsMatch(layout, @"<NavItem\b[^>]*\bIcon="),
            ["OverHttp"] = http,
            ["InEf"] = context is not null,
            // In an app whose pages are static unless they say otherwise, a page in the .Client project says so.
            ["ClientRenderMode"] = !appWide && (http || render == "server"),
            ["HasLayout"] = pageLayout is not null,
            ["HasHttpClient"] = Regex.IsMatch(clientProgram, @"new\s+HttpClient\b|\bAddHttpClient\b|<\s*HttpClient\s*>"),
            // A run with the other store replaces its registration rather than adding a second.
            ["HasStoreRegistration"] = registration.IsMatch(program),
            // The nav links to the pages already (a second run, or a kit's nav): no second link.
            ["NavRenamed"] = navRenamed && !noNav,
            ["HasNavLink"] = noNav || layout is not null && Regex.IsMatch(layout, $@"<NavItem\b[^>]*\bHref=""/?{Regex.Escape(route)}""", RegexOptions.IgnoreCase),
            ["HasViewPolicy"] = settings.GetValueOrDefault("ViewPolicy") is { Length: > 0 },
            ["HasEditPolicy"] = settings.GetValueOrDefault("EditPolicy") is { Length: > 0 },
        };
        var keyType = info.Key.Type.TrimEnd('?');
        var tokens = new Dictionary<string, string>(project.Tokens)
        {
            ["ModelType"] = modelType,
            // Where the interface is: beside the stores, or with --store http in the .Client project, which the pages are in.
            ["StoreNamespace"] = http ? project.ClientNamespace + ".Data" : project.RootNamespace + ".Data",
            // The pages are the server's alone, unless they run in the browser.
            ["ServerOnly"] = http ? "Components/Pages/{{Name}}/-" : "Components/**",
            ["LayoutFile"] = layoutFile,
            ["NavEntry"] = navRenamed ? NavEntry(route, named!) : "",
            // The API is the model's, as the store is, whatever --name and --route the pages have.
            ["ApiRoute"] = Kebab(Plural(info.Name)),
            // Auto where the app has circuits too, so the page works before the browser has the app.
            ["RenderMode"] = render switch { "server" => "InteractiveServer", "webassembly" => "InteractiveWebAssembly", "auto" => "InteractiveAuto", _ => circuits ? "InteractiveAuto" : "InteractiveWebAssembly" },
            ["Layout"] = pageLayout ?? "",
            ["PagesFolder"] = pagesFolder ?? (http ? project.Client + "Pages" : "Components/Pages"),
            // A new record's dates start at today, not the type's empty 01/01/0001.
            ["NewItemInit"] = NewItemInit(shown),
            ["Singular"] = singular,
            ["SingularLower"] = Lower(singular),
            ["Article"] = Article(Lower(singular)),
            ["Plural"] = plural,
            ["PluralLower"] = Lower(plural),
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
            // A record's name in an interpolated string over x: its title field, or "Reading 3" when it has none.
            ["RowName"] = last is not null ? $"{{x.{title!.Name}}} {{x.{last.Name}}}"
                : title is { Required: true } ? $"{{x.{title.Name}}}"
                : title is not null ? $"{{(string.IsNullOrWhiteSpace(x.{title.Name}) ? \"{singular} \" + x.{info.Key.Name} : x.{title.Name})}}"
                : $"{singular} {{x.{info.Key.Name}}}",
            ["TitleSortField"] = (last ?? title ?? info.Key).Name,
            // A string key goes in a URL escaped (INV/001); a number or Guid as it is.
            // The grid's column layout cookie and the export's file name: the pages' name in kebab case.
            ["LayoutKey"] = Kebab(name),
            ["FileName"] = Kebab(name),
            ["UrlKey"] = info.Key.Kind == FieldKind.Text ? "Uri.EscapeDataString(key)" : "key",
            ["KeyLabel"] = info.Key.Label,
            ["KeyLabelLower"] = Lower(info.Key.Label),
            ["KeyArticle"] = Article(Lower(info.Key.Label)),
            // A typed key is text, or a number that is never empty.
            ["KeyTrim"] = info.Key.Kind == FieldKind.Text ? "?.Trim() ?? \"\"" : "",
            ["KeyText"] = info.Key.Kind == FieldKind.Text ? "" : ".ToString()",
            ["IdInit"] = info.Key.Kind == FieldKind.Text ? " = \"\";" : "",
            ["TitleLabel"] = last is not null ? "Name" : title?.ShortLabel ?? singular,
            ["TitleSortKey"] = titleKey,
            ["InitialSort"] = sortDate is not null ? "-" + sortDate.Name.ToLowerInvariant() : titleKey,
            ["SubtitleField"] = subtitle?.Name ?? "",
            ["StatusField"] = status?.Name ?? "",
            ["StatusType"] = status is null ? "" : TypeOf(status),
            ["StatusLabel"] = status?.Label ?? "",
            ["StatusPlural"] = status is null ? "" : Lower(Plural(status.Label)),
            ["SearchPlaceholder"] = search.Count == 0 ? "" : "Search " + Join(search.Select(f => Lower(f.Label))),
            ["NoMatchHint"] = "Try another " + Join(search.Select(f => Lower(f.Label)).Take(1).Concat(status is null ? [] : [Lower(status.Label)])) + ".",
            ["StoreClass"] = (context is null ? "Memory" : "Ef") + info.Name + "Store",
            ["StoreLifetime"] = context is null ? "AddSingleton" : "AddScoped",
            ["Context"] = context?.Name ?? "",
            ["Set"] = context?.SetName ?? "",
        };
        return project with { Facts = facts, Tokens = tokens };
    }

    // Names the generated files use for their own types and the library's: a model or enum named one of these is
    // aliased with a suffix (TaskItem, StatusValue).
    static readonly HashSet<string> Taken =
    [
        "Task", "ValueTask", "Guid", "Enum", "Lock", "List", "IEnumerable", "IQueryable", "StringComparison", "CancellationToken",
        "DbUpdateException", "EF", "IFormattable", "Math", "String", "Object", "Action", "Func", "DateOnly", "DateTime", "TimeOnly",
        "System", "Microsoft", "IzzyBlazor", "Index", "Details", "Edit", "Grid", "EditContext", "ValidationMessageStore",
        "ToastService", "NavigationManager", "DataResult", "DataRequest", "DataGridSave", "DataGridDelete", "Status", "Accent",
        "Length", "Color", "Orientation", "Justify", "Breakpoint", "ButtonVariant", "ControlSize", "TextSize", "FontWeight",
        "ContainerSize", "ContainerAlign", "RequiredMark", "TextFieldType", "DateFieldType", "DataGridSmallScreen",
        "DataGridEditMode", "RowActionOverflow", "Lucide",
        // The API and the store over HTTP.
        "Results", "Validator", "ValidationContext", "ValidationResult", "HttpContext", "HttpClient", "HttpStatusCode", "Uri", "Stream",
        "StatusCodes", "JsonSerializerOptions", "HashSet", "Dictionary", "IResult",
    ];

    static string Alias(string name, string suffix) => Taken.Contains(name) || Setup.ComponentNames.Contains(name) ? name + suffix : name;

    // The text of the nav entry at the route, when it is these words: a pattern for the manifest's replacement.
    static string NavEntry(string route, string words) =>
        $@"(?i)(?<=<NavItem\b[^>]*\bHref=""/?{Regex.Escape(route)}""[^>]*>){Regex.Escape(words)}(?=</NavItem>)";

    // A setting of the last run of crud for the model, from .izzy/items.json; null when there's none.
    static string? Recorded(ProjectInfo project, string model, string setting)
    {
        try
        {
            var items = System.Text.Json.Nodes.JsonNode.Parse(project.ReadFile(".izzy/items.json") ?? "{}")?["items"] as System.Text.Json.Nodes.JsonArray;
            return items?.LastOrDefault(i => (string?)i?["id"] == "crud" && (string?)i?["settings"]?["Model"] == model)?["settings"]?[setting]?.GetValue<string>();
        }
        catch (Exception e) when (e is System.Text.Json.JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    static string Global(string ns, string name) => "global::" + (ns.Length > 0 ? ns + "." : "") + name;

    // " { Joined = DateOnly.FromDateTime(DateTime.Today) }" for the editable dates that can't be empty; "()" with none.
    // A DateTime or DateTimeOffset starts in UTC, the zone its field shows and reads it in: a local midnight would
    // show as the day before east of UTC, and a local time hours from what was saved.
    static string NewItemInit(IEnumerable<ModelField> fields)
    {
        var dates = fields.Where(f => !f.ReadOnly && !f.Nullable && f.Kind is FieldKind.Date or FieldKind.DateTime)
            .Select(f => $"{f.Name} = {(f.Type.TrimEnd('?') switch
            {
                "DateOnly" => "DateOnly.FromDateTime(DateTime.Today)",
                "DateTimeOffset" => f.Kind == FieldKind.Date ? "DateTime.UtcNow.Date" : "DateTimeOffset.UtcNow",
                _ => f.Kind == FieldKind.Date ? "DateTime.UtcNow.Date" : "DateTime.UtcNow",
            })}").ToList();
        return dates.Count == 0 ? "()" : $" {{ {string.Join(", ", dates)} }}";
    }

    static List<IReadOnlyDictionary<string, object>> Records(IEnumerable<ModelField> fields, ModelField? status, IReadOnlyCollection<ModelField> columns, Func<ModelField, string> typeOf) =>
        [.. fields.Select(f => (IReadOnlyDictionary<string, object>)new Dictionary<string, object>
        {
            ["Name"] = f.Name,
            // A date and time is kept and shown in UTC, and its label says so.
            ["Label"] = f.Label + Zone(f),
            ["Column"] = f.ShortLabel + Zone(f),
            ["InList"] = columns.Contains(f),
            ["LabelLower"] = Lower(f.Label),
            ["Article"] = Article(Lower(f.Label)),
            ["Enter"] = Enter(Lower(f.Label)),
            ["Kind"] = f.Kind.ToString(),
            // A bool? can be unset: a Select of Yes and No, not a Switch.
            ["NullableBool"] = f.Kind == FieldKind.Bool && f.Nullable,
            ["Key"] = f.Name.ToLowerInvariant(),
            ["IsStatus"] = f == status,
            ["PriorityAttr"] = f == status ? " Priority=\"1\"" : "",
            ["Format"] = Format(f.Kind),
            ["HasFormat"] = Format(f.Kind).Length > 0,
            ["FormatAttr"] = Format(f.Kind) is { Length: > 0 } format ? $" Format=\"{format}\"" : "",
            // A Guid that isn't the key is made by code, as in the form pages: shown, not typed.
            ["EditableAttr"] = f.ReadOnly || f.Kind == FieldKind.Guid ? " Editable=\"false\"" : "",
            // A list narrows by its choices: a filter in the column's header (the text columns have the search).
            ["FilterAttr"] = Filtered(f.Kind) ? " Filterable" : "",
            ["WidthAttr"] = Width(f.Kind, f.ShortLabel + Zone(f)) is { } rem ? $" Width=\"Length.Rem({rem.ToString(CultureInfo.InvariantCulture)})\"" : "",
            ["Type"] = f.Type,
            ["EnumType"] = f.Kind == FieldKind.Enum ? typeOf(f) : "",
            // A nullable enum's choices are of the nullable type, as its property is.
            ["EnumParam"] = f.Kind == FieldKind.Enum ? typeOf(f) + (f.Nullable ? "?" : "") : "",
            ["EnumItems"] = f.Kind != FieldKind.Enum ? "" : f.Nullable
                ? $"Enum.GetValues<{typeOf(f)}>().Select(v => ({typeOf(f)}?)v)"
                : $"Enum.GetValues<{typeOf(f)}>()",
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
            // A nullable enum is a Select that can be cleared; radios can't go back to none.
            ["Radio"] = f.Kind == FieldKind.Enum && !f.Nullable && f.Values.Count is >= 2 and <= 4,
            // Cleared, it shows its placeholder again, as the design's optional Country ("Choose a country").
            ["ClearableAttr"] = f.Nullable && f.Kind is FieldKind.Enum ? $" Placeholder=\"Choose {Article(Lower(f.Label))} {Lower(f.Label)}\" Clearable" : "",
            ["Values"] = Values(f),
        })];

    static List<IReadOnlyDictionary<string, object>> Values(ModelField field) =>
        [.. field.Values.Select(v => (IReadOnlyDictionary<string, object>)new Dictionary<string, object>
        {
            ["Name"] = v.Name, ["Label"] = v.Label, ["Tone"] = Tone(v.Name),
        })];

    static string Zone(ModelField f) => f.Kind == FieldKind.DateTime && !f.Label.Contains("UTC", StringComparison.Ordinal) ? " (UTC)" : "";

    // What a required field says when it's empty: "Enter a name.", and "Enter notes." or "Enter text." for words
    // that take no article.
    static string Enter(string label)
    {
        var last = label.Split(' ')[^1];
        var bare = last is "text" or "information" or "info" or "feedback" or "content" or "advice" or "data" or "copy"
            || (last.EndsWith('s') && !Regex.IsMatch(last, "(ss|us|is)$"));
        return bare ? $"Enter {label}." : $"Enter {Article(label)} {label}.";
    }

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
        // Wide enough for the title and its sort arrow too: about 0.55rem a letter, 2.75rem of padding and arrow;
        // a filtered column (choices) also has its filter button: 1.5rem and the gap before it.
        ? Math.Max(width, Math.Ceiling((title.Length * 0.55 + 2.75) * 2) / 2) + (Filtered(kind) ? 2 : 0)
        : null;

    // The columns a list filters in their headers: its choices (the text columns have the search).
    static bool Filtered(FieldKind kind) => kind is FieldKind.Enum or FieldKind.Bool;

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

    // A display name as given, capitalized, without characters that would end a string or attribute; null when none.
    static string? Display(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : ModelReader.Safe(char.ToUpperInvariant(text.Trim()[0]) + text.Trim()[1..]);

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
