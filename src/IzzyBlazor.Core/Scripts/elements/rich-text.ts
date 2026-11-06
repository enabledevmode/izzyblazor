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
      out += escape((node.textContent ?? "").replace(/​/g, ""));
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
  return !surface.querySelector("img,hr") && !(surface.textContent ?? "").replace(/[ ​]/g, " ").trim();
}

function exec(command: string, value?: string): void {
  document.execCommand(command, false, value);
}

class RichText extends IzzyElement {
  #saved: Range | null = null;
  #dirty = false;
  #plainPaste = false;
  #dragging = false;
  #link: HTMLAnchorElement | null = null;
  #linkCollapsed = true;
  #linkRange: Range | null = null;

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
      if (this.dataset.ibMarkdown !== "off" && input.inputType === "insertText") this.markdown(input.data);
      this.changed();
    }, { signal });
    this.setupLink(signal);
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
      case "undo":
      case "redo":
        exec(command);
        break;
      case "code":
        this.toggleCode();
        break;
      case "link":
        this.openLink();
        return;
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
    // A code block is code already: inline code has no meaning in it.
    if (!selection?.rangeCount || this.around("pre")) return;
    const code = this.around("code");
    if (selection.isCollapsed) {
      // With no selection it starts code at the caret, or ends it there: what's typed next is or
      // isn't code. A zero-width space holds the caret's place; it never reaches the value.
      if (code && !code.closest("pre")) {
        const after = document.createTextNode("​");
        code.after(after);
        selection.collapse(after, 1);
      } else {
        // Made by hand: a browser's insertHTML turns an empty code into a span.
        const added = document.createElement("code");
        added.textContent = "​";
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
      code: !!this.around("code") && !this.around("pre"),
      link: !!this.around("a"),
      bulleted: list === "ul",
      numbered: list === "ol",
    };
    for (const tool of toolbar.querySelectorAll<HTMLElement>("[data-ib-command][aria-pressed]")) {
      tool.setAttribute("aria-pressed", String(!!states[tool.dataset.ibCommand!]));
    }
    const style = this.blockStyle();
    let label: string | undefined;
    for (const item of toolbar.querySelectorAll<HTMLElement>("[data-ib-command=block][data-ib-value]")) {
      const checked = item.dataset.ibValue === style;
      if (item.hasAttribute("aria-checked")) item.setAttribute("aria-checked", String(checked));
      if (checked) label = item.dataset.ibLabel;
    }
    const button = toolbar.querySelector<HTMLElement>("[data-ib-style-button]");
    const text = button?.querySelector(".ib-rich-text-style-name") ?? null;
    const name = toolbar.querySelector<HTMLElement>(`.ib-rich-text-preview-${style}`)?.textContent;
    if (button && label && name) {
      button.setAttribute("aria-label", label);
      if (text) text.textContent = name;
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
    if (event.key === "Enter" && !event.shiftKey && this.dataset.ibMarkdown !== "off" && this.rule()) {
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
      else if (event.code === "Digit8") command = ["bulleted"];
      else if (event.code === "KeyV") this.#plainPaste = true;
    } else if (event.altKey && !event.shiftKey) {
      const level = { Digit0: "p", Digit2: "h2", Digit3: "h3", Digit4: "h4" }[event.code];
      if (level) command = ["block", level === this.blockStyle() ? "p" : level];
    } else if (!event.altKey) {
      if (event.code === "KeyE") command = ["code"];
      else if (event.code === "KeyK") command = ["link"];
      else if (event.code === "BracketRight") command = ["indent"];
      else if (event.code === "BracketLeft") command = ["outdent"];
    }

    if (command) {
      event.preventDefault();
      this.run(...command);
    }
  }

  /** Typed Markdown becomes formats: a line's start (## , - , 1. , > , ```) or **bold**, *italic*, `code`. */
  private markdown(data: string | null): void {
    const selection = getSelection();
    const node = selection?.anchorNode;
    if (!selection?.isCollapsed || !node || node.nodeType !== Node.TEXT_NODE || this.around("pre")) return;
    const offset = selection.anchorOffset;
    const before = (node.textContent ?? "").slice(0, offset);

    if (data === " ") {
      const block = this.around("p,div");
      const marker = /^(#{1,4}|[-*]|1[.)]|>|```)[ \u00A0]$/.exec(before)?.[1];
      if (!block || !marker || this.around("li,blockquote")) return;
      // Only at the very start of the line.
      const lead = document.createRange();
      lead.setStart(block, 0);
      lead.setEnd(node, 0);
      if (lead.toString() !== "") return;
      const typed = document.createRange();
      typed.setStart(node, 0);
      typed.setEnd(node, offset);
      selection.removeAllRanges();
      selection.addRange(typed);
      exec("delete");
      if (marker.startsWith("#")) exec("formatBlock", `<h${Math.max(2, marker.length)}>`);
      else if (marker === "-" || marker === "*") exec("insertUnorderedList");
      else if (marker === ">") exec("formatBlock", "<blockquote>");
      else if (marker === "```") exec("formatBlock", "<pre>");
      else exec("insertOrderedList");
      return;
    }

    if (data !== "*" && data !== "`" || this.around("code")) return;
    const forms: [RegExp, string][] = [
      [/\*\*([^*\s](?:[^*]*[^*\s])?)\*\*$/, "strong"],
      [/(?:^|[^*\w])\*([^*\s](?:[^*]*[^*\s])?)\*$/, "em"],
      [/`([^`]+)`$/, "code"],
    ];
    for (const [pattern, name] of forms) {
      const match = pattern.exec(before);
      if (!match) continue;
      const marks = name === "strong" ? 2 : 1;
      const start = offset - match[1].length - 2 * marks;
      const range = document.createRange();
      range.setStart(node, start);
      range.setEnd(node, offset);
      range.deleteContents();
      const element = document.createElement(name);
      element.textContent = match[1];
      const after = document.createTextNode("\u200B");
      range.insertNode(after);
      range.insertNode(element);
      selection.collapse(after, 1);
      return;
    }
  }

  /** "---" alone on a line, then Enter, becomes a horizontal line. */
  private rule(): boolean {
    const block = this.around("p,div");
    if (!block || (block.textContent ?? "").trim() !== "---" || this.around("li,blockquote,pre")) return false;
    const line = document.createElement("hr");
    const next = document.createElement("p");
    next.append(document.createElement("br"));
    block.replaceWith(line, next);
    getSelection()?.collapse(next, 0);
    this.changed();
    return true;
  }

  private linkPart<T extends HTMLElement>(name: string): T | null {
    return this.querySelector<T>(`:scope > .ib-rich-text-link [data-ib-link="${name}"]`);
  }

  private setupLink(signal: AbortSignal): void {
    const dialog = this.querySelector<HTMLElement>(":scope > .ib-rich-text-link");
    if (!dialog) return;
    dialog.addEventListener("click", (event) => {
      const action = (event.target as Element).closest<HTMLElement>("[data-ib-link=apply],[data-ib-link=remove]")?.dataset.ibLink;
      if (action === "apply") this.applyLink();
      else if (action === "remove") this.removeLink();
    }, { signal });
    dialog.addEventListener("keydown", (event) => {
      if (event.key === "Enter" && (event.target as Element).matches("input[type=url],input[type=text]")) {
        event.preventDefault();
        this.applyLink();
      } else if (event.key === "Escape") {
        // The popover closes itself; the text takes focus back after, with its selection.
        setTimeout(() => this.restoreLinkRange(), 0);
      }
    }, { signal });
  }

  /** Opens the link dialog under the selection, filled in from the link there, if any. */
  private openLink(): void {
    const dialog = this.querySelector<HTMLElement>(":scope > .ib-rich-text-link");
    const href = this.linkPart<HTMLInputElement>("href");
    const text = this.linkPart<HTMLInputElement>("text");
    const tab = this.linkPart<HTMLInputElement>("tab");
    const selection = getSelection();
    if (!dialog || !href || !text || !tab || !selection?.rangeCount) return;
    this.save();
    const link = this.around("a") as HTMLAnchorElement | null;
    this.#link = link;
    this.#linkCollapsed = selection.isCollapsed;
    this.#linkRange = selection.getRangeAt(0).cloneRange();
    href.value = link?.getAttribute("href") ?? "";
    href.removeAttribute("aria-invalid");
    text.value = link?.textContent ?? "";
    tab.checked = link?.target === "_blank";
    // Text only for a new link with nothing selected, or to change a link's own text.
    this.linkPart("text-field")!.hidden = !link && !selection.isCollapsed;
    this.linkPart("remove")!.hidden = !link;

    const rect = selection.getRangeAt(0).getBoundingClientRect();
    const anchor = rect.height ? rect : this.surface!.getBoundingClientRect();
    dialog.style.position = "fixed";
    dialog.style.margin = "0";
    dialog.style.top = `${Math.round(anchor.bottom + 6)}px`;
    dialog.showPopover();
    const width = dialog.offsetWidth;
    dialog.style.left = `${Math.round(Math.max(8, Math.min(anchor.left, innerWidth - width - 8)))}px`;
    href.focus();
  }

  private applyLink(): void {
    const href = this.linkPart<HTMLInputElement>("href")!;
    const url = safeUrl(href.value.trim(), false);
    if (!url) {
      href.setAttribute("aria-invalid", "true");
      href.focus();
      return;
    }
    const text = this.linkPart<HTMLInputElement>("text")!.value.trim();
    const blank = this.linkPart<HTMLInputElement>("tab")!.checked;
    this.querySelector<HTMLElement>(":scope > .ib-rich-text-link")!.hidePopover();
    this.restoreLinkRange();
    const link = this.#link && this.surface!.contains(this.#link) ? this.#link : null;
    const target = blank ? ' target="_blank" rel="noopener noreferrer"' : "";
    if (link) {
      link.setAttribute("href", url);
      if (blank) {
        link.target = "_blank";
        link.rel = "noopener noreferrer";
      } else {
        link.removeAttribute("target");
        link.removeAttribute("rel");
      }
      if (text && text !== link.textContent) link.textContent = text;
    } else if (this.#linkCollapsed) {
      exec("insertHTML", `<a href="${escape(url, true)}"${target}>${escape(text || url)}</a>`);
    } else {
      exec("createLink", url);
      const made = this.around("a") as HTMLAnchorElement | null;
      if (made && blank) {
        made.target = "_blank";
        made.rel = "noopener noreferrer";
      }
    }
    this.changed();
    this.reflect();
  }

  /** Puts back the selection the dialog was opened on: focus in the dialog collapsed it. */
  private restoreLinkRange(): void {
    const surface = this.surface!;
    surface.focus({ preventScroll: true });
    const range = this.#linkRange;
    if (range && surface.contains(range.commonAncestorContainer)) {
      getSelection()?.removeAllRanges();
      getSelection()?.addRange(range);
    }
  }

  private removeLink(): void {
    this.querySelector<HTMLElement>(":scope > .ib-rich-text-link")!.hidePopover();
    this.restoreLinkRange();
    const link = this.#link;
    if (link && this.surface!.contains(link)) {
      const range = document.createRange();
      range.selectNodeContents(link);
      getSelection()?.removeAllRanges();
      getSelection()?.addRange(range);
      exec("unlink");
    }
    this.changed();
    this.reflect();
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

/** For RichTextEditor.InsertHtmlAsync: inserts HTML the server has sanitized at the caret. */
export function insertHtml(host: HTMLElement, html: string): void {
  (host as RichText).insert(html);
}
