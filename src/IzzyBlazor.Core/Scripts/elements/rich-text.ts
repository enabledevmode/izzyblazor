import { IzzyElement, defineElement } from "../izzy-element";

/**
 * <ib-rich-text> makes a RichTextEditor's text editable. The server renders the text (sanitized)
 * and a textarea of its HTML, the field's real control; this keeps the textarea's value current as
 * you type (an input event each time, a change event when you leave the text), so the field posts
 * in a plain form and @bind updates on interactive pages. The server sanitizes whatever arrives:
 * what's written here is only tidy, not trusted.
 *
 * Formats come from the browser's editing commands (execCommand), which keep its undo history.
 * The browser's own markup (b, i, strike, spans with styles) is written as the editor's elements
 * when the value is read, so the page's DOM is never rewritten under the caret.
 */

const isMac = /Mac|iPhone|iPad/.test(navigator.platform);

// What each element is written as; anything else gives its content only.
const names: Record<string, string> = {
  P: "p", DIV: "p", H1: "h1", H2: "h2", H3: "h3", H4: "h4", H5: "h5", H6: "h6",
  UL: "ul", OL: "ol", LI: "li", BLOCKQUOTE: "blockquote", PRE: "pre", HR: "hr",
  STRONG: "strong", B: "strong", EM: "em", I: "em", U: "u", S: "s", STRIKE: "s", DEL: "s",
  CODE: "code", A: "a", BR: "br", IMG: "img", SPAN: "span", FONT: "span", SUB: "sub", SUP: "sup",
  TABLE: "table", THEAD: "thead", TBODY: "tbody", TR: "tr", TH: "th", TD: "td",
};
const voids = new Set(["br", "hr", "img"]);
const dropped = new Set(["SCRIPT", "STYLE", "TEMPLATE", "IFRAME", "OBJECT", "EMBED", "SVG", "MATH", "NOSCRIPT", "TEXTAREA", "TITLE", "SELECT", "HEAD", "META", "LINK"]);
const blocks = "p,h1,h2,h3,h4,h5,h6,blockquote,pre,li,div";
const styles = ["p", "h1", "h2", "h3", "h4", "h5", "h6", "blockquote", "pre"];
// Alignment and span formats (color, highlight, size, font): a part, loaded only when used.
type FormatPart = typeof import("./rich-text-format");
// The link dialog's code: a part, loaded when the dialog first opens.
type LinkPart = typeof import("./rich-text-link");
// Images (the dialog, uploads, pasted and dropped files): a part, loaded when first used.
type ImagePart = typeof import("./rich-text-image");
// Markdown shortcuts: a part, loaded as an editor that has them sets up.
type MarkdownPart = typeof import("./rich-text-markdown");
// Tables: a part, loaded by editors with the table tool or a table in their text.
type TablePart = typeof import("./rich-text-table");
// A code block's language button: a part, for editors with the code block tool.
type CodePart = typeof import("./rich-text-code");
const formatTools = "align,color,highlight,size,font".split(",");

function escape(text: string, attribute = false): string {
  let out = text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
  if (attribute) out = out.replace(/"/g, "&quot;");
  return out;
}

function safeUrl(value: string, image: boolean): string | null {
  // eslint-disable-next-line no-control-regex
  const url = value.replace(/[\u0000-\u001F\u007F]/g, "").trim();
  if (!url) return null;
  const scheme = /^([a-z][a-z0-9+.-]*):/i.exec(url)?.[1].toLowerCase();
  if (!scheme) return url;
  if (scheme === "http" || scheme === "https") return url;
  if (!image && (scheme === "mailto" || scheme === "tel")) return url;
  return null;
}

// Whether images may be data: URLs (AllowDataImages), for the editor being written.
let embedImages = false;

/** Writes a node's children as the editor's HTML: the same elements the server's sanitizer keeps. */
function serialize(parent: Node, top = 1): string {
  let out = "";
  let openItem = false;
  for (const node of parent.childNodes) {
    if (node.nodeType === Node.TEXT_NODE) {
      // Whitespace between list items or table rows isn't content.
      if (/^(UL|OL|TABLE|THEAD|TBODY|TR)$/.test((parent as Element).tagName ?? "") && !node.textContent?.trim()) continue;
      out += escape((node.textContent ?? "").replace(/\u200B/g, ""));
      openItem = false;
      continue;
    }
    if (!(node instanceof Element) || dropped.has(node.tagName.toUpperCase())) continue;
    const name = level(names[node.tagName.toUpperCase()], top);
    if (!name) {
      out += serialize(node, top);
      continue;
    }

    // A browser's indent puts a list straight in a list: it belongs in the item before it.
    if ((name === "ul" || name === "ol") && openItem && out.endsWith("</li>")) {
      out = out.slice(0, -5) + element(node, name, top) + "</li>";
      continue;
    }

    out += element(node, name, top);
    openItem = name === "li";
  }
  return out;
}

/** A heading above the top level moves down to it, as the sanitizer does. */
function level(name: string | undefined, top: number): string | undefined {
  return name && /^h[1-6]$/.test(name) && Number(name[1]) < top ? `h${top}` : name;
}

function element(node: Element, name: string, top: number): string {
  let attributes = "";
  if (name === "a") {
    const href = safeUrl(node.getAttribute("href") ?? "", false);
    if (!href) return serialize(node, top);
    attributes = ` href="${escape(href, true)}"`;
    if (node.getAttribute("target") === "_blank") attributes += ` target="_blank" rel="noopener noreferrer"`;
  } else if (name === "span") {
    // Only the editor's own classes and font; a span with neither is only its content.
    const classes = [...node.classList].filter((c) => /^ib-(color|highlight|size)-[a-z0-9]+$/.test(c));
    if (classes.length) attributes = ` class="${classes.join(" ")}"`;
    const { ibFont: font, ibFontSize: size } = (node as HTMLElement).dataset;
    const style = [font && `font-family: ${font}`, size && `font-size: ${size}`].filter(Boolean).join("; ");
    if (style) attributes += ` style="${escape(style, true)}"`;
    if (!attributes) return serialize(node, top);
  } else if (/^(p|h[1-6]|li|blockquote|td|th)$/.test(name)) {
    const align = [...node.classList].find((c) => c === "ib-align-center" || c === "ib-align-end");
    if (align) attributes = ` class="${align}"`;
    for (const cell of ["colspan", "rowspan", "scope"]) {
      const value = node.getAttribute(cell);
      if (value && /^(\d{1,4}|row|col|rowgroup|colgroup)$/.test(value)) attributes += ` ${cell}="${value}"`;
    }
  } else if (name === "code") {
    const language = [...node.classList].find((c) => /^language-[a-z0-9+#-]+$/.test(c));
    if (language) attributes = ` class="${language}"`;
  } else if (name === "br" && node.closest("pre")) {
    return "\n";
  } else if (name === "img") {
    const given = node.getAttribute("src") ?? "";
    const src = safeUrl(given, true) ?? (embedImages && /^data:image\/(png|jpeg|gif|webp|avif)[;,]/i.test(given) ? given : null);
    if (!src) return "";
    attributes = ` src="${escape(src, true)}"`;
    const alt = node.getAttribute("alt");
    if (alt !== null) attributes += ` alt="${escape(alt, true)}"`;
    for (const side of ["width", "height"]) {
      const value = node.getAttribute(side);
      if (value && /^\d{1,5}$/.test(value)) attributes += ` ${side}="${value}"`;
    }
  }
  if (voids.has(name)) return `<${name}${attributes}>`;
  // A browser can put a list inside a paragraph; the paragraph gives way to what's in it.
  if (/^(p|h[1-6])$/.test(name) && node.querySelector(":scope > :is(ul,ol,blockquote,pre,table,p,div,h1,h2,h3,h4,h5,h6)")) return serialize(node, top);
  const inner = serialize(node, top);
  if (!inner && name !== "li" && name !== "td" && name !== "th") return "";
  return `<${name}${attributes}>${inner}</${name}>`;
}

/** Whether two font-family values are the same once a browser has read them. */
function sameFont(a: string, b: string): boolean {
  const probe = document.createElement("span");
  probe.style.fontFamily = a;
  return probe.style.fontFamily === b || a === b;
}

function isBlank(surface: Element): boolean {
  return !surface.querySelector("img,hr") && !(surface.textContent ?? "").replace(/[\u00A0\u200B]/g, " ").trim();
}

// The editor's parts (elements/parts/rich-text-*.js), each loaded once, when first needed.
const parts = new Map<string, Promise<unknown>>();
function part<T>(name: string): Promise<T> {
  // A variable, so the bundler leaves the import to the browser (relative to this file).
  const url = `./parts/rich-text-${name}.js`;
  if (!parts.has(name)) parts.set(name, import(url));
  return parts.get(name) as Promise<T>;
}

function exec(command: string, value?: string): void {
  document.execCommand(command, false, value);
}

class RichText extends IzzyElement {
  #saved: Range | null = null;
  #dirty = false;
  #plainPaste = false;
  #dragging = false;
  // The image last clicked: a browser puts the caret beside it, so the editor keeps it here.
  #picked: HTMLImageElement | null = null;
  #markdown: MarkdownPart | null = null;
  #format: FormatPart | null = null;
  #formatLoading: Promise<FormatPart> | null = null;

  private get surface(): HTMLElement | null {
    return this.querySelector<HTMLElement>(":scope > .ib-rich-text-surface");
  }

  private get source(): HTMLTextAreaElement | null {
    return this.querySelector<HTMLTextAreaElement>(":scope > .ib-rich-text-source");
  }

  /** The fonts the editor offers (its Fonts), exactly as the app wrote them. */
  private get fonts(): string[] {
    return this.dataset.ibFonts ? this.dataset.ibFonts.split("|") : [];
  }

  /** The fixed sizes the editor offers (its FontSizes), exactly as the app wrote them. */
  private get fontSizes(): string[] {
    return this.dataset.ibFontSizes ? this.dataset.ibFontSizes.split("|") : [];
  }

  /** The highest heading level (TopHeading). */
  private get top(): number {
    return Number(this.dataset.ibTop) || 1;
  }

  /** Whether a heading level is one the editor offers: the top one and the four below it. */
  private offers(level: number): boolean {
    return level >= this.top && level <= Math.min(this.top + 4, 6);
  }

  private get toolbar(): HTMLElement | null {
    return this.querySelector<HTMLElement>(":scope > .ib-rich-text-toolbar");
  }

  protected setup(signal: AbortSignal): void {
    const source = this.source;
    if (!source) return;
    // The textarea is hidden with script, so the browser can't point at it: the server checks
    // Required, and the text carries aria-required.
    source.required = false;
    this.prepare();

    if (isMac) {
      for (const tool of this.querySelectorAll<HTMLElement>("[aria-keyshortcuts]")) {
        tool.title = tool.title.replace("Ctrl+", "⌘");
        tool.setAttribute("aria-keyshortcuts", tool.getAttribute("aria-keyshortcuts")!.replace("Control+", "Meta+"));
      }
      for (const key of this.querySelectorAll<HTMLElement>("[data-ib-command] .ib-menu-shortcut")) key.textContent = key.textContent!.replace("Ctrl+", "⌘");
    }

    // The label points at the textarea, which is hidden: a click on it focuses the text.
    const label = document.getElementById(this.surface?.getAttribute("aria-labelledby") ?? "");
    label?.addEventListener("click", (event) => {
      event.preventDefault();
      this.surface?.focus();
    }, { signal });

    // Blazor replaces the text when Value changes from outside the field.
    const observer = new MutationObserver(() => this.prepare());
    observer.observe(this, { childList: true });
    signal.addEventListener("abort", () => observer.disconnect());

    this.addEventListener("input", (event) => {
      if (event.target !== this.surface) return;
      const input = event as InputEvent;
      if (input.inputType === "insertText") this.#markdown?.markdown(this.markdownEditor(), input.data);
      this.changed();
    }, { signal });
    // An editor that shows layout tools loads their code now, so the tools show the caret's state.
    if (this.querySelector(formatTools.map((t) => `[data-ib-command="${t}"]`).join(","))) void this.loadFormat();
    if (this.querySelector('[data-ib-command="table"]') || this.surface?.querySelector("table")) void this.tablePart();
    if (this.querySelector("[data-ib-code-language]")) {
      void part<CodePart>("code").then((loaded) =>
        loaded.attach({ host: this, surface: () => this.surface, around: (selector) => this.around(selector), changed: () => this.changed() })
      );
    }
    if (this.dataset.ibMarkdown !== "off" && this.surface?.getAttribute("aria-readonly") !== "true") {
      void part<MarkdownPart>("markdown").then((loaded) => (this.#markdown = loaded));
    }
    this.addEventListener("focusout", (event) => {
      if (event.target !== this.surface) return;
      this.save();
      if (this.#dirty) {
        this.#dirty = false;
        source.dispatchEvent(new Event("change", { bubbles: true }));
      }
    }, { signal });
    this.addEventListener("focusin", (event) => {
      if (event.target !== this.surface) return;
      this.ensureParagraph();
      // The dialogs' code arrives while the text has focus, so the first Ctrl+K opens at once.
      if (this.querySelector(":scope > .ib-rich-text-link")) this.linkPart();
      this.images();
    }, { signal });
    this.addEventListener("keydown", (event) => this.keydown(event), { signal });
    this.addEventListener("paste", (event) => this.paste(event), { signal });
    this.addEventListener("dragstart", () => { this.#dragging = true; }, { signal });
    this.addEventListener("dragend", () => { this.#dragging = false; }, { signal });
    this.addEventListener("drop", (event) => this.drop(event), { signal });

    // Tools keep the selection: a press doesn't take focus from the text.
    this.addEventListener("mousedown", (event) => {
      if ((event.target as Element).closest("button[data-ib-command]")) event.preventDefault();
    }, { signal });
    this.addEventListener("click", (event) => {
      const img = (event.target as Element).closest("img");
      if (this.surface?.contains(event.target as Node)) this.pick(img && this.surface.isContentEditable ? img : null);
      const tool = (event.target as Element).closest<HTMLButtonElement>("button[data-ib-command]");
      if (!tool || !this.contains(tool)) return;
      this.run(tool.dataset.ibCommand!, tool.dataset.ibValue);
      // A menu puts focus back on its button as it closes: the text takes it after that.
      if (tool.closest("[popover]")) setTimeout(() => this.restore(), 0);
    }, { signal });

    document.addEventListener("selectionchange", () => this.reflect(), { signal });
  }

  /** Makes the current text editable (again, after Blazor replaces it). */
  private prepare(): void {
    const surface = this.surface;
    if (!surface || surface.dataset.ibReady !== undefined) return;
    surface.dataset.ibReady = "";
    const locked = surface.getAttribute("aria-readonly") === "true" || surface.getAttribute("aria-disabled") === "true";
    surface.contentEditable = locked ? "false" : "true";
    surface.tabIndex = surface.getAttribute("aria-disabled") === "true" ? -1 : 0;
    surface.spellcheck = true;
    // A font from the server comes as a style: note which of the editor's fonts it is.
    for (const span of surface.querySelectorAll<HTMLElement>("span[style]")) {
      const font = this.fonts.find((f) => sameFont(f, span.style.fontFamily));
      if (font) span.dataset.ibFont = font;
      const size = this.fontSizes.find((f) => f.trim().toLowerCase() === span.style.fontSize.toLowerCase());
      if (size) span.dataset.ibFontSize = size;
    }
    // The count is part of the text's description once it follows the typing.
    const count = document.getElementById(`${this.source?.id}-count`);
    const described = surface.getAttribute("aria-describedby")?.split(" ") ?? [];
    if (count && !described.includes(count.id)) surface.setAttribute("aria-describedby", [...described, count.id].join(" "));
  }

  /** An empty text starts with a paragraph, so the first line typed is one. */
  private ensureParagraph(): void {
    const surface = this.surface!;
    if (surface.isContentEditable && !surface.firstChild) {
      surface.innerHTML = "<p><br></p>";
      getSelection()?.collapse(surface.firstChild, 0);
    }
  }

  private changed(): void {
    const surface = this.surface!;
    // Text typed straight into the box becomes a paragraph; a browser's style attributes go.
    if ([...surface.childNodes].some((n) => n.nodeType === Node.TEXT_NODE && n.textContent?.trim())) exec("formatBlock", "<p>");
    for (const styled of surface.querySelectorAll<HTMLElement>("[style]")) {
      // A font or size the editor set keeps its style; anything else a browser wrote goes.
      const { ibFont: font, ibFontSize: size } = styled.dataset;
      styled.removeAttribute("style");
      if (font) styled.style.fontFamily = font;
      if (size) styled.style.fontSize = size;
    }
    this.sync();
  }

  private sync(): void {
    const surface = this.surface;
    const source = this.source;
    if (!surface || !source) return;
    const blank = isBlank(surface);
    surface.toggleAttribute("data-ib-empty", blank);
    embedImages = this.hasAttribute("data-ib-data-images");
    const html = blank ? "" : serialize(surface, this.top);
    this.count();
    if (html === source.value) return;
    source.value = html;
    this.#dirty = true;
    source.dispatchEvent(new Event("input", { bubbles: true }));
  }

  /** Shows the text's length under the field, marked once it passes the most allowed. */
  private count(): void {
    const count = document.getElementById(`${this.source?.id}-count`);
    const current = count?.querySelector(".ib-field-count-current");
    if (!count || !current) return;
    const length = (this.surface?.textContent ?? "").replace(/\u200B/g, "").length;
    current.textContent = String(length);
    count.toggleAttribute("data-ib-over", length > Number(this.dataset.ibMax));
  }

  /** Inserts sanitized HTML at the caret, as InsertHtmlAsync asks. */
  insert(html: string): void {
    if (!this.surface?.isContentEditable) return;
    this.restore();
    exec("insertHTML", html);
    this.changed();
  }

  private save(): void {
    const selection = getSelection();
    const range = selection?.rangeCount ? selection.getRangeAt(0) : null;
    if (range && this.surface?.contains(range.commonAncestorContainer)) this.#saved = range.cloneRange();
  }

  private restore(): void {
    const surface = this.surface!;
    if (document.activeElement !== surface) surface.focus({ preventScroll: true });
    const selection = getSelection();
    const range = selection?.rangeCount ? selection.getRangeAt(0) : null;
    if (range && surface.contains(range.commonAncestorContainer)) return;
    if (this.#saved && surface.contains(this.#saved.commonAncestorContainer)) {
      selection?.removeAllRanges();
      selection?.addRange(this.#saved);
    }
  }

  private run(command: string, value?: string): void {
    const surface = this.surface;
    if (!surface?.isContentEditable) return;
    this.restore();
    exec("styleWithCSS", "false");
    exec("defaultParagraphSeparator", "p");
    switch (command) {
      case "bold":
      case "italic":
      case "underline":
      case "strikethrough":
      case "subscript":
      case "superscript":
      case "undo":
      case "redo":
        exec(command);
        break;
      case "code":
        this.toggleCode();
        break;
      case "quote":
      case "codeblock":
        // A toggle: on, the text's blocks become a quote or code block; on again, normal text.
        this.formatBlock(command === "quote" ? "blockquote" : "pre");
        break;
      case "table":
        void this.tablePart().then((part) => part.run(this.tableEditor(), value ?? ""));
        return;
      case "image":
        this.save();
        void this.images()?.then(([part, editor]) => part.openImage(editor, this.#picked));
        return;
      case "link": {
        const dialog = this.querySelector<HTMLDialogElement>(":scope > .ib-rich-text-link");
        if (!dialog) return;
        this.save();
        void this.linkPart().then((part) =>
          part.openLink({ surface, dialog, around: (selector) => this.around(selector), changed: () => (this.changed(), this.reflect()) })
        );
        return;
      }
      case "bulleted":
        exec("insertUnorderedList");
        break;
      case "numbered":
        exec("insertOrderedList");
        break;
      case "indent":
      case "outdent":
        // Only list items: anywhere else a browser indents with a styled blockquote.
        if (this.around("li")) exec(command);
        break;
      case "rule":
        exec("insertHorizontalRule");
        break;
      case "align":
      case "color":
      case "highlight":
      case "size":
      case "font": {
        // The code arrives once; the selection is the same when it has.
        const range = getSelection()?.getRangeAt(0).cloneRange();
        void this.loadFormat().then((part) => {
          this.restore();
          if (range) {
            getSelection()?.removeAllRanges();
            getSelection()?.addRange(range);
          }
          if (command === "align") part.align(surface, value ?? "start");
          else part.applyInline(surface, command, value ?? "");
          this.changed();
          this.reflect();
        });
        return;
      }
      case "clear":
        if (this.around("code")) this.toggleCode();
        exec("removeFormat");
        break;
      case "block":
        this.formatBlock(value ?? "p");
        break;
    }
    this.changed();
    this.reflect();
  }

  /** Makes the selection's blocks a style; the style they have already turns them back to normal text. */
  private formatBlock(value: string): void {
    const current = this.blockStyle();
    const style = value === current && value !== "p" ? "p" : value;
    if (current === "blockquote" && style !== "blockquote") exec("outdent");
    exec("formatBlock", `<${style}>`);
  }

  /** The nearest element around the selection matching the selector, inside the text. */
  private around(selector: string): Element | null {
    const selection = getSelection();
    let node: Node | null = selection?.rangeCount ? selection.getRangeAt(0).commonAncestorContainer : null;
    const surface = this.surface;
    if (!node || !surface?.contains(node)) return null;
    if (!(node instanceof Element)) node = node.parentElement;
    const found = (node as Element | null)?.closest(selector);
    return found && surface.contains(found) && found !== surface ? found : null;
  }

  private blockStyle(): string {
    const block = this.around("blockquote") ?? this.around(blocks);
    const name = level(block?.localName, this.top) ?? "p";
    return styles.includes(name) ? name : "p";
  }

  private loadFormat(): Promise<FormatPart> {
    return (this.#formatLoading ??= part<FormatPart>("format").then((loaded) => {
      this.#format = loaded;
      this.reflect();
      return loaded;
    }));
  }

  private toggleCode(): void {
    const selection = getSelection();
    // A code block is code already: inline code has no meaning in it.
    if (!selection?.rangeCount || this.around("pre")) return;
    const code = this.around("code");
    if (selection.isCollapsed) {
      // With no selection it starts code at the caret, or ends it there: what's typed next is or
      // isn't code. A zero-width space holds the caret's place; it never reaches the value.
      if (code && !code.closest("pre")) {
        const after = document.createTextNode("\u200B");
        code.after(after);
        selection.collapse(after, 1);
      } else {
        // Made by hand: a browser's insertHTML turns an empty code into a span.
        const added = document.createElement("code");
        added.textContent = "\u200B";
        selection.getRangeAt(0).insertNode(added);
        selection.collapse(added.firstChild, 1);
      }
      return;
    }
    if (code && !code.closest("pre")) {
      const range = document.createRange();
      range.selectNode(code);
      selection.removeAllRanges();
      selection.addRange(range);
      exec("insertHTML", escape(code.textContent ?? ""));
    } else if (!selection.isCollapsed) {
      exec("insertHTML", `<code>${escape(selection.toString())}</code>`);
    }
  }

  /** Shows the selection's formats on the tools. */
  private reflect(): void {
    const surface = this.surface;
    const selection = getSelection();
    if (!surface || !selection?.rangeCount || !surface.contains(selection.anchorNode)) return;
    const toolbar = this.toolbar;
    if (!toolbar) return;
    const list = this.around("ul,ol")?.localName;
    const states: Record<string, boolean> = {
      bold: document.queryCommandState("bold"),
      italic: document.queryCommandState("italic"),
      underline: document.queryCommandState("underline"),
      strikethrough: document.queryCommandState("strikethrough"),
      subscript: document.queryCommandState("subscript"),
      superscript: document.queryCommandState("superscript"),
      code: !!this.around("code") && !this.around("pre"),
      quote: !!this.around("blockquote"),
      codeblock: !!this.around("pre"),
      link: !!this.around("a"),
      bulleted: list === "ul",
      numbered: list === "ol",
    };
    const format = this.#format;
    const align = format?.alignment(surface) ?? "start";
    for (const tool of toolbar.querySelectorAll<HTMLElement>("[data-ib-command][aria-pressed]")) {
      const command = tool.dataset.ibCommand!;
      tool.setAttribute("aria-pressed", String(command === "align" ? tool.dataset.ibValue === align : !!states[command]));
    }
    // Each menu checks the caret's choice; one with a text button shows it there.
    const current: Record<string, string> = {
      block: /^h[1-6]$/.test(this.blockStyle()) ? this.blockStyle() : "p",
      font: format?.formatAt(surface, "font") ?? "",
      size: format?.formatAt(surface, "size") ?? "",
      color: format?.formatAt(surface, "color") ?? "",
      highlight: format?.formatAt(surface, "highlight") ?? "",
    };
    for (const [menu, value] of Object.entries(current)) {
      let chosen: HTMLElement | null = null;
      for (const item of toolbar.querySelectorAll<HTMLElement>(`[data-ib-command="${menu}"][data-ib-value]`)) {
        const checked = item.dataset.ibValue === value;
        if (item.hasAttribute("aria-checked")) item.setAttribute("aria-checked", String(checked));
        if (checked) chosen = item;
      }
      // The color tools show the caret's color as a bar under their icon.
      const swatch = toolbar.querySelector<HTMLElement>(`[data-ib-swatch="${menu}"]`);
      const preview = value ? chosen?.querySelector<HTMLElement>(".ib-rich-text-menu-text") : null;
      if (swatch) {
        if (preview) swatch.style.setProperty("--ib-swatch", menu === "color" ? getComputedStyle(preview).color : getComputedStyle(preview).backgroundColor);
        else swatch.style.removeProperty("--ib-swatch");
      }
      const button = toolbar.querySelector<HTMLElement>(`[data-ib-menu-button="${menu}"]`);
      const text = button?.querySelector(".ib-rich-text-menu-name");
      const name = chosen?.querySelector(".ib-rich-text-menu-text, [class^='ib-rich-text-preview-']")?.textContent;
      if (button && text && chosen?.dataset.ibLabel && name) {
        button.setAttribute("aria-label", chosen.dataset.ibLabel);
        text.textContent = name;
      }
    }
  }

  private keydown(event: KeyboardEvent): void {
    const surface = this.surface;
    if (!surface) return;

    if (event.altKey && event.key === "F10" && event.target === surface) {
      event.preventDefault();
      this.save();
      const toolbar = this.toolbar;
      const tool = toolbar?.querySelector<HTMLElement>("[tabindex='0']:not([disabled])") ?? toolbar?.querySelector<HTMLElement>("button:not([disabled]),select");
      tool?.focus();
      return;
    }

    if (event.key === "Escape" && this.toolbar?.contains(event.target as Node) && !(event.target as Element).closest("[popover]")) {
      event.preventDefault();
      surface.focus();
      this.restore();
      return;
    }

    if (event.target !== surface || !surface.isContentEditable) return;
    // A clicked image goes with Delete or Backspace; any other key lets it go.
    const picked = this.#picked;
    if (picked && !event.ctrlKey && !event.metaKey && !["Shift", "Control", "Alt", "Meta"].includes(event.key)) {
      this.pick(null);
      if (event.key === "Delete" || event.key === "Backspace") {
        event.preventDefault();
        picked.remove();
        this.changed();
        return;
      }
    }
    if (event.key === "Enter" && !event.shiftKey && this.#markdown?.rule(this.markdownEditor())) {
      event.preventDefault();
      return;
    }
    const mod = isMac ? event.metaKey && !event.ctrlKey : event.ctrlKey && !event.metaKey;
    if (!mod) return;
    // The browser's own Ctrl+B, I and U change the format for what's typed next without moving the
    // selection, so no selectionchange says so: show it once the browser has done it.
    setTimeout(() => this.reflect(), 0);

    let command: [string, string?] | null = null;
    if (event.shiftKey && !event.altKey) {
      if (event.code === "KeyX") command = ["strikethrough"];
      else if (event.code === "Digit7") command = ["numbered"];
      else if (event.code === "Equal") command = ["superscript"];
      else if (event.code === "KeyL") command = ["align", "start"];
      else if (event.code === "KeyE") command = ["align", "center"];
      else if (event.code === "KeyR") command = ["align", "end"];
      else if (event.code === "Digit8") command = ["bulleted"];
      else if (event.code === "KeyV") this.#plainPaste = true;
    } else if (event.altKey && !event.shiftKey) {
      const digit = /^Digit([0-6])$/.exec(event.code)?.[1];
      const style = digit === "0" ? "p" : digit && this.offers(Number(digit)) ? `h${digit}` : null;
      if (style) command = ["block", style === this.blockStyle() ? "p" : style];
    } else if (!event.altKey) {
      if (event.code === "KeyE") command = ["code"];
      else if (event.code === "KeyK") command = ["link"];
      else if (event.code === "Equal") command = ["subscript"];
      else if (event.code === "BracketRight") command = ["indent"];
      else if (event.code === "BracketLeft") command = ["outdent"];
    }

    if (command) {
      event.preventDefault();
      this.run(...command);
    }
  }

  private tableEditor(): Parameters<TablePart["run"]>[0] {
    return { host: this, surface: this.surface!, around: (selector) => this.around(selector), changed: () => (this.changed(), this.reflect()) };
  }

  private tablePart(): Promise<TablePart> {
    return part<TablePart>("table").then((loaded) => {
      loaded.attach(this.tableEditor());
      return loaded;
    });
  }

  private linkPart(): Promise<LinkPart> {
    return part<LinkPart>("link");
  }

  private markdownEditor(): Parameters<MarkdownPart["markdown"]>[0] {
    return { top: this.top, offers: (level) => this.offers(level), around: (selector) => this.around(selector), changed: () => this.changed() };
  }

  /** Marks the image clicked (or none), which the image tool and Delete act on. */
  private pick(img: HTMLImageElement | null): void {
    this.#picked?.removeAttribute("data-ib-selected");
    this.#picked = img;
    img?.setAttribute("data-ib-selected", "");
  }

  /** The image part, and what it needs of this editor; null when the editor has no image tool. */
  private images(): Promise<[ImagePart, Parameters<ImagePart["openImage"]>[0]]> | null {
    const dialog = this.querySelector<HTMLDialogElement>(":scope > .ib-rich-text-image");
    const surface = this.surface;
    if (!dialog || !surface) return null;
    return part<ImagePart>("image").then((loaded) => [
      loaded,
      { host: this, surface, dialog, changed: () => (this.changed(), this.reflect()) },
    ]);
  }

  /** Image files in a paste or drop, when the editor takes them: uploaded or embedded. */
  private imageFiles(data: DataTransfer): File[] {
    if (!this.dataset.ibImageUpload && !this.hasAttribute("data-ib-data-images")) return [];
    return [...data.files].filter((file) => file.type.startsWith("image/"));
  }

  private paste(event: ClipboardEvent): void {
    const surface = this.surface;
    const data = event.clipboardData;
    if (!data || event.target !== surface && !surface?.contains(event.target as Node) || !surface?.isContentEditable) return;
    event.preventDefault();
    const files = this.imageFiles(data);
    if (files.length) {
      void this.images()?.then(([part, editor]) => part.insertFiles(editor, files));
      return;
    }
    const plain = this.#plainPaste;
    this.#plainPaste = false;
    const html = data.getData("text/html");
    if (plain || !html) {
      exec("insertText", data.getData("text/plain"));
    } else {
      // Parsed in a document of its own, where nothing loads or runs.
      embedImages = this.hasAttribute("data-ib-data-images");
      const cleaned = serialize(new DOMParser().parseFromString(html, "text/html").body, this.top);
      if (cleaned) exec("insertHTML", cleaned);
    }
    this.changed();
  }

  private drop(event: DragEvent): void {
    const surface = this.surface;
    const data = event.dataTransfer;
    if (!surface?.isContentEditable || !data || !surface.contains(event.target as Node)) return;
    // Text moved within the text is the browser's to drop.
    if (this.#dragging) return;
    event.preventDefault();
    // Image files go in where they're dropped; other files don't; text from elsewhere goes in cleaned.
    const files = this.imageFiles(data);
    if (data.files.length && !files.length) return;
    const point = document.caretPositionFromPoint?.(event.clientX, event.clientY);
    const range = point ? document.createRange() : document.caretRangeFromPoint?.(event.clientX, event.clientY);
    if (point && range) range.setStart(point.offsetNode, point.offset);
    if (!range) return;
    range.collapse(true);
    surface.focus();
    getSelection()?.removeAllRanges();
    getSelection()?.addRange(range);
    if (files.length) {
      void this.images()?.then(([part, editor]) => part.insertFiles(editor, files));
      return;
    }
    const html = data.getData("text/html");
    embedImages = this.hasAttribute("data-ib-data-images");
    if (html) exec("insertHTML", serialize(new DOMParser().parseFromString(html, "text/html").body, this.top));
    else exec("insertText", data.getData("text/plain"));
    this.changed();
  }
}

defineElement("ib-rich-text", RichText);

/** For RichTextEditor.InsertHtmlAsync: inserts HTML the server has sanitized at the caret. */
export function insertHtml(host: HTMLElement, html: string): void {
  (host as RichText).insert(html);
}
