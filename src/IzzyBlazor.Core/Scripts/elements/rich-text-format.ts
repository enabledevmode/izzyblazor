/**
 * The rich text editor's layout formats, loaded (as elements/parts/rich-text-format.js) only by
 * editors that offer them, or on the first alignment shortcut: alignment of blocks, and a span's
 * color, highlight, size or font. What they write is in RichTextSanitizer's list: the alignment
 * and span classes, and a font from the editor's own list (kept as data-ib-font while editing).
 */

// Blocks that can be aligned, innermost first when nested (a paragraph in a list item).
const alignable = "p,h1,h2,h3,h4,h5,h6,li,blockquote,td,th";

// The class each span format writes; font is a style.
const prefixes: Record<string, string> = { color: "ib-color-", highlight: "ib-highlight-", size: "ib-size-" };

/** The nearest element around the selection matching the selector, inside the text. */
function around(surface: HTMLElement, selector: string): HTMLElement | null {
  const selection = getSelection();
  let node: Node | null = selection?.rangeCount ? selection.getRangeAt(0).commonAncestorContainer : null;
  if (!node || !surface.contains(node)) return null;
  if (!(node instanceof Element)) node = node.parentElement;
  const found = (node as Element | null)?.closest<HTMLElement>(selector);
  return found && surface.contains(found) && found !== surface ? found : null;
}

/** Aligns the blocks in the selection: start (no class), center or end. */
export function align(surface: HTMLElement, value: string): void {
  const selection = getSelection();
  if (!selection?.rangeCount) return;
  const range = selection.getRangeAt(0);
  let targets = [...surface.querySelectorAll<HTMLElement>(alignable)].filter((b) => range.intersectsNode(b) && !b.querySelector(alignable));
  if (!targets.length) targets = [around(surface, alignable)].filter((b): b is HTMLElement => !!b);
  for (const block of targets) {
    block.classList.remove("ib-align-center", "ib-align-end");
    if (value !== "start") block.classList.add(`ib-align-${value}`);
    if (!block.classList.length) block.removeAttribute("class");
  }
}

/** The alignment at the caret. */
export function alignment(surface: HTMLElement): string {
  const block = around(surface, alignable);
  return block?.classList.contains("ib-align-center") ? "center" : block?.classList.contains("ib-align-end") ? "end" : "start";
}

function hasFormat(span: HTMLElement, format: string): boolean {
  return format === "font" ? !!span.dataset.ibFont : [...span.classList].some((c) => c.startsWith(prefixes[format]));
}

/** Takes a format off a span; one left with nothing is only its content. */
function dropFormat(span: HTMLElement, format: string): void {
  if (format === "font") {
    delete span.dataset.ibFont;
    span.style.removeProperty("font-family");
    if (!span.getAttribute("style")) span.removeAttribute("style");
  } else {
    for (const c of [...span.classList]) if (c.startsWith(prefixes[format])) span.classList.remove(c);
    if (!span.classList.length) span.removeAttribute("class");
  }
  if (!span.hasAttribute("class") && !span.dataset.ibFont) span.replaceWith(...span.childNodes);
}

/** A format's value at the caret: a color, size or font, or "" for none. */
export function formatAt(surface: HTMLElement, format: string): string {
  let node: HTMLElement | null = around(surface, "span");
  while (node && surface.contains(node)) {
    if (node.localName === "span" && hasFormat(node, format)) {
      if (format === "font") return node.dataset.ibFont!;
      return [...node.classList].find((c) => c.startsWith(prefixes[format]))!.slice(prefixes[format].length);
    }
    node = node.parentElement;
  }
  return "";
}

/** Splits a span around a mark so the mark sits outside it, the span's other parts kept. */
function lift(mark: Element, span: Element): void {
  const after = document.createRange();
  after.setStartAfter(mark);
  after.setEnd(span, span.childNodes.length);
  const tail = after.extractContents();
  const middle = document.createRange();
  middle.setStartBefore(mark);
  middle.setEnd(span, span.childNodes.length);
  const lifted = middle.extractContents();
  const last = lifted.lastChild;
  span.after(lifted);
  if (tail.textContent && last) {
    const rest = span.cloneNode(false) as Element;
    rest.append(tail);
    last.after(rest);
  }
  if (!span.textContent) span.remove();
}

/**
 * Gives the selection a color, highlight, size or font ("" takes it off). The browser marks the
 * selected text (fontName, which keeps the undo history and splits across elements); each mark
 * then leaves any span of the same format around or inside it, and becomes the new span.
 */
export function applyInline(surface: HTMLElement, format: string, value: string): void {
  const selection = getSelection();
  if (!selection?.rangeCount || selection.isCollapsed) return;
  document.execCommand("fontName", false, "ib-mark");
  const added: Node[] = [];
  for (const mark of [...surface.querySelectorAll<HTMLElement>('font[face="ib-mark"]')]) {
    for (const inner of [...mark.querySelectorAll<HTMLElement>("span")]) if (hasFormat(inner, format)) dropFormat(inner, format);
    let parent = mark.parentElement;
    while (parent && parent !== surface) {
      const next = parent.parentElement;
      if (parent.localName === "span" && hasFormat(parent, format)) lift(mark, parent);
      parent = next;
    }
    const nodes = [...mark.childNodes];
    if (value) {
      const span = document.createElement("span");
      if (format === "font") {
        span.dataset.ibFont = value;
        span.style.fontFamily = value;
      } else {
        span.classList.add(prefixes[format] + value);
      }
      span.append(...nodes);
      mark.replaceWith(span);
      added.push(span);
    } else {
      mark.replaceWith(...nodes);
      added.push(...nodes);
    }
  }
  if (added.length) {
    const range = document.createRange();
    range.setStartBefore(added[0]);
    range.setEndAfter(added[added.length - 1]);
    selection.removeAllRanges();
    selection.addRange(range);
  }
}
