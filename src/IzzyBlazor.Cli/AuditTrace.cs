using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace IzzyBlazor.Cli;

// The trace: from the element axe found to the IzzyBlazor component that drew it, the fix the pack knows for
// it, and the tag in the app's source. Each step is taken when it can be, and a finding keeps what was reached.
static partial class Audit
{
    /// <summary>
    /// A fix the pack knows: for a rule, on a component (or any), where the selectors fit the element as an
    /// <see cref="Entry"/>'s do. <see cref="Set"/> is the option to set; <see cref="Docs"/> an anchor on the
    /// component's page (<c>#accessibility</c>) or an address.
    /// </summary>
    public sealed record Fix(string Id, string Rule, string? Component, string? Matches, string? Closest, string? Has, string? Set, string Text, string? Docs);

    /// <summary>Traces each finding of a page.</summary>
    public sealed class Tracer
    {
        sealed record Marked(string Name, string[] Root, string[] Own, string[] Inside, string[] Like, string? Docs);

        readonly Components _library;
        readonly List<Marked> _marked;
        readonly IReadOnlyList<Fix> _fixes;
        readonly Sources? _sources;

        /// <param name="library">The components, with what marks each one's outermost element.</param>
        /// <param name="fixes">The pack's fixes.</param>
        /// <param name="folders">The app's source folders (its project, and its .Client project), the first being the one paths are written from; none when there is no project.</param>
        public Tracer(Components library, IReadOnlyList<Fix> fixes, IReadOnlyList<string> folders)
        {
            _library = library;
            _marked = [.. library.All.Where(c => c["root"] is JsonArray { Count: > 0 }).Select(c => new Marked((string)c["name"]!,
                [.. c["root"]!.AsArray().Select(m => (string)m!)], [.. (c["own"]?.AsArray() ?? []).Select(m => (string)m!)],
                [.. (c["inside"]?.AsArray() ?? []).Select(m => (string)m!)], [.. (c["like"]?.AsArray() ?? []).Select(m => (string)m!)], (string?)c["docs"]))];
            _fixes = fixes;
            _sources = folders.Count > 0 ? new Sources(folders) : null;
        }

        /// <summary>Whether the app's source is there to trace into.</summary>
        public bool HasSource => _sources is not null;

        /// <summary>
        /// What the page looks for on the elements around a finding, as JSON: <c>keys</c>, the marks that make
        /// an element a component's (one of its own, or the first of a component that has none of its own), and
        /// <c>all</c>, every mark it answers with for such an element.
        /// </summary>
        public string Marks() => new JsonObject
        {
            ["keys"] = new JsonArray([.. _marked.SelectMany(m => m.Own.Length > 0 ? m.Own : [m.Root[0]]).Distinct(StringComparer.Ordinal).Select(m => (JsonNode)m)]),
            ["all"] = new JsonArray([.. _marked.SelectMany(m => m.Root).Distinct(StringComparer.Ordinal).Select(m => (JsonNode)m)]),
        }.ToJsonString();

        /// <summary>
        /// Gives each finding of <paramref name="page"/> its trace, from what the page answered about the
        /// element (<c>at</c>, which is taken out) and the fixes' tests among its claims.
        /// </summary>
        public void Trace(JsonObject page)
        {
            // The page that was checked: where a redirect led, when it did. A route is written unescaped.
            var path = Uri.UnescapeDataString(PathOf((string?)page["landed"] ?? (string)page["url"]!));
            var pageFile = _sources?.PageFor(path);
            var files = pageFile is null ? null : _sources!.Around(pageFile, _library);
            foreach (var rule in page["violations"]!.AsArray())
                foreach (var node in rule!["nodes"]!.AsArray().Select(n => n!.AsObject()))
                {
                    var at = node["at"] as JsonObject;
                    node.Remove("at");
                    try
                    {
                        if (One((string)rule["id"]!, node, at, pageFile, files) is { Count: > 0 } trace) node["trace"] = trace;
                    }
                    catch (Exception e) when (e is InvalidOperationException or NullReferenceException or InvalidCastException or FormatException)
                    {
                        // The page's own script changed what it answers about the element: this finding has no trace.
                    }
                }
        }

        JsonObject One(string rule, JsonObject node, JsonObject? at, string? pageFile, List<string>? files)
        {
            var claims = node["claims"] is JsonArray answered ? answered.Select(c => (string)c!).ToHashSet(StringComparer.Ordinal) : [];
            var trace = new JsonObject();
            // The components around the element, the nearest first: each a name, or the names it may be.
            var levels = (at?["chain"]?.AsArray() ?? []).Select(level => (Marks: level!["marks"]!.AsArray().Select(m => (string)m!).ToHashSet(StringComparer.Ordinal), Said: level["said"], Self: (bool?)level["self"] == true))
                .Select(level => (Names: Resolve(level.Marks), level.Marks, Evidence: Said.From(level.Said), level.Self)).Where(level => level.Names.Count > 0).ToList();
            // What a component draws inside itself (a date field's popover, a grid's pager) is a part of it
            // and no tag of the app's: the trace starts at the component. Unless the app wrote one there (a
            // Button as a Menu's Trigger) that says what the element says: then it is the app's.
            while (levels.Count > 1 && levels[1].Names.Any(name => _marked.First(m => m.Name == name).Inside.Any(levels[0].Marks.Contains))
                && !(files is not null && WrittenInside(files, levels[0].Names, levels[0].Evidence, levels[1].Names)))
            {
                levels.RemoveAt(0);
                levels[0] = (levels[0].Names, levels[0].Marks, levels[0].Evidence, Self: false);
            }
            // An element that is no component's outermost one may be the app's own HTML, written in the
            // page: it is looked for first. One that isn't written there is a part of the component around it.
            JsonObject? source = null;
            var written = false;
            if (pageFile is not null && files is not null)
            {
                trace["page"] = pageFile;
                if (levels.Count == 0 || !levels[0].Self) source = Plain(files, at);
                written = source is not null;
                source ??= Source(files, [.. levels.Select(l => (Names: Written(l.Names), l.Evidence))]);
                // For an element inside a component, a list of the places that component is written says
                // little: one place, or none.
                if (!written && levels.Count > 0 && !levels[0].Self && source?["file"] is null) source = null;
                if (source is not null) trace["source"] = source;
            }
            if (levels.Count > 0)
            {
                // The source says which of the look-alikes it is: the tag found, or the only one of them the
                // page's files have at all (two DataGrids and no TreeList). Else it may be any of them, those
                // that draw the same element with options set too (a Row for a Stack).
                if (!written && (string?)source?["tag"] is { } tag && Written(levels[0].Names).Contains(tag) && (bool?)source!["around"] != true) levels[0] = ([tag], levels[0].Marks, levels[0].Evidence, levels[0].Self);
                else if (!written && files is not null && Written(levels[0].Names) is { Count: > 1 } alike
                    && _sources!.Tags(files, _library).Select(t => t.Tag).Where(alike.Contains).Distinct().ToList() is [var only]) levels[0] = ([only], levels[0].Marks, levels[0].Evidence, levels[0].Self);
                else if (!written) levels[0] = (Written(levels[0].Names), levels[0].Marks, levels[0].Evidence, levels[0].Self);
                var around = levels.Skip(written ? 0 : 1).Where(l => l.Names.Count == 1).Select(l => l.Names[0]).Distinct().ToList();
                if (!written && levels[0].Names.Count == 1) trace["component"] = levels[0].Names[0];
                else if (!written) trace["candidates"] = new JsonArray([.. levels[0].Names.Select(n => (JsonNode)n)]);
                if (around.Count > 0) trace["inside"] = new JsonArray([.. around.Select(n => (JsonNode)n)]);
            }

            // A fix for a component is for the element's own, once it is known which that is.
            var own = (string?)trace["component"];
            if (_fixes.FirstOrDefault(f => f.Rule == rule && (f.Component is null || f.Component == own)
                && (f.Matches is null && f.Closest is null && f.Has is null || claims.Contains($"f:{f.Id}"))) is { } fix)
            {
                var about = fix.Component ?? own;
                trace["fix"] = new JsonObject { ["text"] = fix.Text, ["component"] = about, ["option"] = fix.Set, ["entry"] = fix.Id };
                if (DocsOf(about, fix.Docs) is { } docs) trace["docs"] = docs;
            }
            if (trace["docs"] is null && DocsOf(own, null) is { } componentPage) trace["docs"] = componentPage;
            return trace;
        }

        string? DocsOf(string? component, string? docs)
        {
            if (docs is not null && docs.Contains("://", StringComparison.Ordinal)) return docs;
            var page = component is null ? null : _marked.FirstOrDefault(m => m.Name == component)?.Docs ?? (string?)_library.All.FirstOrDefault(c => (string)c["name"]! == component)?["docs"];
            return page is null ? null : page + docs;
        }

        // The tags that may be written for these components: their own, and those of the components that
        // draw the same element with options set (a Row for a Stack).
        List<string> Written(List<string> names) => [.. names.Concat(names.SelectMany(name => _marked.First(m => m.Name == name).Like)).Distinct(StringComparer.Ordinal)];

        // Whether the app wrote one of these components inside a tag of one of those, saying something the
        // element says (its text, a Label that is its name).
        bool WrittenInside(List<string> files, List<string> names, Said evidence, List<string> around)
        {
            var (inner, outer) = (Written(names), Written(around));
            return _sources!.Tags(files, _library).Any(tag => inner.Contains(tag.Tag) && tag.Within.Any(outer.Contains) && evidence.Score(tag) != (0, 0));
        }

        // The components an element with these marks is the outermost element of. A mark that only one
        // component draws settles it. Without one, it is the component with all its marks there that has the
        // most of them, or each of those that look the same (Dialog and Drawer): the source tells them apart.
        List<string> Resolve(HashSet<string> marks)
        {
            var own = _marked.Select(m => (m.Name, Count: m.Own.Count(marks.Contains))).Where(m => m.Count > 0).ToList();
            if (own.Count > 0) return [.. own.Where(m => m.Count == own.Max(o => o.Count)).Select(m => m.Name)];
            var shared = _marked.Where(m => m.Own.Length == 0 && m.Root.All(marks.Contains)).ToList();
            return [.. shared.Where(m => m.Root.Length == shared.Max(s => s.Root.Length)).Select(m => m.Name)];
        }

        // Where the element's component is written: its own tag, else the tag of the component around it (a
        // button a DataGrid draws is no tag of the app's; the DataGrid is). Further out is the page's frame,
        // which says nothing of the element.
        JsonObject? Source(List<string> files, List<(List<string> Names, Said Evidence)> levels)
        {
            foreach (var (level, index) in levels.Take(2).Select((l, i) => (l, i)))
            {
                var found = _sources!.Tags(files, _library).Where(tag => level.Names.Contains(tag.Tag)).ToList();
                if (found.Count == 0) continue;
                var source = Pick(found, level.Evidence);
                if (index == 0)
                {
                    // The one tag there is, with nothing written on it that the element says, inside another
                    // component: that component may have drawn the element (a pager's button), so less sure.
                    if (found.Count == 1 && levels.Count > 1 && level.Evidence.Score(found[0]) == (0, 0)) source["confidence"] = "medium";
                    return source;
                }
                // When the source has one place for the component around it: a list of places an element
                // may be inside helps no one.
                if (source["file"] is null) return null;
                source["around"] = true;
                return source;
            }
            return null;
        }

        // Where an element of plain HTML is written: its tag with its id, or with its classes.
        JsonObject? Plain(List<string> files, JsonObject? at)
        {
            if (at is null || (string?)at["tag"] is not { } element) return null;
            var id = (string?)at["id"];
            var classes = (at["classes"]?.AsArray() ?? []).Select(c => (string)c!).ToList();
            if (id is null && classes.Count == 0) return null;
            var plain = _sources!.Tags(files, _library).Where(t => t.Tag == element && (
                id is not null && t.Literals.GetValueOrDefault("id") == id
                || classes.Count > 0 && t.Literals.GetValueOrDefault("class") is { } written && classes.All(written.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains))).ToList();
            if (plain.Count == 0) return null;
            var source = Pick(plain, Said.From(at["said"]));
            // An HTML element's place has no component's tag.
            source.Remove("tag");
            return source;
        }

        // One place: that line. Several: the one whose written text is in the element, else all of them.
        static JsonObject Pick(List<Sources.Written> found, Said evidence)
        {
            static JsonObject Place(Sources.Written w) => new() { ["file"] = w.File, ["line"] = w.Line };
            if (found.Count == 1) return new JsonObject { ["file"] = found[0].File, ["line"] = found[0].Line, ["tag"] = found[0].Tag, ["confidence"] = "high" };
            // What is written whole in the element (an id, a Label that is its name) counts before what is
            // only part of its text.
            var scored = found.Select(w => (Written: w, Score: evidence.Score(w))).ToList();
            var best = scored.Max(s => s.Score);
            if (best != (0, 0) && scored.Count(s => s.Score == best) == 1)
            {
                var one = scored.First(s => s.Score == best).Written;
                return new JsonObject { ["file"] = one.File, ["line"] = one.Line, ["tag"] = one.Tag, ["confidence"] = "medium" };
            }
            var among = best != (0, 0) ? scored.Where(s => s.Score == best).Select(s => s.Written).ToList() : found;
            var source = new JsonObject { ["confidence"] = "low", ["places"] = new JsonArray([.. among.Take(5).Select(w => (JsonNode)Place(w))]) };
            if (among.Count > 5) source["more"] = among.Count - 5;
            if (among.Select(w => w.Tag).Distinct().Count() == 1) source["tag"] = among[0].Tag;
            return source;
        }
    }

    /// <summary>What an element says: the values of its attributes, and the start of its text.</summary>
    sealed record Said(HashSet<string> Attributes, string Text)
    {
        public static Said From(JsonNode? said) => new(
            (said?["attributes"]?.AsArray() ?? []).Select(a => Spaces().Replace((string)a!, " ").Trim()).ToHashSet(StringComparer.Ordinal),
            Spaces().Replace((string?)said?["text"] ?? "", " ").Trim());

        /// <summary>How much of what a tag has written is in the element: how many are the whole of an attribute or of its text, then how many are a part of its text.</summary>
        public (int Whole, int Part) Score(Sources.Written tag)
        {
            var (whole, part) = (0, 0);
            foreach (var written in tag.Literals.Values.Append(tag.Text).Where(t => t is not null).Select(t => Spaces().Replace(t!, " ").Trim()).Where(t => t.Length >= 2))
            {
                if (Attributes.Contains(written) || Text == written) whole++;
                else if (Regex.IsMatch(Text, $@"(?<!\w){Regex.Escape(written)}(?!\w)")) part++;
            }
            return (whole, part);
        }
    }

    /// <summary>The app's .razor files, read as far as a trace needs: which one a path's page is, and where a tag is written.</summary>
    sealed partial class Sources
    {
        /// <summary>A tag as written: its file from the project's folder, its line, and what it says in plain text.</summary>
        /// <param name="Literals">The attributes written as text (no <c>@</c>), by name: for a component, its text options and plain HTML attributes.</param>
        /// <param name="Tag">The tag's name.</param>
        /// <param name="Text">The text right inside the tag, when it is plain.</param>
        /// <param name="Within">The names of the tags it is written inside, in its file.</param>
        public sealed record Written(string File, int Line, string Tag, Dictionary<string, string> Literals, string? Text, HashSet<string> Within);

        readonly string _home;
        readonly Dictionary<string, string> _text = new(StringComparer.Ordinal);
        readonly List<string> _files = [];
        readonly List<(Regex Route, (int Literal, int Open, int Optional) Rank, string File)> _routes = [];
        // Several browsers trace at once.
        readonly System.Collections.Concurrent.ConcurrentDictionary<string, List<Written>> _tags = new(StringComparer.Ordinal);
        readonly System.Collections.Concurrent.ConcurrentDictionary<string, List<string>> _around = new(StringComparer.Ordinal);

        public Sources(IReadOnlyList<string> folders)
        {
            _home = folders[0];
            foreach (var full in folders.Where(Directory.Exists).SelectMany(Razor).Order(StringComparer.Ordinal))
            {
                var file = Path.GetRelativePath(_home, full).Replace('\\', '/');
                if (_text.ContainsKey(file)) continue;
                try { _text[file] = File.ReadAllText(full); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { continue; }
                _files.Add(file);
                foreach (Match page in PageDirective().Matches(_text[file]))
                    _routes.Add((Route(page.Groups[1].Value, out var rank), rank, file));
            }
        }

        // The .razor files under a folder: not what a build makes, nor a folder whose name starts with a dot
        // (a copy of the app kept by a tool), nor one that can't be read.
        static IEnumerable<string> Razor(string folder)
        {
            var options = new EnumerationOptions { IgnoreInaccessible = true };
            foreach (var file in Directory.EnumerateFiles(folder, "*.razor", options)) yield return file;
            foreach (var inner in Directory.EnumerateDirectories(folder, "*", options))
            {
                var name = Path.GetFileName(inner);
                if (name is "bin" or "obj" or "node_modules" || name.StartsWith('.')) continue;
                foreach (var file in Razor(inner)) yield return file;
            }
        }

        // "/orders/{id:int}" as what a path fits: a parameter is one part, {*rest} any number, {id?} one or none.
        // Its rank: the more parts written out the better, then no catch-all, then fewer optional parts.
        static Regex Route(string template, out (int Literal, int Open, int Optional) rank)
        {
            var pattern = new StringBuilder("^");
            rank = (0, 0, 0);
            foreach (var part in template.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                if (part.StartsWith("{*", StringComparison.Ordinal))
                {
                    pattern.Append("(/.*)?");
                    rank.Open--;
                }
                else if (part.StartsWith('{') && part.EndsWith("?}", StringComparison.Ordinal))
                {
                    pattern.Append("(/[^/]+)?");
                    rank.Optional--;
                }
                else if (part.StartsWith('{')) pattern.Append("/[^/]+");
                else
                {
                    pattern.Append('/').Append(Regex.Escape(part));
                    rank.Literal++;
                }
            }
            return new Regex(pattern.Append("/?$").ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        /// <summary>The file whose <c>@page</c> a path fits, the closest fit when several do; null when none does.</summary>
        public string? PageFor(string path) =>
            _routes.Where(r => r.Route.IsMatch(path == "/" ? "" : path)).OrderByDescending(r => r.Rank).Select(r => r.File).FirstOrDefault();

        /// <summary>A page's file, its layouts, and the app's own components they use, the page first.</summary>
        public List<string> Around(string pageFile, Components library) => _around.GetOrAdd(pageFile, _ =>
        {
            var files = new List<string> { pageFile };
            // The layout the page names, else the one the router gives every page; and a layout's own.
            var layout = LayoutOf(pageFile) ?? _files.Select(f => DefaultLayout().Match(_text[f])).Where(m => m.Success).Select(m => Named(m.Groups[1].Value)).FirstOrDefault(f => f is not null);
            while (layout is not null && !files.Contains(layout))
            {
                files.Add(layout);
                layout = LayoutOf(layout);
            }
            // A tag that is a library component's is not a file of the app's, whatever the app names its files.
            for (var i = 0; i < files.Count && files.Count < 80; i++)
                foreach (var tag in Tags([files[i]], library).Select(t => t.Tag).Where(n => char.IsUpper(n[0]) && library.Options(n) is null).Distinct())
                    if (Named(tag) is { } used && !files.Contains(used)) files.Add(used);
            return files;
        });

        string? LayoutOf(string file) => LayoutDirective().Match(_text[file]) is { Success: true } m ? Named(m.Groups[1].Value) : null;

        // Layout.MainLayout, or MainLayout: the file of that name.
        string? Named(string type)
        {
            var name = type[(type.LastIndexOf('.') + 1)..] + ".razor";
            return _files.FirstOrDefault(f => Path.GetFileName(f).Equals(name, StringComparison.Ordinal));
        }

        /// <summary>The tags written in <paramref name="files"/>, in the files' order. A file is read for its tags once.</summary>
        public IEnumerable<Written> Tags(List<string> files, Components library) => files.SelectMany(file => _tags.GetOrAdd(file, _ => [.. Read(file, library)]));

        IEnumerable<Written> Read(string file, Components library)
        {
            var text = _text[file];
            var tags = RazorPage.Tags(text);
            // Where each component's element ends: what is written before that is inside it.
            var open = new List<(string Name, int End)>();
            foreach (var tag in tags)
            {
                open.RemoveAll(around => around.End < tag.Start);
                var within = open.Select(around => around.Name).ToHashSet(StringComparer.Ordinal);
                if (!tag.SelfClosing && char.IsUpper(tag.Name[0]) && RazorPage.ElementEnd(text, tag.Start) is var end and > 0) open.Add((tag.Name, end));
                // A component's options that take text; any attribute of an HTML element, and one a component passes on.
                var options = library.Options(tag.Name);
                var literals = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var a in tag.Attributes.Where(a => a.HasValue && a.Quote != '\0' && !a.Name.StartsWith('@')))
                {
                    var value = text.Substring(a.ValueStart, a.ValueLength);
                    if (value.Contains('@') || options is not null && options.TryGetValue(a.Name, out var takesText) && !takesText) continue;
                    literals.TryAdd(a.Name, value);
                }
                string? inside = null;
                if (!tag.SelfClosing && text.IndexOf('<', tag.End) is var next and >= 0 && text[tag.End..next].Trim() is { Length: > 0 } content && !content.Contains('@')) inside = content;
                yield return new Written(file, 1 + text.AsSpan(0, tag.Start).Count('\n'), tag.Name, literals, inside, within);
            }
        }

        [GeneratedRegex(@"^[ \t]*@page\s+""([^""]+)""", RegexOptions.Multiline)]
        private static partial Regex PageDirective();

        [GeneratedRegex(@"^[ \t]*@layout\s+([\w.]+)", RegexOptions.Multiline)]
        private static partial Regex LayoutDirective();

        [GeneratedRegex(@"DefaultLayout=""@?typeof\(\s*([\w.]+)\s*\)""")]
        private static partial Regex DefaultLayout();
    }

    // What a report says of a finding's trace: the component, the fix, and where it is written.
    static string TraceLines(JsonObject node)
    {
        if (node["trace"] is not JsonObject trace) return "";
        var text = new StringBuilder();
        var docs = (string?)trace["docs"];
        var component = (string?)trace["component"];
        if (component is not null)
            text.Append($"  - Component: `{component}`{((trace["inside"] as JsonArray)?.Count > 0 ? $", inside {string.Join(", ", trace["inside"]!.AsArray().Select(n => $"`{(string)n!}`"))}" : "")}{(docs is not null && trace["fix"] is null ? $". [Docs]({docs})" : "")}\n");
        else if (trace["candidates"] is JsonArray candidates)
            text.Append($"  - Component: {Among(candidates.Select(n => $"`{(string)n!}`"))} (they draw the same element)\n");
        else if (trace["inside"] is JsonArray { Count: > 0 } inside)
            text.Append($"  - Inside: {string.Join(", ", inside.Select(n => $"`{(string)n!}`"))}\n");
        if (trace["fix"] is JsonObject fix)
            text.Append($"  - Fix: {((string)fix["text"]!).TrimEnd('.')}.{(docs is not null ? $" [Docs]({docs})" : "")}\n");
        if (trace["source"] is JsonObject source)
        {
            var around = (bool?)source["around"] == true ? $" (the `{(string?)source["tag"]}` it is in)" : "";
            if ((string?)source["file"] is { } file) text.Append($"  - Source: `{file}:{(int)source["line"]!}`{around}, confidence {(string)source["confidence"]!}\n");
            else
            {
                var places = source["places"]!.AsArray().Select(p => $"`{(string)p!["file"]!}:{(int)p["line"]!}`").ToList();
                text.Append($"  - Source: one of {string.Join(", ", places)}{((int?)source["more"] is { } more ? $" and {more} more" : "")}{around}\n");
            }
        }
        else if ((string?)trace["page"] is { } page) text.Append($"  - Source: the page is `{page}`; the element wasn't found in it\n");
        return text.ToString();
    }

    static string Among(IEnumerable<string> names) => names.ToList() is { Count: > 1 } list ? $"{string.Join(", ", list.Take(list.Count - 1))} or {list[^1]}" : names.FirstOrDefault() ?? "";

    /// <summary>
    /// results.sarif: the real findings and those a person must look at, each on its line in the source. One
    /// that wasn't traced is on line 1 of its page's file, with its selector in the message. One on a page that
    /// has no file in the app is left out and counted (<c>unplaced</c>).
    /// </summary>
    /// <param name="prefix">The project's folder from the repository's root, with a slash at its end, or empty.</param>
    public static JsonObject Sarif(JsonObject run, string prefix)
    {
        var findings = AllFindings(run).Where(f => f.Verdict is Real or NeedsPerson).ToList();
        var rules = new JsonArray();
        foreach (var rule in findings.Select(f => f.Rule).DistinctBy(r => (string)r["id"]!))
            rules.Add(new JsonObject
            {
                ["id"] = (string)rule["id"]!,
                ["shortDescription"] = new JsonObject { ["text"] = (string)rule["help"]! },
                ["fullDescription"] = new JsonObject { ["text"] = Criterion(rule["tags"]!.AsArray().Select(t => (string)t!).ToList()) },
                ["helpUri"] = (string)rule["helpUrl"]!,
            });
        var results = new JsonArray();
        var unplaced = 0;
        foreach (var finding in findings)
        {
            var trace = finding.Node["trace"] as JsonObject;
            var source = trace?["source"] as JsonObject;
            var place = source?["file"] is not null ? source : source?["places"]?[0] as JsonObject;
            var message = new StringBuilder(finding.Why ?? (string)finding.Rule["help"]!);
            message.Append($". Element {(string)finding.Node["target"]!} on {PathOf((string)finding.Page["url"]!)}");
            if ((string?)trace?["component"] is { } component) message.Append($", drawn by {component}");
            message.Append('.');
            if ((string?)trace?["fix"]?["text"] is { } fix) message.Append($" {fix.TrimEnd('.')}.");
            if (finding.Verdict == NeedsPerson) message.Append(" A person should look.");
            var result = new JsonObject
            {
                ["ruleId"] = (string)finding.Rule["id"]!,
                ["level"] = finding.Verdict == NeedsPerson ? "note" : (string?)finding.Rule["impact"] is "critical" or "serious" ? "error" : "warning",
                ["message"] = new JsonObject { ["text"] = message.ToString() },
                ["partialFingerprints"] = new JsonObject { ["izzyAudit/v1"] = finding.Id },
            };
            // Code scanning takes no result without a place: one on a page with no file in the app is in the other reports.
            if (((string?)place?["file"] ?? (string?)trace?["page"]) is not { } file)
            {
                unplaced++;
                continue;
            }
            // From the repository's root, with no ".." in it (a file of the .Client project beside this one).
            var parts = new List<string>();
            foreach (var part in (prefix + file).Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                if (part == ".." && parts.Count > 0 && parts[^1] != "..") parts.RemoveAt(parts.Count - 1);
                else if (part != ".") parts.Add(part);
            }
            result["locations"] = new JsonArray(new JsonObject
            {
                ["physicalLocation"] = new JsonObject
                {
                    ["artifactLocation"] = new JsonObject { ["uri"] = string.Join('/', parts.Select(Uri.EscapeDataString)) },
                    ["region"] = new JsonObject { ["startLine"] = (int?)place?["line"] ?? 1 },
                },
            });
            if ((string?)trace?["docs"] is { } docs) result["properties"] = new JsonObject { ["docs"] = docs };
            results.Add(result);
        }
        return new JsonObject
        {
            ["$schema"] = "https://json.schemastore.org/sarif-2.1.0.json",
            ["version"] = "2.1.0",
            ["runs"] = new JsonArray(new JsonObject
            {
                ["tool"] = new JsonObject
                {
                    ["driver"] = new JsonObject
                    {
                        ["name"] = "izzy audit", ["version"] = (string)run["version"]!, ["informationUri"] = "https://izzyblazor.com/docs/accessibility/audit", ["rules"] = rules,
                    },
                },
                ["results"] = results,
                ["properties"] = new JsonObject { ["unplaced"] = unplaced },
            }),
        };
    }
}
