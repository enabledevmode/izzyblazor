import { IzzyElement, defineElement } from "../izzy-element";
import { track } from "../position";
import { fetchPart } from "../fetch-part";

/**
 * <ib-mentions> suggests names in a text area as a trigger (data-prefixes, "@" by default) is
 * typed at the start of the text or after a space. The list opens at the caret: a hidden mark
 * (.ib-mentions-caret) is moved to the caret's line, measured with a copy of the text area, and
 * the list is anchored to it (or placed by position.ts where anchor positioning isn't supported).
 * Choosing writes the trigger, the name and data-split into the text, then fires input and change,
 * so Blazor's @bind and the form see it.
 *
 * Every option has data-prefix (empty: for every trigger) and data-text, what's written. Options
 * are narrowed here by "contains", unless they answer the typed text: with data-server, Blazor
 * fills the list from what's written to .ib-mentions-query ("@an") and says which text it answers
 * in the list's data-query; on a static page with a search (data-ib-suggest-key), each pause
 * fetches the page with the text in its address and takes that trigger's options out of it.
 */
const fold = (text: string) =>
  text
    .normalize("NFD")
    .replace(/\p{Diacritic}/gu, "")
    .toLocaleLowerCase();

// What the copy of the text area takes from it, so its text wraps the same.
const copied = [
  "boxSizing", "width", "borderTopWidth", "borderRightWidth", "borderBottomWidth", "borderLeftWidth", "borderStyle",
  "paddingTop", "paddingRight", "paddingBottom", "paddingLeft", "fontFamily", "fontSize", "fontWeight", "fontStyle",
  "fontVariant", "letterSpacing", "wordSpacing", "lineHeight", "textTransform", "textIndent", "tabSize", "direction",
] as const;

interface Query {
  prefix: string;
  text: string;
  /** Where the trigger starts in the text. */
  start: number;
}

class Mentions extends IzzyElement {
  #area!: HTMLTextAreaElement;
  #list!: HTMLElement;
  #caret!: HTMLElement;
  #prefixes: string[] = [];
  #query: Query | null = null;
  #active: HTMLElement | null = null;
  #untrack: (() => void) | null = null;
  // Escape closed the list for this text; it opens again once the text changes.
  #dismissed: string | null = null;
  #announced = -1;
  // Just chose: the input event that follows mustn't open the list again (Split "").
  #chose = false;
  // Page fetch: each trigger's first results, the text the options answer, the wait and the request.
  #fetchKey: string | null = null;
  #first = new Map<string, Node[]>();
  #answered = "";
  // Page fetch: the text whose fetch has finished, answered or not.
  #settled = "";
  #timer = 0;
  #request: AbortController | null = null;

  protected setup(signal: AbortSignal): void {
    const area = this.querySelector<HTMLTextAreaElement>(":scope > textarea");
    const list = this.querySelector<HTMLElement>(":scope > [role=listbox]");
    const caret = this.querySelector<HTMLElement>(":scope > .ib-mentions-caret");
    if (!area || !list || !caret || !("showPopover" in list)) return;
    this.#area = area;
    this.#list = list;
    this.#caret = caret;
    this.#prefixes = (this.dataset.prefixes || "@").split("\n").filter(Boolean).sort((a, b) => b.length - a.length);
    this.#fetchKey = this.hasAttribute("data-server") ? null : (this.dataset.ibSuggestKey ?? null);
    this.#first.clear();
    for (const option of this.options) {
      const prefix = option.dataset.prefix ?? "";
      this.#first.set(prefix, [...(this.#first.get(prefix) ?? []), option.cloneNode(true)]);
    }
    this.dataset.ready = "";
    // Only once there's a list to offer: without script it's a plain text area.
    area.setAttribute("aria-autocomplete", "list");
    area.setAttribute("aria-controls", list.id);

    area.addEventListener("input", () => this.update(true), { signal });
    area.addEventListener("click", () => this.update(false), { signal });
    area.addEventListener("keyup", (event) => {
      if (["ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown", "Home", "End", "PageUp", "PageDown"].includes(event.key)) this.update(false);
    }, { signal });
    area.addEventListener("keydown", (event) => this.onKey(event), { signal });
    area.addEventListener("blur", () => this.close(), { signal });
    area.addEventListener("scroll", () => this.isOpen && this.placeCaret(), { signal, passive: true });
    area.form?.addEventListener("reset", () => this.close(), { signal });

    // Pressing an option mustn't take focus from the text area (and close the list).
    const keep = (event: Event) => event.preventDefault();
    list.addEventListener("pointerdown", keep, { signal });
    list.addEventListener("mousedown", keep, { signal });
    list.addEventListener("click", (event) => {
      const option = (event.target as Element).closest<HTMLElement>("[role=option]");
      if (option && !option.hidden && option.getAttribute("aria-disabled") !== "true") this.choose(option);
    }, { signal });
    list.addEventListener("pointermove", (event) => {
      const option = (event.target as Element).closest<HTMLElement>("[role=option]");
      if (option && option !== this.#active && !option.hidden) this.activate(option, false);
    }, { signal });

    // Blazor's results (or new Items) came: narrow them again and show them, for the mention
    // the caret is in now (it may have moved without an input).
    const observer = new MutationObserver(() => {
      if (!this.#query || document.activeElement !== area) return;
      const q = this.find();
      if (!q || this.key(q) !== this.key(this.#query) || q.start !== this.#query.start) this.update(false);
      else this.show();
    });
    observer.observe(list, { childList: true, subtree: true, attributes: true, attributeFilter: ["aria-busy", "data-query"] });
    signal.addEventListener("abort", () => {
      observer.disconnect();
      clearTimeout(this.#timer);
      this.#request?.abort();
      this.close();
      area.removeAttribute("aria-autocomplete");
      area.removeAttribute("aria-controls");
      delete this.dataset.ready;
    });
  }

  private get isOpen(): boolean {
    return this.#list.matches(":popover-open");
  }

  private get options(): HTMLElement[] {
    return [...this.#list.querySelectorAll<HTMLElement>(":scope > [role=option]")];
  }

  private get visible(): HTMLElement[] {
    return this.options.filter((o) => !o.hidden && o.getAttribute("aria-disabled") !== "true");
  }

  private get busy(): boolean {
    return this.#list.getAttribute("aria-busy") === "true";
  }

  private key(q: Query): string {
    return q.prefix + q.text;
  }

  /** The trigger and text before the caret, if it's in a mention being typed. */
  private find(): Query | null {
    const area = this.#area;
    if (area.selectionStart !== area.selectionEnd) return null;
    const before = area.value.slice(0, area.selectionStart);
    // After any white space, as Mention.Parse (a no-break or ideographic space too).
    const start = before.search(/\S*$/);
    const word = before.slice(start);
    const prefix = this.#prefixes.find((p) => word.startsWith(p));
    return prefix ? { prefix, text: word.slice(prefix.length), start } : null;
  }

  /** After typing (`typed`) or moving the caret: look for a mention and ask for its suggestions. */
  private update(typed: boolean): void {
    if (this.#area.readOnly || this.#area.disabled) return;
    if (this.#chose) {
      this.#chose = false;
      this.#query = null;
      this.close();
      return;
    }
    const q = this.find();
    const before = this.#query;
    // Only typing opens the list; moving the caret keeps it for the same mention or closes it, so
    // a click onto a finished mention leaves Enter and Tab alone.
    if (!typed && (!q || !before || this.key(before) !== this.key(q) || before.start !== q.start)) {
      this.#query = null;
      this.close();
      return;
    }
    this.#query = q;
    if (!q) {
      this.close();
      return;
    }
    if (typed) this.#dismissed = null;
    if (!before || this.key(before) !== this.key(q)) this.ask(q);
    this.show();
  }

  /** Whether a search for what's typed hasn't answered yet: the list waits, "Searching…", rather than closing. */
  private pending(key: string): boolean {
    if (this.querySelector(":scope > .ib-mentions-query")) return this.#list.dataset.query !== key;
    return !!this.#fetchKey && this.#settled !== key;
  }

  /** Asks for the suggestions for what's typed: Blazor on interactive pages, the page on static ones. */
  private ask(q: Query): void {
    const reset = this.querySelector<HTMLInputElement>(":scope > .ib-mentions-query");
    if (reset) {
      // Even when the list answers this text already: a search for other text may be on its way.
      reset.value = this.key(q);
      reset.dispatchEvent(new Event("input", { bubbles: true }));
      return;
    }
    if (!this.#fetchKey) return;
    clearTimeout(this.#timer);
    this.#request?.abort();
    this.#list.removeAttribute("aria-busy");
    if (!q.text) this.take(q.prefix, this.#first.get(q.prefix) ?? [], this.key(q));
    else if (this.key(q) !== this.#answered) {
      this.#timer = window.setTimeout(() => this.fetchFor(q), Number(this.dataset.ibDebounce ?? 250));
    }
  }

  private async fetchFor(q: Query): Promise<void> {
    const request = (this.#request = new AbortController());
    const key = this.#fetchKey!;
    const url = new URL(location.href);
    url.hash = "";
    url.searchParams.set("ib-suggest", key);
    url.searchParams.set("ib-suggest-q", this.key(q));
    if (this.closest("ib-table, ib-grid, ib-listview")) url.searchParams.set("ib-suggest-in", "1");
    this.#list.setAttribute("aria-busy", "true");
    let answer: Element | null = null;
    try {
      answer = await fetchPart(url, `ib-mentions[data-ib-suggest-key="${CSS.escape(key)}"] > [role=listbox][data-ib-answered]`, request.signal);
    } catch {
      if (request.signal.aborted) return;
    }
    if (request !== this.#request) return;
    this.#request = null;
    // No answer: the options stay, narrowed here.
    if (answer) this.take(q.prefix, [...answer.querySelectorAll(":scope > [role=option]")], this.key(q));
    this.#settled = this.key(q);
    this.#list.removeAttribute("aria-busy");
  }

  /** Page fetch: puts in a trigger's options, answering `answered` (the trigger alone for its first results). */
  private take(prefix: string, options: Node[], answered: string): void {
    const empty = this.#list.querySelector(".ib-combobox-empty");
    for (const o of this.options) if ((o.dataset.prefix ?? "") === prefix) o.remove();
    for (const node of options) this.#list.insertBefore(document.importNode(node, true), empty);
    this.options.forEach((o, i) => {
      o.id = `${this.#list.id}-${i}`;
      o.hidden = false;
      o.removeAttribute("data-active");
      o.removeAttribute("aria-selected");
    });
    this.#answered = this.#settled = answered;
  }

  /** Narrows the options to the mention being typed and opens, keeps or closes the list. */
  private show(): void {
    const q = this.#query;
    if (!q) return;
    const key = this.key(q);
    const answered = this.#list.dataset.query === key || (!!this.#fetchKey && this.#answered === key);
    const typed = fold(q.text);
    for (const option of this.options) {
      const prefix = option.dataset.prefix ?? "";
      option.hidden = !((prefix === "" || prefix === q.prefix) && (answered || fold(option.dataset.text ?? "").includes(typed)));
    }
    const visible = this.visible;
    const waiting = this.busy || this.pending(key);
    if (this.#dismissed === key || (!visible.length && !waiting)) {
      this.close();
      return;
    }
    if (!this.isOpen) {
      try {
        this.#list.showPopover();
      } catch {
        return;
      }
      this.#announced = -1;
      this.placeCaret();
      this.fit();
      this.#untrack = track(this.#list, this.#caret, this.hasAttribute("data-top") ? "top-start" : "bottom-start");
    } else {
      // The word may have wrapped to the next line, or the field grown.
      this.placeCaret();
      this.fit();
    }
    const keep = this.#active && visible.includes(this.#active) ? this.#active : null;
    this.activate(keep ?? visible[0] ?? null);
    if (visible.length !== this.#announced && !waiting) {
      this.#announced = visible.length;
      this.say(visible.length === 1 ? (this.dataset.countOne ?? "1 suggestion.") : (this.dataset.count ?? "{0} suggestions.").replace("{0}", String(visible.length)));
    }
  }

  private close(): void {
    this.#untrack?.();
    this.#untrack = null;
    if (this.#list?.matches(":popover-open")) this.#list.hidePopover();
    this.activate(null);
    this.#announced = -1;
    // So the same count is said again when it opens next.
    this.say("");
  }

  private say(text: string): void {
    const status = this.querySelector<HTMLElement>(":scope > .ib-combobox-status");
    if (status && status.textContent !== text) status.textContent = text;
  }

  private onKey(event: KeyboardEvent): void {
    if (event.isComposing || event.ctrlKey || event.metaKey || event.altKey) return;
    if (!this.isOpen) {
      // Escape while its suggestions are on their way: they mustn't open the list after it.
      if (event.key === "Escape" && this.#query) this.#dismissed = this.key(this.#query);
      return;
    }
    const options = this.visible;
    const index = this.#active ? options.indexOf(this.#active) : -1;
    switch (event.key) {
      case "ArrowDown":
      case "ArrowUp": {
        event.preventDefault();
        const step = event.key === "ArrowDown" ? 1 : -1;
        this.activate(options[(index + step + options.length) % options.length] ?? null);
        break;
      }
      case "Enter":
      case "Tab":
        // Shift+Tab goes back, as anywhere.
        if (event.key === "Tab" && event.shiftKey) {
          this.close();
          return;
        }
        if (!this.#active) return;
        event.preventDefault();
        this.choose(this.#active);
        break;
      case "Escape":
        // Don't let a surrounding dialog close too.
        event.preventDefault();
        event.stopPropagation();
        this.#dismissed = this.#query ? this.key(this.#query) : null;
        this.close();
        break;
    }
  }

  private activate(option: HTMLElement | null, scroll = true): void {
    this.#active?.removeAttribute("data-active");
    this.#active?.removeAttribute("aria-selected");
    this.#active = option;
    if (option) {
      option.setAttribute("data-active", "");
      // Highlighted is selected, for a list whose choice is written into the text.
      option.setAttribute("aria-selected", "true");
      this.#area?.setAttribute("aria-activedescendant", option.id);
      if (scroll) option.scrollIntoView({ block: "nearest" });
    } else {
      this.#area?.removeAttribute("aria-activedescendant");
    }
  }

  /** Writes the trigger and the option's text over the whole mention the caret is in, then the split. */
  private choose(option: HTMLElement): void {
    // Where the caret is now, not where the list was opened.
    const q = this.find();
    if (!q) {
      this.close();
      return;
    }
    const area = this.#area;
    const end = area.selectionStart + (area.value.slice(area.selectionStart).match(/^\S*/)?.[0].length ?? 0);
    const split = this.dataset.split ?? " ";
    const after = area.value.slice(end);
    const text = q.prefix + (option.dataset.text ?? "") + (split && !after.startsWith(split) ? split : "");
    // The browser doesn't hold a script to maxlength: a name that doesn't fit isn't written.
    if (area.maxLength >= 0 && area.value.length - (end - q.start) + text.length > area.maxLength) {
      this.close();
      return;
    }
    this.#query = null;
    this.close();
    this.#chose = true;
    // As typing, so Ctrl+Z takes it back (setRangeText isn't in the undo history); it fires input.
    area.setSelectionRange(q.start, end);
    let typed = false;
    try {
      typed = document.execCommand("insertText", false, text);
    } catch {
      typed = false;
    }
    if (!typed) {
      area.setRangeText(text, q.start, end, "end");
      area.dispatchEvent(new Event("input", { bubbles: true }));
    }
    this.#chose = false;
    // Past a split that was already there.
    if (split && after.startsWith(split)) area.setSelectionRange(area.selectionStart + split.length, area.selectionStart + split.length);
    area.dispatchEvent(new Event("change", { bubbles: true }));
  }

  /**
   * On a narrow screen, moves the mark back from the edge the list grows toward, so the list has
   * its full width (anchor positioning would squeeze it into the room beside the mark).
   */
  private fit(): void {
    const list = this.#list;
    const room = document.documentElement.clientWidth;
    list.style.inlineSize = "max-content";
    const width = Math.min(list.getBoundingClientRect().width, room - 16);
    list.style.inlineSize = "";
    const mark = this.#caret.getBoundingClientRect();
    const rtl = getComputedStyle(this.#area).direction === "rtl";
    const shift = rtl ? Math.max(0, 8 + width - mark.left) : Math.min(0, room - 8 - width - mark.left);
    if (shift) this.#caret.style.left = `${parseFloat(this.#caret.style.left) + shift}px`;
  }

  /** Moves the caret mark to the caret's line, measured with a hidden copy of the text area. */
  private placeCaret(): void {
    const area = this.#area;
    const style = getComputedStyle(area);
    const copy = document.createElement("div");
    for (const name of copied) copy.style[name] = style[name];
    copy.style.position = "absolute";
    copy.style.visibility = "hidden";
    copy.style.top = "0";
    copy.style.left = "-9999px";
    copy.style.whiteSpace = "pre-wrap";
    copy.style.overflowWrap = "break-word";
    copy.style.overflowY = area.scrollHeight > area.clientHeight ? "scroll" : "hidden";
    const at = this.#query?.start ?? area.selectionStart;
    const prefix = this.#query?.prefix ?? ".";
    copy.textContent = area.value.slice(0, at);
    // Only the trigger in the mark, so its edge is the trigger's in either direction.
    const mark = document.createElement("span");
    mark.textContent = prefix;
    copy.append(mark, area.value.slice(at + prefix.length));
    document.body.append(copy);
    const top = mark.offsetTop - area.scrollTop;
    const left = mark.offsetLeft + (style.direction === "rtl" ? mark.offsetWidth : 0) - area.scrollLeft;
    const line = parseFloat(style.lineHeight) || parseFloat(style.fontSize) * 1.2;
    copy.remove();
    // Kept inside the text area when the caret's line is scrolled out of it.
    const y = Math.min(Math.max(top, 0), Math.max(area.clientHeight - line, 0));
    this.#caret.style.top = `${area.offsetTop + y}px`;
    this.#caret.style.left = `${area.offsetLeft + Math.min(Math.max(left, 0), area.clientWidth)}px`;
    this.#caret.style.blockSize = `${line}px`;
  }
}

defineElement("ib-mentions", Mentions);
