// Builds IzzyBlazor's browser assets.
//
//   node build/build-assets.mjs [--configuration Debug|Release] [--watch]
//
// 1. Generates the responsive style-prop CSS from STYLE_PROPS below
//    (src/IzzyBlazor.Core/Styles/_generated/*.css).
// 2. Bundles src/IzzyBlazor.Core/Styles/izzyblazor.css -> wwwroot/izzyblazor.css
//    and src/IzzyBlazor.Core/Styles/{date,carousel,chart}.css -> wwwroot/{date,carousel,chart}.css
// 3. Bundles src/IzzyBlazor.Core/Scripts/index.ts      -> wwwroot/IzzyBlazor.Core.lib.module.js
//    (Blazor loads "<AssemblyName>.lib.module.js" automatically as a JS initializer.)
//
// 4. Copies the Inter font (variable weight and optical size, every subset) from
//    @fontsource-variable/inter -> wwwroot/fonts, with its @font-face rules in
//    src/IzzyBlazor.Core/Styles/_generated/fonts.css. Browsers download only the subsets a page uses.
//
// 5. Generates the typed Lucide class from lucide-static
//    -> src/IzzyBlazor.Core/Icons/_generated/Lucide.g.cs
//    (only when the package's version changes).
//
// STYLE_PROPS must stay in sync with the ApplyResponsive calls in src/IzzyBlazor.Core.

import * as esbuild from "esbuild";
import { copyFileSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, utimesSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const lib = join(root, "src", "IzzyBlazor.Core");
const args = process.argv.slice(2);
const watch = args.includes("--watch");
const configuration = args[args.indexOf("--configuration") + 1] ?? "Debug";
const release = configuration.toLowerCase() === "release";

// Breakpoints, mobile first. Keep in sync with Core/Breakpoints.cs.
export const BREAKPOINTS = { sm: "640px", md: "768px", lg: "1024px", xl: "1280px" };

// Each entry: a modifier class (added by C# only when the prop is set, suffixed
// with the breakpoint for overrides) and the CSS property it drives from a
// non-inheriting custom property.
export const STYLE_PROPS = [
  // Shared style props (StyledComponentBase)
  { cls: "ib-px", prop: "padding-inline", v: "px" },
  { cls: "ib-py", prop: "padding-block", v: "py" },
  { cls: "ib-mx", prop: "margin-inline", v: "mx" },
  { cls: "ib-my", prop: "margin-block", v: "my" },
  // Single sides. C# uses these instead of the axis class whenever a side on that axis is set.
  { cls: "ib-pt", prop: "padding-block-start", v: "pt" },
  { cls: "ib-pb", prop: "padding-block-end", v: "pb" },
  { cls: "ib-ps", prop: "padding-inline-start", v: "ps" },
  { cls: "ib-pe", prop: "padding-inline-end", v: "pe" },
  { cls: "ib-mt", prop: "margin-block-start", v: "mt" },
  { cls: "ib-mb", prop: "margin-block-end", v: "mb" },
  { cls: "ib-ms", prop: "margin-inline-start", v: "ms" },
  { cls: "ib-me", prop: "margin-inline-end", v: "me" },
  { cls: "ib-bg", prop: "background-color", v: "bg" },
  { cls: "ib-fg", prop: "color", v: "fg" },
  // Variable names must not clash with tokens: --ib-radius-md is a token, so the
  // Radius prop uses --ib-rounded and --ib-rounded-md. The build checks this.
  { cls: "ib-radius", prop: "border-radius", v: "rounded" },
  { cls: "ib-shadow", prop: "box-shadow", v: "elevation" },
  { cls: "ib-w", prop: "width", v: "w" },
  { cls: "ib-maxw", prop: "max-width", v: "maxw" },
  { cls: "ib-minw", prop: "min-width", v: "minw" },
  { cls: "ib-h", prop: "height", v: "h" },
  { cls: "ib-maxh", prop: "max-height", v: "maxh" },
  { cls: "ib-minh", prop: "min-height", v: "minh" },
  { cls: "ib-ov", prop: "overflow", v: "ov" },
  // Borders: each side is one shorthand (width, style, color), so it touches only that side and
  // never resets a component's own borders. The style and color are BorderStyle and BorderColor at
  // the current breakpoint (--ib-bst-now and --ib-bc-now, which each ib-bst and ib-bc class sets),
  // else solid and the border token, so a width set at a larger breakpoint keeps a style or color
  // set at a smaller one. ib-bc also sets border-color, so BorderColor alone recolors an existing
  // border. ib-bst sets only the variable: border-style on every side would give a side with no
  // width the browser's 3px default. Components with their own border (Card) read the variable.
  { cls: "ib-bx", prop: "border-inline", v: "bx", tpl: "{v} var(--ib-bst-now, solid) var(--ib-bc-now, var(--ib-color-border))" },
  { cls: "ib-by", prop: "border-block", v: "by", tpl: "{v} var(--ib-bst-now, solid) var(--ib-bc-now, var(--ib-color-border))" },
  { cls: "ib-bt", prop: "border-block-start", v: "bt", tpl: "{v} var(--ib-bst-now, solid) var(--ib-bc-now, var(--ib-color-border))" },
  { cls: "ib-bb", prop: "border-block-end", v: "bb", tpl: "{v} var(--ib-bst-now, solid) var(--ib-bc-now, var(--ib-color-border))" },
  { cls: "ib-bs", prop: "border-inline-start", v: "bs", tpl: "{v} var(--ib-bst-now, solid) var(--ib-bc-now, var(--ib-color-border))" },
  { cls: "ib-be", prop: "border-inline-end", v: "be", tpl: "{v} var(--ib-bst-now, solid) var(--ib-bc-now, var(--ib-color-border))" },
  { cls: "ib-bc", prop: "border-color", v: "bc", now: "--ib-bc-now" },
  { cls: "ib-bst", v: "bst", now: "--ib-bst-now" },
  { cls: "ib-grow", prop: "flex-grow", v: "grow" },
  { cls: "ib-shrink", prop: "flex-shrink", v: "shrink" },
  { cls: "ib-basis", prop: "flex-basis", v: "basis" },
  { cls: "ib-self", prop: "align-self", v: "self" },
  // Flex container props (Stack, Row, Column)
  { cls: "ib-dir", prop: "flex-direction", v: "dir" },
  // --ib-gap-now is the gap at the current breakpoint, for components that space more than the
  // gap itself by it (Fieldset Dividers pads each row by it).
  { cls: "ib-gap", prop: "gap", v: "gap", now: "--ib-gap-now" },
  { cls: "ib-align", prop: "align-items", v: "align" },
  { cls: "ib-justify", prop: "justify-content", v: "justify" },
  { cls: "ib-wrap", prop: "flex-wrap", v: "wrap" },
  // Grid
  { cls: "ib-cols", prop: "grid-template-columns", v: "cols" },
  { cls: "ib-colspan", prop: "grid-column", v: "colspan" },
  { cls: "ib-rowspan", prop: "grid-row", v: "rowspan" },
  // Typography (Text, Heading)
  { cls: "ib-fs", prop: "font-size", v: "fs" },
  { cls: "ib-ta", prop: "text-align", v: "ta" },
];

function generateCss() {
  const bps = Object.keys(BREAKPOINTS);
  const header = "/* Generated by build/build-assets.mjs. Do not edit. */\n";

  // Non-inheriting custom properties, so a nested component never picks up
  // its parent's layout values.
  // Registering a token's name as non-inheriting would break that token everywhere,
  // so fail the build if a style-prop variable shares a name with one.
  const tokens = new Set(
    [...readFileSync(join(root, "src/IzzyBlazor.Core/Styles/tokens.css"), "utf8").matchAll(/(--ib-[a-z0-9-]+)\s*:/g)].map((m) => m[1])
  );
  let props = header;
  for (const { v } of STYLE_PROPS) {
    for (const name of [v, ...bps.map((bp) => `${v}-${bp}`)]) {
      if (tokens.has(`--ib-${name}`)) {
        throw new Error(`Style prop variable --ib-${name} clashes with a design token of the same name. Rename the prop's variable.`);
      }
      props += `@property --ib-${name} { syntax: "*"; inherits: false; }\n`;
    }
  }
  for (const { now } of STYLE_PROPS.filter((p) => p.now)) {
    if (tokens.has(now)) {
      throw new Error(`Style prop variable ${now} clashes with a design token of the same name. Rename it.`);
    }
    props += `@property ${now} { syntax: "*"; inherits: false; }\n`;
  }

  // One class per breakpoint (.ib-px, .ib-px-md...). C# adds a class only for the
  // breakpoints that have a value, so an unset base never overrides a component's
  // default, and a larger breakpoint wins by source order.
  const rules = (bp) =>
    STYLE_PROPS.map(({ cls, prop, v, tpl, now }) => {
      const suffix = bp ? `-${bp}` : "";
      const value = `var(--ib-${v}${suffix})`;
      const current = now ? `${now}: ${value}; ` : "";
      const declaration = prop ? `${prop}: ${tpl ? tpl.replace("{v}", value) : value}; ` : "";
      return `  .${cls}${suffix} { ${current}${declaration}}`;
    }).join("\n");

  let styleProps = header + "@layer izzy.utilities {\n" + rules(null) + "\n";
  for (const bp of bps) {
    styleProps += `\n  @media (min-width: ${BREAKPOINTS[bp]}) {\n${rules(bp).replace(/^/gm, "  ")}\n  }\n`;
  }
  styleProps += "}\n";

  const out = join(lib, "Styles", "_generated");
  mkdirSync(out, { recursive: true });
  writeFileSync(join(out, "properties.css"), props);
  writeFileSync(join(out, "style-props.css"), styleProps);
}

// Inter, IzzyBlazor's default body font: the "standard" files carry both the weight and the optical
// size axes, so headings get Inter Display's tighter shapes automatically.
function generateFonts() {
  const pkgDir = join(root, "node_modules", "@fontsource-variable", "inter");
  if (!existsSync(join(pkgDir, "package.json"))) {
    throw new Error("@fontsource-variable/inter isn't installed. Run `npm ci` in the repository root, then build again.");
  }
  const fonts = join(lib, "wwwroot", "fonts");
  mkdirSync(fonts, { recursive: true });
  let css = "/* Generated by build/build-assets.mjs from @fontsource-variable/inter. Do not edit.\n" +
    "   Inter is licensed under the SIL Open Font License 1.1; see THIRD-PARTY-NOTICES.md. */\n";
  for (const sheet of ["standard.css", "standard-italic.css"]) {
    const source = readFileSync(join(pkgDir, sheet), "utf8");
    for (const [, file] of source.matchAll(/url\(\.\/files\/([\w.-]+\.woff2)\)/g)) {
      copyFileSync(join(pkgDir, "files", file), join(fonts, file));
    }
    css += source.replace(/'Inter Variable'/g, "Inter").replace(/url\(\.\/files\//g, "url(fonts/");
  }
  const out = join(lib, "Styles", "_generated");
  mkdirSync(out, { recursive: true });
  writeFileSync(join(out, "fonts.css"), css);
}

const common = {
  bundle: true,
  minify: release,
  // Debug maps are inline, so no separate .map files ever land in wwwroot or the package.
  sourcemap: release ? false : "inline",
  logLevel: "info",
  legalComments: "none",
};

// The elements that load on demand, read from lazyNames in Scripts/lazy.ts so the two can't
// drift apart: each name there is built to wwwroot/elements/<name>.js.
const lazyElements = readFileSync(join(lib, "Scripts", "lazy.ts"), "utf8").match(/const lazyNames =\s*"([a-z -]+)"/)?.[1].split(" ") ?? [];
if (lazyElements.length === 0) throw new Error("No on-demand elements found in Scripts/lazy.ts.");

const builds = [
  {
    ...common,
    // No map for the CSS, even in Debug: inline, it made the stylesheet 1.1 MB, which every
    // page and every docs example frame downloads. The JS keeps its inline map.
    sourcemap: false,
    entryPoints: [join(lib, "Styles", "izzyblazor.css")],
    outfile: join(lib, "wwwroot", "izzyblazor.css"),
    // Font files are served beside the stylesheet (wwwroot/fonts), so their URLs stay as written.
    external: ["*.woff2"],
  },
  {
    // A sheet of its own, linked by the components that draw a calendar.
    ...common,
    sourcemap: false,
    entryPoints: [join(lib, "Styles", "date.css")],
    outfile: join(lib, "wwwroot", "date.css"),
  },
  {
    // A sheet of its own, linked by Carousel.
    ...common,
    sourcemap: false,
    entryPoints: [join(lib, "Styles", "carousel.css")],
    outfile: join(lib, "wwwroot", "carousel.css"),
  },
  {
    // A sheet of its own, linked by HeatmapChart beside chart.css.
    ...common,
    sourcemap: false,
    entryPoints: [join(lib, "Styles", "heatmap.css")],
    outfile: join(lib, "wwwroot", "heatmap.css"),
  },
  {
    // A sheet of its own, linked by a Chart with a RangeSelector beside chart.css.
    ...common,
    sourcemap: false,
    entryPoints: [join(lib, "Styles", "range-selector.css")],
    outfile: join(lib, "wwwroot", "range-selector.css"),
  },
  {
    // A sheet of its own, linked by Scheduler.
    ...common,
    sourcemap: false,
    entryPoints: [join(lib, "Styles", "scheduler.css")],
    outfile: join(lib, "wwwroot", "scheduler.css"),
  },
  {
    // A sheet of its own, linked by RadarChart beside chart.css.
    ...common,
    sourcemap: false,
    entryPoints: [join(lib, "Styles", "radar.css")],
    outfile: join(lib, "wwwroot", "radar.css"),
  },
  {
    // A sheet of its own, linked by SankeyChart beside chart.css.
    ...common,
    sourcemap: false,
    entryPoints: [join(lib, "Styles", "sankey.css")],
    outfile: join(lib, "wwwroot", "sankey.css"),
  },
  {
    // A sheet of its own, linked by LinearGauge, and by a Gauge with GaugePointers.
    ...common,
    sourcemap: false,
    entryPoints: [join(lib, "Styles", "gauge.css")],
    outfile: join(lib, "wwwroot", "gauge.css"),
  },
  {
    // A sheet of its own, linked by BulletChart beside chart.css.
    ...common,
    sourcemap: false,
    entryPoints: [join(lib, "Styles", "bullet.css")],
    outfile: join(lib, "wwwroot", "bullet.css"),
  },
  {
    // A sheet of its own, linked by TreemapChart beside chart.css.
    ...common,
    sourcemap: false,
    entryPoints: [join(lib, "Styles", "treemap.css")],
    outfile: join(lib, "wwwroot", "treemap.css"),
  },
  {
    // A sheet of its own, linked by ActionSheet.
    ...common,
    sourcemap: false,
    entryPoints: [join(lib, "Styles", "action-sheet.css")],
    outfile: join(lib, "wwwroot", "action-sheet.css"),
  },
  {
    // A sheet of its own, linked by Mentions and MentionText.
    ...common,
    sourcemap: false,
    entryPoints: [join(lib, "Styles", "mentions.css")],
    outfile: join(lib, "wwwroot", "mentions.css"),
  },
  {
    // A sheet of its own, linked by Comment.
    ...common,
    sourcemap: false,
    entryPoints: [join(lib, "Styles", "comment.css")],
    outfile: join(lib, "wwwroot", "comment.css"),
  },
  {
    // A sheet of its own, linked by Blockquote.
    ...common,
    sourcemap: false,
    entryPoints: [join(lib, "Styles", "blockquote.css")],
    outfile: join(lib, "wwwroot", "blockquote.css"),
  },
  {
    // A sheet of its own, linked by Chat.
    ...common,
    sourcemap: false,
    entryPoints: [join(lib, "Styles", "chat.css")],
    outfile: join(lib, "wwwroot", "chat.css"),
  },
  {
    // A sheet of its own, linked by Tabs with close or add controls.
    ...common,
    sourcemap: false,
    entryPoints: [join(lib, "Styles", "tabs-edit.css")],
    outfile: join(lib, "wwwroot", "tabs-edit.css"),
  },
  {
    // A sheet of its own, linked by a DataGrid's column picker.
    ...common,
    sourcemap: false,
    entryPoints: [join(lib, "Styles", "column-picker.css")],
    outfile: join(lib, "wwwroot", "column-picker.css"),
  },
  {
    // A sheet of its own, linked by a Table or DataGrid with column bands.
    ...common,
    sourcemap: false,
    entryPoints: [join(lib, "Styles", "bands.css")],
    outfile: join(lib, "wwwroot", "bands.css"),
  },
  {
    // A sheet of its own, linked by ColorPanel.
    ...common,
    sourcemap: false,
    entryPoints: [join(lib, "Styles", "color-panel.css")],
    outfile: join(lib, "wwwroot", "color-panel.css"),
  },
  {
    // The editor's content classes, linked by RichTextEditor and by RichText when it needs them.
    ...common,
    sourcemap: false,
    entryPoints: [join(lib, "Styles", "rich-content.css")],
    outfile: join(lib, "wwwroot", "rich-content.css"),
  },
  {
    // A sheet of its own, linked by RichTextEditor.
    ...common,
    sourcemap: false,
    entryPoints: [join(lib, "Styles", "rich-text.css")],
    outfile: join(lib, "wwwroot", "rich-text.css"),
  },
  {
    // A sheet of its own, linked by TreeSelect.
    ...common,
    sourcemap: false,
    entryPoints: [join(lib, "Styles", "treeselect.css")],
    outfile: join(lib, "wwwroot", "treeselect.css"),
  },
  {
    // A sheet of its own, linked by SpeedDial.
    ...common,
    sourcemap: false,
    entryPoints: [join(lib, "Styles", "speed-dial.css")],
    outfile: join(lib, "wwwroot", "speed-dial.css"),
  },
  {
    // A sheet of its own, linked by the charts.
    ...common,
    sourcemap: false,
    entryPoints: [join(lib, "Styles", "chart.css")],
    outfile: join(lib, "wwwroot", "chart.css"),
  },
  {
    ...common,
    entryPoints: [join(lib, "Scripts", "index.ts")],
    outfile: join(lib, "wwwroot", "IzzyBlazor.Core.lib.module.js"),
    format: "esm",
    target: "es2022",
  },
  {
    // Elements most pages don't have, one file each in wwwroot/elements, loaded on demand by
    // Scripts/lazy.ts. Each bundles its own copy of the small shared code (IzzyElement), so the
    // main module stays a single request and a page with a combobox makes two.
    ...common,
    entryPoints: Object.fromEntries(
      lazyElements.map((name) => [`elements/${name}`, join(lib, "Scripts", "elements", `${name}.ts`)])
    ),
    outdir: join(lib, "wwwroot"),
    format: "esm",
    target: "es2022",
    metafile: true,
    plugins: [
      {
        // Parts of an element it loads only when needed (elements/parts, built below): leave
        // those imports for the browser.
        name: "element-parts",
        setup(build) {
          build.onResolve({ filter: /^\.\/parts\/[a-z-]+\.js$/ }, (args) =>
            args.kind === "dynamic-import" ? { path: args.path, external: true } : undefined
          );
        },
      },
      {
        // Remove files from elements that are no longer built, after writing the new ones, so a
        // page loading during a watch rebuild never finds the folder empty.
        name: "remove-stale-elements",
        setup(build) {
          build.onEnd((result) => {
            const dir = join(lib, "wwwroot", "elements");
            if (!result.metafile || !existsSync(dir)) return;
            const built = new Set(Object.keys(result.metafile.outputs).map((file) => file.split("/").pop()));
            for (const file of readdirSync(dir)) if (file.endsWith(".js") && !built.has(file)) rmSync(join(dir, file));
          });
        },
      },
    ],
  },
  {
    // Parts an element loads only when needed: chart-draw.js, which draws a chart again without
    // the series its legend hides, loads on the first toggle.
    ...common,
    entryPoints: {
      "elements/parts/chart-draw": join(lib, "Scripts", "elements", "chart-draw.ts"),
      // Chart's Crosshair, loaded where a chart has one.
      "elements/parts/chart-cross": join(lib, "Scripts", "elements", "chart-cross.ts"),
      // HeatmapChart's tooltip, keys, legend and fitting, loaded where there's a heatmap.
      "elements/parts/chart-heat": join(lib, "Scripts", "elements", "chart-heat.ts"),
      // RadarChart's tooltip, keys and legend, loaded where there's a radar.
      "elements/parts/chart-radar": join(lib, "Scripts", "elements", "chart-radar.ts"),
      // SankeyChart's tooltip, fading and keys, loaded where there's a Sankey.
      "elements/parts/chart-sankey": join(lib, "Scripts", "elements", "chart-sankey.ts"),
      // A pie's slices drawn again without those the legend hides, loaded on the first toggle.
      "elements/parts/chart-draw-pie": join(lib, "Scripts", "elements", "chart-draw-pie.ts"),
      // PieChart's and NestedDonutChart's tooltip, keys and legend, loaded where there's one.
      "elements/parts/chart-pie": join(lib, "Scripts", "elements", "chart-pie.ts"),
      // Live data on a static page (Chart Refresh), loaded where a chart refreshes.
      "elements/parts/chart-live": join(lib, "Scripts", "elements", "chart-live.ts"),
      // Zoom's gestures (box, wheel, pinch), loaded where a chart zooms.
      "elements/parts/chart-gesture": join(lib, "Scripts", "elements", "chart-gesture.ts"),
      // Synced charts' matching by argument, loaded where a chart has a Sync name.
      "elements/parts/chart-sync": join(lib, "Scripts", "elements", "chart-sync.ts"),
      // The RangeSelector's window, handles and buttons, loaded where there's one.
      "elements/parts/chart-select": join(lib, "Scripts", "elements", "chart-select.ts"),
      // A chart saved as a PNG (Download), loaded on the first press of a download button.
      "elements/parts/chart-export": join(lib, "Scripts", "elements", "chart-export.ts"),
      // TreemapChart's tooltip and keys, loaded where there's a treemap.
      "elements/parts/chart-treemap": join(lib, "Scripts", "elements", "chart-treemap.ts"),
      // BulletChart's tooltip and keys, loaded where there's a bullet chart.
      "elements/parts/chart-bullet": join(lib, "Scripts", "elements", "chart-bullet.ts"),
      // A DataGrid's grouped-by chips moved by drag or keys, loaded on the first press or key on one.
      "elements/parts/grid-group": join(lib, "Scripts", "elements", "grid-group.ts"),
      // A TreeList's cascade of checkboxes, loaded for a tree list that has one.
      "elements/parts/grid-tree": join(lib, "Scripts", "elements", "grid-tree.ts"),
      // The tooltip for a grid's text cut off at one line.
      "elements/parts/grid-tip": join(lib, "Scripts", "elements", "grid-tip.ts"),
      // RichTextEditor's alignment and span formats, for editors that use them.
      "elements/parts/rich-text-format": join(lib, "Scripts", "elements", "rich-text-format.ts"),
      // Its link dialog, loaded when the dialog first opens.
      "elements/parts/rich-text-link": join(lib, "Scripts", "elements", "rich-text-link.ts"),
      // Its images: the image dialog, uploads, pasted and dropped files.
      "elements/parts/rich-text-image": join(lib, "Scripts", "elements", "rich-text-image.ts"),
      // Its Markdown shortcuts, for editors that have them on.
      "elements/parts/rich-text-markdown": join(lib, "Scripts", "elements", "rich-text-markdown.ts"),
      // Its tables, for editors with the table tool or a table in their text.
      "elements/parts/rich-text-table": join(lib, "Scripts", "elements", "rich-text-table.ts"),
      // Its code block language button.
      "elements/parts/rich-text-code": join(lib, "Scripts", "elements", "rich-text-code.ts"),
      // Its task lists, for editors with the task list tool or a task in their text.
      "elements/parts/rich-text-task": join(lib, "Scripts", "elements", "rich-text-task.ts"),
    },
    outdir: join(lib, "wwwroot"),
    format: "esm",
    target: "es2022",
  },
];

// Lucide -> one typed C# class, Lucide. Each icon is an expression-bodied property, so nothing is
// allocated for icons an app never uses, and a trimmed WebAssembly app keeps only the ones it
// references. Lucide draws a 2-unit stroke; IzzyBlazor draws it at 1.5 (see IconData), which gives
// 1, 1.25 and 1.5px lines at the 16, 20 and 24px control sizes.
function generateIcons() {
  const pkgDir = join(root, "node_modules", "lucide-static");
  if (!existsSync(join(pkgDir, "package.json"))) {
    throw new Error("lucide-static isn't installed. Run `npm ci` in the repository root, then build again.");
  }
  const json = (file) => JSON.parse(readFileSync(join(pkgDir, file), "utf8"));
  const pkg = json("package.json");
  const marker = `// lucide-static ${pkg.version}`;
  const generated = join(lib, "Icons", "_generated");
  const out = join(generated, "Lucide.g.cs");
  // Classes from an earlier icon set (Tabler.g.cs, Hugeicons.g.cs) would still compile in; remove them.
  if (existsSync(generated)) {
    for (const file of readdirSync(generated)) {
      if (file.endsWith(".g.cs") && file !== "Lucide.g.cs") rmSync(join(generated, file));
    }
  }
  if (existsSync(out) && readFileSync(out, "utf8").includes(marker)) {
    // Up to date: refresh the timestamp so MSBuild's incremental check sees a current output.
    const now = new Date();
    utimesSync(out, now, now);
    return;
  }

  const nodes = json("icon-nodes.json");
  const tags = json("tags.json");
  const pascal = (name) => name.split("-").map((w) => w[0].toUpperCase() + w.slice(1)).join("");
  const xmlEscape = (text) => text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
  const reserved = new Set(["Lucide", "Equals", "GetHashCode", "ToString", "GetType", "ReferenceEquals", "MemberwiseClone", "Finalize"]);

  const lines = [];
  const seen = new Set();
  for (const name of Object.keys(nodes).sort()) {
    let id = pascal(name);
    if (/^[0-9]/.test(id)) id = "_" + id;
    if (reserved.has(id)) id += "Icon";
    if (seen.has(id)) throw new Error(`Duplicate icon identifier ${id} (${name})`);
    seen.add(id);

    const body = nodes[name]
      .map(([el, attrs]) => `<${el} ${Object.entries(attrs).map(([k, v]) => `${k}="${v}"`).join(" ")}/>`)
      .join("");
    const keywords = (tags[name] ?? []).join(", ");
    lines.push(
      `    /// <summary><c>${name}</c>${keywords ? `. Also: ${xmlEscape(keywords)}` : ""}.</summary>`,
      `    public static IconData ${id} => new("0 0 24 24", ${JSON.stringify(body)}, IconStyle.Stroke, 1.5, "${name}");`,
      ""
    );
  }

  const source = `// <auto-generated />
${marker}
// Generated by build/build-assets.mjs from lucide-static. Do not edit.
// Lucide is licensed under the ISC License; see THIRD-PARTY-NOTICES.md.
#nullable enable

namespace IzzyBlazor;

/// <summary>
/// The Lucide icon set (${seen.size} icons, ISC License), bundled with IzzyBlazor and drawn with a
/// 1.5-unit stroke. Browse them at https://lucide.dev/icons.
/// </summary>
/// <example><code>&lt;Icon Name="Lucide.House" /&gt;</code></example>
public static class Lucide
{
${lines.join("\n").trimEnd()}
}
`;
  mkdirSync(dirname(out), { recursive: true });
  writeFileSync(out, source);
  console.log(`IzzyBlazor: generated ${seen.size} Lucide icons (lucide-static ${pkg.version}).`);
}

generateCss();
generateFonts();
generateIcons();

if (watch) {
  for (const options of builds) {
    const ctx = await esbuild.context(options);
    await ctx.watch();
  }
  console.log("IzzyBlazor assets: watching for changes...");
} else {
  await Promise.all(builds.map((options) => esbuild.build(options)));
}
