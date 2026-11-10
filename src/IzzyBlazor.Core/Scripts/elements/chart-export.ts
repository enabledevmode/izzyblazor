/**
 * A chart as a PNG file (Chart Download). A chart is HTML (its labels, legend and data labels) over
 * SVG layers, so there's no one SVG to save: this copies the chart's figure with every element's
 * computed style written onto it (the theme's light-dark() colors and variables come through
 * resolved, in the scheme on screen), the fonts it uses embedded, puts the copy in an SVG
 * foreignObject and draws that into a canvas at twice the size. The buttons, tooltips and hover
 * lines are left out.
 */

const SVG = "http://www.w3.org/2000/svg";
// Left out of the image: text for screen readers only, the tools row (zoom, pause, download), Show data, tooltips, the hover line, the brush.
const LEAVE_OUT = ".ib-visually-hidden, .ib-chart-tools, .ib-chart-data, .ib-chart-tips, .ib-chart-tooltip, .ib-chart-hover, .ib-chart-brush, [data-ib-chart-download]";
// The properties that change what's drawn (longhands: a computed style has no shorthands). Reading
// every computed property of every element takes about a second for one chart; these, a fraction.
const sides = (prefix: string, suffix = "") => ["top", "right", "bottom", "left"].map((s) => `${prefix}-${s}${suffix}`);
const PROPERTIES = [
  "display", "position", "top", "right", "bottom", "left", "z-index", "float", "box-sizing",
  "width", "height", "min-width", "min-height", "max-width", "max-height", "aspect-ratio",
  ...sides("margin"), ...sides("padding"), ...sides("border", "-width"), ...sides("border", "-style"), ...sides("border", "-color"),
  "border-top-left-radius", "border-top-right-radius", "border-bottom-right-radius", "border-bottom-left-radius",
  "overflow-x", "overflow-y", "visibility", "opacity", "transform", "transform-origin", "translate", "rotate", "scale",
  "flex-direction", "flex-wrap", "flex-grow", "flex-shrink", "flex-basis", "order", "align-items", "align-self", "align-content",
  "justify-content", "justify-items", "justify-self", "row-gap", "column-gap",
  "grid-template-columns", "grid-template-rows", "grid-template-areas", "grid-auto-flow", "grid-auto-columns", "grid-auto-rows",
  "grid-row-start", "grid-row-end", "grid-column-start", "grid-column-end",
  "color", "background-color", "background-image", "background-size", "background-position", "background-repeat", "background-clip",
  "box-shadow", "outline-style", "outline-width", "outline-color", "outline-offset", "filter", "clip-path", "mix-blend-mode", "isolation",
  "mask-image", "-webkit-mask-image",
  "font-family", "font-size", "font-weight", "font-style", "font-stretch", "font-variant-numeric", "font-feature-settings",
  "line-height", "letter-spacing", "word-spacing", "text-align", "text-transform", "text-indent", "text-overflow", "white-space",
  "word-break", "overflow-wrap", "vertical-align", "direction", "writing-mode", "text-orientation", "-webkit-line-clamp", "-webkit-box-orient",
  "text-decoration-line", "text-decoration-color", "text-decoration-style", "text-decoration-thickness", "text-underline-offset",
  "text-shadow", "list-style-type", "content", "object-fit",
  "fill", "fill-opacity", "fill-rule", "stroke", "stroke-width", "stroke-opacity", "stroke-dasharray", "stroke-dashoffset",
  "stroke-linecap", "stroke-linejoin", "stroke-miterlimit", "paint-order", "vector-effect", "shape-rendering",
  "marker-start", "marker-mid", "marker-end", "stop-color", "stop-opacity", "text-anchor", "dominant-baseline", "alignment-baseline",
  // Geometry the CSS sets (a legend key's rounded corners); the rest is in attributes.
  "rx", "ry", "r",
];
// An SVG shape's geometry is in its attributes; Chrome also lists it as style.
const GEOMETRY = new Set(["width", "height", "top", "right", "bottom", "left"]);

const MARGIN = 16;

// The empty frame defaultsFor reads from, there only while a chart is copied (enhanced navigation
// would take it out of the body); what it gave is kept.
let frame: HTMLIFrameElement | undefined;
let defaults: Document | undefined;
const defaultStyles = new Map<string, Map<string, string>>();

/**
 * A press on a download button: saves its chart (the figure it's in, or the one whose id it names),
 * the button busy meanwhile so a second press doesn't make a second file.
 */
export function press(button: Element): void {
  if (button.matches(":disabled, [aria-busy=true]")) return;
  const id = button.getAttribute("data-ib-chart-download");
  const figure = id ? document.getElementById(id) : button.closest<HTMLElement>("figure");
  if (!figure) return;
  button.setAttribute("aria-busy", "true");
  downloadChart(figure)
    .catch((error: unknown) => console.error("IzzyBlazor: the chart couldn't be saved.", error))
    .finally(() => button.removeAttribute("aria-busy"));
}

/** Saves the chart in this figure as "<its title>.png". */
export async function downloadChart(figure: HTMLElement): Promise<void> {
  const link = document.createElement("a");
  link.href = await chartPng(figure);
  link.download = `${fileName(figure)}.png`;
  document.body.append(link);
  link.click();
  link.remove();
}

/** The chart in this figure as a PNG data URL, `scale` times its size on screen. */
export async function chartPng(figure: HTMLElement, scale = 2): Promise<string> {
  const box = figure.getBoundingClientRect();
  const sheet: string[] = [];
  const families = new Set<string>();
  let copy: HTMLElement;
  try {
    copy = clone(figure, null, sheet, families) as HTMLElement;
    // A chart that doesn't show its title (TitledBy, HideTitle): the image does, styled as a
    // chart's own, from a title put in the figure for as long as it takes to copy it. The room
    // kept above the chart for its buttons goes.
    const name = title(figure);
    if (name && !figure.querySelector(":scope > figcaption.ib-chart-label")) {
      const label = document.createElement("div");
      label.className = "ib-chart-label";
      label.textContent = name;
      figure.prepend(label);
      try {
        copy.prepend(clone(label, null, sheet, families)!);
      } finally {
        label.remove();
      }
      copy.style.paddingBlockStart = "0";
    }
  } finally {
    frame?.remove();
    frame = defaults = undefined;
  }
  // A margin around the chart, so the title doesn't touch the image's edge.
  copy.style.margin = `${MARGIN}px`;
  copy.style.width = `${box.width}px`;
  copy.style.height = "auto";
  // Its height without what's left out (Show data), measured off screen.
  const probe = document.createElement("div");
  probe.style.cssText = "position:fixed;top:0;left:-100000px;visibility:hidden";
  probe.append(copy);
  document.body.append(probe);
  const tall = copy.getBoundingClientRect().height;
  probe.remove();
  copy.style.height = `${tall}px`;
  const width = Math.ceil(box.width) + 2 * MARGIN;
  const height = Math.ceil(tall) + 2 * MARGIN;
  copy.setAttribute("xmlns", "http://www.w3.org/1999/xhtml");

  const fonts = await fontFaces(families, copy.textContent + sheet.join(""));
  const markup =
    `<svg xmlns="${SVG}" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}">` +
    `<style>${escapeText(fonts + sheet.join(""))}</style>` +
    `<foreignObject x="0" y="0" width="${width}" height="${height}">${new XMLSerializer().serializeToString(copy)}</foreignObject></svg>`;

  const image = new Image();
  image.src = `data:image/svg+xml;charset=utf-8,${encodeURIComponent(markup)}`;
  await image.decode();

  const canvas = document.createElement("canvas");
  canvas.width = Math.round(width * scale);
  canvas.height = Math.round(height * scale);
  const context = canvas.getContext("2d")!;
  context.fillStyle = background(figure);
  context.fillRect(0, 0, canvas.width, canvas.height);
  context.drawImage(image, 0, 0, canvas.width, canvas.height);
  // toDataURL, not toBlob: Chrome's first toBlob (and OffscreenCanvas's convertToBlob) in a page
  // takes most of a second; this, tens of milliseconds.
  return canvas.toDataURL("image/png");
}

/** A copy of this node with its computed style inline, leaving out what isn't drawn. */
function clone(node: Node, parent: Map<string, string> | null, sheet: string[], families: Set<string>): Node | null {
  if (node.nodeType === Node.TEXT_NODE) return node.cloneNode();
  if (node.nodeType !== Node.ELEMENT_NODE) return null;
  const element = node as Element;
  if (element.matches(LEAVE_OUT)) return null;
  const inDefs = element.closest("defs, symbol") !== null;
  const style = getComputedStyle(element);
  if (style.display === "none" && !inDefs) return null;
  if (element instanceof HTMLScriptElement || element instanceof HTMLStyleElement || element instanceof HTMLLinkElement) return null;

  const copy = element.cloneNode(false) as Element;
  // Custom elements stay as plain elements in the image, so a page's own definitions don't run.
  copy.removeAttribute("tabindex");
  const [text, values] = styleText(element, style, parent);
  if (text) copy.setAttribute("style", text);
  else copy.removeAttribute("style");
  families.add(style.fontFamily);

  for (const pseudo of ["::before", "::after"] as const) {
    const p = getComputedStyle(element, pseudo);
    if (p.content === "none" || p.content === "normal" || p.display === "none") continue;
    const id = `ib-x${sheet.length}`;
    copy.classList.add(id);
    sheet.push(`.${id}${pseudo}{${styleText(null, p, values, element.namespaceURI === SVG)[0]}}`);
    families.add(p.fontFamily);
  }

  for (const child of element.childNodes) {
    const c = clone(child, values, sheet, families);
    if (c) copy.append(c);
  }
  if (element instanceof HTMLCanvasElement) {
    // A canvas's drawing doesn't copy: it goes in as a picture.
    const picture = document.createElement("img");
    picture.src = element.toDataURL();
    picture.setAttribute("style", text);
    return picture;
  }
  return copy;
}

/** The properties whose values differ from the element's defaults or from its parent's. */
function styleText(element: Element | null, style: CSSStyleDeclaration, parent: Map<string, string> | null, svg = element?.namespaceURI === SVG): [string, Map<string, string>] {
  const base = defaultsFor(element);
  const shape = svg && element?.localName !== "svg";
  const values = new Map<string, string>();
  let text = "";
  for (const name of PROPERTIES) {
    if (shape && GEOMETRY.has(name)) continue;
    const value = style.getPropertyValue(name);
    values.set(name, value);
    if (value === base.get(name) && (!parent || value === parent.get(name))) continue;
    text += `${name}:${value};`;
  }
  return [text, values];
}

/** An element's computed style with no page styles, by its tag, from an empty frame. */
function defaultsFor(element: Element | null): Map<string, string> {
  const key = element ? `${element.namespaceURI}|${element.localName}` : "pseudo";
  let map = defaultStyles.get(key);
  if (map) return map;
  if (!defaults) {
    frame = document.createElement("iframe");
    frame.setAttribute("aria-hidden", "true");
    frame.style.cssText = "position:fixed;width:0;height:0;border:0;visibility:hidden";
    document.body.append(frame);
    defaults = frame.contentDocument!;
    defaults.open();
    defaults.write("<!doctype html><html><body></body></html>");
    defaults.close();
  }
  const sample = element ? defaults.createElementNS(element.namespaceURI, element.localName) : defaults.createElement("span");
  const host = element?.namespaceURI === SVG && element.localName !== "svg" ? defaults.createElementNS(SVG, "svg") : defaults.body;
  if (host !== defaults.body) defaults.body.append(host);
  host.append(sample);
  const style = defaults.defaultView!.getComputedStyle(sample);
  map = new Map();
  for (const name of PROPERTIES) map.set(name, style.getPropertyValue(name));
  (host === defaults.body ? sample : host).remove();
  defaultStyles.set(key, map);
  return map;
}

/**
 * The page's @font-face rules for these families that cover this text, their files inlined as data
 * URLs. A font split by script (latin, cyrillic, greek...) brings only the parts the chart uses.
 */
async function fontFaces(stacks: Set<string>, text: string): Promise<string> {
  const points = new Set<number>();
  for (const c of text) points.add(c.codePointAt(0)!);
  const used = new Set<string>();
  for (const stack of stacks) for (const name of stack.split(",")) used.add(name.trim().replace(/^["']|["']$/g, "").toLowerCase());
  const rules: CSSFontFaceRule[] = [];
  const walk = (list: CSSRuleList) => {
    for (const rule of list) {
      if (rule instanceof CSSFontFaceRule) {
        const family = rule.style.getPropertyValue("font-family").trim().replace(/^["']|["']$/g, "").toLowerCase();
        if (used.has(family) && covers(rule.style.getPropertyValue("unicode-range"), points)) rules.push(rule);
      } else if (rule instanceof CSSImportRule) {
        // An @import's sheet (another origin's can't be read).
        try {
          if (rule.styleSheet) walk(rule.styleSheet.cssRules);
        } catch {}
      } else if ("cssRules" in rule) walk((rule as CSSGroupingRule).cssRules);
    }
  };
  for (const sheet of document.styleSheets) {
    try {
      walk(sheet.cssRules);
    } catch {
      // Another origin's sheet can't be read; its fonts stay out (the image falls back).
    }
  }
  const faces = await Promise.all(
    rules.map(async (rule) => {
      const base = rule.parentStyleSheet?.href ?? document.baseURI;
      let text = rule.cssText;
      for (const match of text.matchAll(/url\((["']?)([^"')]+)\1\)/g)) {
        try {
          const response = await fetch(new URL(match[2], base));
          text = text.replace(match[0], `url(${await dataUrl(await response.blob())})`);
        } catch {
          return "";
        }
      }
      return text;
    })
  );
  return faces.join("");
}

/** Whether a unicode-range ("U+0-FF, U+131, U+4??") has any of these characters; none set means all. */
function covers(range: string, points: Set<number>): boolean {
  if (!range.trim()) return true;
  return range.split(",").some((part) => {
    const [from, to] = part.trim().replace(/^u\+/i, "").split("-");
    const low = parseInt(from.replace(/\?/g, "0"), 16);
    const high = parseInt((to ?? from).replace(/\?/g, "F"), 16);
    for (const p of points) if (p >= low && p <= high) return true;
    return false;
  });
}

function dataUrl(blob: Blob): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result as string);
    reader.onerror = () => reject(reader.error);
    reader.readAsDataURL(blob);
  });
}

/** What's behind the chart: the first background color up the page, or the scheme's canvas. */
function background(figure: Element): string {
  for (let e: Element | null = figure; e; e = e.parentElement) {
    const color = getComputedStyle(e).backgroundColor;
    if (color && color !== "transparent" && !/^rgba\(.*,\s*0\)$/.test(color)) return color;
  }
  return getComputedStyle(document.documentElement).colorScheme.includes("dark") ? "#000" : "#fff";
}

/** The chart's title: its caption, or the heading that names it (TitledBy). */
function title(figure: Element): string {
  const by = figure.getAttribute("aria-labelledby");
  return ((by ? document.getElementById(by) : figure.querySelector(":scope > figcaption"))?.textContent ?? "").replace(/\s+/g, " ").trim();
}

/** The file's name: the title, without what a file name can't have. */
function fileName(figure: Element): string {
  return title(figure).replace(/[\\/:*?"<>|]+/g, "").slice(0, 100) || "chart";
}

function escapeText(text: string): string {
  return text.replace(/&/g, "&amp;").replace(/</g, "&lt;");
}
