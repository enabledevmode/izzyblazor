using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using IzzyBlazor.Generation;

namespace IzzyBlazor.Cli;

// report.html: the report as one file to hand to a client, with a picture of each element where it is on the
// page, in each color scheme. The pictures are taken during the run, while the page is open.
static partial class Audit
{
    /// <summary>How many elements of a group of findings a report shows; the rest are counted.</summary>
    const int Shown = 10;

    /// <summary>
    /// The pictures of a run: for each finding, the part of the page around its element with the element
    /// outlined, once for each color scheme. A group of findings (a rule, a verdict and its reason) has
    /// pictures of the elements a report shows and no more, so a fault on every page of a site is ten
    /// pictures, not a thousand.
    /// </summary>
    public sealed class Pictures
    {
        /// <summary>A picture as WebP in base64, its size, and the width of the page it was taken at.</summary>
        public sealed record Picture(string Data, int Width, int Height, int PageWidth);

        readonly ConcurrentDictionary<(string Id, string Scheme), Picture> _taken = new();
        readonly Dictionary<string, HashSet<string>> _groups = new(StringComparer.Ordinal);

        // The findings whose element the page does draw, and no picture came of it (the capture failed, or the
        // page changed since): a report doesn't say of them that they aren't drawn.
        readonly ConcurrentDictionary<string, bool> _failed = new();

        /// <summary>Takes the pictures of a page's findings: the page is still open, and Collect kept its elements.</summary>
        public async Task Take(Chromium browser, JsonObject page, int width, string scheme)
        {
            var rules = page["violations"]!.AsArray();
            for (var r = 0; r < rules.Count; r++)
            {
                var nodes = rules[r]!["nodes"]!.AsArray();
                for (var n = 0; n < nodes.Count; n++)
                {
                    var id = (string)nodes[n]!["id"]!;
                    if (_taken.ContainsKey((id, scheme))) continue;
                    var group = $"{(string)rules[r]!["id"]!}|{(string?)nodes[n]!["verdict"]?["is"]}|{(string?)nodes[n]!["verdict"]?["why"]}";
                    // Its place among the group's ten is held while the picture is taken, and given back if none comes.
                    HashSet<string>? pictured;
                    bool held;
                    lock (_groups)
                    {
                        if (!_groups.TryGetValue(group, out pictured)) _groups[group] = pictured = [];
                        if (!pictured.Contains(id) && pictured.Count >= Shown) continue;
                        held = pictured.Add(id);
                    }
                    var took = false;
                    try
                    {
                        // The page outlines the element and says which part of itself to take; null for an
                        // element that isn't drawn (a meta tag, a closed menu's item).
                        try
                        {
                            if ((string?)await browser.Evaluate(Outline.Replace("__RULE__", r.ToString()).Replace("__NODE__", n.ToString())) is { } answer)
                            {
                                var clip = JsonNode.Parse(answer)!.AsObject();
                                var scale = (double)clip["scale"]!;
                                var shot = await browser.Send("Page.captureScreenshot", new JsonObject { ["format"] = "webp", ["quality"] = 80, ["clip"] = clip.DeepClone(), ["captureBeyondViewport"] = false });
                                if ((string?)shot["data"] is { Length: > 0 } data)
                                    {
                                    _taken.TryAdd((id, scheme), new Picture(data, (int)Math.Round((double)clip["width"]! * scale), (int)Math.Round((double)clip["height"]! * scale), width));
                                    took = true;
                                }
                                else _failed[id] = true;
                            }
                        }
                        finally { await browser.Evaluate("document.getElementById('__izzy-outline')?.remove()"); }
                    }
                    // A picture that couldn't be taken is a finding without one, and the audit goes on. A page
                    // that failed once (it hangs, or answers with something else) is asked for no more: each
                    // try can wait two minutes.
                    catch (Exception e) when (e is GenerationException or System.Text.Json.JsonException or InvalidOperationException or NullReferenceException or InvalidCastException && !browser.Closed)
                    {
                        _failed[id] = true;
                        return;
                    }
                    finally
                    {
                        if (held && !took) lock (_groups) if (!_taken.ContainsKey((id, "light")) && !_taken.ContainsKey((id, "dark"))) pictured.Remove(id);
                    }
                }
            }
        }

        /// <summary>Whether the finding's element is drawn and still has no picture.</summary>
        public bool Failed(string id) => _failed.ContainsKey(id);

        /// <summary>The pictures of a finding, light first.</summary>
        public List<(string Scheme, Picture Picture)> Of(string id) =>
            [.. ((string[])["light", "dark"]).Where(scheme => _taken.ContainsKey((id, scheme))).Select(scheme => (scheme, _taken[(id, scheme)]))];

        // Scrolls the element into the window, outlines it with an element of izzy's own over the page (the
        // page's element is not touched), and answers with the part of the document to take: the element
        // with some of the page around it, at most the window, scaled down when it is wider than 960px.
        const string Outline = """
            (async () => {
              document.getElementById("__izzy-outline")?.remove();
              const e = ((window.__izzyFound || [])[__RULE__] || [])[__NODE__];
              if (!e || !e.isConnected) throw new Error("The element is gone.");
              const pad = 32, vw = innerWidth, vh = innerHeight;
              // Where it is in the window, through the frames it is in. An element with no box of its own
              // (display: contents) is where its content is.
              const rect = () => {
                let r = e.getBoundingClientRect();
                if (!r.width || !r.height) {
                  const range = e.ownerDocument.createRange();
                  range.selectNodeContents(e);
                  r = range.getBoundingClientRect();
                }
                let x = r.left, y = r.top, width = r.width, height = r.height;
                for (let w = e.ownerDocument.defaultView; w && w !== window;) {
                  const frame = w.frameElement;
                  if (!frame) return null;
                  // The frame's content starts inside its border and padding, and a frame drawn smaller
                  // (a preview scaled down) draws what is in it smaller too.
                  const around = frame.getBoundingClientRect(), style = w.parent.getComputedStyle(frame);
                  const sx = frame.offsetWidth ? around.width / frame.offsetWidth : 1, sy = frame.offsetHeight ? around.height / frame.offsetHeight : 1;
                  x = around.left + (frame.clientLeft + parseFloat(style.paddingLeft) + x) * sx;
                  y = around.top + (frame.clientTop + parseFloat(style.paddingTop) + y) * sy;
                  width *= sx;
                  height *= sy;
                  w = frame.ownerDocument.defaultView;
                }
                return { x, y, width, height };
              };
              const drawn = () => new Promise(done => { requestAnimationFrame(() => requestAnimationFrame(done)); setTimeout(done, 250); });
              let r = rect();
              if (!r || !r.width || !r.height) return null;
              const most = Math.min(600, vh);
              const tall = r.height + 2 * pad > most, wide = r.width + 2 * pad > vw;
              // An element with no box of its own doesn't scroll: what is in it does.
              (e.getClientRects().length ? e : e.firstElementChild || e.parentElement || e).scrollIntoView({ block: tall ? "start" : "center", inline: wide ? "start" : "center", behavior: "instant" });
              if (tall) scrollBy({ top: -pad, behavior: "instant" });
              await drawn();
              r = rect();
              // A header that stays at the top of the window covers the top of what was scrolled up to it:
              // the page is scrolled back by as much as it covers.
              if (r && tall && e.ownerDocument === document) {
                const y = Math.max(r.y, 0) + 1;
                let over = document.elementFromPoint(Math.min(Math.max(r.x + r.width / 2, 1), vw - 1), y);
                while (over && (over.contains(e) || e.contains(over) || !["sticky", "fixed"].includes(getComputedStyle(over).position))) over = over.parentElement;
                if (over && over.getBoundingClientRect().bottom > r.y) {
                  scrollBy({ top: r.y - over.getBoundingClientRect().bottom - pad, behavior: "instant" });
                  await drawn();
                  r = rect();
                }
              }
              if (!r || r.x >= vw || r.y >= vh || r.x + r.width <= 0 || r.y + r.height <= 0) return null;
              // An element as wide or as tall as the window has its outline inside the window's edge, where
              // it shows: one line across the page marks nothing.
              const edge = 8;
              const left = Math.max(r.x, edge), top = Math.max(r.y, edge), right = Math.max(Math.min(r.x + r.width, vw - edge), left), bottom = Math.max(Math.min(r.y + r.height, vh - edge), top);
              const mark = document.createElement("div");
              mark.id = "__izzy-outline";
              mark.style.cssText = "all: initial; position: fixed; z-index: 2147483647; pointer-events: none; box-sizing: border-box; border-radius: 2px;"
                + "outline: 3px solid #d6006f; outline-offset: 2px; box-shadow: 0 0 0 7px #fff;"
                + `left: ${left}px; top: ${top}px; width: ${Math.max(right - left, 1)}px; height: ${Math.max(bottom - top, 1)}px;`;
              document.documentElement.appendChild(mark);
              await drawn();
              const width = Math.min(Math.max(r.width + 2 * pad, 480), vw), height = Math.min(Math.max(r.height + 2 * pad, 160), most);
              const x = Math.max(0, Math.min(r.x + r.width / 2 - width / 2, vw - width));
              const y = Math.max(0, Math.min(tall ? r.y - pad : r.y + r.height / 2 - height / 2, vh - height));
              return JSON.stringify({ x: Math.round(x + scrollX), y: Math.round(y + scrollY), width: Math.round(width), height: Math.round(height), scale: Math.min(1, 960 / width) });
            })()
            """;
    }

    /// <summary>
    /// report.html: the report of a run with verdicts as one file, nothing loaded from anywhere. What the
    /// audited page said is text in it, never markup, and the file's own policy lets no script run.
    /// </summary>
    public static string Html(JsonObject run, Pictures? pictures)
    {
        var pages = run["pages"]!.AsArray().Select(p => p!.AsObject()).ToList();
        var urls = pages.Select(p => (string)p["url"]!).Distinct().ToList();
        var groups = Groups(run);
        var sites = urls.Select(u => Uri.TryCreate(u, UriKind.Absolute, out var parsed) ? parsed.Authority : u).Distinct().ToList();
        var subject = urls.Count == 1 ? urls[0] : sites.Count == 1 ? $"{sites[0]}, {urls.Count} pages" : $"{urls.Count} pages";
        var day = DateTimeOffset.TryParse((string)run["at"]!, System.Globalization.CultureInfo.InvariantCulture, out var at) ? at.ToString("MMMM d, yyyy", System.Globalization.CultureInfo.InvariantCulture) : ((string)run["at"]!)[..10];

        // A contrast is measured once for each scheme: the colors of a dark page are not those of the light one.
        var measured = new Dictionary<string, List<(string Scheme, JsonNode Contrast)>>(StringComparer.Ordinal);
        foreach (var page in pages)
            foreach (var node in page["violations"]!.AsArray().SelectMany(rule => rule!["nodes"]!.AsArray()).Where(n => n!["contrast"] is not null))
            {
                if (!measured.TryGetValue((string)node!["id"]!, out var each)) measured[(string)node["id"]!] = each = [];
                if (each.All(m => m.Scheme != (string)page["scheme"]!)) each.Add(((string)page["scheme"]!, node["contrast"]!));
            }

        var text = new StringBuilder();
        text.Append("<!doctype html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
        text.Append("<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; img-src data:; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'\">\n<meta name=\"color-scheme\" content=\"light dark\">\n");
        text.Append($"<title>Accessibility audit: {H(subject)}</title>\n<style>\n{Style}</style>\n</head>\n<body>\n");
        text.Append($"<header>\n<p class=\"kicker\">Accessibility audit</p>\n<h1>{H(subject)}</h1>\n");
        text.Append($"<p class=\"made\">{H(day)}. Checked with axe-core {H((string)run["engine"]!["version"]!)} in {H((string?)run["browser"] ?? "a browser")}, at {H(Listed(run["widths"]!.AsArray().Select(w => $"{w}px")))}, in {H(Listed(run["schemes"]!.AsArray().Select(s => (string)s!)))}.</p>\n</header>\n<main>\n");

        text.Append("<section aria-labelledby=\"summary\">\n<h2 id=\"summary\">Summary</h2>\n");
        if (groups.Count == 0) text.Append("<p>No findings.</p>\n");
        else
        {
            text.Append("<ul class=\"tiles\">\n");
            foreach (var (verdict, meaning) in Verdicts)
                if (groups.Where(g => g.Verdict == verdict).Sum(g => g.Findings.Count) is > 0 and var count)
                    text.Append($"<li class=\"{Class(verdict)}\"><a href=\"#{Class(verdict)}\"><span class=\"count\">{count}</span> <span class=\"name\">{H(Title(verdict))}</span></a><span class=\"meaning\">{Inline(meaning)}.</span></li>\n");
            text.Append("</ul>\n");
        }
        if (run["baseline"] is { } baseline)
            text.Append($"<p>Against the run of {H((string?)baseline["at"] is { Length: >= 10 } before ? before[..10] : "an unknown date")}: {(int)baseline["new"]!} new, {baseline["fixed"]!.AsArray().Count} fixed.</p>\n");
        text.Append("<p class=\"note\">An automated check finds part of what WCAG asks. What this one doesn’t cover is listed at the end.</p>\n</section>\n");

        var section = "";
        foreach (var group in groups)
        {
            if (group.Verdict != section)
            {
                if (section.Length > 0) text.Append("</section>\n");
                section = group.Verdict;
                text.Append($"<section aria-labelledby=\"{Class(section)}\">\n<h2 id=\"{Class(section)}\">{H(Title(section))}</h2>\n<p class=\"note\">{Inline(Verdicts.First(v => v.Verdict == section).Meaning)}.</p>\n");
            }
            text.Append($"<article>\n<h3>{H(group.Help)} <code>{H(group.RuleId)}</code></h3>\n<dl class=\"facts\">\n");
            text.Append($"<div><dt>Criterion</dt><dd>{H(Criterion(group.Rule["tags"]!.AsArray().Select(t => (string)t!).ToList()))}; axe severity {H(group.Impact)}.{Link((string?)group.Rule["helpUrl"], "About this rule")}</dd></div>\n");
            var first = group.Findings[0].Node["verdict"];
            var who = group.Verdict == Accepted && ((string?)first?["by"], (string?)first?["on"]) is var (by, on) && (by ?? on) is not null ? $" ({string.Join(", ", ((string?[])[by, on]).Where(p => p is not null))})" : "";
            text.Append($"<div><dt>Verdict</dt><dd><span class=\"verdict {Class(group.Verdict)}\">{H(group.Verdict)}</span> {(group.Why is null ? "Nothing known explains it, so it counts as a failure" : Inline(group.Why.TrimEnd('.')))}{H(who)}. Confidence: {H((string?)first?["confidence"] ?? "medium")}.</dd></div>\n");
            var fresh = group.Findings.Count(f => f.New);
            text.Append($"<div><dt>Found</dt><dd>{Elements(group.Findings.Count)} on {group.Pages} page{(group.Pages == 1 ? "" : "s")}{(fresh > 0 ? $", {fresh} new" : "")}</dd></div>\n</dl>\n");
            // Those with a picture first: a group has pictures of as many elements as are shown.
            var shown = group.Findings.OrderBy(f => pictures?.Of(f.Id).Count > 0 ? 0 : 1).Take(Shown).ToList();
            foreach (var finding in shown) text.Append(ElementHtml(finding, pictures, measured.GetValueOrDefault(finding.Id) ?? []));
            if (group.Findings.Count > shown.Count) text.Append($"<p class=\"note\">…and {Elements(group.Findings.Count - shown.Count, "more element")} with the same finding.</p>\n");
            text.Append("</article>\n");
        }
        if (section.Length > 0) text.Append("</section>\n");

        text.Append("<section aria-labelledby=\"checked\">\n<h2 id=\"checked\">What was checked</h2>\n");
        var addresses = pages.GroupBy(p => (string)p["url"]!).ToList();
        text.Append($"<p>{addresses.Count} page{(addresses.Count == 1 ? "" : "s")}, each at {H(Listed(pages.Select(p => $"{(int)p["width"]!}px {(string)p["scheme"]!}").Distinct()))}.</p>\n");
        // A few loads are listed each, so one that failed has its width, scheme and why; a site's pages once each.
        if (pages.Count <= 16)
        {
            text.Append("<table>\n<thead><tr><th scope=\"col\">Page</th><th scope=\"col\">Width</th><th scope=\"col\">Scheme</th><th scope=\"col\">Elements found</th></tr></thead>\n<tbody>\n");
            foreach (var page in pages)
            {
                var count = page["violations"]!.AsArray().Sum(v => v!["nodes"]!.AsArray().Count);
                var note = (string?)page["error"] is { } error ? $"Not checked: {error}" : (int?)page["status"] is >= 400 and var status ? $"{count} (the page answered {status})" : $"{count}";
                if ((string?)page["landed"] is { } landed) note += $" (it led to {landed}, which is what was checked)";
                text.Append($"<tr><td>{PageLink((string)page["url"]!)}</td><td>{(int)page["width"]!}px</td><td>{H((string)page["scheme"]!)}</td><td>{H(Spaces().Replace(note, " "))}</td></tr>\n");
            }
        }
        else
        {
            text.Append("<table>\n<thead><tr><th scope=\"col\">Page</th><th scope=\"col\">Elements found</th></tr></thead>\n<tbody>\n");
            foreach (var address in addresses)
            {
                var count = address.SelectMany(p => p["violations"]!.AsArray()).SelectMany(v => v!["nodes"]!.AsArray()).Select(n => (string?)n!["id"]).Distinct().Count();
                var failed = address.Where(p => p["error"] is not null).ToList();
                var note = failed.Count == address.Count() ? $"Not checked: {(string)failed[0]["error"]!}" : failed.Count > 0 ? $"{count} ({failed.Count} of {address.Count()} loads failed)" : $"{count}";
                if (address.FirstOrDefault(p => (int?)p["status"] is >= 400) is { } answered) note += $" (the page answered {(int)answered["status"]!})";
                if (address.FirstOrDefault(p => p["landed"] is not null) is { } led) note += $" (it led to {(string)led["landed"]!}, which is what was checked)";
                text.Append($"<tr><td>{PageLink(address.Key)}</td><td>{H(note)}</td></tr>\n");
            }
        }
        text.Append("</tbody>\n</table>\n</section>\n");

        if (Undecided(run) is { Count: > 0 } undecided)
        {
            text.Append("<section aria-labelledby=\"undecided\">\n<h2 id=\"undecided\">Could not be decided by the tool</h2>\n<p>axe reports these as “incomplete”: it could not measure them. They are not failures, and a person should look.</p>\n");
            text.Append("<table>\n<thead><tr><th scope=\"col\">Reason</th><th scope=\"col\">Elements</th></tr></thead>\n<tbody>\n");
            foreach (var (reason, count) in undecided) text.Append($"<tr><td>{H(Spaces().Replace(reason, " "))}</td><td>{count}</td></tr>\n");
            text.Append("</tbody>\n</table>\n</section>\n");
        }

        text.Append("<section aria-labelledby=\"not-checked\">\n<h2 id=\"not-checked\">Not checked</h2>\n<ul>\n");
        text.Append("<li>The page is checked as it loads: closed dialogs, menus and popovers are not opened.</li>\n");
        text.Append("<li>Keyboard order, focus traps and what a screen reader says.</li>\n");
        text.Append("<li>Automated checks find part of what WCAG asks, so a report with no findings is not a statement of conformance.</li>\n");
        text.Append("</ul>\n</section>\n</main>\n");
        text.Append($"<footer><p>Made by izzy audit {H((string)run["version"]!)}{(run["verdicts"] is { } judged ? $", verdicts from pack {H((string?)judged["pack"] ?? "")}{((string?)judged["project"] is { } project ? $" and {H(project)}" : "")}" : "")}.</p></footer>\n</body>\n</html>\n");
        return text.ToString();
    }

    // One element: where it was found, its pictures, and what is known of it.
    static string ElementHtml(Finding finding, Pictures? pictures, List<(string Scheme, JsonNode Contrast)> measured)
    {
        var node = finding.Node;
        var text = new StringBuilder("<div class=\"element\">\n");
        text.Append($"<p class=\"where\">On {PageLink((string)finding.Page["url"]!)} at {H(string.Join("; ", finding.Where.Distinct()))}{(finding.New ? " <span class=\"new\">new</span>" : "")}</p>\n");
        if (pictures is not null)
        {
            var taken = pictures.Of(finding.Id);
            if (taken.Count == 0) text.Append(pictures.Failed(finding.Id) ? "<p class=\"note\">No picture of this element could be taken.</p>\n" : "<p class=\"note\">No picture: the element isn’t drawn on the page as it loads.</p>\n");
            else
            {
                // Side by side when both fit at their own size; a wide one is small at half the page, so they stack.
                text.Append($"<div class=\"pictures{(taken.Any(t => t.Picture.Width > 480) ? " stacked" : "")}\">\n");
                foreach (var (scheme, picture) in taken)
                    text.Append($"<figure><img src=\"data:image/webp;base64,{picture.Data}\" width=\"{picture.Width}\" height=\"{picture.Height}\" alt=\"The element outlined on the page, {scheme}, at {picture.PageWidth}px wide\"><figcaption>{Title(scheme)}, {picture.PageWidth}px</figcaption></figure>\n");
                text.Append("</div>\n");
            }
        }
        text.Append("<dl class=\"facts\">\n");
        void Row(string name, string html) => text.Append($"<div><dt>{name}</dt><dd>{html}</dd></div>\n");
        Row("Selector", $"<code>{H((string)node["target"]!)}</code>");
        Row("HTML", $"<code>{H(Spaces().Replace((string)node["html"]!, " "))}</code>");
        // axe's summary is a lead ("Fix any of the following:") and a line for each way: a list when there are several.
        if ((string?)node["summary"] is { } summary)
        {
            var lines = summary.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var ways = lines.Where(line => !FixLead().IsMatch(line + " ")).ToList();
            Row("axe", ways.Count <= 1 ? H(FixLead().Replace(Spaces().Replace(summary, " "), "")) : string.Concat(lines.Select(line => FixLead().IsMatch(line + " ") ? $"{H(line)}" : $"<span class=\"way\">{H(line)}</span>")));
        }
        // One row when the schemes measure alike, else one for each.
        static string Colors(JsonNode contrast) => $"{Swatch((string?)contrast["foreground"])}{H((string?)contrast["foreground"] ?? "")} on {Swatch((string?)contrast["background"])}{H((string?)contrast["background"] ?? "")}, {H(contrast["ratio"]?.ToString() ?? "")}:1 at {H((string?)contrast["size"] ?? "")}; needs {H((string?)contrast["needs"] ?? "")}";
        var colors = measured.Select(m => (m.Scheme, Text: Colors(m.Contrast))).ToList();
        if (colors.Select(c => c.Text).Distinct().Count() == 1) Row("Measured", colors[0].Text);
        else foreach (var (scheme, said) in colors) Row($"Measured, {scheme}", said);
        if (node["trace"] is JsonObject trace)
        {
            var docs = (string?)trace["docs"];
            static string Names(JsonArray names) => string.Join(", ", names.Select(n => $"<code>{H((string)n!)}</code>"));
            if ((string?)trace["component"] is { } component)
                Row("Component", $"<code>{H(component)}</code>{(trace["inside"] is JsonArray { Count: > 0 } within ? $", inside {Names(within)}" : "")}{(trace["fix"] is null ? Link(docs, "Docs") : "")}");
            else if (trace["candidates"] is JsonArray candidates) Row("Component", $"{Among(candidates.Select(n => $"<code>{H((string)n!)}</code>"))} (they draw the same element)");
            else if (trace["inside"] is JsonArray { Count: > 0 } inside) Row("Inside", Names(inside));
            if (trace["fix"] is JsonObject fix) Row("Fix", $"{Inline(((string)fix["text"]!).TrimEnd('.'))}.{Link(docs, "Docs")}");
            if (trace["source"] is JsonObject source)
            {
                var around = (bool?)source["around"] == true ? $" (the <code>{H((string?)source["tag"] ?? "")}</code> it is in)" : "";
                if ((string?)source["file"] is { } file) Row("Source", $"<code>{H(file)}:{(int)source["line"]!}</code>{around}, confidence {H((string)source["confidence"]!)}");
                else Row("Source", $"one of {string.Join(", ", source["places"]!.AsArray().Select(p => $"<code>{H((string)p!["file"]!)}:{(int)p["line"]!}</code>"))}{((int?)source["more"] is { } more ? $" and {more} more" : "")}{around}");
            }
            else if ((string?)trace["page"] is { } page) Row("Source", $"the page is <code>{H(page)}</code>; the element wasn’t found in it");
        }
        text.Append("</dl>\n</div>\n");
        return text.ToString();
    }

    static string H(string text) => WebUtility.HtmlEncode(text);

    // Text of ours or the pack's, where `Label` is code.
    static string Inline(string text) => CodeSpan().Replace(H(text), "<code>$1</code>");

    static string Title(string words) => char.ToUpperInvariant(words[0]) + words[1..];

    static string Class(string verdict) => "v-" + verdict.Replace(' ', '-');

    static string Listed(IEnumerable<string> items) => items.ToList() is { Count: > 1 } list ? $"{string.Join(", ", list.Take(list.Count - 1))} and {list[^1]}" : items.FirstOrDefault() ?? "";

    // A link only to a web address: nothing else a page or a pack says becomes an href.
    static bool Web(string? address) => Uri.TryCreate(address, UriKind.Absolute, out var parsed) && parsed.Scheme is "http" or "https";

    static string Link(string? address, string words) => Web(address) ? $" <a href=\"{H(address!)}\">{words}</a>" : "";

    static string PageLink(string url) => Web(url) ? $"<a href=\"{H(url)}\">{H(url)}</a>" : H(url);

    // The measured color beside its value, when it is a color and nothing else.
    static string Swatch(string? color) => color is not null && HexColor().IsMatch(color) ? $"<span class=\"swatch\" style=\"background:{color}\"></span>" : "";

    [GeneratedRegex("`([^`]+)`")]
    private static partial Regex CodeSpan();

    [GeneratedRegex(@"^#[0-9a-fA-F]{3,8}\z")]
    private static partial Regex HexColor();

    const string Style = """
        :root {
          color-scheme: light dark;
          --bg: #fff; --text: #1b1b1f; --quiet: #5a5a66; --line: #dfdfe6; --soft: #f5f5f8; --link: #1d55b8;
          --real: #b3261e; --person: #855400; --accepted: #1d55b8; --other: #5a5a66;
        }
        @media (prefers-color-scheme: dark) {
          :root {
            --bg: #131316; --text: #ececf1; --quiet: #a9a9b6; --line: #33333c; --soft: #1e1e23; --link: #9bbcff;
            --real: #ff9a8f; --person: #f0c26a; --accepted: #9bbcff; --other: #a9a9b6;
          }
        }
        * { box-sizing: border-box; }
        body { margin: 0 auto; padding: 2.5rem 1.5rem 4rem; max-width: 68rem; background: var(--bg); color: var(--text); font: 1rem/1.55 system-ui, -apple-system, "Segoe UI", Roboto, sans-serif; overflow-wrap: anywhere; }
        h1 { margin: 0 0 0.5rem; font-size: 1.75rem; line-height: 1.25; }
        h2 { margin: 3rem 0 0.5rem; padding-top: 1.5rem; border-top: 1px solid var(--line); font-size: 1.375rem; line-height: 1.3; }
        h3 { margin: 0 0 0.75rem; font-size: 1.0625rem; line-height: 1.4; }
        h3 code { margin-inline-start: 0.25rem; font-weight: 400; }
        p, ul, dl, table, figure { margin: 0 0 1rem; }
        a { color: var(--link); }
        code { padding: 0.1em 0.3em; border-radius: 0.25rem; background: var(--soft); font: 0.875em/1.5 ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; }
        .kicker { margin: 0 0 0.25rem; color: var(--quiet); font-size: 0.8125rem; font-weight: 600; letter-spacing: 0.06em; text-transform: uppercase; }
        .made, .note, .where, figcaption, footer { color: var(--quiet); font-size: 0.875rem; }
        .tiles { display: grid; grid-template-columns: repeat(auto-fit, minmax(11rem, 1fr)); gap: 0.75rem; padding: 0; list-style: none; }
        .tiles li { padding: 0.875rem 1rem; border: 1px solid var(--line); border-top: 3px solid var(--tone); border-radius: 0.5rem; }
        .tiles a { display: block; color: inherit; text-decoration: none; }
        .tiles a:hover .name, .tiles a:focus-visible .name { text-decoration: underline; }
        .tiles .count { display: block; font-size: 2rem; font-weight: 650; line-height: 1.1; }
        .tiles .name { font-weight: 600; }
        .tiles .meaning { display: block; margin-top: 0.25rem; color: var(--quiet); font-size: 0.8125rem; line-height: 1.45; }
        .v-real { --tone: var(--real); } .v-needs-a-person { --tone: var(--person); } .v-accepted { --tone: var(--accepted); }
        .v-demo-page, .v-false-positive { --tone: var(--other); }
        .verdict, .new { display: inline-block; padding: 0 0.5rem; border: 1px solid var(--tone, var(--real)); border-radius: 1rem; color: var(--tone, var(--real)); font-size: 0.8125rem; font-weight: 600; }
        article { margin: 1.5rem 0; padding: 1.25rem; border: 1px solid var(--line); border-radius: 0.5rem; }
        .facts > div { display: grid; grid-template-columns: 6.5rem 1fr; gap: 0.75rem; padding: 0.3rem 0; }
        .facts dt { color: var(--quiet); font-size: 0.875rem; }
        .facts dd { margin: 0; min-width: 0; }
        .element { margin-top: 1.25rem; padding-top: 1.25rem; border-top: 1px solid var(--line); }
        .element .facts { margin: 0; font-size: 0.9375rem; }
        .where { margin-bottom: 0.75rem; }
        .pictures { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(18rem, 100%), 1fr)); gap: 0.75rem; align-items: start; margin-bottom: 0.75rem; }
        .pictures.stacked { grid-template-columns: 1fr; }
        figure { margin: 0; }
        img { display: block; max-width: 100%; height: auto; border: 1px solid var(--line); border-radius: 0.375rem; }
        figcaption { margin-top: 0.25rem; }
        .swatch { display: inline-block; width: 0.8em; height: 0.8em; margin-inline-end: 0.3em; border: 1px solid var(--quiet); border-radius: 2px; vertical-align: -0.05em; }
        table { width: 100%; border-collapse: collapse; font-size: 0.9375rem; }
        th, td { padding: 0.4rem 0.75rem 0.4rem 0; border-bottom: 1px solid var(--line); text-align: start; vertical-align: top; }
        th { color: var(--quiet); font-size: 0.875rem; font-weight: 600; white-space: nowrap; overflow-wrap: normal; }
        .way { display: list-item; margin-inline-start: 1.25rem; }
        footer { margin-top: 3rem; padding-top: 1rem; border-top: 1px solid var(--line); }
        @media (max-width: 30rem) { .facts > div { grid-template-columns: 1fr; gap: 0; } }
        @media print {
          :root { --bg: #fff; --text: #000; --quiet: #444; --line: #bbb; --soft: #f2f2f2; --link: #000; --real: #8c1d18; --person: #6b4400; --accepted: #173f8a; --other: #444; }
          body { padding: 0; max-width: none; font-size: 10.5pt; }
          .element, .tiles li { break-inside: avoid; }
          h2, h3, article > .facts { break-after: avoid; }
        }

        """;
}
