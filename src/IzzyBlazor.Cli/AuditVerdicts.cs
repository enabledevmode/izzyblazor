using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using IzzyBlazor.Generation;

namespace IzzyBlazor.Cli;

// Verdicts: what each of axe's findings is, decided from the app's own decisions (.izzy/audit.json) and the
// pack of known patterns that comes with IzzyBlazor Pro. A run with verdicts also has tasks.md and a baseline.
static partial class Audit
{
    public const string Accepted = "accepted", FalsePositive = "false positive", DemoPage = "demo page", NeedsPerson = "needs a person", Real = "real";

    /// <summary>The verdicts in the order a report shows them, with what each means.</summary>
    static readonly (string Verdict, string Meaning)[] Verdicts =
    [
        (Real, "A problem for users: fix it"),
        (NeedsPerson, "A tool can't settle it: look, then fix or accept it"),
        (Accepted, "Accepted in `.izzy/audit.json`, with the reason given there"),
        (DemoPage, "Caused by a page that shows a component several times; a page of an app doesn't have it"),
        (FalsePositive, "The rule is wrong here, for the reason given"),
    ];

    /// <summary>
    /// A pattern the pack knows. <see cref="Matches"/>, <see cref="Closest"/> and <see cref="Has"/> are CSS
    /// selectors tested in the page on the element axe found (itself, itself or an ancestor, a descendant); an
    /// entry with none fits every element of its rule.
    /// </summary>
    public sealed record Entry(string Id, string Rule, string Verdict, string Why, string? Matches, string? Closest, string? Has);

    /// <summary>A finding the app's developers accepted: the rule, where (a selector, pages, both or neither) and why.</summary>
    public sealed record Acceptance(string Rule, string? Selector, string? Pages, string Reason, string? By, string? On);

    /// <summary>Pages that show components for their own sake. With <see cref="Examples"/>, only what is inside such an element counts.</summary>
    public sealed record Demo(string Pages, string? Examples);

    /// <summary>Decides each finding of a page. The first of these that fits wins: accepted, false positive, demo page, needs a person, real.</summary>
    public sealed class Judge
    {
        readonly List<Entry> _pack;
        readonly List<Acceptance> _accepted = [];
        readonly List<Demo> _demo = [];

        /// <summary>The app's file, as the report names it; null when the app has none.</summary>
        public string? ProjectFile { get; }

        public string PackVersion { get; }

        /// <summary>The fixes the pack knows, for the trace.</summary>
        public IReadOnlyList<Fix> Fixes { get; }

        /// <param name="packFile">The pack: <c>{ "version", "entries": [{ id, rule, verdict, why, matches?, closest?, has? }], "fixes": [{ id, rule, fix, component?, set?, docs?, matches?, closest?, has? }] }</c>.</param>
        /// <param name="projectFile">The app's <c>.izzy/audit.json</c>, when it has one: <c>{ "accepted": [{ rule, selector?, pages?, reason, by?, on? }], "demoPages": ["/docs/**" or { pages, examples }] }</c>.</param>
        public Judge(string packFile, string? projectFile)
        {
            var pack = JsonNode.Parse(File.ReadAllText(packFile), documentOptions: Lenient)!;
            PackVersion = (string?)pack["version"] ?? "1";
            _pack = [.. pack["entries"]!.AsArray().Select(e => new Entry((string)e!["id"]!, (string)e["rule"]!, (string)e["verdict"]!, (string)e["why"]!, (string?)e["matches"], (string?)e["closest"], (string?)e["has"]))];
            Fixes = [.. (pack["fixes"]?.AsArray() ?? []).Select(f => new Fix((string)f!["id"]!, (string)f["rule"]!, (string?)f["component"], (string?)f["matches"], (string?)f["closest"], (string?)f["has"], (string?)f["set"], (string)f["fix"]!, (string?)f["docs"]))];
            if (projectFile is null) return;

            ProjectFile = ".izzy/audit.json";
            JsonNode? project;
            try { project = JsonNode.Parse(File.ReadAllText(projectFile), documentOptions: Lenient); }
            catch (JsonException e) { throw new GenerationException($"{ProjectFile} isn't valid JSON: {e.Message}"); }
            if (project is not JsonObject) throw new GenerationException($"{ProjectFile} must be an object with \"accepted\", \"demoPages\" or both.");
            foreach (var (item, at) in List(project, "accepted").Select((item, at) => (item, at)))
            {
                string? Text(string name) => item is JsonObject o && o[name] is JsonValue v && v.TryGetValue<string>(out var text) && text.Length > 0 ? text : null;
                var rule = Text("rule") ?? throw new GenerationException($"{ProjectFile}: accepted[{at}] needs \"rule\", the axe rule it accepts (color-contrast).");
                var reason = Text("reason") ?? throw new GenerationException($"{ProjectFile}: accepted[{at}] needs \"reason\": why this finding is accepted. The report prints it.");
                _accepted.Add(new Acceptance(rule, Text("selector"), Text("pages"), reason, Text("by"), Text("on")));
            }
            foreach (var (item, at) in List(project, "demoPages").Select((item, at) => (item, at)))
                _demo.Add(item switch
                {
                    JsonValue v when v.TryGetValue<string>(out var pages) => new Demo(pages, null),
                    JsonObject o when o["pages"] is JsonValue p && p.TryGetValue<string>(out var pages) && pages.Length > 0
                        && (o["examples"] is null || o["examples"] is JsonValue x && x.TryGetValue<string>(out _)) => new Demo(pages, (string?)o["examples"]),
                    _ => throw new GenerationException($"{ProjectFile}: demoPages[{at}] must be a path pattern (\"/docs/components/**\") or {{ \"pages\": …, \"examples\": a selector }}."),
                });
        }

        IEnumerable<JsonNode?> List(JsonNode project, string name) => project[name] switch
        {
            null => [],
            JsonArray array => array,
            _ => throw new GenerationException($"{ProjectFile}: \"{name}\" must be a list."),
        };

        /// <summary>The selector tests the page runs on each element found, as JSON: each has a key that comes back when it fits.</summary>
        public string Claims()
        {
            var claims = new JsonArray();
            foreach (var entry in _pack.Where(e => e.Matches is not null || e.Closest is not null || e.Has is not null))
                claims.Add(new JsonObject { ["key"] = $"p:{entry.Id}", ["rule"] = entry.Rule, ["matches"] = entry.Matches, ["closest"] = entry.Closest, ["has"] = entry.Has });
            foreach (var fix in Fixes.Where(f => f.Matches is not null || f.Closest is not null || f.Has is not null))
                claims.Add(new JsonObject { ["key"] = $"f:{fix.Id}", ["rule"] = fix.Rule, ["matches"] = fix.Matches, ["closest"] = fix.Closest, ["has"] = fix.Has });
            for (var i = 0; i < _accepted.Count; i++)
                if (_accepted[i].Selector is { } selector) claims.Add(new JsonObject { ["key"] = $"a:{i}", ["rule"] = _accepted[i].Rule, ["closest"] = selector });
            for (var i = 0; i < _demo.Count; i++)
                if (_demo[i].Examples is { } examples) claims.Add(new JsonObject { ["key"] = $"d:{i}", ["rule"] = "*", ["examples"] = examples });
            return claims.ToJsonString();
        }

        /// <summary>
        /// Stops the run for a selector of the app's that the browser refused (<paramref name="keys"/>, the
        /// claims' keys): it is the app's to correct. The pack's are tested before it ships.
        /// </summary>
        public void Refuse(IEnumerable<string> keys)
        {
            foreach (var key in keys)
            {
                if (key.StartsWith("a:", StringComparison.Ordinal)) throw new UsageException($"{ProjectFile}: accepted[{key[2..]}] has a selector the browser can't read: {_accepted[int.Parse(key[2..])].Selector}");
                if (key.StartsWith("d:", StringComparison.Ordinal)) throw new UsageException($"{ProjectFile}: demoPages[{key[2..]}] has a selector the browser can't read: {_demo[int.Parse(key[2..])].Examples}");
            }
        }

        /// <summary>Gives each finding of <paramref name="page"/> its verdict, from the tests the page answered.</summary>
        public void Decide(JsonObject page)
        {
            var path = PathOf((string)page["url"]!);
            if (page["refused"] is JsonArray refused)
            {
                page.Remove("refused");
                Refuse(refused.Select(k => (string)k!));
            }
            foreach (var rule in page["violations"]!.AsArray())
                foreach (var node in rule!["nodes"]!.AsArray().Select(n => n!.AsObject()))
                {
                    var claims = node["claims"] is JsonArray answered ? answered.Select(c => (string)c!).ToHashSet(StringComparer.Ordinal) : [];
                    node.Remove("claims");
                    // A sweep says what its own finding is (measured, or for a person to look at); the app's
                    // decisions and the pack come first, as for axe's.
                    var judged = node["judged"];
                    node.Remove("judged");
                    node["verdict"] = Verdict((string)rule["id"]!, path, claims, judged);
                }
        }

        JsonObject Verdict(string rule, string path, HashSet<string> claims, JsonNode? judged)
        {
            for (var i = 0; i < _accepted.Count; i++)
            {
                var a = _accepted[i];
                if (a.Rule != rule || (a.Pages is not null && !Glob(a.Pages, path)) || (a.Selector is not null && !claims.Contains($"a:{i}"))) continue;
                var verdict = new JsonObject { ["is"] = Accepted, ["why"] = a.Reason, ["confidence"] = "high" };
                if (a.By is not null) verdict["by"] = a.By;
                if (a.On is not null) verdict["on"] = a.On;
                return verdict;
            }
            // What a sweep measured to be no fault (a hit area larger than the box) is not the pack's to overrule.
            if ((string?)judged?["is"] == FalsePositive) return new JsonObject { ["is"] = FalsePositive, ["why"] = (string?)judged["why"], ["confidence"] = (string?)judged["confidence"] ?? "medium" };
            var demo = _demo.Select((d, i) => (d, i)).Any(x => Glob(x.d.Pages, path) && (x.d.Examples is null || claims.Contains($"d:{x.i}")));
            foreach (var kind in (string[])[FalsePositive, DemoPage, NeedsPerson, Real])
                foreach (var entry in _pack)
                {
                    if (entry.Verdict != kind || entry.Rule != rule || (kind == DemoPage && !demo)) continue;
                    var tested = entry.Matches is not null || entry.Closest is not null || entry.Has is not null;
                    if (tested && !claims.Contains($"p:{entry.Id}")) continue;
                    return new JsonObject { ["is"] = kind, ["why"] = entry.Why, ["confidence"] = "high", ["entry"] = entry.Id };
                }
            if ((string?)judged?["is"] is { } own) return new JsonObject { ["is"] = own, ["why"] = (string?)judged["why"], ["confidence"] = (string?)judged["confidence"] ?? "medium" };
            // An axe violation nothing above claims.
            return new JsonObject { ["is"] = Real, ["confidence"] = "medium" };
        }
    }

    static readonly JsonDocumentOptions Lenient = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    // A page's path, without a slash at its end: /docs/ and /docs are one page, as a site's pages are found.
    static string PathOf(string url) => !Uri.TryCreate(url, UriKind.Absolute, out var parsed) ? url : parsed.AbsolutePath.TrimEnd('/') is { Length: > 0 } path ? path : "/";

    /// <summary>Whether a path fits a pattern: <c>*</c> is anything but a slash, <c>**</c> anything. A pattern with neither fits that path alone.</summary>
    public static bool Glob(string pattern, string path)
    {
        var regex = "^" + Regex.Escape(pattern.TrimEnd('/')).Replace(@"/\*\*", "(/.*)?").Replace(@"\*\*", ".*").Replace(@"\*", "[^/]*") + "/?$";
        return Regex.IsMatch(path, regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    /// <summary>One element with a finding, once for every width and scheme it was found at.</summary>
    public sealed record Finding(string Id, JsonObject Rule, JsonObject Page, JsonObject Node, List<string> Where)
    {
        public string Verdict => (string?)Node["verdict"]?["is"] ?? Real;
        public string? Why => (string?)Node["verdict"]?["why"];
        public bool New => (bool?)Node["new"] == true;
    }

    /// <summary>Findings that share a rule, a verdict and its reason: what a report shows as one.</summary>
    public sealed record Group(string Verdict, string? Why, JsonObject Rule, List<Finding> Findings)
    {
        public string RuleId => (string)Rule["id"]!;
        public string Help => (string)Rule["help"]!;
        public string Impact => (string?)Rule["impact"] ?? "minor";
        public string Says => Why ?? Help;
        public int Pages => Findings.Select(f => PathOf((string)f.Page["url"]!)).Distinct().Count();
    }

    public static List<Finding> AllFindings(JsonObject run)
    {
        var findings = new Dictionary<string, Finding>(StringComparer.Ordinal);
        foreach (var page in run["pages"]!.AsArray().Select(p => p!.AsObject()))
            foreach (var rule in page["violations"]!.AsArray().Select(r => r!.AsObject()))
                foreach (var node in rule["nodes"]!.AsArray().Select(n => n!.AsObject()))
                {
                    var id = (string)node["id"]!;
                    if (!findings.TryGetValue(id, out var finding)) findings[id] = finding = new Finding(id, rule, page, node, []);
                    finding.Where.Add($"{(int)page["width"]!}px {(string)page["scheme"]!}");
                }
        return [.. findings.Values];
    }

    /// <summary>The findings of a judged run as a report shows them: real first, the worst and the most first.</summary>
    public static List<Group> Groups(JsonObject run) =>
        [.. AllFindings(run).GroupBy(f => (f.Verdict, f.Why, (string)f.Rule["id"]!)).Select(g => new Group(g.Key.Verdict, g.Key.Why, g.First().Rule, [.. g]))
            .OrderBy(g => Array.FindIndex(Verdicts, v => v.Verdict == g.Verdict))
            .ThenBy(g => Array.IndexOf(Impacts, g.Impact) is var at and >= 0 ? at : Impacts.Length)
            .ThenByDescending(g => g.Findings.Count)];

    /// <summary>How many elements a judged run found with <paramref name="verdict"/>; with <paramref name="newOnly"/>, those the baseline lacks.</summary>
    public static int Count(JsonObject run, string verdict, bool newOnly = false) => AllFindings(run).Count(f => f.Verdict == verdict && (!newOnly || f.New));

    /// <summary>
    /// Compares a run with a past one: a finding the past run lacks is marked new, and a finding of the past run
    /// that is gone from a page this run checked again is listed as fixed.
    /// </summary>
    public static void Compare(JsonObject run, JsonObject baseline, string file)
    {
        if (baseline["pages"] is not JsonArray) throw new GenerationException($"{file} isn't a results.json from izzy audit.");
        List<Finding> before;
        try { before = AllFindings(baseline); }
        catch (Exception e) when (e is InvalidOperationException or NullReferenceException or InvalidCastException) { throw new GenerationException($"{file} isn't a results.json from izzy audit."); }
        var known = before.Select(f => f.Id).ToHashSet(StringComparer.Ordinal);
        var now = AllFindings(run);
        var added = 0;
        foreach (var finding in now.Where(f => !known.Contains(f.Id)))
        {
            added++;
            // Every width and scheme's copy of the element.
            foreach (var node in run["pages"]!.AsArray().SelectMany(p => p!["violations"]!.AsArray()).SelectMany(r => r!["nodes"]!.AsArray()).Where(n => (string)n!["id"]! == finding.Id))
                node!["new"] = true;
        }
        var present = now.Select(f => f.Id).ToHashSet(StringComparer.Ordinal);
        // Fixed means looked for again and not found: at every width and scheme the past run found it at, on a
        // page that loaded and didn't answer with an error.
        var checkedAgain = Unchecked(run) is var failed ? run["pages"]!.AsArray().Select(p => p!.AsObject()).Where(p => !failed.Contains(p))
            .Select(p => $"{PathOf((string)p["url"]!)}|{(int)p["width"]!}px {(string)p["scheme"]!}").ToHashSet(StringComparer.OrdinalIgnoreCase) : [];
        var gone = new JsonArray();
        foreach (var finding in before.Where(f => !present.Contains(f.Id) && f.Verdict is Real or NeedsPerson && f.Where.All(at => checkedAgain.Contains($"{PathOf((string)f.Page["url"]!)}|{at}"))))
            gone.Add(new JsonObject
            {
                ["id"] = finding.Id, ["rule"] = (string)finding.Rule["id"]!, ["help"] = finding.Why ?? (string)finding.Rule["help"]!,
                ["page"] = PathOf((string)finding.Page["url"]!), ["target"] = (string)finding.Node["target"]!,
            });
        run["baseline"] = new JsonObject { ["file"] = file, ["at"] = (string?)baseline["at"], ["new"] = added, ["fixed"] = gone };
    }

    /// <summary>The report of a run with verdicts: a summary by verdict, then each group of findings under its verdict.</summary>
    static string JudgedMarkdown(JsonObject run)
    {
        var pages = run["pages"]!.AsArray().Select(p => p!.AsObject()).ToList();
        var urls = pages.Select(p => (string)p["url"]!).Distinct().ToList();
        var groups = Groups(run);
        var text = new StringBuilder();
        text.Append($"# Accessibility audit: {(urls.Count == 1 ? urls[0] : $"{urls.Count} pages")}\n\n");
        text.Append($"Made by izzy audit {(string)run["version"]!} on {((string)run["at"]!)[..10]}, with axe-core {(string)run["engine"]!["version"]!}{Swept(run)} in {(string)run["browser"]!}. ");
        text.Append($"Each finding has its rule, the WCAG criterion, a verdict with its reason, the element and where it was found{(run["trace"] is null ? "" : (bool?)run["trace"]!["source"] == true ? ", the component that drew it and its place in the source" : ", and the component that drew it")}. The same results as data are in `results.json`.\n\n");

        text.Append("## Summary\n\n");
        if (groups.Count == 0) text.Append("No findings.\n");
        else
        {
            text.Append("| Verdict | Elements | Meaning |\n|---|---|---|\n");
            foreach (var (verdict, meaning) in Verdicts)
                if (groups.Where(g => g.Verdict == verdict).Sum(g => g.Findings.Count) is > 0 and var count) text.Append($"| {verdict} | {count} | {meaning} |\n");
        }
        if (run["baseline"] is { } baseline)
            text.Append($"\nAgainst the run of {((string?)baseline["at"] ?? "an unknown date")[..Math.Min(10, ((string?)baseline["at"] ?? "an unknown date").Length)]} (`{(string)baseline["file"]!}`): {(int)baseline["new"]!} new, {baseline["fixed"]!.AsArray().Count} fixed.\n");

        text.Append('\n').Append(Checked(pages));

        var section = "";
        foreach (var group in groups)
        {
            if (group.Verdict != section)
            {
                section = group.Verdict;
                text.Append($"\n## {char.ToUpperInvariant(section[0])}{section[1..]}\n");
            }
            text.Append($"\n### {group.Help} (`{group.RuleId}`)\n\n");
            text.Append($"- **Criterion:** {Criterion(group.Rule["tags"]!.AsArray().Select(t => (string)t!).ToList())}; {Severity(group.RuleId)} {group.Impact}. [About this rule]({(string)group.Rule["helpUrl"]!})\n");
            var first = group.Findings[0].Node["verdict"];
            var who = group.Verdict == Accepted && ((string?)first?["by"], (string?)first?["on"]) is var (by, on) && (by ?? on) is not null ? $" ({string.Join(", ", ((string?[])[by, on]).Where(p => p is not null))})" : "";
            text.Append($"- **Verdict:** {group.Verdict}{(group.Why is null ? ". Nothing known explains it, so it counts as a failure" : $". {group.Why.TrimEnd('.')}")}{who}. Confidence: {(string?)first?["confidence"] ?? "medium"}.\n");
            var fresh = group.Findings.Count(f => f.New);
            text.Append($"- **Found:** {Elements(group.Findings.Count)} on {group.Pages} page{(group.Pages == 1 ? "" : "s")}{(fresh > 0 ? $", {fresh} new" : "")}\n");
            foreach (var finding in group.Findings.Take(10)) text.Append(ElementBlock(group.RuleId, finding.Page, finding.Node, finding.Where.Distinct()));
            if (group.Findings.Count > 10) text.Append($"\n  …and {Elements(group.Findings.Count - 10, "more element")}, in `results.json`.\n");
        }

        text.Append(Closing(run));
        return text.ToString();
    }

    // Where a group's findings are written, for a task: the one line they share, or the first of them.
    static string Written(Group group)
    {
        var lines = group.Findings.Select(f => f.Node["trace"]?["source"]).Where(s => s?["file"] is not null).Select(s => $"{(string)s!["file"]!}:{(int)s["line"]!}").Distinct().ToList();
        var fix = group.Findings.Select(f => (string?)f.Node["trace"]?["fix"]?["text"]).Where(f => f is not null).Distinct().ToList() is [{ } one] ? $". {one.TrimEnd('.')}" : "";
        return lines.Count == 0 ? fix : $"; in {Code(lines[0])}{(lines.Count > 1 ? $" and {lines.Count - 1} more place{(lines.Count == 2 ? "" : "s")}" : "")}{fix}";
    }

    static string Elements(int count, string noun = "element") => $"{count} {noun}{(count == 1 ? "" : "s")}";

    /// <summary>What the terminal shows after a run with verdicts: the count for each verdict and the five worst.</summary>
    static string JudgedSummary(JsonObject run)
    {
        var groups = Groups(run);
        var text = new StringBuilder();
        if (groups.Count == 0) text.Append("No findings.\n");
        else
        {
            var counts = Verdicts.Select(v => (v.Verdict, Count: groups.Where(g => g.Verdict == v.Verdict).Sum(g => g.Findings.Count))).Where(c => c.Count > 0).ToList();
            var total = counts.Sum(c => c.Count);
            text.Append($"{total} finding{(total == 1 ? "" : "s")}: {string.Join(", ", counts.Select(c => $"{c.Count} {c.Verdict}"))}.\n");
            var worst = groups.Where(g => g.Verdict is Real or NeedsPerson).Take(5).ToList();
            if (worst.Count > 0)
            {
                var (verdict, impact, id) = (worst.Max(g => g.Verdict.Length), worst.Max(g => g.Impact.Length), worst.Max(g => g.RuleId.Length));
                foreach (var group in worst) text.Append($"  {group.Verdict.PadRight(verdict)}  {group.Impact.PadRight(impact)}  {group.RuleId.PadRight(id)}  {group.Findings.Count,4}  {group.Says}\n");
            }
        }
        if ((bool?)run["trace"]?["source"] == true && AllFindings(run).Where(f => f.Verdict is Real or NeedsPerson).ToList() is { Count: > 0 } open)
            text.Append($"{open.Count(f => f.Node["trace"]?["source"] is not null)} of {open.Count} to fix or look at were found in the source.\n");
        if (run["baseline"] is { } baseline)
            text.Append($"Against the baseline: {(int)baseline["new"]!} new, {baseline["fixed"]!.AsArray().Count} fixed.\n");
        return text.ToString();
    }

    /// <summary>
    /// tasks.md: a checkbox for each group of real findings, and of those a person must look at, for an issue or
    /// an agent's list. What a baseline had and this run doesn't is checked off.
    /// </summary>
    public static string Tasks(JsonObject run)
    {
        var urls = run["pages"]!.AsArray().Select(p => (string)p!["url"]!).Distinct().ToList();
        var groups = Groups(run);
        var text = new StringBuilder();
        text.Append($"# Accessibility audit: {(urls.Count == 1 ? urls[0] : $"{urls.Count} pages")}: tasks\n\n");
        text.Append("From `report.md`, where each has its element, pages and reason.\n");
        foreach (var (verdict, title) in ((string, string)[])[(Real, "To fix"), (NeedsPerson, "To look at")])
        {
            var open = groups.Where(g => g.Verdict == verdict).ToList();
            if (open.Count == 0) continue;
            text.Append($"\n## {title}\n\n");
            foreach (var group in open)
            {
                var example = group.Findings[0];
                var criterion = Criterion(group.Rule["tags"]!.AsArray().Select(t => (string)t!).ToList());
                text.Append($"- [ ] **{Cell(group.Says)}** (`{group.RuleId}`, {criterion}; {Elements(group.Findings.Count)} on {group.Pages} page{(group.Pages == 1 ? "" : "s")}, such as {Code(PathOf((string)example.Page["url"]!))} {Code((string)example.Node["target"]!)}{Written(group)})\n");
            }
        }
        if (run["baseline"]?["fixed"] is JsonArray { Count: > 0 } gone)
        {
            text.Append($"\n## Fixed since {((string?)run["baseline"]!["at"] is { Length: >= 10 } at ? at[..10] : "the baseline")}\n\n");
            foreach (var rule in gone.GroupBy(f => ((string)f!["rule"]!, (string)f["help"]!)))
                text.Append($"- [x] **{Cell(rule.Key.Item2)}** (`{rule.Key.Item1}`; {Elements(rule.Count())}, such as {Code((string)rule.First()!["page"]!)} {Code((string)rule.First()!["target"]!)})\n");
        }
        if (!groups.Any(g => g.Verdict is Real or NeedsPerson) && run["baseline"]?["fixed"] is not JsonArray { Count: > 0 }) text.Append("\nNothing to do.\n");
        return text.ToString();
    }
}
