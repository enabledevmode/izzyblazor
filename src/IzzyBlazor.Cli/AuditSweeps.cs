using System.IO.Compression;
using System.Text.Json.Nodes;
using IzzyBlazor.Generation;

namespace IzzyBlazor.Cli;

// The sweeps: checks of izzy's own beside axe's, for what axe doesn't look at. Each measures something in
// the open page and answers with a rule as axe does, so a finding of a sweep gets its verdict, trace and
// picture like any other. They come with IzzyBlazor Pro.
//
//   target-size                  axe's own rule, off unless asked for: on in a run with sweeps. axe measures a
//                                control's box, so one it finds small is clicked around its center: a hit
//                                area larger than the box (a ::after stretched over it) makes it a false positive.
//   focus-not-obscured           a link or button under a bar that stays in place is given focus.
//   focus-visible-forced-colors  a picture of each kind of control before and after it takes focus.
//   forced-colors-indicator      a part of a control drawn only by a fill, which forced colors replace.
//
// What a sweep can't settle it says so: a slight change on focus and a part that disappears are for a
// person to look at, and only what was measured outright is real.
static partial class Audit
{
    /// <summary>The scheme of the load a run with sweeps adds for each address.</summary>
    public const string Forced = "forced colors";

    /// <summary>The checks a run with sweeps adds, by their rules.</summary>
    static readonly (string Id, bool Own)[] Sweeps = [("target-size", false), ("focus-not-obscured", true), ("focus-visible-forced-colors", true), ("forced-colors-indicator", true)];

    // Whether a rule is one of izzy's own: a report says whose words a finding's are.
    static bool Swept(string rule) => Sweeps.Any(s => s.Own && s.Id == rule);

    static string Severity(string rule) => Swept(rule) ? "severity" : "axe severity";

    // After "with axe-core 4.x" in a report's first line.
    static string Swept(JsonObject run) => run["sweeps"] is null ? "" : " and izzy's sweeps (target size, focus under bars that stay in place, focus and indicators in forced colors)";

    static Task Media(Chromium browser, string scheme, bool forced) => browser.Send("Emulation.setEmulatedMedia", new JsonObject
    {
        ["features"] = new JsonArray(
            new JsonObject { ["name"] = "prefers-color-scheme", ["value"] = scheme },
            new JsonObject { ["name"] = "forced-colors", ["value"] = forced ? "active" : "none" },
            // Nothing moves while pictures are compared.
            new JsonObject { ["name"] = "prefers-reduced-motion", ["value"] = forced ? "reduce" : "" }),
    });

    static void Add(List<JsonNode> rules, JsonNode? answer)
    {
        if ((string?)answer is { } rule) rules.Add(JsonNode.Parse(rule)!);
    }

    // The sweeps of a page as it is audited, at each width and scheme. Bars are tried in the first scheme
    // alone: where a page scrolls to is the same in light and in dark.
    static async Task<List<JsonNode>> InPage(Chromium browser, JsonObject page, bool bars)
    {
        var rules = new List<JsonNode>();
        await browser.Evaluate(Shared);
        if (page["violations"]!.AsArray().FirstOrDefault(rule => (string)rule!["id"]! == "target-size") is { } small)
            foreach (var at in JsonNode.Parse((string)(await browser.Evaluate(Hits))!)!.AsArray().Select(i => (int)i!).Where(i => i < small["nodes"]!.AsArray().Count))
                small["nodes"]![at]!["judged"] = new JsonObject
                {
                    ["is"] = FalsePositive, ["confidence"] = "high",
                    ["why"] = "Measured: the control takes clicks across a circle of 24 pixels, through a hit area larger than its box. axe measures the box",
                };
        if (bars) Add(rules, await browser.Evaluate(Obscured));
        return rules;
    }

    // The sweeps of the load in forced colors. The page is open without them: what a fill draws is noted, then
    // they are turned on and the page is looked at again.
    static async Task<List<JsonNode>> InForcedColors(Chromium browser, string scheme)
    {
        var rules = new List<JsonNode>();
        await browser.Evaluate(Shared);
        await browser.Evaluate(Fills);
        await Media(browser, scheme, forced: true);
        await browser.Evaluate(Still);
        Add(rules, await browser.Evaluate(Erased));

        // One control of each kind is measured, the first that can be. When it fails, the others of its
        // kind are measured too (ten at most): two of one look can differ by a rule on one of them, and a
        // finding names the controls that failed, whichever came first on the page.
        var kinds = JsonNode.Parse((string)(await browser.Evaluate(Stops))!)!.AsArray().Select(members => (int)members!).ToList();
        var failed = new JsonArray();
        for (var kind = 0; kind < kinds.Count; kind++)
        {
            int measured = 0, unmeasured = 0;
            var failing = false;
            for (var member = 0; member < kinds[kind] && measured < (failing ? 10 : 1) && unmeasured < 3; member++)
            {
                // Around the control itself, then around what draws its ring for it (a card around its link).
                if (await Change(browser, kind, member, around: false) is not { } change)
                {
                    unmeasured++;
                    continue;
                }
                measured++;
                if (change.Score < Enough && await Change(browser, kind, member, around: true) is { } wider && wider.Score > change.Score) change = wider;
                if (change.Score >= (change.Outline ? Outlined : Enough)) continue;
                failing = true;
                failed.Add(new JsonObject { ["kind"] = kind, ["member"] = member, ["score"] = Math.Round(change.Score, 2), ["pixels"] = change.Pixels });
            }
        }
        Add(rules, await browser.Evaluate(Unseen.Replace("__FAILED__", failed.ToJsonString()).Replace("__NONE__", Nothing.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        return rules;
    }

    // A ring two pixels wide around a control scores about 2, a border that changes color about 1, nothing 0.
    // An outline is the browser's to draw, and forced colors keep it: the browser's own ring is a pixel wide
    // and scores about 1, so with an outline the picture only has to show that most of it is in sight.
    const double Enough = 1.5, Outlined = 0.6, Nothing = 0.2;

    // How much the picture of a control changes when it takes focus: the pixels that change strongly, against
    // the length of its edge. Null for a control that can't be measured (gone, off the window, or one the
    // browser doesn't show focus on when a script gives it).
    static async Task<(double Score, int Pixels, bool Outline)?> Change(Chromium browser, int kind, int member, bool around)
    {
        var index = $"[{kind}][{member}]";
        if ((string?)await browser.Evaluate(Box.Replace("__I__", index).Replace("__AROUND__", around ? "true" : "false")) is not { } answer) return null;
        var box = JsonNode.Parse(answer)!;
        if ((bool?)box["appears"] == true) return (Enough, 0, true);
        var clip = new JsonObject { ["x"] = (int)box["x"]!, ["y"] = (int)box["y"]!, ["width"] = (int)box["width"]!, ["height"] = (int)box["height"]!, ["scale"] = 1 };
        async Task<Png?> Shot() => (string?)(await browser.Send("Page.captureScreenshot", new JsonObject { ["format"] = "png", ["clip"] = clip.DeepClone(), ["captureBeyondViewport"] = false }))["data"] is { Length: > 0 } data ? Png.Read(Convert.FromBase64String(data)) : null;
        var before = await Shot();
        var focus = JsonNode.Parse((string)(await browser.Evaluate(Focus.Replace("__I__", index)))!)!;
        var shown = (bool)focus["shown"]!;
        var after = await Shot();
        if (!shown || before is null || after is null || before.Width != after.Width || before.Height != after.Height) return null;
        var pixels = before.Differing(after);
        return (pixels / Math.Max(1, (double)box["edge"]!), pixels, (bool)focus["outline"]!);
    }

    /// <summary>A picture the browser took, as its pixels: eight bits a channel, as the browser writes them.</summary>
    sealed class Png(int width, int height, int channels, byte[] pixels)
    {
        public int Width => width;
        public int Height => height;

        /// <summary>The pixels of a PNG, or null for one written another way than the browser's.</summary>
        public static Png? Read(byte[] file)
        {
            if (file.Length < 33 || file[0] != 0x89 || file[1] != 'P') return null;
            int width = 0, height = 0, channels = 0;
            using var packed = new MemoryStream();
            for (var at = 8; at + 12 <= file.Length;)
            {
                var length = (file[at] << 24) | (file[at + 1] << 16) | (file[at + 2] << 8) | file[at + 3];
                var type = System.Text.Encoding.ASCII.GetString(file, at + 4, 4);
                var data = at + 8;
                if (length < 0 || length > file.Length - data) return null;
                if (type == "IHDR")
                {
                    width = (file[data] << 24) | (file[data + 1] << 16) | (file[data + 2] << 8) | file[data + 3];
                    height = (file[data + 4] << 24) | (file[data + 5] << 16) | (file[data + 6] << 8) | file[data + 7];
                    // Eight bits, RGB or RGBA, not interlaced.
                    channels = file[data + 9] switch { 2 => 3, 6 => 4, _ => 0 };
                    if (file[data + 8] != 8 || channels == 0 || file[data + 12] != 0) return null;
                }
                else if (type == "IDAT") packed.Write(file, data, length);
                else if (type == "IEND") break;
                at = data + length + 4;
            }
            if (width <= 0 || height <= 0 || (long)width * height > 40_000_000) return null;

            var stride = width * channels;
            var raw = new byte[(stride + 1) * height];
            packed.Position = 0;
            try
            {
                using var zlib = new ZLibStream(packed, CompressionMode.Decompress);
                zlib.ReadExactly(raw);
            }
            catch (Exception e) when (e is InvalidDataException or EndOfStreamException) { return null; }

            // Each row says how it was written: as it is, or as its difference from the pixel before, the row above, or both.
            var pixels = new byte[stride * height];
            for (var y = 0; y < height; y++)
            {
                var filter = raw[y * (stride + 1)];
                var from = y * (stride + 1) + 1;
                var row = y * stride;
                for (var x = 0; x < stride; x++)
                {
                    int left = x >= channels ? pixels[row + x - channels] : 0, up = y > 0 ? pixels[row - stride + x] : 0, corner = x >= channels && y > 0 ? pixels[row - stride + x - channels] : 0;
                    var guess = filter switch
                    {
                        0 => 0,
                        1 => left,
                        2 => up,
                        3 => (left + up) / 2,
                        4 => Math.Abs(up - corner) <= Math.Abs(left - corner) && Math.Abs(up - corner) <= Math.Abs(left + up - 2 * corner)
                            ? left : Math.Abs(left - corner) <= Math.Abs(left + up - 2 * corner) ? up : corner,
                        _ => -1,
                    };
                    if (guess < 0) return null;
                    pixels[row + x] = (byte)(raw[from + x] + guess);
                }
            }
            return new Png(width, height, channels, pixels);
        }

        /// <summary>How many pixels differ strongly from <paramref name="other"/>'s: a border that turns a shade darker doesn't count.</summary>
        public int Differing(Png other)
        {
            var count = 0;
            for (var i = 0; i < width * height; i++)
            {
                int a = i * channels, b = i * other.Channels;
                if (Math.Abs(pixels[a] - other.Pixels[b]) + Math.Abs(pixels[a + 1] - other.Pixels[b + 1]) + Math.Abs(pixels[a + 2] - other.Pixels[b + 2]) > 120) count++;
            }
            return count;
        }

        int Channels => channels;
        byte[] Pixels => pixels;
    }

    // What the sweeps share, in the page.
    const string Shared = """
        window.__izzyS = (() => {
          const frames = () => new Promise(done => { requestAnimationFrame(() => requestAnimationFrame(done)); setTimeout(done, 250); });
          const shown = (e) => {
            try { if (e.checkVisibility && !e.checkVisibility({ checkOpacity: true, checkVisibilityCSS: true })) return false; } catch { }
            const r = e.getBoundingClientRect();
            return r.width > 0 && r.height > 0;
          };
          // One of each kind is looked at: a page's forty buttons of one look are one question.
          const kind = (e) => [e.localName, e.getAttribute("type") || "", e.getAttribute("role") || "", [...e.classList].sort().join(".")].join("|");
          // What takes focus and has a place on the page. An input drawn by its label is often there at no
          // opacity: it takes focus all the same.
          const placed = (e) => {
            try { if (e.checkVisibility && !e.checkVisibility({ checkVisibilityCSS: true })) return false; } catch { }
            const r = e.getBoundingClientRect();
            return r.width > 0 && r.height > 0;
          };
          const closed = (e) => { try { return !!e.closest("[inert], [hidden], dialog:not([open]), [popover]:not(:popover-open)"); } catch { return !!e.closest("[inert], [hidden], dialog:not([open])"); } };
          const stops = (root) => [...root.querySelectorAll('a[href], button:not([disabled]), input:not([type=hidden]):not([disabled]), select:not([disabled]), textarea:not([disabled]), summary, [tabindex]:not([tabindex="-1"])')]
            .filter(e => e.tabIndex >= 0 && !closed(e) && placed(e));
          const named = (e) => {
            const text = (e.getAttribute("aria-label") || e.textContent || e.value || e.getAttribute("placeholder") || "").replace(/\s+/g, " ").trim().slice(0, 40);
            return "<" + e.localName + ">" + (text ? " “" + text + "”" : "");
          };
          // Any color the browser knows, as red, green, blue and alpha.
          const canvas = document.createElement("canvas");
          canvas.width = canvas.height = 1;
          const ink = canvas.getContext("2d", { willReadFrequently: true });
          const rgba = (color) => {
            ink.clearRect(0, 0, 1, 1);
            ink.fillStyle = "#000";
            ink.fillStyle = color;
            ink.fillRect(0, 0, 1, 1);
            const d = ink.getImageData(0, 0, 1, 1).data;
            return [d[0], d[1], d[2], d[3] / 255];
          };
          const over = (c, back) => [0, 1, 2].map(i => Math.round(c[i] * c[3] + back[i] * (1 - c[3]))).concat(1);
          const light = (c) => [0, 1, 2].map(i => c[i] / 255).map(v => v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4).reduce((sum, v, i) => sum + v * [0.2126, 0.7152, 0.0722][i], 0);
          const differs = (a, b) => {
            const x = light(a), y = light(b);
            return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05) >= 1.2 || [0, 1, 2].some(i => Math.abs(a[i] - b[i]) >= 48);
          };
          // What is behind an element: the nearest fill around it, else the page's own.
          const behind = (e) => {
            for (let p = e.parentElement; p; p = p.parentElement) {
              const c = rgba(getComputedStyle(p).backgroundColor);
              if (c[3] >= 0.5) return over(c, [255, 255, 255, 1]);
            }
            const probe = document.createElement("div");
            probe.style.cssText = "position: fixed; width: 0; height: 0; background-color: Canvas;";
            document.documentElement.appendChild(probe);
            const c = rgba(getComputedStyle(probe).backgroundColor);
            probe.remove();
            return c;
          };
          // What draws a box: "edge" for a border, an outline or a picture, "fill" for a color alone, null for nothing.
          const box = (style, back) => {
            if (style.backgroundImage && style.backgroundImage !== "none") return "edge";
            for (const side of ["Top", "Right", "Bottom", "Left"])
              if (parseFloat(style["border" + side + "Width"]) > 0 && !["none", "hidden"].includes(style["border" + side + "Style"]) && differs(over(rgba(style["border" + side + "Color"]), back), back)) return "edge";
            if (style.outlineStyle !== "none" && parseFloat(style.outlineWidth) > 0 && differs(over(rgba(style.outlineColor), back), back)) return "edge";
            const fill = rgba(style.backgroundColor);
            return fill[3] > 0.15 && differs(over(fill, back), back) ? "fill" : null;
          };
          const drawn = (e) => {
            const back = behind(e);
            const parts = [box(getComputedStyle(e), back)];
            for (const pseudo of ["::before", "::after"]) {
              const style = getComputedStyle(e, pseudo);
              if (style.content !== "none" && style.content !== "normal") parts.push(box(style, back));
            }
            return parts.includes("edge") ? "edge" : parts.includes("fill") ? "fill" : null;
          };
          // What has an outline near a control: itself, what it is inside, and what is beside it (four levels
          // up, 600 elements). A part stretched over something (a card's link) counts as that thing. What is
          // there with focus and wasn't without is the control's ring, wherever the page draws it: on the
          // control, on a wrapper, or on a button beside a hidden input.
          const ringed = (style) => style.outlineStyle !== "none" && parseFloat(style.outlineWidth) > 0;
          const outlines = (e) => {
            const found = new Set();
            const look = (p) => {
              if (ringed(getComputedStyle(p))) found.add(p);
              for (const pseudo of ["::before", "::after"]) {
                const style = getComputedStyle(p, pseudo);
                if (style.content !== "none" && style.content !== "normal" && ringed(style)) found.add((p.offsetParent && p.offsetParent !== document.body ? p.offsetParent : p.parentElement) || p);
              }
            };
            let scope = e;
            for (let p = e, up = 0; p && p !== document.documentElement; p = p.parentElement, up++) {
              look(p);
              if (up < 4 && p !== document.body) scope = p;
            }
            let count = 0;
            for (const near of scope.querySelectorAll("*")) {
              if (++count > 600) break;
              look(near);
            }
            return found;
          };
          return { frames, shown, kind, stops, named, drawn, rgba, outlines };
        })();
        """;

    // WCAG 2.5.8, after axe: which of the controls axe found small take a click anywhere on a circle of
    // 24 pixels around their center, or around a point up to eight pixels from it (a hit area that grows to one side:
    // one of two handles that touch). Those have the room the criterion asks for. The button inside a
    // select the page styles is the select's own part: a click anywhere on the select is a click on it.
    const string Hits = """
        (async () => {
          const rule = (window.__izzyRules || []).indexOf("target-size");
          const hit = [], [left, top] = [scrollX, scrollY];
          for (const [i, e] of ((window.__izzyFound || [])[rule] || []).entries()) {
            if (!e || !e.isConnected || e.ownerDocument !== document) continue;
            e.scrollIntoView({ block: "center", inline: "center", behavior: "instant" });
            const whole = e.matches("select > button") ? e.parentElement : e;
            const r = whole.getBoundingClientRect(), x = r.left + r.width / 2, y = r.top + r.height / 2;
            const takes = [[0, 0], [-4, 0], [4, 0], [0, -4], [0, 4], [-8, 0], [8, 0], [0, -8], [0, 8]].some(([dx, dy]) => [0, 1, 2, 3, 4, 5, 6, 7].every(step => {
              const over = document.elementFromPoint(x + dx + 11.5 * Math.cos(step * Math.PI / 4), y + dy + 11.5 * Math.sin(step * Math.PI / 4));
              return !!over && whole.contains(over);
            }));
            if (takes) hit.push(i);
          }
          scrollTo({ left, top, behavior: "instant" });
          return JSON.stringify(hit);
        })()
        """;

    // WCAG 2.4.11: a control that takes focus under a bar that stays in place (a header, a bar of actions at the
    // bottom) is not left wholly hidden. A link or button is scrolled under the bar's middle and given focus,
    // as Shift+Tab from below gives it: a page that reserves the bar's room (scroll-padding, scroll-margin)
    // scrolls it out. One finding for each bar.
    const string Obscured = """
        (async () => {
          const S = window.__izzyS;
          const root = document.scrollingElement || document.documentElement;
          const scrollerOf = (e) => {
            for (let p = e.parentElement; p && p !== document.body && p !== document.documentElement; p = p.parentElement) {
              const flow = getComputedStyle(p).overflowY;
              if ((flow === "auto" || flow === "scroll") && p.scrollHeight > p.clientHeight + 1) return p;
            }
            return root;
          };
          const view = (s) => {
            if (s === root) return { top: 0, bottom: innerHeight, width: innerWidth };
            const r = s.getBoundingClientRect();
            return { top: r.top + s.clientTop, bottom: r.top + s.clientTop + s.clientHeight, width: s.clientWidth };
          };
          const stays = (e) => ["sticky", "fixed"].includes(getComputedStyle(e).position);
          const inBar = (e) => { for (let p = e; p && p !== document.documentElement; p = p.parentElement) if (stays(p)) return p; return null; };
          const bars = [];
          for (const e of document.querySelectorAll("body *")) {
            if (bars.length >= 6) break;
            if (!stays(e) || (e.parentElement && inBar(e.parentElement)) || !S.shown(e)) continue;
            const scroller = getComputedStyle(e).position === "fixed" ? root : scrollerOf(e);
            const r = e.getBoundingClientRect(), v = view(scroller);
            if (r.width >= v.width * 0.5 && r.height >= 16 && r.height <= (v.bottom - v.top) * 0.4) bars.push({ bar: e, scroller });
          }
          if (!bars.length) return null;

          const kept = new Map();
          // Wholly under the bar: inside its box, with the bar on top at its middle. "solid" when the bar
          // has a fill there; a bar that is see-through leaves what is under it in sight.
          const covered = (bar, t) => {
            const b = bar.getBoundingClientRect(), r = t.getBoundingClientRect();
            if (r.top < b.top - 0.5 || r.bottom > b.bottom + 0.5 || r.left < b.left - 0.5 || r.right > b.right + 0.5) return null;
            const at = document.elementFromPoint(Math.min(Math.max(r.left + r.width / 2, 1), innerWidth - 1), Math.min(Math.max(r.top + r.height / 2, 1), innerHeight - 1));
            if (!at || !bar.contains(at)) return null;
            for (let e = at; e; e = e === bar ? null : e.parentElement) {
              const style = getComputedStyle(e);
              if (S.rgba(style.backgroundColor)[3] >= 0.9 || style.backgroundImage !== "none") return "solid";
            }
            return "clear";
          };
          const items = [];
          for (const { bar, scroller } of bars) {
            if (!kept.has(scroller)) kept.set(scroller, scroller.scrollTop);
            const height = bar.getBoundingClientRect().height;
            const all = S.stops(scroller === root ? document : scroller).filter(t => t.matches("a[href], button") && !inBar(t) && scrollerOf(t) === scroller && t.getBoundingClientRect().height <= height - 2);
            // A few from down the page, where there is room to scroll.
            const tried = [...new Set([0.5, 0.75, 0.25, 1].map(part => all[Math.min(all.length - 1, Math.floor(all.length * part))]))].filter(Boolean);
            let passed = 0;
            for (const t of tried) {
              if (passed >= 2) break;
              for (let turn = 0; turn < 2; turn++) {
                const b = bar.getBoundingClientRect(), r = t.getBoundingClientRect();
                scroller.scrollTo({ top: scroller.scrollTop + r.top - (b.top + (b.height - r.height) / 2), behavior: "instant" });
                await S.frames();
              }
              if (!covered(bar, t)) continue;
              t.focus();
              await S.frames();
              // A page that scrolls smoothly is still on its way.
              if (getComputedStyle(scroller === root ? document.documentElement : scroller).scrollBehavior === "smooth") await new Promise(done => setTimeout(done, 600));
              if (document.activeElement !== t) continue;
              // The page's own script may be scrolling it out: it gets half a second more.
              if (covered(bar, t)) await new Promise(done => setTimeout(done, 500));
              const under = covered(bar, t);
              if (!under) { passed++; continue; }
              const b = bar.getBoundingClientRect(), v = view(scroller);
              const edge = b.top - v.top <= v.bottom - b.bottom ? "top" : "bottom";
              // At the page's end there is nowhere to scroll to: the page needs room, not a scroll rule.
              const ended = edge === "top" ? scroller.scrollTop <= 0 : scroller.scrollTop + scroller.clientHeight >= scroller.scrollHeight - 1;
              items.push({
                element: bar,
                summary: ended
                  ? `${S.named(t)} stays hidden under this bar when it takes focus, and the page can't scroll further. Give the page room at its ${edge === "top" ? "start" : "end"}: padding-${edge} of the bar's height (${Math.ceil(b.height)}px) on what scrolls.`
                  : `${S.named(t)} stays hidden under this bar when it takes focus: the page doesn't scroll it out. Reserve the bar's room: scroll-padding-${edge}: ${Math.ceil(b.height)}px on what scrolls, or scroll-margin-${edge} on what takes focus.`,
                judged: under === "solid"
                  ? { is: "real", why: "A link or button that takes focus under a bar that stays in place is left hidden: reserve the bar's room with scroll-padding or scroll-margin", confidence: "high" }
                  : { is: "needs a person", why: "A link or button that takes focus is left under a bar that stays in place, and the bar has no solid fill. If it can't be seen through the bar, reserve the bar's room with scroll-padding or scroll-margin" },
              });
              break;
            }
          }
          if (document.activeElement && document.activeElement !== document.body) document.activeElement.blur();
          for (const [scroller, top] of kept) scroller.scrollTo({ top, behavior: "instant" });
          await S.frames();
          return window.__izzyRule({
            id: "focus-not-obscured", impact: "serious", help: "A control that takes focus must not be hidden under a bar that stays in place",
            helpUrl: "https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html", tags: ["wcag22aa", "wcag2411"],
          }, items);
        })()
        """;

    // WCAG 1.4.11, first half: before forced colors, the parts inside controls and meters that only a fill
    // draws (a progress bar's value, a slider's track, a carousel's dot). Forced colors replace fills.
    const string Fills = """
        (() => {
          const S = window.__izzyS;
          const within = 'progress, meter, [role=progressbar], [role=meter], [role=slider], [role=scrollbar], [role=switch], [role=checkbox], [role=radio], [role=tab], [role=option], [role=menuitemcheckbox], [role=menuitemradio], [aria-current]:not([aria-current="false"]), button, a[href], label';
          const never = "img, svg, canvas, video, picture, iframe, input, select, textarea, progress, meter, hr, br, script, style, template";
          const found = [];
          for (const e of document.querySelectorAll("body *")) {
            if (found.length >= 200) break;
            if (e.matches(never) || e.closest("svg") || !e.closest(within)) continue;
            if ((e.textContent || "").trim() || e.querySelector("img, svg, canvas, video, input")) continue;
            const r = e.getBoundingClientRect();
            if (r.width < 2 || r.height < 2 || r.width * r.height < 16 || !S.shown(e) || S.drawn(e) !== "fill") continue;
            found.push({ element: e, width: Math.round(r.width), height: Math.round(r.height) });
          }
          window.__izzyFills = found;
          return found.length;
        })()
        """;

    // In forced colors: nothing blinks or moves while pictures are compared.
    const string Still = """
        (async () => {
          // Animations end at once, where they would have ended: what one brings into view stays in view.
          const css = "*, *::before, *::after { caret-color: transparent !important; transition-duration: 0s !important; transition-delay: 0s !important; animation-duration: 0s !important; animation-delay: 0s !important; animation-iteration-count: 1 !important; scroll-behavior: auto !important; }";
          try {
            const sheet = new CSSStyleSheet();
            sheet.replaceSync(css);
            document.adoptedStyleSheets = [...document.adoptedStyleSheets, sheet];
          } catch {
            const style = document.createElement("style");
            style.textContent = css;
            document.documentElement.appendChild(style);
          }
          await window.__izzyS.frames();
        })()
        """;

    // WCAG 1.4.11, second half: in forced colors, the parts a fill drew that nothing draws now.
    const string Erased = """
        (() => {
          const S = window.__izzyS;
          const items = [];
          for (const { element, width, height } of window.__izzyFills || []) {
            if (!element.isConnected || !S.shown(element) || S.drawn(element)) continue;
            items.push({
              element,
              summary: `In forced colors nothing draws this part (${width}×${height}px): its fill is replaced by the page's background, and it has no border.`,
              judged: { is: "needs a person", why: "Only a fill draws this part, and forced colors replace fills with the page's background. If it shows a value or a state, give it a border (a transparent one is drawn in forced colors) or a system color in a forced-colors media query; if it is decoration, accept it" },
            });
          }
          return window.__izzyRule({
            id: "forced-colors-indicator", impact: "moderate", help: "A part that shows a value or a state must show in forced colors",
            helpUrl: "https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html", tags: ["wcag21aa", "wcag1411"],
          }, items);
        })()
        """;

    // WCAG 2.4.7 in forced colors: what a person reaches with Tab, by kind (at most 40 kinds), one of each
    // group of radios. Answers with how many there are of each kind.
    const string Stops = """
        (() => {
          const S = window.__izzyS;
          const kinds = new Map(), groups = new Set();
          for (const e of S.stops(document)) {
            if (e.type === "radio") {
              const group = (e.form ? e.form.id : "") + ":" + e.name;
              if (groups.has(group)) continue;
              groups.add(group);
            }
            const kind = S.kind(e);
            if (!kinds.has(kind) && kinds.size >= 40) continue;
            if (!kinds.has(kind)) kinds.set(kind, []);
            kinds.get(kind).push(e);
          }
          window.__izzyStops = [...kinds.values()];
          return JSON.stringify(window.__izzyStops.map(members => members.length));
        })()
        """;

    // Where to take a control's picture: itself with some of the page around it, without focus. Around: what
    // gets an outline when it takes focus, other than itself, or null.
    const string Box = """
        (async () => {
          const S = window.__izzyS, e = window.__izzyStops__I__;
          if (!e || !e.isConnected) return null;
          if (document.activeElement && document.activeElement !== document.body) document.activeElement.blur();
          e.scrollIntoView({ block: "center", inline: "nearest", behavior: "instant" });
          await S.frames();
          const before = window.__izzyBefore = S.outlines(e);
          let subject = e;
          if (__AROUND__) {
            e.focus({ preventScroll: true });
            await S.frames();
            subject = [...S.outlines(e)].find(ring => ring !== e && !before.has(ring));
            e.blur();
            if (!subject) return null;
          }
          // A control out of sight until it has focus (a skip link, a button for the keyboard alone) is
          // where it is drawn with focus: appearing there is how its focus shows.
          let shown = null;
          window.__izzyKept = null;
          if (subject === e && e.getBoundingClientRect().width < 6) {
            // Scrolled to as Tab scrolls to it: in a box that scrolls it may be drawn below what shows.
            e.focus();
            await S.frames();
            if (e.getBoundingClientRect().width >= 6) {
              shown = e.getBoundingClientRect();
              // Drawn with focus and in sight (its middle is its own): appearing is how its focus shows.
              // No pictures: giving it focus scrolled the page as a person would, and a page that
              // loads more at its end may be changing under them.
              const over = document.elementFromPoint(shown.left + shown.width / 2, shown.top + shown.height / 2);
              if (over && e.contains(over) && e.matches(":focus-visible")) {
                e.blur();
                return JSON.stringify({ appears: true });
              }
              // Without focus it takes no room, and a box scrolled to its end scrolls back: where each
              // was is kept, for the picture with focus.
              const boxes = [];
              for (let p = e.parentElement; p; p = p.parentElement) if (p.scrollTop || p.scrollLeft) boxes.push([p, p.scrollLeft, p.scrollTop]);
              window.__izzyKept = { e, boxes, x: scrollX, y: scrollY };
            }
            e.blur();
            if (shown) scrollTo({ left: window.__izzyKept.x, top: window.__izzyKept.y, behavior: "instant" });
          }
          // An input drawn by its label (a radio a pixel wide) is where the label is.
          while (!shown && subject.getBoundingClientRect().width < 6 && subject.parentElement) subject = subject.parentElement;
          await S.frames();
          const r = shown || subject.getBoundingClientRect(), pad = 8;
          const left = Math.max(0, Math.floor(r.left - pad)), top = Math.max(0, Math.floor(r.top - pad));
          const right = Math.min(innerWidth, Math.ceil(r.right + pad)), bottom = Math.min(innerHeight, Math.ceil(r.bottom + pad));
          if (right - left < 4 || bottom - top < 4) return null;
          const across = Math.min(r.right, right) - Math.max(r.left, left), down = Math.min(r.bottom, bottom) - Math.max(r.top, top);
          return JSON.stringify({
            x: left + Math.round(scrollX), y: top + Math.round(scrollY), width: right - left, height: bottom - top,
            // A link that runs over two lines has a ring around its lines.
            // The browser may draw them as one outline: the shortest it can be is what counts.
            edge: subject.getClientRects().length > 1 ? 2 * (Math.max(...[...subject.getClientRects()].map(line => line.width)) + [...subject.getClientRects()].reduce((sum, line) => sum + line.height, 0))
              // Of a box larger than the window, the sides that are in the picture.
              : (r.top >= top ? 1 : 0) * across + (r.bottom <= bottom ? 1 : 0) * across + (r.left >= left ? 1 : 0) * down + (r.right <= right ? 1 : 0) * down,
          });
        })()
        """;

    // Gives the control focus; whether the browser shows focus on it (a script's focus counts as the keyboard's
    // while no mouse has been used), and whether an outline is drawn for it.
    const string Focus = """
        (async () => {
          const e = window.__izzyStops__I__;
          if (!e || !e.isConnected) return JSON.stringify({ shown: false, outline: false });
          e.focus({ preventScroll: true });
          const kept = window.__izzyKept;
          if (kept && kept.e === e) {
            for (const [box, left, top] of kept.boxes) box.scrollTo({ left, top, behavior: "instant" });
            scrollTo({ left: kept.x, top: kept.y, behavior: "instant" });
          }
          await window.__izzyS.frames();
          return JSON.stringify({ shown: document.activeElement === e && e.matches(":focus-visible"), outline: [...window.__izzyS.outlines(e)].some(ring => !(window.__izzyBefore || new Set()).has(ring)) });
        })()
        """;

    const string Unseen = """
        (() => {
          if (document.activeElement && document.activeElement !== document.body) document.activeElement.blur();
          const kinds = window.__izzyStops || [];
          const items = __FAILED__.map(f => ({ f, element: (kinds[f.kind] || [])[f.member] })).filter(({ element }) => element && element.isConnected).map(({ f, element }) => f.score < __NONE__
            ? {
              element,
              summary: "In forced colors nothing changes when this control takes focus. A focus look made of a border color, a fill or a box-shadow is replaced there: add an outline (outline: 2px solid transparent is drawn in forced colors).",
              judged: { is: "real", why: "Measured: nothing around the control changes when it takes focus in forced colors, so a keyboard user can't see where they are" },
            }
            : {
              element,
              summary: `In forced colors little changes when this control takes focus: ${f.pixels} pixels, ${f.score} for each pixel of its edge, where a ring two pixels wide gives about 2.`,
              judged: { is: "needs a person", why: "Measured: the control changes only slightly when it takes focus in forced colors (a border that changes color, or a ring that is mostly cut off). Look at it with focus, then give it an outline or accept it" },
            });
          return window.__izzyRule({
            id: "focus-visible-forced-colors", impact: "serious", help: "Focus must show in forced colors",
            helpUrl: "https://www.w3.org/WAI/WCAG22/Understanding/focus-visible.html", tags: ["wcag2aa", "wcag247"],
          }, items);
        })()
        """;
}
