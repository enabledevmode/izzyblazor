using System.Text;
using System.Text.RegularExpressions;
using IzzyBlazor.DocsGen;
using IzzyBlazor.Site.Components.Parts;

namespace IzzyBlazor.Site.Services.Mcp;

/// <summary>
/// What the MCP server's tools answer, as Markdown: the API metadata, components.json, the examples
/// and the docs pages, for an assistant writing code with IzzyBlazor. Links are absolute, on
/// izzyblazor.com. A name that isn't found is an <see cref="McpDocsException"/> with the closest names.
/// </summary>
public sealed partial class McpDocs(ApiMetadata api, Registry registry, ExampleCatalog catalog, SearchIndex search, PageText pages)
{
    /// <summary>The first line of every answer: which release it describes.</summary>
    private string Release => $"IzzyBlazor {api.Version}";

    private static string Url(string path) => Sitemap.Origin + path;

    // Words an assistant's question carries that no title or summary is about.
    private static readonly HashSet<string> Filler =
    [
        "a", "an", "and", "are", "build", "by", "can", "create", "do", "does", "for", "from", "how", "i", "in", "is", "it",
        "make", "me", "my", "need", "of", "on", "or", "show", "that", "the", "this", "to", "use", "using", "want", "what",
        "which", "with",
    ];

    /// <summary>
    /// The site's search, which wants every word to match. An assistant asks in sentences, so when
    /// that finds nothing the filler words go, then the entries matching the most words are shown.
    /// </summary>
    public string Search(string query, int limit = 10)
    {
        query ??= "";
        limit = Math.Clamp(limit, 1, 50);
        var results = search.Search(query, limit);
        var text = new StringBuilder($"{Release}: ");
        if (results.Count > 0)
        {
            text.Append($"{results.Count} result{(results.Count == 1 ? "" : "s")} for \"{results.Query}\".\n");
            foreach (var group in results.Groups)
            {
                text.Append($"\n## {group.Title}{(group.Total > group.Entries.Count ? $" (top {group.Entries.Count} of {group.Total})" : "")}\n\n");
                foreach (var entry in group.Entries)
                {
                    text.Append(Line(entry));
                }
            }

            return text.ToString();
        }

        var words = SearchIndex.Terms(query).Where(w => !Filler.Contains(w)).Distinct().ToList();
        var hits = new Dictionary<string, (SearchEntry Entry, int Words, int Rank)>(StringComparer.Ordinal);
        foreach (var word in words)
        {
            var rank = 0;
            foreach (var entry in search.Search(word, 50).Groups.SelectMany(g => g.Entries))
            {
                rank++;
                hits[entry.Href] = hits.TryGetValue(entry.Href, out var hit) ? (hit.Entry, hit.Words + 1, Math.Min(hit.Rank, rank)) : (entry, 1, rank);
            }
        }

        var best = hits.Values.OrderByDescending(h => h.Words).ThenBy(h => h.Rank).Take(limit).ToList();
        if (best.Count == 0)
        {
            return text.Append($"no results for \"{results.Query}\". Search takes a few words from a component's name or purpose (\"file upload\", \"modal\"); list_components lists everything. IzzyBlazor may not have it: don't invent a component.\n").ToString();
        }

        text.Append($"nothing matches every word of \"{results.Query}\". The closest, matching {best[0].Words} of {words.Count} words ({string.Join(", ", words)}) at best:\n\n");
        foreach (var hit in best)
        {
            text.Append(Line(hit.Entry));
        }

        return text.ToString();
    }

    // A result, and the call that gives its details.
    private string Line(SearchEntry entry)
    {
        var line = new StringBuilder($"- {entry.Kind}: **{entry.Title}**");
        if (entry.Context is { Length: > 0 } context)
        {
            line.Append($" ({context})");
        }

        if (entry.Summary is { Length: > 0 } summary)
        {
            line.Append($": {summary}");
        }

        line.Append(NextCall(entry) is { } call ? $" → {call}" : $" {Url(entry.Href)}");

        return line.Append('\n').ToString();
    }

    /// <summary>The tool call that answers for a search result: <c>get_example("form-signup")</c>.</summary>
    private string? NextCall(SearchEntry entry)
    {
        var path = entry.Href.Split('#')[0];
        var anchor = entry.Href.Contains('#') ? entry.Href[(entry.Href.IndexOf('#') + 1)..] : null;
        string Call(string tool, string argument) => $"{tool}(\"{argument}\")";

        switch (entry.Kind)
        {
            case SearchKind.Component or SearchKind.Part:
                return Call("get_component", entry.Title);
            case SearchKind.Type:
                return Call("get_type", entry.Title);
            case SearchKind.TypeMember when entry.Context is { } type:
                return Call("get_type", type);
            case SearchKind.Example when anchor?.StartsWith("ex-", StringComparison.Ordinal) == true:
                return Call("get_example", anchor[3..]);
            case SearchKind.Demo when ScenarioExample(path) is { } id:
                return Call("get_example", id);
            case SearchKind.Member when entry.Context?.Split(" · ")[0] is { } owner:
                return Call("get_component", owner);
        }

        // Articles, sections and style props: the page they're on.
        return path.StartsWith("docs/", StringComparison.Ordinal) && !path.EndsWith("/api", StringComparison.Ordinal)
            ? Call("get_article", path)
            : null;
    }

    // demos/{component}/{scenario} to the scenario's example id.
    private string? ScenarioExample(string path)
    {
        var parts = path.Trim('/').Split('/');
        return parts is ["demos", var id, var scenario] && registry.ById(id) is { } component
            ? component.Scenarios.FirstOrDefault(s => s.Id == scenario)?.Example.Id
            : null;
    }

    public string ListComponents(string? category = null)
    {
        var categories = registry.Categories.ToList();
        if (!string.IsNullOrWhiteSpace(category))
        {
            var match = categories.FirstOrDefault(c => string.Equals(c, category.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new McpDocsException($"No category \"{category}\". Categories: {string.Join(", ", categories)}.");
            categories = [match];
        }

        var text = new StringBuilder($"{Release}: {registry.Components.Count} components");
        text.Append(categories.Count == 1 ? $" ({registry.InCategory(categories[0]).Count()} in {categories[0]}).\n" : ", by category.\n");
        text.Append("Modes: Static is static server-side rendering (no circuit, no WebAssembly), Interactive is Server, WebAssembly or Auto. ")
            .Append("Only limits are noted. Parts are components written inside another and documented with it.\n");
        foreach (var name in categories)
        {
            text.Append($"\n## {name}\n\n");
            foreach (var component in registry.InCategory(name))
            {
                text.Append($"- **{component.Name}**: {component.Summary}");
                var limited = api.Component(component.Name) is { } apiComponent
                    ? ModeRows(apiComponent, component).Where(r => (r.Static, r.Interactive) != ("full", "full")).ToList()
                    : [];
                if (limited.Count > 0)
                {
                    text.Append(" Limits: ").AppendJoin("; ", limited.Select(r =>
                        $"{(limited.Count == 1 && r.Feature == "Everything" ? "" : r.Feature + ", ")}{Limits(new ApiModes { NoScript = r.NoScript, Static = r.Static, Interactive = r.Interactive })}"))
                        .Append('.');
                }

                if (component.Parts.Count > 0)
                {
                    text.Append($" Parts: {string.Join(", ", component.Parts)}.");
                }

                text.Append('\n');
            }
        }

        return text.ToString();
    }

    public string GetComponent(string name)
    {
        var component = FindComponent(Input(name, "a component name"));
        var owner = registry.ByName(component.Name) ?? registry.OwnerOfPart(component.Name);
        var text = new StringBuilder($"# {component.DisplayName}\n\n");

        // A server app installs IzzyBlazor, which includes IzzyBlazor.Core: say so, or "package
        // IzzyBlazor.Core" reads as the one to install.
        var package = component.Package == "IzzyBlazor.Core" ? "package IzzyBlazor.Core (included in IzzyBlazor)" : $"package {component.Package}";
        var facts = new List<string> { Release, $"namespace {component.FullName[..component.FullName.LastIndexOf('.')]}", package };
        if (component.AddedIn is { } added)
        {
            facts.Add($"added in {added}");
        }

        text.Append(string.Join(" · ", facts)).Append('\n');
        if (owner is not null)
        {
            var isPart = owner.Name != component.Name;
            text.Append(isPart ? $"A part of {owner.Name}, documented on its pages. " : "")
                .Append($"Overview: {Url(Registry.OverviewHref(owner))} · API: {Url(registry.ApiHref(component.Name, null) ?? Registry.ApiPageHref(owner))}\n");
        }

        // The remarks often finish the summary's thought ("What confirming does:"), so they follow it.
        Section(text, null, component.Summary);
        Section(text, null, component.Remarks);
        if (component.Examples is { Count: > 0 } usage)
        {
            text.Append("\n## Usage\n\n").AppendJoin("\n\n", usage.Select(u => Markdown.Fence(u, "razor"))).Append('\n');
        }

        ModeSupport(text, component, owner);
        Members(text, "Parameters", component.Properties, component);
        if (component.Bases.Count > 0)
        {
            // Shared by many components, so named here and described by get_type.
            text.Append("\nIt also takes these shared parameters; get_type with the base's name gives their types, defaults and descriptions:\n\n");
            foreach (var shared in component.Bases)
            {
                var names = api.Base(shared)?.Properties.Where(p => p.Obsolete is null).Select(p => p.Name) ?? [];
                text.Append($"- {Markdown.Code(shared)}: {string.Join(", ", names)}\n");
            }
        }

        Members(text, "Events", component.Events, component);
        Members(text, "Methods", component.Methods, component);
        if (component.Types.Count > 0)
        {
            text.Append($"\n## Types\n\nIts members use {string.Join(", ", component.Types.Select(Markdown.Code))}: get_type gives their values and members.\n");
        }

        if (owner is not null && owner.Name == component.Name && owner.Parts.Count > 0)
        {
            text.Append("\n## Parts\n\n");
            foreach (var part in owner.Parts)
            {
                text.Append($"- **{part}**: {Markdown.FirstSentence(api.Component(part)?.Summary)}\n");
            }
        }

        if (owner is not null)
        {
            // A part can be listed on more than one component (ToolbarSplitButton on SplitButton and Toolbar).
            var sources = registry.Components.Where(c => c.Parts.Contains(component.Name)).Append(owner).Distinct();
            var examples = sources.SelectMany(c => c.Examples.Select(e => (e.Id, e.Title, Lead: e.Lead))
                .Concat(c.Scenarios.Select(s => (s.Example.Id, $"Demo: {s.Title}", Lead: (string?)s.Description)))).ToList();
            if (owner.Name != component.Name)
            {
                // A part: only the owner's examples that use it.
                examples = examples.Where(e => catalog.SourceOf(e.Id)?.Source.Contains($"<{component.Name}", StringComparison.Ordinal) == true).ToList();
            }
            if (examples.Count > 0)
            {
                text.Append($"\n## Examples{(owner.Name != component.Name ? $" on {owner.Name}'s page that use it" : "")}\n\nget_example gives an example's whole source by its id.\n\n");
                foreach (var (id, title, lead) in examples)
                {
                    text.Append($"- `{id}`: {title}{(lead is null ? "" : $". {lead}")}\n");
                }
            }

            text.Append($"\nThe Overview as prose, with every example's source: get_article(\"{Registry.OverviewHref(owner)}\").\n");
        }

        Section(text, "Accessibility", component.Accessibility);
        if (component.Keyboard is { Count: > 0 } keyboard)
        {
            text.Append("\n## Keyboard\n");
            foreach (var group in keyboard)
            {
                text.Append(group.Title is null ? "\n" : $"\n### {group.Title}\n\n").Append("| Press this key | To do this |\n|---|---|\n");
                foreach (var row in group.Rows)
                {
                    text.Append($"| {string.Join(", ", row.Keys.Select(k => $"`{k}`"))} | {Markdown.Cell(row.Action)} |\n");
                }
            }
        }

        var related = (component.SeeAlso ?? []).ToList();
        var articles = Articles.About(component.Name).Select(a => Markdown.Link(a.Article.Title, Url(a.Section.HrefOf(a.Article)))).ToList();
        if (related.Count > 0 || articles.Count > 0)
        {
            text.Append("\n## See also\n\n");
            foreach (var other in related)
            {
                text.Append($"- {other}: {Markdown.FirstSentence(api.Component(other)?.Summary) ?? registry.ByName(other)?.Summary}\n");
            }

            foreach (var article in articles)
            {
                text.Append($"- Troubleshooting: {article}\n");
            }
        }

        return text.ToString();
    }

    public string GetType(string name)
    {
        name = Input(name, "a type name");
        var wanted = Bare(name);
        if (string.Equals(wanted, "Responsive", StringComparison.OrdinalIgnoreCase))
        {
            // Not in the metadata (it's a generic struct), but the type of most layout parameters.
            return $$"""
                # Responsive<T>

                {{Release}} · struct `IzzyBlazor.Responsive<T>`

                One value, or a value per breakpoint, for parameters such as Gap, Columns, Width and Margin. A plain value converts to it
                (`Gap="4"`); per breakpoint, give `Base` and the breakpoints that change it, mobile first:
                `Columns="new() { Base = 1, Md = 2, Lg = 3 }"`. Breakpoints: Sm, Md, Lg, Xl.

                get_article("docs/common/responsive") explains it with examples.
                """;
        }

        if (api.Base(wanted) is { } shared)
        {
            var text = new StringBuilder($"# {shared.Name}\n\n{Release} · shared parameters, taken by every component that lists {Markdown.Code(shared.Name)} in its bases.\n");
            Section(text, null, shared.Summary);
            Members(text, "Parameters", shared.Properties, null);
            return text.ToString();
        }

        var type = api.File.Types.FirstOrDefault(t => string.Equals(t.Name, wanted, StringComparison.OrdinalIgnoreCase))
            ?? api.File.Types.FirstOrDefault(t => string.Equals(Bare(t.Name), wanted, StringComparison.OrdinalIgnoreCase));
        if (type is null)
        {
            if (FindComponentOrNull(name) is { } component)
            {
                throw new McpDocsException($"{component.Name} is a component: use get_component.");
            }

            throw NotFound("type", name, api.File.Types.Select(t => t.Name).Concat(api.File.Bases.Select(b => b.Name)));
        }

        var answer = new StringBuilder($"# {type.Name}\n\n{Release} · {type.Kind} {Markdown.Code(GenericArity().Replace(type.FullName, "<T>"))}");
        answer.Append(type.AddedIn is { } added ? $" · added in {added}\n" : "\n");
        Section(answer, null, type.Summary);
        var users = api.File.Components.Where(c => c.Types.Contains(type.Name) && c.Obsolete is null && !IsPreview(c.Name)).Select(c => c.Name).ToList();
        if (type.Members.Count > 0)
        {
            answer.Append(type.Kind == "enum" ? "\n## Values\n\n" : "\n## Members\n\n");
            foreach (var member in type.Members.Where(m => m.Obsolete is null))
            {
                // A method's Type is already its signature, name included.
                var declaration = type.Kind == "enum" ? member.Name : member.Type.Contains('(') ? member.Type : $"{member.Type} {member.Name}";
                answer.Append($"- {Markdown.Code(declaration)}");
                if (Plain(member.Summary) is { Length: > 0 } summary)
                {
                    answer.Append($": {summary}");
                }

                answer.Append('\n');
            }
        }

        if (type.Name == "Length")
        {
            answer.Append("\nShorthand: with `@using IzzyBlazor.Units` (C# 14), `15.Rem`, `240.Px`, `100.Dvh` and the other units are the same as `Length.Rem(15)`.\n");
        }

        if (users.Count > 0)
        {
            answer.Append($"\nUsed by {string.Join(", ", users)}.\n");
        }

        return answer.ToString();
    }

    public string GetExample(string id)
    {
        id = Input(id, "an example id");
        // Accept the forms the site shows: an anchor (#ex-button-variants) or a demo's address.
        var key = id.Split('#')[^1];
        key = key.StartsWith("ex-", StringComparison.Ordinal) && catalog.SourceOf(key[3..]) is not null ? key[3..] : key;
        if (catalog.SourceOf(key) is null && ScenarioExample(LocalPath(key)) is { } scenarioId)
        {
            key = scenarioId;
        }

        if (catalog.SourceOf(key) is not { } source || registry.Example(key)?.Component.Preview == true)
        {
            if (FindComponentOrNull(key) is { } component && (registry.ByName(component.Name) ?? registry.OwnerOfPart(component.Name)) is { } owner)
            {
                var ids = owner.Examples.Select(e => e.Id).Concat(owner.Scenarios.Select(s => s.Example.Id)).ToList();
                throw new McpDocsException($"\"{id}\" is a component, not an example id. {owner.Name}'s examples: {string.Join(", ", ids)}.");
            }

            throw NotFound("example", id, catalog.Ids.Where(e => registry.Example(e)?.Component.Preview != true));
        }

        var text = new StringBuilder();
        if (registry.Example(key) is { } found && !found.Component.Preview)
        {
            var (owner, example) = found;
            text.Append($"# {example.Title}\n\n{Release} · an example on {owner.Name}'s page, {Url(Registry.OverviewHref(owner))}");
            var scenario = owner.Scenarios.FirstOrDefault(s => s.Example.Id == key);
            if (scenario is not null)
            {
                text.Append($" (a demo: {Url(Registry.ScenarioHref(owner, scenario))})");
            }

            text.Append('\n');
            if ((scenario?.Description ?? example.Lead) is { } lead)
            {
                text.Append($"\n{lead}\n");
            }

            if (ModeSwitch.Offered.Where(example.Modes.Contains).ToList() is { } offered && offered.Count < ModeSwitch.Offered.Length)
            {
                text.Append($"\nShown in {string.Join(" and ", offered.Select(ModeChip.Name))} only.\n");
            }

            foreach (var (mode, note) in example.Notes)
            {
                text.Append($"\n{ModeChip.Name(mode)}: {note}\n");
            }
        }
        else
        {
            text.Append($"# {key}\n\n{Release} · an example used in the docs.\n");
        }

        text.Append($"\n`{source.FileName}`:\n\n{Markdown.Fence(source.Source, "razor")}\n");
        text.Append("\nLines the docs hide (sample data, scaffolding) are left out.\n");
        return text.ToString();
    }

    public async Task<string> GetArticleAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ArticleList();
        }

        path = Input(path, "a path");
        var wanted = LocalPath(path);
        if (wanted.Length == 0)
        {
            return ArticleList();
        }

        // The whole path, its end (troubleshooting/onclick, onclick), a title, or a component for its Overview.
        var resolved = ArticlePaths().FirstOrDefault(p => string.Equals(p, wanted, StringComparison.OrdinalIgnoreCase))
            ?? ArticlePaths().FirstOrDefault(p => p.EndsWith("/" + wanted, StringComparison.OrdinalIgnoreCase))
            ?? Articles.Sections.SelectMany(s => s.Articles.Select(a => (Path: s.HrefOf(a), a.Title)))
                .FirstOrDefault(a => string.Equals(a.Title, wanted, StringComparison.OrdinalIgnoreCase)).Path
            ?? (FindComponentOrNull(wanted.Split('/')[^1]) is { } component && (registry.ByName(component.Name) ?? registry.OwnerOfPart(component.Name)) is { } owner
                ? Registry.OverviewHref(owner) : null);
        if (resolved is null)
        {
            var close = Closest(wanted.Split('/')[^1], ArticlePaths().Select(p => p.Split('/')[^1]))
                .SelectMany(slug => ArticlePaths().Where(p => p.EndsWith("/" + slug, StringComparison.Ordinal))).ToList();
            throw new McpDocsException($"No article \"{path}\".{(close.Count > 0 ? $" Did you mean {string.Join(", ", close)}?" : "")} get_article with no path lists them all.");
        }

        var markdown = await pages.OfAsync(resolved) ?? throw new McpDocsException($"No article \"{path}\".");
        return $"{Release} · {Url(resolved)}\n\n{markdown}\n";
    }

    /// <summary>Every article's path and title, for get_article's description and an empty or unknown path.</summary>
    public static string ArticleList()
    {
        var text = new StringBuilder("Articles:\n");
        foreach (var section in Articles.Sections)
        {
            text.Append($"\n{section.Title}:\n");
            foreach (var article in section.Articles)
            {
                text.Append($"- {section.HrefOf(article)}: {article.Title}. {article.Summary}\n");
            }
        }

        return text.Append("\nA component's Overview is docs/components/{id} (docs/components/button).\n").ToString();
    }

    private static IEnumerable<string> ArticlePaths() => Articles.Sections.SelectMany(s => s.Articles.Select(s.HrefOf));

    // ---- Pieces ----

    private static void Section(StringBuilder text, string? heading, string? html)
    {
        var markdown = Markdown.FromHtml(html);
        if (markdown.Length == 0)
        {
            return;
        }

        text.Append(heading is null ? "\n" : $"\n## {heading}\n\n").Append(markdown).Append('\n');
    }

    /// <summary>What works in each mode, a row per feature, as the Overview's table shows it.</summary>
    private static List<(string Feature, string NoScript, string Static, string Interactive)> ModeRows(ApiComponent component, RegistryComponent? owner) =>
        owner is not null && owner.Name == component.Name && owner.Features.Count > 0
            ? owner.Features.Select(f => f.Member is { } member && ModeSupportTable.ModesOf(component, member) is { } modes
                ? (f.Feature, modes.NoScript, modes.Static, modes.Interactive)
                : (f.Feature, Level(f.NoScript), Level(f.Static), Level(f.Interactive))).ToList()
            : component.Modes is { } all ? [("Everything", all.NoScript, all.Static, all.Interactive)] : [];

    private static void ModeSupport(StringBuilder text, ApiComponent component, RegistryComponent? owner)
    {
        var rows = ModeRows(component, owner);
        if (rows.Count == 0)
        {
            return;
        }

        text.Append("\n## Mode support\n\nStatic: static server-side rendering, no circuit or WebAssembly. Interactive: Server, WebAssembly or Auto.\n\n")
            .Append("| Feature | Static | Interactive |\n| --- | --- | --- |\n");
        foreach (var (feature, _, @static, interactive) in rows)
        {
            text.Append($"| {Markdown.Cell(feature)} | {@static} | {interactive} |\n");
        }

        // The fallback, not a mode: what the plain HTML does before the scripts run.
        var noScript = rows.Where(r => r.NoScript != "full" && r.Static != "none").Select(r => $"{r.Feature}: {r.NoScript}").ToList();
        if (component.Modes?.NoScriptNotes is { Length: > 0 } fallback)
        {
            noScript.Add(fallback);
        }

        if (noScript.Count > 0)
        {
            text.Append("\nWith no script (before the scripts run, or with JavaScript off): ").AppendJoin("; ", noScript).Append('\n');
        }

        if (component.Modes?.Notes is { Length: > 0 } notes)
        {
            text.Append($"\n{notes}\n");
        }
    }

    private static string Level(ModeLevel level) => level.ToString().ToLowerInvariant();

    private static void Members(StringBuilder text, string heading, IEnumerable<ApiMember> members, ApiComponent? component)
    {
        var shown = members.Where(m => m.Obsolete is null).ToList();
        if (shown.Count == 0)
        {
            return;
        }

        text.Append($"\n## {heading}\n\n");
        if (heading == "Methods")
        {
            foreach (var method in shown)
            {
                var added = method.AddedIn is { } version ? $" Added in {version}." : "";
                text.Append($"- {Markdown.Code(method.Type)}: {Plain(method.Summary)}{Remarks(method)}{added}\n");
            }

            return;
        }

        // Events have no default worth a column.
        var defaults = heading != "Events";
        text.Append(defaults ? "| Name | Type | Default | Description |\n| --- | --- | --- | --- |\n" : "| Name | Type | Description |\n| --- | --- | --- |\n");
        foreach (var member in shown)
        {
            var notes = new List<string>();
            if (member.Required)
            {
                notes.Add("Required.");
            }

            if (member.Bindable)
            {
                notes.Add($"Two-way: @bind-{member.Name}{(member.BindModes is { } bind && Limits(bind) is { } limits ? $" ({limits})" : "")}.");
            }

            if (member.Responsive)
            {
                notes.Add("Takes a value per breakpoint.");
            }

            if (member.Modes is { } modes && Limits(modes) is { } modeLimits)
            {
                notes.Add($"{modeLimits}.");
            }

            if (member.Modes?.NoScriptNotes is { Length: > 0 } fallback)
            {
                notes.Add($"With no script: {fallback}");
            }

            if (member.DeclaredOn is { } declared && component is not null)
            {
                notes.Add($"From {declared}.");
            }

            if (member.AddedIn is { } added)
            {
                notes.Add($"Added in {added}.");
            }

            var description = string.Join(" ", new[] { Plain(member.Summary) + Remarks(member) }.Concat(notes).Where(s => s.Length > 0));
            var @default = !defaults ? "" : member.Default is { } d ? $" {Markdown.Cell(Markdown.Code(d))} |" : "  |";
            text.Append($"| {Markdown.Cell(member.Name)} | {Markdown.Cell(Markdown.Code(member.Type))} |{@default} {Markdown.Cell(description)} |\n");
        }
    }

    private static string Remarks(ApiMember member) => Plain(member.Remarks) is { Length: > 0 } remarks ? " " + remarks : "";

    /// <summary>A member's doc comment as one line of Markdown.</summary>
    private static string Plain(string? html) => Markdown.Cell(Markdown.FromHtml(html).Replace("\n", " "));

    /// <summary>"Static: none; Interactive: partial", leaving out the modes where it fully works; null when it works in both.</summary>
    private static string? Limits(ApiModes modes)
    {
        var limits = new[] { ("Static", modes.Static), ("Interactive", modes.Interactive) }
            .Where(m => m.Item2 != "full").Select(m => $"{m.Item1}: {m.Item2}").ToList();
        return limits.Count == 0 ? null : string.Join("; ", limits);
    }

    // ---- Finding things by name ----

    /// <summary>The longest name, id or path a tool reads; past it, input is cut (as search cuts its query).</summary>
    public const int MaxInput = 200;

    /// <summary>A tool's argument, trimmed and cut to <see cref="MaxInput"/>; missing is the caller's error.</summary>
    private static string Input(string? value, string what)
    {
        var trimmed = value?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            throw new McpDocsException($"Give {what}.");
        }

        return trimmed.Length > MaxInput ? trimmed[..MaxInput] : trimmed;
    }

    // A path on the site from a path or an izzyblazor.com address, without its query or fragment.
    private static string LocalPath(string path)
    {
        var local = path.Trim();
        if (local.StartsWith(Sitemap.Origin, StringComparison.OrdinalIgnoreCase))
        {
            local = local[Sitemap.Origin.Length..];
        }

        return local.Split('#', '?')[0].Trim('/');
    }

    private ApiComponent FindComponent(string name) =>
        FindComponentOrNull(name) ?? throw NotFound("component", name, api.File.Components.Where(c => c.Obsolete is null && !IsPreview(c.Name)).Select(c => c.Name));

    /// <summary>A component by its name in any case, written as in Razor (<c>&lt;Select TValue&gt;</c>, <c>Select&lt;TValue&gt;</c>) or by its page id (<c>app-shell</c>).</summary>
    private ApiComponent? FindComponentOrNull(string name)
    {
        var wanted = Bare(name);
        var found = api.Component(wanted)
            ?? api.File.Components.FirstOrDefault(c => string.Equals(c.Name, wanted, StringComparison.OrdinalIgnoreCase))
            ?? (registry.ById(wanted.ToLowerInvariant()) is { } byId ? api.Component(byId.Name) : null);
        return found is not null && IsPreview(found.Name) ? null : found;
    }

    private bool IsPreview(string name) => registry.ByName(name)?.Preview == true;

    // "<Select TValue>", "Select<TValue>", "IzzyBlazor.Button" and " button " all mean the type's name.
    private static string Bare(string name)
    {
        var bare = name.Trim().Trim('<', '>', '/', ' ', '`');
        var cut = bare.IndexOfAny(['<', ' ']);
        bare = cut > 0 ? bare[..cut] : bare;
        return bare[(bare.LastIndexOf('.') + 1)..];
    }

    private static McpDocsException NotFound(string kind, string name, IEnumerable<string> known)
    {
        var close = Closest(Bare(name), known).ToList();
        return new McpDocsException($"No {kind} \"{name}\".{(close.Count > 0 ? $" Did you mean {string.Join(", ", close)}?" : "")} Try search.");
    }

    /// <summary>Up to five names containing the query, or nearest to it by edits.</summary>
    public static IEnumerable<string> Closest(string query, IEnumerable<string> names)
    {
        if (query.Length == 0)
        {
            return [];
        }

        var lower = query.ToLowerInvariant();
        return names.Distinct()
            .Select(n => (Name: n, Score: n.Contains(query, StringComparison.OrdinalIgnoreCase) || (lower.Length >= 3 && lower.Contains(n.ToLowerInvariant()))
                ? 0 : Distance(lower, n.ToLowerInvariant())))
            .Where(x => x.Score <= Math.Max(2, query.Length / 3))
            .OrderBy(x => x.Score).ThenBy(x => x.Name.Length)
            .Take(5).Select(x => x.Name);
    }

    private static int Distance(string a, string b)
    {
        var previous = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var current = new int[b.Length + 1];
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            }

            previous = current;
        }

        return previous[b.Length];
    }

    [GeneratedRegex(@"`\d+")]
    private static partial Regex GenericArity();
}

/// <summary>A name the tools don't know; the message says what to ask for instead.</summary>
public sealed class McpDocsException(string message) : Exception(message);
