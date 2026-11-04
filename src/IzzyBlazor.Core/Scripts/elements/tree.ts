import { IzzyElement, defineElement } from "../izzy-element";
import { fetchPart } from "../fetch-part";

/**
 * <ib-tree> adds to a TreeView:
 * - Branches a person opened or closed stay that way as they move between pages (enhanced
 *   navigation puts back the server's markup), kept per page while the tab lives. On an
 *   interactive page each branch's opening is reported (ibtoggle, the item's key) for @bind-Expanded.
 * - With a SelectionMode: an ARIA tree over the server's list of boxes. One Tab stop (roving
 *   tabindex on the items), arrows, Home, End, `*`, typeahead, Space and Enter; each item a
 *   treeitem with its level, open state and aria-checked or aria-selected. The boxes and the
 *   branches' summaries leave the Tab order and the accessibility tree; they still post and click.
 * - With checkboxes and cascade: a box checks or unchecks its branch, and each parent is checked
 *   when its whole branch is, mixed when part of it is. Every box it changes reports a change, so
 *   posting and @bind-Selection see the boxes as they are.
 * - On a static page, a branch whose children load (ChildrenProvider) fetches the page with the
 *   tree's key and the item's (the ib-suggest keys, as a Combobox's search) the first time it
 *   opens, and takes the answered list in. A failure shows Retry.
 * - Searchable: the box narrows the tree to matches and their parents, opening their branches and
 *   marking the matched text (CSS highlights, so Blazor's markup is untouched); clearing it puts
 *   the branches back as they were.
 */

// Branches opened (true) or closed (false) by hand, by page and tree, then by item key.
const memory = new Map<string, Map<string, boolean>>();

class Tree extends IzzyElement {
  #list: HTMLUListElement | null = null;
  #selects = false;
  #syncing = false;
  #typed = "";
  #typedAt = 0;
  // Each branch's open state before a search opened it, to put back when the search is cleared.
  #before = new Map<HTMLDetailsElement, boolean>();

  protected setup(signal: AbortSignal): void {
    const list = this.querySelector<HTMLUListElement>(":scope > .ib-tree-list");
    if (!list) return;
    this.#list = list;
    const interactive = this.hasAttribute("data-ib-interactive");
    this.dataset.ibReady = "";
    signal.addEventListener("abort", () => delete this.dataset.ibReady);
    const trees = [...document.querySelectorAll("ib-tree")];
    const id = `${location.pathname}|${this.id || trees.indexOf(this)}`;
    let remembered = memory.get(id);
    if (!remembered) memory.set(id, (remembered = new Map()));
    if (!interactive) {
      for (const [key, open] of remembered) {
        const details = this.#detailsOf(this.#byKey(key));
        if (details) details.open = open;
      }
      // Open as the page came (the current item's branch): kept open as you move on.
      for (const li of this.#items) {
        if (li.dataset.ibKey && this.#detailsOf(li)?.open && !remembered.has(li.dataset.ibKey)) remembered.set(li.dataset.ibKey, true);
      }
    }

    // By hand: a summary's click (mouse, or Enter and Space on it) and the tree's keys.
    const byHand = (details: HTMLDetailsElement, open: boolean) => {
      const key = (details.parentElement as HTMLElement).dataset.ibKey;
      if (key) remembered!.set(key, open);
    };
    this.addEventListener(
      "click",
      (event) => {
        const summary = (event.target as Element).closest("summary");
        const details = summary?.parentElement;
        if (details instanceof HTMLDetailsElement && details.closest("ib-tree") === this && !event.defaultPrevented) byHand(details, !details.open);
      },
      { signal }
    );
    this.#byHand = byHand;

    // toggle doesn't bubble: listen as it goes down.
    this.addEventListener(
      "toggle",
      (event) => {
        const details = event.target as HTMLDetailsElement;
        if (!(details instanceof HTMLDetailsElement) || details.closest("ib-tree") !== this) return;
        const item = details.parentElement as HTMLElement;
        if (this.#selects) item.setAttribute("aria-expanded", String(details.open));
        if (interactive && item.dataset.ibKey) this.emit("ibtoggle", { id: item.dataset.ibKey, open: details.open, seq: 0 });
        else if (details.open && item.hasAttribute("data-ib-lazy")) this.#load(item, signal);
      },
      { signal, capture: true }
    );
    this.addEventListener(
      "click",
      (event) => {
        const retry = (event.target as Element).closest("[data-ib-retry]");
        const item = retry?.closest<HTMLElement>("li[data-ib-lazy]");
        if (item && item.closest("ib-tree") === this) this.#load(item, signal);
      },
      { signal }
    );
    // Branches that arrived open, before this script was here to hear them open.
    for (const item of this.querySelectorAll<HTMLElement>("li[data-ib-lazy]")) {
      const details = this.#detailsOf(item);
      if (!details?.open || item.closest("ib-tree") !== this) continue;
      if (interactive) {
        if (item.dataset.ibKey) this.emit("ibtoggle", { id: item.dataset.ibKey, open: true, seq: 0 });
      } else this.#load(item, signal);
    }
    this.querySelector<HTMLInputElement>(":scope > .ib-tree-search [data-ib-tree-search]")?.addEventListener("input", (event) => this.#search((event.target as HTMLInputElement).value), { signal });
    signal.addEventListener("abort", () => CSS.highlights?.delete("ib-tree-match"));

    this.#selects = this.hasAttribute("data-ib-select");
    if (!this.#selects) return;
    this.#prepare();
    if (this.hasAttribute("data-ib-cascade")) {
      if (!interactive) {
        this.#syncing = true;
        for (const li of this.#items) {
          if (!this.#boxOf(li)?.checked) continue;
          for (const below of li.querySelectorAll<HTMLElement>(":scope > details li")) {
            const own = this.#boxOf(below);
            if (own) this.#set(own, true);
          }
        }
        this.#syncing = false;
      }
      this.#mixAll();
    }
    this.#syncAll();
    // Blazor adds and changes items on interactive pages, and redraws their state when the
    // selection changes from code (it sets the boxes' checked, which fires nothing).
    const observer = new MutationObserver((records) => {
      if (records.some((r) => r.type === "attributes")) {
        for (const li of this.#items) {
          const box = this.#boxOf(li);
          if (box) box.indeterminate = li.dataset.ibChecked === "mixed";
        }
      }
      this.#prepare();
      this.#syncAll();
    });
    observer.observe(list, { childList: true, subtree: true, attributes: true, attributeFilter: ["data-ib-checked"] });
    signal.addEventListener("abort", () => observer.disconnect());

    list.addEventListener("keydown", (event) => this.#onKey(event), { signal });
    list.addEventListener("change", (event) => this.#onChange(event), { signal });
    // A click on an item's line focuses the item, not its hidden box or summary; the click still
    // checks the box or opens the branch.
    list.addEventListener(
      "mousedown",
      (event) => {
        const item = (event.target as Element).closest<HTMLElement>("li[role=treeitem]");
        if (!item || item.closest("ib-tree") !== this) return;
        event.preventDefault();
        this.#focus(item);
      },
      { signal }
    );
    list.addEventListener("focusin", (event) => {
      const item = event.target as HTMLElement;
      if (item.matches("li[role=treeitem]")) this.#rove(item);
    }, { signal });
  }

  #byHand: (details: HTMLDetailsElement, open: boolean) => void = () => {};

  get #items(): HTMLElement[] {
    const items: HTMLElement[] = [];
    const walk = (list: Element) => {
      for (const li of list.children) {
        if (!(li instanceof HTMLLIElement)) continue;
        items.push(li);
        const group = li.querySelector(":scope > details > ul");
        if (group) walk(group);
      }
    };
    if (this.#list) walk(this.#list);
    return items;
  }

  // The items a person can see: none in a closed branch.
  get #shown(): HTMLElement[] {
    return this.#items.filter((li) => {
      if (li.hidden) return false;
      for (let d = li.parentElement?.closest("details"); d && this.contains(d); d = d.parentElement?.closest("details")) if (!d.open) return false;
      return true;
    });
  }

  #byKey(key: string): HTMLElement | null {
    return this.#items.find((li) => li.dataset.ibKey === key) ?? null;
  }

  #detailsOf(item: Element | null): HTMLDetailsElement | null {
    return item?.querySelector<HTMLDetailsElement>(":scope > details") ?? null;
  }

  /** An item's line: its link, label or text, or the summary that opens its branch. */
  #rowOf(item: Element): HTMLElement | null {
    return item.querySelector<HTMLElement>(":scope > .ib-tree-item, :scope > details > summary.ib-tree-item");
  }

  #boxOf(item: Element): HTMLInputElement | null {
    return item.querySelector<HTMLInputElement>(":scope > .ib-tree-item > .ib-tree-select");
  }

  #parentOf(item: Element): HTMLElement | null {
    return item.parentElement?.closest<HTMLElement>("li") ?? null;
  }

  #childrenOf(item: Element): HTMLElement[] {
    return [...(item.querySelector(":scope > details > ul")?.children ?? [])].filter((c): c is HTMLElement => c instanceof HTMLLIElement);
  }

  /** Roles, levels and names, and the boxes and summaries out of the way. */
  #prepare(): void {
    const list = this.#list!;
    list.setAttribute("role", "tree");
    const label = this.dataset.ibLabel;
    if (label) list.setAttribute("aria-label", label);
    if (this.dataset.ibSelect === "multiple") list.setAttribute("aria-multiselectable", "true");
    let stop: HTMLElement | null = null;
    for (const li of this.#items) {
      li.setAttribute("role", "treeitem");
      let level = 1;
      for (let p = this.#parentOf(li); p; p = this.#parentOf(p)) level++;
      li.setAttribute("aria-level", String(level));
      const name = li.querySelector<HTMLElement>(":scope > .ib-tree-item [data-ib-label]")?.dataset.ibLabel;
      if (name) li.setAttribute("aria-label", name);
      const details = this.#detailsOf(li);
      if (details) {
        li.setAttribute("aria-expanded", String(details.open));
        details.querySelector(":scope > ul")?.setAttribute("role", "group");
        const summary = details.querySelector<HTMLElement>(":scope > summary");
        summary?.setAttribute("tabindex", "-1");
        summary?.setAttribute("aria-hidden", "true");
      }
      const box = this.#boxOf(li);
      if (box) {
        box.tabIndex = -1;
        box.setAttribute("aria-hidden", "true");
        if (box.disabled) li.setAttribute("aria-disabled", "true");
        else li.removeAttribute("aria-disabled");
      }
      if (li.tabIndex === 0) stop = li;
      else li.tabIndex = -1;
    }
    // The stop: the one focus was on, else the first selected one showing, else the first.
    if (!stop || !this.#shown.includes(stop)) {
      const shown = this.#shown;
      stop = shown.find((li) => this.#boxOf(li)?.checked) ?? shown[0] ?? null;
    }
    if (stop) this.#rove(stop);
  }

  #rove(item: HTMLElement): void {
    for (const li of this.#items) if (li !== item && li.tabIndex !== -1) li.tabIndex = -1;
    item.tabIndex = 0;
  }

  #focus(item: HTMLElement): void {
    this.#rove(item);
    item.focus();
  }

  #syncAll(): void {
    const multiple = this.dataset.ibSelect === "multiple";
    for (const li of this.#items) {
      const box = this.#boxOf(li);
      if (!box) continue;
      if (multiple) li.setAttribute("aria-checked", box.indeterminate ? "mixed" : String(box.checked));
      else li.setAttribute("aria-selected", String(box.checked));
    }
  }

  /** Each parent's mixed state from its branch, deepest first. */
  #mixAll(): void {
    for (const li of this.#items.reverse()) this.#mix(li);
  }

  #mix(item: HTMLElement): void {
    const box = this.#boxOf(item);
    const kids = this.#childrenOf(item).map((c) => this.#boxOf(c)).filter((b): b is HTMLInputElement => !!b);
    if (!box || kids.length === 0) return;
    box.indeterminate = !box.checked && kids.some((k) => k.checked || k.indeterminate);
  }

  #set(box: HTMLInputElement, on: boolean): void {
    if (box.disabled || box.checked === on) return;
    box.checked = on;
    box.dispatchEvent(new Event("input", { bubbles: true }));
    box.dispatchEvent(new Event("change", { bubbles: true }));
  }

  #onChange(event: Event): void {
    const box = event.target as HTMLInputElement;
    if (!box.matches(".ib-tree-select")) return;
    if (this.dataset.ibSelect === "multiple" && this.hasAttribute("data-ib-cascade") && !this.#syncing) {
      const item = box.closest<HTMLElement>("li")!;
      this.#syncing = true;
      try {
        box.indeterminate = false;
        // The branch follows the box.
        for (const li of item.querySelectorAll<HTMLElement>(":scope > details li")) {
          const own = this.#boxOf(li);
          if (own) {
            this.#set(own, box.checked);
            own.indeterminate = false;
          }
        }
        // Each parent up: checked when its whole branch is, mixed when part of it is.
        for (let p = this.#parentOf(item); p; p = this.#parentOf(p)) {
          const own = this.#boxOf(p);
          if (!own) continue;
          const kids = this.#childrenOf(p).map((c) => this.#boxOf(c)).filter((b): b is HTMLInputElement => !!b);
          this.#set(own, kids.length > 0 && kids.every((k) => k.checked));
          this.#mix(p);
        }
      } finally {
        this.#syncing = false;
      }
    }
    this.#syncAll();
  }

  #onKey(event: KeyboardEvent): void {
    const item = (event.target as Element).closest<HTMLElement>("li[role=treeitem]");
    if (!item || item.closest("ib-tree") !== this || event.altKey || event.ctrlKey || event.metaKey) return;
    const shown = this.#shown;
    const at = shown.indexOf(item);
    const details = this.#detailsOf(item);
    const open = (d: HTMLDetailsElement, to: boolean) => {
      if (d.open === to) return;
      d.open = to;
      this.#byHand(d, to);
    };
    let to: HTMLElement | undefined;
    switch (event.key) {
      case "ArrowDown":
        to = shown[at + 1];
        break;
      case "ArrowUp":
        to = shown[at - 1];
        break;
      case "Home":
        to = shown[0];
        break;
      case "End":
        to = shown[shown.length - 1];
        break;
      case "ArrowRight":
        if (!details) break;
        if (!details.open) open(details, true);
        else to = this.#childrenOf(item)[0];
        break;
      case "ArrowLeft":
        if (details?.open) open(details, false);
        else to = this.#parentOf(item) ?? undefined;
        break;
      case "*":
        for (const sibling of [...(item.parentElement?.children ?? [])]) {
          const d = this.#detailsOf(sibling);
          if (d) open(d, true);
        }
        break;
      case " ":
      case "Enter": {
        const box = this.#boxOf(item);
        if (box && !box.disabled && !(box.type === "radio" && box.checked)) box.click();
        break;
      }
      default:
        if (event.key.length !== 1) return;
        to = this.#typeahead(shown, at, event.key);
        if (!to) return;
    }
    event.preventDefault();
    if (to) this.#focus(to);
  }

  /** On a static page: a branch's children, fetched from the page the first time it opens. */
  async #load(item: HTMLElement, signal: AbortSignal): Promise<void> {
    const tree = this.dataset.ibTreeKey;
    const key = item.dataset.ibKey;
    const status = item.querySelector<HTMLElement>(":scope > details > .ib-tree-status");
    if (!tree || !key || !status || item.dataset.ibLoading !== undefined) return;
    item.dataset.ibLoading = "";
    status.removeAttribute("data-ib-failed");
    const url = new URL(location.href);
    url.hash = "";
    url.searchParams.set("ib-suggest", tree);
    url.searchParams.set("ib-suggest-q", key);
    let list: Element | null = null;
    try {
      list = await fetchPart(url, `ib-tree[data-ib-tree-key="${CSS.escape(tree)}"] > [data-ib-answered]`, signal);
    } catch {
      // Offline or left the page: shown as failed below, unless it's gone.
    }
    delete item.dataset.ibLoading;
    if (signal.aborted || !status.isConnected) return;
    if (!list) {
      status.setAttribute("data-ib-failed", "");
      return;
    }
    const group = document.importNode(list, true) as HTMLElement;
    group.removeAttribute("data-ib-answered");
    status.replaceWith(group);
    item.removeAttribute("data-ib-lazy");
    // A checked parent's children arrive checked.
    const box = this.#boxOf(item);
    if (box?.checked && this.hasAttribute("data-ib-cascade")) {
      this.#syncing = true;
      for (const li of group.querySelectorAll<HTMLElement>("li")) {
        const own = this.#boxOf(li);
        if (own) this.#set(own, true);
      }
      this.#syncing = false;
    }
  }

  /** Narrows the tree to the items whose text holds the query, and their parents. */
  #search(text: string): void {
    const query = text.trim().toLowerCase();
    const items = this.#items;
    const ranges: Range[] = [];
    if (!query) {
      for (const li of items) li.hidden = false;
      for (const [details, open] of this.#before) details.open = open;
      this.#before.clear();
    } else {
      for (const li of items) {
        const details = this.#detailsOf(li);
        if (details && !this.#before.has(details)) this.#before.set(details, details.open);
      }
      // Deepest first, so each parent knows whether any child stays.
      for (const li of items.reverse()) {
        const own = this.#mark(this.#rowOf(li), query, ranges);
        const kept = this.#childrenOf(li).some((c) => !c.hidden);
        li.hidden = !own && !kept;
        const details = this.#detailsOf(li);
        if (details) details.open = kept;
      }
    }
    if (CSS.highlights) {
      if (ranges.length) CSS.highlights.set("ib-tree-match", new Highlight(...ranges));
      else CSS.highlights.delete("ib-tree-match");
    }
    // The Tab stop may have been hidden.
    if (this.#selects) this.#prepare();
    const found = this.querySelector<HTMLElement>(":scope > [data-ib-tree-found]");
    if (found) found.textContent = query && !items.some((li) => !li.hidden) ? this.dataset.ibNoMatches ?? "" : "";
  }

  /** Whether a row's text holds the query, adding a range over each place it does. */
  #mark(row: HTMLElement | null, query: string, ranges: Range[]): boolean {
    if (!row) return false;
    let found = false;
    const walker = document.createTreeWalker(row, NodeFilter.SHOW_TEXT);
    for (let node = walker.nextNode() as Text | null; node; node = walker.nextNode() as Text | null) {
      const text = node.data.toLowerCase();
      for (let at = text.indexOf(query); at !== -1; at = text.indexOf(query, at + query.length)) {
        found = true;
        const range = new Range();
        range.setStart(node, at);
        range.setEnd(node, at + query.length);
        ranges.push(range);
      }
    }
    return found;
  }

  /** The next item whose name starts with what's been typed; a letter typed again cycles. */
  #typeahead(shown: HTMLElement[], at: number, key: string): HTMLElement | undefined {
    const now = performance.now();
    const char = key.toLowerCase();
    this.#typed = now - this.#typedAt > 500 ? char : this.#typed + char;
    this.#typedAt = now;
    const repeat = [...this.#typed].every((c) => c === char);
    const prefix = repeat ? char : this.#typed;
    const name = (li: HTMLElement) => (li.getAttribute("aria-label") ?? this.#rowOf(li)?.textContent ?? "").trim().toLowerCase();
    const from = repeat || this.#typed.length === 1 ? at + 1 : at;
    for (let i = 0; i < shown.length; i++) {
      const li = shown[(from + i) % shown.length];
      if (name(li).startsWith(prefix)) return li;
    }
    return undefined;
  }
}

defineElement("ib-tree", Tree);
