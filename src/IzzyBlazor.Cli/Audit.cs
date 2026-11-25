using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using IzzyBlazor.Generation;

namespace IzzyBlazor.Cli;

/// <summary>
/// izzy audit: pages opened in a browser on this machine and checked with axe-core, at each width and color
/// scheme asked for. The result is data (<see cref="Run"/>), which the reports are written from.
/// </summary>
static partial class Audit
{
    /// <summary>The axe-core inside this izzy.</summary>
    public static string Engine => field ??= Resource("axe.min.js");

    // What every window gets as it is made: axe as a function that starts it, not axe started. axe keeps the
    // document it starts in, and the browser can make an iframe's window on its first, empty document and keep it
    // for the page that loads there: axe started then checks the empty one. The page that is audited starts axe
    // in itself and in the frames it can reach (Collect); a frame from another site starts its own once loaded.
    static string Loader => field ??= "window.__izzyAxe = function () { if (window.__izzyAxeOn) return; window.__izzyAxeOn = true; (function () {\n" + Engine + """

        }).call(window);
          // axe answers frames of its own origin only, unless told: a frame from another site is part of the page.
          window.axe.configure({ allowedOrigins: ["<unsafe_all_origins>"] });
        };
        if (window !== window.top) window.addEventListener("load", function () {
          try { if (window.top.document) return; } catch { }
          window.__izzyAxe();
        });
        """;

    public static string EngineVersion => field ??= EngineHeader().Match(Engine) is { Success: true } m ? m.Groups[1].Value : "unknown";

    static string Resource(string name)
    {
        using var stream = typeof(Audit).Assembly.GetManifestResourceStream(name) ?? throw new GenerationException($"This izzy has no {name}.");
        return new StreamReader(stream).ReadToEnd();
    }

    /// <summary>
    /// Audits each address at each width and scheme; <paramref name="progress"/> hears of each page as it starts.
    /// With a <paramref name="judge"/>, each finding gets its verdict. <paramref name="cookies"/> go with every
    /// request to the addresses' sites. A long run is shared between <paramref name="browsers"/> browsers.
    /// With a <paramref name="tracer"/>, each finding is traced to its component and, with the app's source, its line.
    /// With <paramref name="pictures"/>, a picture is taken of each element a report shows.
    /// </summary>
    public static async Task<JsonObject> Run(string browserPath, IReadOnlyList<string> urls, IReadOnlyList<int> widths, IReadOnlyList<string> schemes, string izzyVersion, Action<string>? progress = null, Judge? judge = null,
        IReadOnlyList<KeyValuePair<string, string>>? cookies = null, int browsers = 1, Tracer? tracer = null, Pictures? pictures = null)
    {
        var loads = (from width in widths from scheme in schemes from url in urls select (Width: width, Scheme: scheme, Url: url)).ToList();
        var pages = new JsonObject[loads.Count];
        var sites = urls.Select(u => Uri.TryCreate(u, UriKind.Absolute, out var parsed) ? parsed.GetLeftPart(UriPartial.Authority) : null).Where(s => s is not null).Distinct().ToList();
        var next = -1;
        string? product = null;
        using var stop = new CancellationTokenSource();

        // One browser's share: it takes the next load until none is left, or another browser's failure ends the run.
        async Task Work()
        {
            try
            {
                await using var browser = await Chromium.StartAsync(browserPath);
                product ??= (string?)(await browser.Send("Browser.getVersion"))["product"] ?? Path.GetFileName(browserPath);
                await browser.Send("Page.enable");
                await browser.Send("Runtime.enable");
                await browser.Send("Page.setLifecycleEventsEnabled", new JsonObject { ["enabled"] = true });
                // axe for every document, frames too, so it checks inside iframes.
                await browser.Send("Page.addScriptToEvaluateOnNewDocument", new JsonObject { ["source"] = Loader });
                // The app's selectors are read once, before any page: a wrong one stops the run, where it would
                // otherwise only show on a page that has a finding of its rule.
                if (judge is not null)
                    judge.Refuse(JsonNode.Parse((string)(await browser.Evaluate(Selectors.Replace("__CLAIMS__", judge.Claims())))!)!.AsArray().Select(k => (string)k!));
                foreach (var cookie in cookies ?? [])
                    foreach (var site in sites)
                        if ((bool?)(await browser.Send("Network.setCookie", new JsonObject { ["name"] = cookie.Key, ["value"] = cookie.Value, ["url"] = site, ["secure"] = site?.StartsWith("https:", StringComparison.OrdinalIgnoreCase) == true }))["success"] == false)
                            throw new UsageException($"The browser refused the cookie {cookie.Key} for {site}.");

                (int, string)? set = null;
                while (!stop.IsCancellationRequested && Interlocked.Increment(ref next) is var at && at < loads.Count)
                {
                    var load = loads[at];
                    if (set != (load.Width, load.Scheme))
                    {
                        await browser.Send("Emulation.setDeviceMetricsOverride", new JsonObject { ["width"] = load.Width, ["height"] = load.Width < 600 ? 800 : 900, ["deviceScaleFactor"] = 1, ["mobile"] = load.Width < 600 });
                        await browser.Send("Emulation.setEmulatedMedia", new JsonObject { ["features"] = new JsonArray(new JsonObject { ["name"] = "prefers-color-scheme", ["value"] = load.Scheme }) });
                        set = (load.Width, load.Scheme);
                    }
                    progress?.Invoke($"{(loads.Count > 8 ? $"[{at + 1}/{loads.Count}] " : "")}{load.Url} at {load.Width}, {load.Scheme}");
                    pages[at] = await Page(browser, load.Url, load.Width, load.Scheme, judge, tracer, pictures);
                    // A browser that died fails every load it is given at once, and so would take the rest of
                    // the run from the browsers that work: the run ends, as for one browser.
                    if (browser.Closed) throw new GenerationException("The browser closed during the audit.");
                }
            }
            catch
            {
                stop.Cancel();
                throw;
            }
        }
        await Task.WhenAll(Enumerable.Range(0, Math.Clamp(browsers, 1, Math.Max(1, loads.Count))).Select(_ => Work()));

        var run = new JsonObject
        {
            ["tool"] = "izzy audit",
            ["version"] = izzyVersion,
            ["engine"] = new JsonObject { ["name"] = "axe-core", ["version"] = EngineVersion },
            ["browser"] = product,
            ["at"] = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture),
            ["widths"] = new JsonArray([.. widths.Select(w => (JsonNode)w)]),
            ["schemes"] = new JsonArray([.. schemes.Select(s => (JsonNode)s)]),
            ["pages"] = new JsonArray([.. pages]),
        };
        if (judge is not null) run["verdicts"] = new JsonObject { ["pack"] = judge.PackVersion, ["project"] = judge.ProjectFile };
        if (tracer is not null) run["trace"] = new JsonObject { ["source"] = tracer.HasSource };
        return run;
    }

    static async Task<JsonObject> Page(Chromium browser, string url, int width, string scheme, Judge? judge, Tracer? tracer, Pictures? pictures)
    {
        var page = new JsonObject { ["url"] = url, ["width"] = width, ["scheme"] = scheme };
        try
        {
            browser.Drain();
            var navigation = await browser.Send("Page.navigate", new JsonObject { ["url"] = url });
            if ((string?)navigation["errorText"] is { Length: > 0 } failed) throw new GenerationException(failed);
            // Loaded, and the network quiet for half a second: scripts have had their turn.
            var loader = (string?)navigation["loaderId"];
            if (loader is not null)
                await browser.Event(e => (string?)e["method"] == "Page.lifecycleEvent" && (string?)e["params"]?["name"] == "networkIdle" && (string?)e["params"]?["loaderId"] == loader, TimeSpan.FromSeconds(20));
            await Task.Delay(400);

            var answer = await browser.Evaluate(Collect.Replace("__CLAIMS__", judge?.Claims() ?? "[]").Replace("__MARKS__", tracer?.Marks() ?? "{ \"keys\": [], \"all\": [] }").Replace("__KEEP__", pictures is null ? "false" : "true"));
            try
            {
                var found = JsonNode.Parse((string)answer!)!.AsObject();
                foreach (var (name, value) in found.ToList())
                {
                    found.Remove(name);
                    page[name] = value;
                }
                // Somewhere else than asked (a sign-in page in front of it): the report says which page it is.
                if ((string?)page["landed"] is { } landed && Uri.TryCreate(landed, UriKind.Absolute, out var there) && Uri.TryCreate(url, UriKind.Absolute, out var asked)
                    && string.Equals(there.AbsolutePath.TrimEnd('/'), asked.AbsolutePath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                    page.Remove("landed");
                // A finding's id stays the same from run to run: its rule, the page's path and the element. Two of
                // one component have one selector once their made ids are out of it, so the second is the second.
                var path = Uri.TryCreate(url, UriKind.Absolute, out var parsed) ? PathOf(url) + parsed.Query : url;
                foreach (var rule in page["violations"]!.AsArray())
                {
                    var met = new Dictionary<string, int>(StringComparer.Ordinal);
                    foreach (var node in rule!["nodes"]!.AsArray())
                    {
                        var element = Stable((string)node!["target"]!);
                        var nth = met[element] = met.GetValueOrDefault(element) + 1;
                        // Only a tracer writes a trace: a page's own is not printed.
                        node.AsObject().Remove("trace");
                        node["id"] = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{(string)rule["id"]!}|{path}|{element}{(nth > 1 ? $"|{nth}" : "")}")))[..12];
                    }
                }
            }
            catch (Exception e) when (e is InvalidOperationException or NullReferenceException or System.Text.Json.JsonException or InvalidCastException)
            {
                // The page's own script stands where axe's does, or changed what it answers.
                throw new GenerationException("The page answered the audit with something izzy can't read.");
            }
            if (tracer is not null) tracer.Trace(page);
            else foreach (var node in page["violations"]!.AsArray().SelectMany(rule => rule!["nodes"]!.AsArray())) node!.AsObject().Remove("at");
            judge?.Decide(page);
            if (pictures is not null) await pictures.Take(browser, page);
        }
        catch (GenerationException e)
        {
            page["error"] = e.Message;
            page["violations"] = new JsonArray();
            page["incomplete"] = new JsonArray();
        }
        return page;
    }

    // What a page answers with, as a string: axe's violations with each element, and what it couldn't decide as counts
    // by reason. A judge's selector tests run here, on the element itself: each element answers with the keys of
    // those that fit. axe hands over no element for one inside an iframe: it is found again from its target, a
    // selector for each frame on the way, and an ancestor is looked for through the frames around it, so an
    // example drawn in an iframe is inside its example. A frame from another site can't be read: its elements fit none.
    // The keys of the claims whose selectors the browser can't read.
    const string Selectors = """
        JSON.stringify(__CLAIMS__.filter(c => [c.matches, c.closest, c.has, c.examples].some(s => {
          if (!s) return false;
          try { document.querySelector(s); return false; } catch { return true; }
        })).map(c => c.key))
        """;

    const string Collect = """
        (async () => {
          const claims = __CLAIMS__;
          // What marks a component's outermost element: its tag ("<ib-dialog>") or a class.
          // An element with one of the keys is a component's; it answers with all it has of the marks.
          const marked = __MARKS__;
          const marks = new Set(marked.all);
          const keys = new Set(marked.keys);
          // For the pictures: the elements found stay in the page, in the order of the answer.
          const keep = __KEEP__;
          // The frames under a document or a shadow root, those inside shadow roots too.
          const framesIn = (root) => {
            const found = [...root.querySelectorAll("iframe, frame")];
            for (const e of root.querySelectorAll("*")) if (e.shadowRoot) found.push(...framesIn(e.shadowRoot));
            return found;
          };
          const start = (w) => {
            try {
              w.__izzyAxe();
              for (const frame of framesIn(w.document)) start(frame.contentWindow);
            } catch { }
          };
          start(window);
          const r = await axe.run(document, { resultTypes: ["violations", "incomplete"], elementRef: claims.length > 0 || marks.size > 0 || keep });
          const refused = new Set();
          // The element of a node: axe's, or the one its target leads to. Each part of a target but the last is
          // an iframe (a list of parts goes through shadow roots).
          const find = (root, selector) => Array.isArray(selector)
            ? selector.reduce((scope, part, at) => scope && (at ? scope.shadowRoot : scope) && (at ? scope.shadowRoot : scope).querySelector(part), root)
            : root.querySelector(selector);
          const elementOf = (n) => {
            if (n.element) return n.element;
            try {
              let root = document;
              for (const [at, part] of n.target.entries()) {
                const found = root && find(root, part);
                if (!found || at === n.target.length - 1) return found || null;
                root = found.contentDocument;
              }
            } catch { }
            return null;
          };
          // The element or an ancestor: in its own tree, around the shadow root it is in, or around its frame.
          const closest = (element, selector) => {
            for (let e = element; e;) {
              const found = e.closest(selector);
              if (found) return found;
              const root = e.getRootNode();
              e = root.host || (root.defaultView && root.defaultView.frameElement);
            }
            return null;
          };
          const fits = (claim, element, related) => {
            try {
              if (claim.matches && !element.matches(claim.matches)) return false;
              if (claim.closest && !closest(element, claim.closest)) return false;
              if (claim.has && !element.querySelector(claim.has)) return false;
              // Inside an example, or sharing its fault with something that is (two landmarks with one name).
              if (claim.examples && ![element].concat(related).some(e => closest(e, claim.examples))) return false;
              return true;
            } catch (e) {
              // A selector the browser can't read; anything else (a frame gone since) fits nothing.
              if (e instanceof DOMException && e.name === "SyntaxError") refused.add(claim.key);
              return false;
            }
          };
          if (keep) window.__izzyFound = r.violations.map(v => v.nodes.map(elementOf));
          const answer = (rule, n) => {
            const element = claims.length ? elementOf(n) : null;
            if (!element) return undefined;
            const related = n.any.concat(n.all, n.none).flatMap(c => c.relatedNodes || []).map(elementOf).filter(Boolean);
            return claims.filter(c => (c.rule === "*" || c.rule === rule) && fits(c, element, related)).map(c => c.key);
          };
          // Where the element is, for the trace: itself, and each element around it that carries a component's
          // mark, the nearest first, with what it says (its attributes and the start of its text).
          const up = (e) => {
            if (e.parentElement) return e.parentElement;
            try { const root = e.getRootNode(); return root.host || (root.defaultView && root.defaultView.frameElement) || null; } catch { return null; }
          };
          const marksOf = (e) => {
            const found = marks.has("<" + e.localName + ">") ? ["<" + e.localName + ">"] : [];
            for (const c of e.classList) if (marks.has(c)) found.push(c);
            return found.some(m => keys.has(m)) ? found : [];
          };
          const said = (e) => ({
            attributes: [...e.attributes].slice(0, 40).map(a => a.value).filter(v => v.length >= 2 && v.length <= 200),
            text: (e.textContent || "").slice(0, 4000).replace(/\s+/g, " ").trim().slice(0, 600),
          });
          const place = (n) => {
            const element = marks.size ? elementOf(n) : null;
            if (!element) return undefined;
            try {
              const chain = [];
              for (let e = element; e && chain.length < 6; e = up(e)) {
                const found = marksOf(e);
                if (found.length) chain.push({ marks: found, said: said(e), self: e === element });
              }
              return { tag: String(element.localName), id: element.getAttribute("id") || undefined, classes: (element.getAttribute("class") || "").split(/\s+/).filter(Boolean).slice(0, 8), said: said(element), chain };
            } catch { return undefined; }
          };
          // Text cut in the middle of a character (an emoji at the 300th place) can't be written as JSON.
          const whole = (text) => text.toWellFormed ? text.toWellFormed() : text;
          const where = (target) => target.map(t => Array.isArray(t) ? t.join(" ") : t).join(" >> ");
          const navigation = performance.getEntriesByType("navigation")[0];
          return JSON.stringify({
            title: document.title,
            status: navigation && navigation.responseStatus || 0,
            landed: location.href,
            frames: document.querySelectorAll("iframe").length,
            violations: r.violations.map(v => ({ v, nodes: v.nodes.map(n => ({ n, claims: answer(v.id, n) })) })).map(({ v, nodes }) => ({
              id: v.id, impact: v.impact, help: v.help, helpUrl: v.helpUrl, tags: v.tags.filter(t => /^(wcag|best-practice)/.test(t)),
              nodes: nodes.map(({ n, claims }) => {
                const data = n.any.concat(n.all, n.none).map(c => c.data).find(d => d && d.contrastRatio);
                return {
                  target: where(n.target), html: n.html.length > 300 ? n.html.slice(0, 300) + "…" : n.html, summary: n.failureSummary,
                  contrast: data ? { foreground: data.fgColor, background: data.bgColor, ratio: data.contrastRatio, needs: data.expectedContrastRatio, size: data.fontSize } : undefined,
                  claims, at: place(n),
                };
              }),
            })),
            incomplete: r.incomplete.map(v => {
              const reasons = {};
              for (const n of v.nodes) { const why = whole((n.any.concat(n.all, n.none)[0] || {}).message || v.help); reasons[why] = (reasons[why] || 0) + 1; }
              return { id: v.id, help: v.help, count: v.nodes.length, reasons };
            }),
            refused: refused.size ? [...refused] : undefined,
          }, (key, value) => typeof value === "string" ? whole(value) : value);
        })()
        """;

    // An id IzzyBlazor makes for an element is new on every load (ib-banner-d7y5jo): without it, the element
    // has one selector at every width and scheme, and from run to run.
    static string Stable(string selector) => MadeId().Replace(selector, "$1");

    /// <summary>
    /// The findings of a run, one for each rule, worst first. An element found at several widths and schemes
    /// counts once.
    /// </summary>
    public sealed record Rule(string Id, string Impact, string Help, string HelpUrl, string Criterion, int Elements, List<JsonObject> Pages);

    static readonly string[] Impacts = ["critical", "serious", "moderate", "minor"];

    public static List<Rule> Rules(JsonObject run)
    {
        var rules = new Dictionary<string, Rule>(StringComparer.Ordinal);
        var elements = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var page in run["pages"]!.AsArray().Select(p => p!.AsObject()))
            foreach (var violation in page["violations"]!.AsArray())
            {
                var id = (string)violation!["id"]!;
                if (!rules.TryGetValue(id, out var rule))
                    rules[id] = rule = new Rule(id, (string?)violation["impact"] ?? "minor", (string)violation["help"]!, (string)violation["helpUrl"]!, Criterion(violation["tags"]!.AsArray().Select(t => (string)t!).ToList()), 0, []);
                rule.Pages.Add(page);
                if (!elements.TryGetValue(id, out var seen)) elements[id] = seen = [];
                foreach (var node in violation["nodes"]!.AsArray()) seen.Add(Element(page, node!));
            }
        foreach (var (id, seen) in elements) rules[id] = rules[id] with { Elements = seen.Count };
        return rules.Values.OrderBy(r => Array.IndexOf(Impacts, r.Impact) is var at and >= 0 ? at : Impacts.Length).ThenByDescending(r => r.Elements).ToList();
    }

    // One element of a page, whatever the width and scheme it was found at.
    static string Element(JsonObject page, JsonNode node) => $"{(string)page["url"]!}|{(string?)node["id"] ?? Stable((string)node["target"]!)}";

    // "wcag143" and "wcag2aa" as "WCAG 1.4.3 (level AA)".
    static string Criterion(List<string> tags)
    {
        var criteria = tags.Where(t => SuccessCriterion().IsMatch(t)).Select(t => t[4..]).Select(d => $"{d[0]}.{d[1]}.{d[2..]}").ToList();
        if (criteria.Count == 0) return "Best practice, not a WCAG failure";
        var level = tags.Any(t => t is "wcag2a" or "wcag21a") ? "A" : tags.Any(t => t.EndsWith("aaa", StringComparison.Ordinal)) ? "AAA" : tags.Any(t => t.EndsWith("aa", StringComparison.Ordinal)) ? "AA" : null;
        return $"WCAG {string.Join(", ", criteria)}{(level is null ? "" : $" (level {level})")}";
    }

    /// <summary>What a run couldn't decide, as each reason with its count of elements, most first.</summary>
    public static List<(string Reason, int Elements)> Undecided(JsonObject run)
    {
        // A page's count is its largest at any width and scheme: the same elements, met again.
        var reasons = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var address in run["pages"]!.AsArray().GroupBy(p => (string)p!["url"]!))
        {
            var most = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var page in address)
                foreach (var rule in page!["incomplete"]!.AsArray())
                    foreach (var (why, count) in rule!["reasons"]!.AsObject())
                    {
                        var key = $"{(string)rule["id"]!}: {why}";
                        most[key] = Math.Max(most.GetValueOrDefault(key), (int)count!);
                    }
            foreach (var (key, count) in most) reasons[key] = reasons.GetValueOrDefault(key) + count;
        }
        return reasons.OrderByDescending(r => r.Value).Select(r => (r.Key, r.Value)).ToList();
    }

    /// <summary>The report a person or an agent reads: what was checked, each finding with its element, and what wasn't checked.</summary>
    public static string Markdown(JsonObject run)
    {
        if (run["verdicts"] is not null) return JudgedMarkdown(run);
        var pages = run["pages"]!.AsArray().Select(p => p!.AsObject()).ToList();
        var urls = pages.Select(p => (string)p["url"]!).Distinct().ToList();
        var rules = Rules(run);
        var text = new StringBuilder();
        text.Append($"# Accessibility audit: {(urls.Count == 1 ? urls[0] : $"{urls.Count} pages")}\n\n");
        text.Append($"Made by izzy audit {(string)run["version"]!} on {((string)run["at"]!)[..10]}, with axe-core {(string)run["engine"]!["version"]!} in {(string)run["browser"]!}. ");
        text.Append("Each finding has its rule, the WCAG criterion, the element and where it was found. The same results as data are in `results.json`.\n\n");

        text.Append("## Summary\n\n");
        if (rules.Count == 0) text.Append("No findings.\n\n");
        else
        {
            text.Append("| Severity | Rule | Elements | What it means |\n|---|---|---|---|\n");
            foreach (var rule in rules) text.Append($"| {rule.Impact} | `{rule.Id}` | {rule.Elements} | {Cell(rule.Help)} |\n");
            text.Append('\n');
        }

        text.Append(Checked(pages));

        if (rules.Count > 0) text.Append("\n## Findings\n");
        foreach (var rule in rules)
        {
            text.Append($"\n### {rule.Help} (`{rule.Id}`)\n\n");
            text.Append($"- **Criterion:** {rule.Criterion}; axe severity {rule.Impact}. [About this rule]({rule.HelpUrl})\n");
            text.Append($"- **Found:** {rule.Elements} element{(rule.Elements == 1 ? "" : "s")}\n");
            // One element found at several widths and schemes is shown once, with where.
            var elements = rule.Pages.SelectMany(page => page["violations"]!.AsArray().Where(v => (string)v!["id"]! == rule.Id).SelectMany(v => v!["nodes"]!.AsArray()).Select(node => (Page: page, Node: node!)))
                .GroupBy(e => Element(e.Page, e.Node)).ToList();
            foreach (var element in elements.Take(10))
                text.Append(ElementBlock(element.First().Page, element.First().Node.AsObject(), element.Select(e => $"{(int)e.Page["width"]!}px {(string)e.Page["scheme"]!}").Distinct()));
            if (elements.Count > 10) text.Append($"\n  …and {elements.Count - 10} more elements, in `results.json`.\n");
        }

        text.Append(Closing(run));
        return text.ToString();
    }

    // What was checked: each load of a few pages, or for a site each page with its loads together.
    static string Checked(List<JsonObject> pages)
    {
        var text = new StringBuilder();
        var addresses = pages.GroupBy(p => (string)p["url"]!).ToList();
        if (pages.Count <= 16)
        {
            text.Append("## What was checked\n\n| Page | Width | Scheme | Elements found |\n|---|---|---|---|\n");
            foreach (var page in pages) text.Append(CheckedRow(page));
            return text.ToString();
        }
        var each = pages.Select(p => $"{(int)p["width"]!}px {(string)p["scheme"]!}").Distinct().ToList();
        text.Append($"## What was checked\n\n{addresses.Count} pages, each at {string.Join(", ", each)}.\n\n| Page | Elements found |\n|---|---|\n");
        foreach (var address in addresses)
        {
            var count = address.SelectMany(p => p["violations"]!.AsArray()).SelectMany(v => v!["nodes"]!.AsArray()).Select(n => (string?)n!["id"]).Distinct().Count();
            var failed = address.Where(p => p["error"] is not null).ToList();
            var note = failed.Count == address.Count() ? $"Not checked: {Cell((string)failed[0]["error"]!)}" : failed.Count > 0 ? $"{count} ({failed.Count} of {address.Count()} loads failed)" : $"{count}";
            if (address.FirstOrDefault(p => (int?)p["status"] is >= 400) is { } answered) note += $" (the page answered {(int)answered["status"]!})";
            if (address.FirstOrDefault(p => p["landed"] is not null) is { } led) note += $" (it led to {Cell((string)led["landed"]!)}, which is what was checked)";
            text.Append($"| {Cell(address.Key)} | {note} |\n");
        }
        return text.ToString();
    }

    static string CheckedRow(JsonObject page)
    {
        var count = page["violations"]!.AsArray().Sum(v => v!["nodes"]!.AsArray().Count);
        var note = (string?)page["error"] is { } error ? $"Not checked: {Cell(error)}" : (int?)page["status"] is >= 400 and var status ? $"{count} (the page answered {status})" : $"{count}";
        if ((string?)page["landed"] is { } landed) note += $" (it led to {Cell(landed)}, which is what was checked)";
        return $"| {Cell((string)page["url"]!)} | {(int)page["width"]!} | {(string)page["scheme"]!} | {note} |\n";
    }

    // One element: its HTML, selector, what axe says and a measured contrast.
    static string ElementBlock(JsonObject page, JsonObject node, IEnumerable<string> where)
    {
        var text = new StringBuilder();
        text.Append($"\n  On {Code((string)page["url"]!)} at {string.Join("; ", where)}:\n\n");
        text.Append($"  ```html\n  {Spaces().Replace((string)node["html"]!, " ")}\n  ```\n");
        text.Append($"  - Selector: {Code((string)node["target"]!)}\n");
        if ((string?)node["summary"] is { } summary) text.Append($"  - axe: {Plain(FixLead().Replace(Spaces().Replace(summary, " "), ""))}\n");
        if (node["contrast"] is { } contrast)
            text.Append($"  - Measured: {(string?)contrast["foreground"]} on {(string?)contrast["background"]}, {contrast["ratio"]}:1 at {(string?)contrast["size"]}; needs {(string?)contrast["needs"]}\n");
        text.Append(TraceLines(node));
        return text.ToString();
    }

    // What every report ends with: what axe couldn't decide, and what no run checks.
    static string Closing(JsonObject run)
    {
        var text = new StringBuilder();
        var undecided = Undecided(run);
        if (undecided.Count > 0)
        {
            text.Append("\n## Could not be decided by the tool\n\naxe reports these as \"incomplete\": it could not measure them. They are not failures, and a person should look.\n\n| Reason | Elements |\n|---|---|\n");
            foreach (var (reason, count) in undecided) text.Append($"| {Cell(reason)} | {count} |\n");
        }

        text.Append("\n## Not checked\n\n");
        text.Append("- The page is checked as it loads: closed dialogs, menus and popovers are not opened.\n");
        text.Append("- Keyboard order, focus traps and what a screen reader says.\n");
        text.Append("- Automated checks find part of what WCAG asks, so a report with no findings is not a statement of conformance.\n");
        return text.ToString();
    }

    static string Cell(string text) => Spaces().Replace(text, " ").Replace("|", "/");

    // Text from the page as a code span, whatever backticks it holds: the page can't end the span and write
    // Markdown of its own into the report.
    static string Code(string text)
    {
        text = Spaces().Replace(text, " ");
        var fence = new string('`', Backticks().Matches(text).Select(m => m.Length).DefaultIfEmpty(0).Max() + 1);
        return fence.Length == 1 ? $"`{text}`" : $"{fence} {text} {fence}";
    }

    // Text that quotes the page (an attribute's value in axe's summary), with nothing in it read as Markdown.
    static string Plain(string text) => MarkdownMarks().Replace(text, @"\$0");

    /// <summary>What the terminal shows after a run.</summary>
    public static string Summary(JsonObject run)
    {
        var pages = run["pages"]!.AsArray().Select(p => p!.AsObject()).ToList();
        var urls = pages.Select(p => (string)p["url"]!).Distinct().ToList();
        var text = new StringBuilder();
        text.Append($"Audited {(urls.Count == 1 ? urls[0] : $"{urls.Count} pages")} at {string.Join(" and ", run["widths"]!.AsArray().Select(w => $"{w}px"))}, {string.Join(" and ", run["schemes"]!.AsArray().Select(s => (string)s!))}, with axe-core {(string)run["engine"]!["version"]!}.\n");
        var failed = pages.Where(p => p["error"] is not null).ToList();
        foreach (var page in failed.Take(5))
            text.Append($"Not checked: {(string)page["url"]!} at {(int)page["width"]!}px, {(string)page["scheme"]!}: {(string)page["error"]!}\n");
        if (failed.Count > 5) text.Append($"…and {failed.Count - 5} more loads that weren't checked: the report lists them.\n");
        foreach (var page in pages.Where(p => (int?)p["status"] is >= 400).DistinctBy(p => (string)p["url"]!))
            text.Append($"{(string)page["url"]!} answered {(int)page["status"]!}: what was checked is its error page.\n");
        foreach (var page in pages.Where(p => p["landed"] is not null).DistinctBy(p => (string)p["url"]!))
            text.Append($"{(string)page["url"]!} led to {(string)page["landed"]!}: that is the page that was checked.\n");
        var rules = Rules(run);
        if (run["verdicts"] is not null) text.Append(JudgedSummary(run));
        else if (rules.Count == 0) text.Append("No findings.\n");
        else
        {
            var elements = rules.Sum(r => r.Elements);
            text.Append($"{elements} finding{(elements == 1 ? "" : "s")} in {rules.Count} rule{(rules.Count == 1 ? "" : "s")}:\n");
            var (impact, id) = (rules.Max(r => r.Impact.Length), rules.Max(r => r.Id.Length));
            foreach (var rule in rules) text.Append($"  {rule.Impact.PadRight(impact)}  {rule.Id.PadRight(id)}  {rule.Elements,4}  {rule.Help}\n");
        }
        if (Undecided(run).Sum(u => u.Elements) is > 0 and var undecided)
            text.Append($"{undecided} element{(undecided == 1 ? "" : "s")} axe could not decide (text over a picture or gradient, mostly): a person should look.\n");
        return text.ToString();
    }

    /// <summary>
    /// The pages a run didn't check as asked: one that didn't load, or that answered with an error. A build that
    /// fails on findings fails on these too: nothing found there says nothing.
    /// </summary>
    public static List<JsonObject> Unchecked(JsonObject run) =>
        [.. run["pages"]!.AsArray().Select(p => p!.AsObject()).Where(p => p["error"] is not null || (int?)p["status"] is >= 400)];

    /// <summary>How many elements a run found failing.</summary>
    public static int Findings(JsonObject run) => Rules(run).Sum(r => r.Elements);

    /// <summary>
    /// The app of <paramref name="csproj"/>, started with <c>dotnet run</c> on a port of its own for the time
    /// of an audit. <see cref="Url"/> is where it answers.
    /// </summary>
    public sealed class App : IDisposable
    {
        readonly Process _process;
        readonly StringBuilder _said = new();
        IDisposable? _interrupt;

        public string Url { get; }

        App(Process process, string url) => (_process, Url) = (process, url);

        public static async Task<App> StartAsync(string csproj, Action<string> progress)
        {
            int port;
            using (var probe = new TcpListener(IPAddress.Loopback, 0))
            {
                probe.Start();
                port = ((IPEndPoint)probe.LocalEndpoint).Port;
            }
            var url = $"http://127.0.0.1:{port}";
            var start = new ProcessStartInfo(DotnetHost.Path) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(csproj)! };
            foreach (var argument in (string[])["run", "--project", csproj, "--no-launch-profile"]) start.ArgumentList.Add(argument);
            start.Environment["ASPNETCORE_URLS"] = url;
            start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            start.Environment["DOTNET_ENVIRONMENT"] = "Development";
            progress($"Starting {Path.GetFileNameWithoutExtension(csproj)} at {url}");
            Process process;
            try { process = Process.Start(start) ?? throw new GenerationException("dotnet run didn't start, so nothing was audited."); }
            catch (System.ComponentModel.Win32Exception) { throw new GenerationException("izzy couldn't start dotnet (it isn't on the PATH, and DOTNET_ROOT doesn't point at it), so nothing was audited. Start the app yourself and give izzy audit its address."); }
            var app = new App(process, url);
            AppDomain.CurrentDomain.ProcessExit += app.Kill;
            app._interrupt = Interrupt.On(() => app.Kill(null, EventArgs.Empty));
            process.OutputDataReceived += (_, e) => { lock (app._said) app._said.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { lock (app._said) app._said.AppendLine(e.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // It builds first: up to five minutes for the app to answer.
            using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(10) };
            var patience = Stopwatch.StartNew();
            while (patience.Elapsed < TimeSpan.FromMinutes(5))
            {
                if (process.HasExited) break;
                try
                {
                    using var _ = await http.GetAsync(url);
                    return app;
                }
                catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { await Task.Delay(500); }
            }
            string said;
            lock (app._said) said = string.Join('\n', app._said.ToString().Trim().Split('\n').TakeLast(15));
            app.Dispose();
            throw new GenerationException($"The app didn't start, so nothing was audited. dotnet run said:\n{said}");
        }

        void Kill(object? sender, EventArgs e)
        {
            try { if (!_process.HasExited) _process.Kill(entireProcessTree: true); }
            catch (Exception x) when (x is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        }

        public void Dispose()
        {
            _interrupt?.Dispose();
            AppDomain.CurrentDomain.ProcessExit -= Kill;
            Kill(null, EventArgs.Empty);
            _process.Dispose();
        }
    }

    [GeneratedRegex(@"axe v(\d+\.\d+\.\d+)")]
    private static partial Regex EngineHeader();

    [GeneratedRegex(@"^wcag\d{3,4}$")]
    private static partial Regex SuccessCriterion();

    // In an id or an attribute's value (#ib-menu-1c36cl-trigger, [aria-labelledby="ib-kanban-title-e0nxa1"]), never in a class.
    [GeneratedRegex(@"(?<![.\w-])(ib-[a-z]+(?:-[a-z]+)*?-)[a-z0-9]{6}(?![a-z0-9])")]
    private static partial Regex MadeId();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex("`+")]
    private static partial Regex Backticks();

    [GeneratedRegex(@"[\\`*_\[\]<>!|]")]
    private static partial Regex MarkdownMarks();

    [GeneratedRegex(@"^Fix (any|all) of the following: ")]
    private static partial Regex FixLead();
}
