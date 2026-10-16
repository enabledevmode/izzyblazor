// Builds IzzyBlazor's browser assets.
//
//   node build/build-assets.mjs [--configuration Debug|Release] [--watch]
//
// 1. Generates the responsive style-prop CSS from STYLE_PROPS below
//    (src/IzzyBlazor.Core/Styles/_generated/*.css).
// 2. Bundles src/IzzyBlazor.Core/Styles/izzyblazor.css -> wwwroot/izzyblazor.css
// 3. Bundles src/IzzyBlazor.Core/Scripts/index.ts      -> wwwroot/IzzyBlazor.Core.lib.module.js
//    (Blazor loads "<AssemblyName>.lib.module.js" automatically as a JS initializer.)
//
// 4. Copies the Inter font (variable weight and optical size, every subset) from
//    @fontsource-variable/inter -> wwwroot/fonts, with its @font-face rules in
//    src/IzzyBlazor.Core/Styles/_generated/fonts.css. Browsers download only the subsets a page uses.
//
// 5. Generates the typed Hugeicons class from @hugeicons/core-free-icons
//    -> src/IzzyBlazor.Core/Icons/_generated/Hugeicons.g.cs
//    (only when the package's version changes).
//
// STYLE_PROPS must stay in sync with the ApplyResponsive calls in src/IzzyBlazor.Core.

import * as esbuild from "esbuild";
import { copyFileSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, utimesSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

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
  // Borders: each side is one shorthand (width, solid, color), so it touches only that side and
  // never resets a component's own borders. The color is BorderColor at the current breakpoint
  // (--ib-bc-now, which each ib-bc class sets), else the border token, so a width set at a larger
  // breakpoint keeps a color set at a smaller one. ib-bc also sets border-color, so BorderColor
  // alone recolors an existing border.
  { cls: "ib-bx", prop: "border-inline", v: "bx", tpl: "{v} solid var(--ib-bc-now, var(--ib-color-border))" },
  { cls: "ib-by", prop: "border-block", v: "by", tpl: "{v} solid var(--ib-bc-now, var(--ib-color-border))" },
  { cls: "ib-bt", prop: "border-block-start", v: "bt", tpl: "{v} solid var(--ib-bc-now, var(--ib-color-border))" },
  { cls: "ib-bb", prop: "border-block-end", v: "bb", tpl: "{v} solid var(--ib-bc-now, var(--ib-color-border))" },
  { cls: "ib-bs", prop: "border-inline-start", v: "bs", tpl: "{v} solid var(--ib-bc-now, var(--ib-color-border))" },
  { cls: "ib-be", prop: "border-inline-end", v: "be", tpl: "{v} solid var(--ib-bc-now, var(--ib-color-border))" },
  { cls: "ib-bc", prop: "border-color", v: "bc", now: "--ib-bc-now" },
  { cls: "ib-grow", prop: "flex-grow", v: "grow" },
  { cls: "ib-shrink", prop: "flex-shrink", v: "shrink" },
  { cls: "ib-basis", prop: "flex-basis", v: "basis" },
  // Flex container props (Stack, Row, Column)
  { cls: "ib-dir", prop: "flex-direction", v: "dir" },
  { cls: "ib-gap", prop: "gap", v: "gap" },
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
      return `  .${cls}${suffix} { ${current}${prop}: ${tpl ? tpl.replace("{v}", value) : value}; }`;
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

const builds = [
  {
    ...common,
    entryPoints: [join(lib, "Styles", "izzyblazor.css")],
    outfile: join(lib, "wwwroot", "izzyblazor.css"),
    // Font files are served beside the stylesheet (wwwroot/fonts), so their URLs stay as written.
    external: ["*.woff2"],
  },
  {
    ...common,
    entryPoints: [join(lib, "Scripts", "index.ts")],
    outfile: join(lib, "wwwroot", "IzzyBlazor.Core.lib.module.js"),
    format: "esm",
    target: "es2022",
  },
];

// Hugeicons (Stroke Rounded, the free set) -> one typed C# class, Hugeicons. Each icon is an
// expression-bodied property, so nothing is allocated for icons an app never uses, and a trimmed
// WebAssembly app keeps only the ones it references.
async function generateIcons() {
  const pkgDir = join(root, "node_modules", "@hugeicons", "core-free-icons");
  if (!existsSync(join(pkgDir, "package.json"))) {
    throw new Error("@hugeicons/core-free-icons isn't installed. Run `npm ci` in the repository root, then build again.");
  }
  const pkg = JSON.parse(readFileSync(join(pkgDir, "package.json"), "utf8"));
  const marker = `// @hugeicons/core-free-icons ${pkg.version}`;
  const generated = join(lib, "Icons", "_generated");
  const out = join(generated, "Hugeicons.g.cs");
  // Classes from an earlier icon set (Tabler.g.cs, Lucide.g.cs) would still compile in; remove them.
  if (existsSync(generated)) {
    for (const file of readdirSync(generated)) {
      if (file.endsWith(".g.cs") && file !== "Hugeicons.g.cs") rmSync(join(generated, file));
    }
  }
  if (existsSync(out) && readFileSync(out, "utf8").includes(marker)) {
    // Up to date: refresh the timestamp so MSBuild's incremental check sees a current output.
    const now = new Date();
    utimesSync(out, now, now);
    return;
  }

  // The index re-exports each icon file under its name ("Delete02Icon"), a "FreeIcons" twin and
  // sometimes other names ("Braces" for FirstBracket), which become search keywords.
  const index = join(pkgDir, "dist", "esm", "index.js");
  const icons = await import(pathToFileURL(index).href);
  const names = new Map();
  for (const [, list, file] of readFileSync(index, "utf8").matchAll(/export \{ (.+?) \} from '\.\/(\w+)Icon\.js';/g)) {
    const aliases = [...list.matchAll(/default as (\w+)/g)].map((m) => m[1])
      .filter((a) => !a.endsWith("Icon") && !a.endsWith("FreeIcons"));
    names.set(file, aliases);
  }

  // Stroke, round caps and joins and the 1.5 width are set once on the <svg> (IconData's
  // defaults), so they're dropped from each element and StrokeWidth can change them. An element
  // drawn without round caps or joins says so, since it would otherwise inherit them.
  const kebab = (k) => k.replace(/[A-Z]/g, (c) => "-" + c.toLowerCase());
  // Coordinates come with up to five decimals; two (under a hundredth of a pixel at 24px) look the
  // same and keep the assembly smaller.
  const round = (v) => String(v).replace(/-?\d*\.\d+/g, (n) => String(Number(Number(n).toFixed(2))));
  const element = ([el, attrs]) => {
    const out = [];
    for (const [k, v] of Object.entries(attrs)) {
      if (k === "key" || (k === "stroke" && v === "currentColor") || (k === "strokeWidth" && v === "1.5")) continue;
      if ((k === "strokeLinecap" || k === "strokeLinejoin") && v === "round") continue;
      out.push(`${kebab(k)}="${k === "d" || /^(c[xy]|r[xy]?|x|y|width|height)$/.test(k) ? round(v) : v}"`);
    }
    if (attrs.stroke && !attrs.strokeLinecap) out.push('stroke-linecap="butt"');
    if (attrs.stroke && !attrs.strokeLinejoin) out.push('stroke-linejoin="miter"');
    if (!attrs.stroke) out.push('stroke="none"');
    return `<${el} ${out.join(" ")}/>`;
  };
  const xmlEscape = (text) => text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
  const reserved = new Set(["Hugeicons", "Equals", "GetHashCode", "ToString", "GetType", "ReferenceEquals", "MemberwiseClone", "Finalize"]);

  const lines = [];
  for (const name of [...names.keys()].sort()) {
    const data = icons[`${name}Icon`];
    if (!Array.isArray(data)) throw new Error(`Hugeicons export ${name}Icon is missing`);
    let id = /^[0-9]/.test(name) ? "_" + name : name;
    if (reserved.has(id)) id += "Icon";
    const body = data.map(element).join("");
    const aliases = names.get(name);
    lines.push(
      `    /// <summary><c>${name}</c>${aliases.length ? `. Also: ${xmlEscape(aliases.join(", "))}` : ""}.</summary>`,
      `    public static IconData ${id} => new("0 0 24 24", ${JSON.stringify(body)}, IconStyle.Stroke, 1.5, "${name}");`,
      ""
    );
  }

  const source = `// <auto-generated />
${marker}
// Generated by build/build-assets.mjs from @hugeicons/core-free-icons. Do not edit.
// Hugeicons' free icons are licensed under the MIT License; see THIRD-PARTY-NOTICES.md.
#nullable enable

namespace IzzyBlazor;

/// <summary>
/// The free Hugeicons set, Stroke Rounded style (${names.size} icons, MIT License), bundled with
/// IzzyBlazor. Browse them at https://hugeicons.com/icons (choose Stroke Rounded and Free).
/// </summary>
/// <example><code>&lt;Icon Name="Hugeicons.Home03" /&gt;</code></example>
public static class Hugeicons
{
${lines.join("\n").trimEnd()}
}
`;
  mkdirSync(dirname(out), { recursive: true });
  writeFileSync(out, source);
  console.log(`IzzyBlazor: generated ${names.size} Hugeicons (@hugeicons/core-free-icons ${pkg.version}).`);
}

generateCss();
generateFonts();
await generateIcons();

if (watch) {
  for (const options of builds) {
    const ctx = await esbuild.context(options);
    await ctx.watch();
  }
  console.log("IzzyBlazor assets: watching for changes...");
} else {
  await Promise.all(builds.map((options) => esbuild.build(options)));
}
