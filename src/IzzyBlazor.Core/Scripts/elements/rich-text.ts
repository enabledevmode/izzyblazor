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
  P: "p", DIV: "p", H1: "h2", H2: "h2", H3: "h3", H4: "h4", H5: "h4", H6: "h4",
  UL: "ul", OL: "ol", LI: "li", BLOCKQUOTE: "blockquote", PRE: "pre", HR: "hr",
  STRONG: "strong", B: "strong", EM: "em", I: "em", U: "u", S: "s", STRIKE: "s", DEL: "s",
  CODE: "code", A: "a", BR: "br", IMG: "img",
  TABLE: "table", THEAD: "thead", TBODY: "tbody", TR: "tr", TH: "th", TD: "td",
};
const voids = new Set(["br", "hr", "img"]);
const dropped = new Set(["SCRIPT", "STYLE", "TEMPLATE", "IFRAME", "OBJECT", "EMBED", "SVG", "MATH", "NOSCRIPT", "TEXTAREA", "TITLE", "SELECT", "HEAD", "META", "LINK"]);
const blocks = "p,h1,h2,h3,h4,h5,h6,blockquote,pre,li,div";
const styles = ["p", "h2", "h3", "h4", "blockquote", "pre"];

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

/** Writes a node's children as the editor's HTML: the same elements the server's sanitizer keeps. */
function serialize(parent: Node): string {
  let out = "";
  let openItem = false;
  for (const node of parent.childNodes) {
    if (node.nodeType === Node.TEXT_NODE) {
      // Whitespace between list items or table rows isn't content.
      if (/^(UL|OL|TABLE|THEAD|TBODY|TR)$/.test((parent as Element).tagName ?? "") && !node.textContent?.trim()) continue;
      out += escape(node.textContent ?? "");
      openItem = false;
      continue;
    }
    if (!(node instanceof Element) || dropped.has(node.tagName.toUpperCase())) continue;
    const name = names[node.tagName.toUpperCase()];
    if (!name) {
      out += serialize(node);
      continue;
    }

    // A browser's indent puts a list straight in a list: it belongs in the item before it.
    if ((name === "ul" || name === "ol") && openItem && out.endsWith("</li>")) {
      out = out.slice(0, -5) + element(node, name) + "</li>";
      continue;
    }

    out += element(node, name);
    openItem = name === "li";
  }
  return out;
}

function element(node: Element, name: string): string {
  let attributes = "";
  if (name === "a") {
    const href = safeUrl(node.getAttribute("href") ?? "", false);
    if (!href) return serialize(node);
    attributes = ` href="${escape(href, true)}"`;
    if (node.getAttribute("target") === "_blank") attributes += ` target="_blank" rel="noopener noreferrer"`;
  } else if (name === "img") {
    const src = safeUrl(node.getAttribute("src") ?? "", true);
    if (!src) return "";
    attributes = ` src="${escape(src, true)}"`;
    const alt = node.getAttribute("alt");
    if (alt !== null) attributes += ` alt="${escape(alt, true)}"`;
  }
  if (voids.has(name)) return `<${name}${attributes}>`;
  // A browser can put a list inside a paragraph; the paragraph gives way to what's in it.
  if (/^(p|h[234])$/.test(name) && node.querySelector(":scope > :is(ul,ol,blockquote,pre,table,p,div,h1,h2,h3,h4,h5,h6)")) return serialize(node);
  const inner = serialize(node);
  if (!inner && name !== "li" && name !== "td" && name !== "th") return "";
  return `<${name}${attributes}>${inner}</${name}>`;
}

function isBlank(surface: Element): boolean {
  return !surface.querySelector("img,hr") && !(surface.textContent ?? "").replace(/ /g, " ").trim();
}

function exec(command: string, value?: string): void {
  document.execCommand(command, false, value);
}

class RichText extends IzzyElement {
  #saved: Range | null = null;
  #dirty = false;
  #plainPaste = false;
  #dragging = false;

  private get surface(): HTMLElement | null {
    return this.querySelector<HTMLElement>(":scope > .ib-rich-text-surface");
  }

  private get source(): HTMLTextAreaElement | null {
    return this.querySelector<HTMLTextAreaElement>(":scope > .ib-rich-text-source");
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
      if (event.target === this.surface) this.changed();
    }, { signal });
    this.addEventListener("focusout", (event) => {
      if (event.target !== this.surface) return;
      this.save();
      if (this.#dirty) {
        this.#dirty = false;
        source.dispatchEvent(new Event("change", { bubbles: true }));
      }
    }, { signal });
    this.addEventListener("focusin", (event) => {
      if (event.target === this.surface) this.ensureParagraph();
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
      const tool = (event.target as Element).closest<HTMLButtonElement>("button[data-ib-command]");
      if (tool && this.contains(tool)) this.run(tool.dataset.ibCommand!);
    }, { signal });
    this.addEventListener("change", (event) => {
      const select = event.target as HTMLSelectElement;
      if (select.matches?.("select[data-ib-command]")) this.run("block", select.value);
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
    for (const styled of surface.querySelectorAll("[style]")) styled.removeAttribute("style");
    this.sync();
  }

  private sync(): void {
    const surface = this.surface;
    const source = this.source;
    if (!surface || !source) return;
    const blank = isBlank(surface);
    surface.toggleAttribute("data-ib-empty", blank);
    const html = blank ? "" : serialize(surface);
    if (html === source.value) return;
    source.value = html;
    this.#dirty = true;
    source.dispatchEvent(new Event("input", { bubbles: true }));
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
      case "undo":
      case "redo":
        exec(command);
        break;
      case "code":
        this.toggleCode();
        break;
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
      case "clear":
        if (this.around("code")) this.toggleCode();
        exec("removeFormat");
        break;
      case "block": {
        // Choosing the style the text has already turns it back into a paragraph.
        const current = this.blockStyle();
        const style = value === current && value !== "p" ? "p" : value ?? "p";
        if (current === "blockquote" && style !== "blockquote") exec("outdent");
        exec("formatBlock", `<${style}>`);
        break;
      }
    }
    this.changed();
    this.reflect();
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
    const name = block?.localName ?? "p";
    if (name === "h1") return "h2";
    if (name === "h5" || name === "h6") return "h4";
    return styles.includes(name) ? name : "p";
  }

  private toggleCode(): void {
    const selection = getSelection();
    if (!selection?.rangeCount) return;
    const code = this.around("code");
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
      code: !!this.around("code") && !this.around("pre"),
      bulleted: list === "ul",
      numbered: list === "ol",
    };
    for (const tool of toolbar.querySelectorAll<HTMLElement>("[data-ib-command][aria-pressed]")) {
      tool.setAttribute("aria-pressed", String(!!states[tool.dataset.ibCommand!]));
    }
    const select = toolbar.querySelector<HTMLSelectElement>("select[data-ib-command]");
    if (select) select.value = this.blockStyle();
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
    const mod = isMac ? event.metaKey && !event.ctrlKey : event.ctrlKey && !event.metaKey;
    if (!mod) return;

    let command: [string, string?] | null = null;
    if (event.shiftKey && !event.altKey) {
      if (event.code === "KeyX") command = ["strikethrough"];
      else if (event.code === "Digit7") command = ["numbered"];
      else if (event.code === "Digit8") command = ["bulleted"];
      else if (event.code === "KeyV") this.#plainPaste = true;
    } else if (event.altKey && !event.shiftKey) {
      const level = { Digit0: "p", Digit2: "h2", Digit3: "h3", Digit4: "h4" }[event.code];
      if (level) command = ["block", level === this.blockStyle() ? "p" : level];
    } else if (!event.altKey) {
      if (event.code === "KeyE") command = ["code"];
      else if (event.code === "BracketRight") command = ["indent"];
      else if (event.code === "BracketLeft") command = ["outdent"];
    }

    if (command) {
      event.preventDefault();
      this.run(...command);
    }
  }

  private paste(event: ClipboardEvent): void {
    const surface = this.surface;
    const data = event.clipboardData;
    if (!data || event.target !== surface && !surface?.contains(event.target as Node) || !surface?.isContentEditable) return;
    event.preventDefault();
    const plain = this.#plainPaste;
    this.#plainPaste = false;
    const html = data.getData("text/html");
    if (plain || !html) {
      exec("insertText", data.getData("text/plain"));
    } else {
      // Parsed in a document of its own, where nothing loads or runs.
      const cleaned = serialize(new DOMParser().parseFromString(html, "text/html").body);
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
    // Files wait for images (a later step); text from elsewhere goes in cleaned.
    if (data.files.length) return;
    const point = document.caretPositionFromPoint?.(event.clientX, event.clientY);
    const range = point ? document.createRange() : document.caretRangeFromPoint?.(event.clientX, event.clientY);
    if (point && range) range.setStart(point.offsetNode, point.offset);
    if (!range) return;
    range.collapse(true);
    surface.focus();
    getSelection()?.removeAllRanges();
    getSelection()?.addRange(range);
    const html = data.getData("text/html");
    if (html) exec("insertHTML", serialize(new DOMParser().parseFromString(html, "text/html").body));
    else exec("insertText", data.getData("text/plain"));
    this.changed();
  }
}

defineElement("ib-rich-text", RichText);
