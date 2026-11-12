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
  // Page fetch: each trigger's first results, the text the options answer, the wait and the request.
  #fetchKey: string | null = null;
  #first = new Map<string, Node[]>();
  #answered = "";
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

    area.addEventListener("input", () => this.update(true), { signal });
    area.addEventListener("click", () => this.update(false), { signal });
    area.addEventListener("keyup", (event) => {
      if (["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)) this.update(false);
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

    // Blazor's results (or new Items) came: narrow them again and show them.
    const observer = new MutationObserver(() => {
      if (this.#query && document.activeElement === area) this.show();
    });
    observer.observe(list, { childList: true, subtree: true, attributes: true, attributeFilter: ["aria-busy", "data-query"] });
    signal.addEventListener("abort", () => {
      observer.disconnect();
      clearTimeout(this.#timer);
      this.#request?.abort();
      this.close();
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
    const start = Math.max(before.lastIndexOf(" "), before.lastIndexOf("\n"), before.lastIndexOf("\t")) + 1;
    const word = before.slice(start);
    const prefix = this.#prefixes.find((p) => word.startsWith(p));
    return prefix ? { prefix, text: word.slice(prefix.length), start } : null;
  }

  /** After typing (`typed`) or moving the caret: look for a mention and ask for its suggestions. */
  private update(typed: boolean): void {
    if (this.#area.readOnly || this.#area.disabled) return;
    const q = this.find();
    const before = this.#query;
    this.#query = q;
    if (!q) {
      this.close();
      return;
    }
    if (typed) this.#dismissed = null;
    if (!before || this.key(before) !== this.key(q)) this.ask(q);
    this.show();
  }

  /** Asks for the suggestions for what's typed: Blazor on interactive pages, the page on static ones. */
  private ask(q: Query): void {
    const reset = this.querySelector<HTMLInputElement>(":scope > .ib-mentions-query");
    if (reset) {
      if (this.#list.dataset.query === this.key(q)) return;
      reset.value = this.key(q);
      reset.dispatchEvent(new Event("input", { bubbles: true }));
      return;
    }
    if (!this.#fetchKey) return;
    clearTimeout(this.#timer);
    this.#request?.abort();
    if (!q.text) this.take(q.prefix, this.#first.get(q.prefix) ?? [], "");
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
    this.#list.removeAttribute("aria-busy");
  }

  /** Page fetch: puts in a trigger's options, answering `answered` ("" for its first results). */
  private take(prefix: string, options: Node[], answered: string): void {
    const empty = this.#list.querySelector(".ib-combobox-empty");
    for (const o of this.options) if ((o.dataset.prefix ?? "") === prefix) o.remove();
    for (const node of options) this.#list.insertBefore(document.importNode(node, true), empty);
    this.options.forEach((o, i) => {
      o.id = `${this.#list.id}-${i}`;
      o.hidden = false;
      o.removeAttribute("data-active");
    });
    this.#answered = answered;
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
    if (this.#dismissed === key || (!visible.length && !this.busy)) {
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
      this.#untrack = track(this.#list, this.#caret, this.hasAttribute("data-top") ? "top-start" : "bottom-start");
    }
    const keep = this.#active && visible.includes(this.#active) ? this.#active : null;
    this.activate(keep ?? visible[0] ?? null);
    if (visible.length !== this.#announced && !this.busy) {
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
  }

  private say(text: string): void {
    const status = this.querySelector<HTMLElement>(":scope > .ib-combobox-status");
    if (status && status.textContent !== text) status.textContent = text;
  }

  private onKey(event: KeyboardEvent): void {
    if (event.isComposing || event.ctrlKey || event.metaKey || event.altKey || !this.isOpen) return;
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
    this.#active = option;
    if (option) {
      option.setAttribute("data-active", "");
      this.#area?.setAttribute("aria-activedescendant", option.id);
      if (scroll) option.scrollIntoView({ block: "nearest" });
    } else {
      this.#area?.removeAttribute("aria-activedescendant");
    }
  }

  /** Writes the trigger and the option's text over the mention being typed, then the split. */
  private choose(option: HTMLElement): void {
    const q = this.#query;
    if (!q) return;
    const area = this.#area;
    const end = area.selectionStart;
    const split = this.dataset.split ?? " ";
    const after = area.value.slice(end);
    const text = q.prefix + (option.dataset.text ?? "") + (split && !after.startsWith(split) ? split : "");
    area.setRangeText(text, q.start, end, "end");
    // Past a split that was already there.
    if (split && after.startsWith(split)) area.setSelectionRange(area.selectionStart + split.length, area.selectionStart + split.length);
    this.#query = null;
    this.close();
    area.dispatchEvent(new Event("input", { bubbles: true }));
    area.dispatchEvent(new Event("change", { bubbles: true }));
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
    copy.textContent = area.value.slice(0, at);
    const mark = document.createElement("span");
    mark.textContent = area.value.slice(at) || ".";
    copy.append(mark);
    document.body.append(copy);
    const top = mark.offsetTop - area.scrollTop;
    const left = mark.offsetLeft - area.scrollLeft;
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
