import { IzzyElement, defineElement } from "../izzy-element";
import { setupLoadOnScroll } from "../data-view";
import { navigateTo } from "../filtering";
import { fetchPart } from "../fetch-part";

/**
 * <ib-filter-list> adds what a list filter can't do in HTML: Select all, which ticks or unticks the
 * rows shown and fires `change` on each it flips; the search, which narrows the rows and applies
 * nothing; and the rows as one Tab stop, with arrows and Home and End. Applying is <ib-table>'s on
 * static pages and C#'s on bound ones. Without script both Select all and the search are hidden by
 * CSS, the boxes are Tab stops of their own, and they post with Apply.
 *
 * A list of looked-up values may have more rows than are here, so nothing can be worked out from the
 * boxes. On an interactive page C# does Select all and the search, and says what Select all shows in
 * data-ib-all (data-ib-list-remote). On a static page there's no C# to ask (data-ib-list-fetch): this
 * fetches the page's own address with one more key, takes the list out of the response, and writes
 * ticks to the address itself, when Apply or Enter says so; closing the panel any other way drops
 * them. Its children are data-permanent, so a navigation leaves them, and the ticks are set from the address. Either way it keeps its keys and its one Tab stop as rows
 * arrive, loads more as the last row comes into view, and takes focus from Load more to the first
 * new row.
 */
const PAUSE = 350;

// Which rows are ticked, as the address says it: those named, all but those named, none, or all.
interface Ticks {
  op: "in" | "notin" | "";
  none: boolean;
  values: (string | null)[];
}

class FilterList extends IzzyElement {
  // Kept when setup runs again as a response arrives, which puts the box back to what the server drew.
  #search = "";

  // A fetched list's, kept the same way.
  #pending: Ticks | null = null;
  #applied = false;
  #searchTimer = 0;
  #request: AbortController | null = null;
  #first = "";
  #next: number | null = null;
  #lastSkip = 0;
  #loading = false;
  #failed = false;
  #whole = false;
  #rowsSearch = "";

  protected setup(signal: AbortSignal): void {
    const fetched = this.hasAttribute("data-ib-list-fetch");
    const remote = fetched || this.hasAttribute("data-ib-list-remote");
    const all = this.querySelector<HTMLInputElement>("[data-ib-list-all]")!;
    const search = this.querySelector<HTMLInputElement>("[data-ib-list-search], [data-ib-bound-search]");
    const results = this.querySelector<HTMLElement>("[data-ib-list-results]")!;
    const items = this.querySelector<HTMLElement>(".ib-filter-list-items")!;
    const rows = () => [...this.querySelectorAll<HTMLLabelElement>(".ib-filter-list-items > label")];
    const box = (row: HTMLLabelElement) => row.querySelector("input")!;
    const shown = () => rows().filter((row) => !row.hidden).map(box);

    // One Tab stop among the rows: the one with focus, else the one that was, else the first shown.
    const rove = () => {
      const boxes = shown().filter((b) => !b.disabled);
      const stop = boxes.includes(document.activeElement as HTMLInputElement) ? document.activeElement : (boxes.find((b) => b.tabIndex === 0) ?? boxes[0]);
      for (const row of rows()) box(row).tabIndex = box(row) === stop ? 0 : -1;
    };
    const sync = () => {
      if (fetched) return this.#paint(all, search, results, rows().map(box));
      if (remote) {
        all.checked = this.dataset.ibAll === "on";
        all.indeterminate = this.dataset.ibAll === "mixed";
        return;
      }
      const boxes = shown();
      const ticked = boxes.filter((b) => b.checked).length;
      all.checked = ticked > 0 && ticked === boxes.length;
      all.indeterminate = ticked > 0 && ticked < boxes.length;
      all.disabled = boxes.length === 0;
    };
    const narrow = () => {
      if (!remote) {
        const text = this.#search.trim().toLocaleLowerCase();
        for (const row of rows()) row.hidden = !!text && !row.textContent!.toLocaleLowerCase().includes(text);
        (results.previousElementSibling as HTMLElement).hidden = !!text;
        results.hidden = !text;
        results.textContent = text ? this.dataset.ibResults!.replace("{0}", String(shown().length)) : "";
      }
      rove();
      sync();
    };

    this.addEventListener(
      "change",
      (event) => {
        if (fetched) {
          const target = event.target as HTMLInputElement;
          if (target === all) this.#selectAll(all.checked, rows().map(box), search);
          else if (target !== search) this.#tick(target, target.checked);
        } else if (!remote && event.target === all) {
          // Read once: each row's change comes back here, and syncs Select all.
          const on = all.checked;
          for (const b of shown()) {
            if (b.checked === on) continue;
            b.checked = on;
            b.dispatchEvent(new Event("change", { bubbles: true }));
          }
        }
        sync();
      },
      { signal }
    );
    if (!remote) {
      search?.addEventListener(
        "input",
        () => {
          this.#search = search.value;
          narrow();
        },
        { signal }
      );
    }
    this.addEventListener(
      "keydown",
      (event) => {
        const stops = [search, all, ...shown()].filter((s): s is HTMLInputElement => !!s && !s.disabled);
        const at = stops.indexOf(event.target as HTMLInputElement);
        const boxes = shown().filter((b) => !b.disabled);
        // In the search, Home and End are the caret's.
        const moves: Record<string, HTMLInputElement | undefined> = { ArrowDown: stops[at + 1], ArrowUp: stops[at - 1] };
        if (event.target !== search) Object.assign(moves, { Home: boxes[0], End: boxes[boxes.length - 1] });
        if (at < 0 || !(event.key in moves)) return;
        event.preventDefault();
        event.stopPropagation();
        moves[event.key]?.focus();
      },
      { signal }
    );
    this.addEventListener("focusin", rove, { signal });
    // A search lasts as long as the panel is open.
    const panel = this.closest<HTMLElement>("[popover]");
    panel?.addEventListener(
      "toggle",
      (event) => {
        // A list opens at its top, where what's unticked now comes first.
        if ((event as ToggleEvent).newState !== "closed") {
          if (remote) items.scrollTop = 0;
          if (fetched) this.#load(0, search, narrow);
          return;
        }
        if (fetched) {
          this.#request?.abort();
          clearTimeout(this.#searchTimer);
          this.#loading = false;
          this.removeAttribute("aria-busy");
          // What a search found isn't what opening again will ask for.
          if (this.#rowsSearch) items.replaceChildren();
          this.#next = this.#rowsSearch ? null : this.#next;
          this.#rowsSearch = "";
        }
        if (!search) return;
        search.value = this.#search = "";
        narrow();
      },
      { signal }
    );

    // A static page's response changes the checked attributes, and a bound page's render the count, or what C# says.
    const observer = new MutationObserver(sync);
    observer.observe(this, { attributes: true, subtree: true, attributeFilter: ["checked", "data-ib-ticked", "data-ib-all"] });
    signal.addEventListener("abort", () => observer.disconnect());

    if (remote) {
      setupLoadOnScroll(this, ".ib-filter-list-items > label:last-of-type", "[data-ib-load-next]", signal);
      // Load more goes when there's no more, and new rows are above it: focus goes to the first of them.
      let from = -1;
      this.addEventListener(
        "click",
        (event) => {
          const pressed = event.target as Element;
          const more = pressed.closest("[data-ib-list-more]");
          const retry = pressed.closest("[data-ib-list-retry]");
          // Retry goes as it answers.
          if (retry) search?.focus();
          if (!fetched) {
            if (more) from = rows().length;
            return;
          }
          if (retry) this.#load(this.#lastSkip, search, narrow);
          // The trigger stays while its page loads, and anything changing here presses it again. So one
          // at a time, not after a failure, which is Retry's, and not over a search that's on its way.
          // Load more pressed as a page loads wants that page's first row.
          else if ((more || pressed.closest("[data-ib-load-next]")) && this.#next !== null && !this.#failed && (search?.value.trim() ?? "") === this.#rowsSearch) {
            if (more) from = rows().length;
            if (!this.#loading) this.#load(this.#next, search, narrow);
          }
        },
        { signal }
      );
      const arrivals = new MutationObserver(() => {
        narrow();
        const first = from < 0 ? undefined : rows()[from];
        if (first) {
          from = -1;
          box(first).focus();
        }
      });
      arrivals.observe(items, { childList: true });
      signal.addEventListener("abort", () => arrivals.disconnect());
    }

    if (fetched) this.#setupFetched(signal, search, narrow, panel);
    if (search && !remote && search.value !== this.#search) search.value = this.#search;
    narrow();
  }

  #setupFetched(signal: AbortSignal, search: HTMLInputElement | null, narrow: () => void, panel: HTMLElement | null): void {
    const asked = () => {
      clearTimeout(this.#searchTimer);
      this.#searchTimer = window.setTimeout(() => this.#load(0, search, narrow), PAUSE);
      narrow();
    };
    this.addEventListener("input", (event) => {
      if (event.target === search && !(event as InputEvent).isComposing) asked();
    }, { signal });
    this.addEventListener("compositionend", (event) => {
      if (event.target === search) asked();
    }, { signal });
    // Enter and Apply are done with the panel, which <ib-table> closes, so what's waiting goes now.
    this.addEventListener("keydown", (event) => {
      if (event.key !== "Enter" || event.isComposing || event.target instanceof HTMLButtonElement) return;
      this.#apply();
    }, { signal });
    panel?.addEventListener("click", (event) => {
      if ((event.target as Element).closest(".ib-table-filter-apply")) this.#apply();
    }, { signal });
    // Back or Forward: the address decides. A tag's link or a Clear removes what's waiting, as <ib-table>'s own ticks.
    const drop = () => {
      this.#pending = null;
      narrow();
    };
    window.addEventListener("popstate", drop, { signal });
    // Closed without Apply, what's waiting is gone. After Apply, it was on its way already.
    panel?.addEventListener("toggle", (event) => {
      if ((event as ToggleEvent).newState === "closed" && !this.#applied) drop();
    }, { signal });
    document.addEventListener("click", (event) => {
      const link = event.target instanceof Element ? event.target.closest("a[href]") : null;
      if (!link || event.ctrlKey || event.metaKey || event.shiftKey) return;
      const table = link.closest("ib-table");
      if (!table || (table === this.closest("ib-table") && link.closest(".ib-table-filter-tags, [data-ib-filter-clear]"))) drop();
    }, { signal });

    // This setup runs as a response arrives: what was on its way is in the address, and it's the one to
    // believe. What's still being ticked in an open panel stays.
    if (this.#applied || !panel?.matches(":popover-open")) this.#pending = null;
    this.#applied = false;

    // Opened before this script arrived.
    if (panel?.matches(":popover-open") && !this.#first && !this.#loading) this.#load(0, search, narrow);
  }

  #same(a: string | null, b: string | null): boolean {
    if (a === b) return true;
    return a !== null && b !== null && this.hasAttribute("data-ib-ignore-case") && a.toLowerCase() === b.toLowerCase();
  }

  #valueOf(input: HTMLInputElement): string | null {
    return input.hasAttribute("data-ib-blank") ? null : input.value;
  }

  // What the address says of this column, or what's waiting to go there.
  #ticks(): Ticks {
    if (this.#pending) return this.#pending;
    const key = this.dataset.ibListKey!;
    const query = new URLSearchParams(location.search);
    const op = (query.get(`${key}-op`) ?? "").toLowerCase();
    const values: (string | null)[] = query.getAll(key).filter((v) => v !== "");
    if (query.get(`${key}-blank`)) values.push(null);
    if (op === "in" || op === "notin") return { op, none: false, values };
    // As ColumnFilter reads it: a tick outweighs the none marker.
    if (!query.get(`${key}-none`)) return { op: "", none: false, values: [] };
    return values.length > 0 ? { op: "in", none: false, values } : { op: "", none: true, values: [] };
  }

  #isTicked(ticks: Ticks, value: string | null): boolean {
    if (ticks.none) return false;
    const listed = ticks.values.some((v) => this.#same(v, value));
    return ticks.op === "in" && ticks.values.length > 0 ? listed : ticks.op === "notin" ? !listed : true;
  }

  // As ColumnFilter.TickValue: what's ticked once Select all has been cleared, what's unticked from all ticked.
  #withTick(ticks: Ticks, value: string | null, on: boolean): Ticks {
    if (this.#isTicked(ticks, value) === on) return ticks;
    const inList = !ticks.none && ticks.op === "in" && ticks.values.length > 0;
    const named = inList || (!ticks.none && ticks.op === "notin") ? ticks.values : [];
    const op = inList || ticks.none ? "in" : "notin";
    if (on === (op === "in") && named.length >= Number(this.dataset.ibMax)) return ticks;
    const next = on === (op === "in") ? [...named, value] : named.filter((v) => !this.#same(v, value));
    return next.length > 0 ? { op, none: false, values: next } : { op: "", none: op === "in", values: [] };
  }

  #tick(input: HTMLInputElement, on: boolean): void {
    this.#wait(this.#withTick(this.#ticks(), this.#valueOf(input), on), [...this.querySelectorAll<HTMLInputElement>(".ib-filter-list-items > label input")]);
  }

  #selectAll(on: boolean, boxes: HTMLInputElement[], search: HTMLInputElement | null): void {
    if (!this.#canSelectAll(this.#ticks(), boxes, search)) return;
    if (!this.#rowsSearch) {
      this.#wait({ op: "", none: !on, values: [] }, boxes);
      return;
    }
    let next = this.#ticks();
    for (const b of boxes) next = this.#withTick(next, this.#valueOf(b), on);
    this.#wait(next, boxes);
  }

  // With every value there is on these rows and every one the same, all ticked is no filter and none
  // ticked is none, however it got there.
  #wait(ticks: Ticks, boxes: HTMLInputElement[]): void {
    const every = this.#whole && boxes.length > 0 && !ticks.none && ticks.values.length > 0 && boxes.every((b) => ticks.values.some((v) => this.#same(v, this.#valueOf(b))));
    this.#pending = every ? { op: "", none: ticks.op === "notin", values: [] } : ticks;
  }

  // Onto the address as it is now, so a sort or a page made in the pause stands.
  #apply(): void {
    const ticks = this.#pending;
    if (!ticks || !this.isConnected) return;
    const key = this.dataset.ibListKey!;
    const url = new URL(location.href);
    for (const name of [key, `${key}-op`, `${key}-to`, `${key}-blank`, `${key}-none`]) url.searchParams.delete(name);
    if (ticks.none) url.searchParams.set(`${key}-none`, "1");
    else if (ticks.values.length > 0) {
      url.searchParams.set(`${key}-op`, ticks.op);
      for (const value of ticks.values) if (value !== null) url.searchParams.append(key, value);
      if (ticks.values.includes(null)) url.searchParams.set(`${key}-blank`, "1");
    }
    const table = this.closest<HTMLElement>("ib-table");
    if (table?.dataset.ibPageKey) url.searchParams.delete(table.dataset.ibPageKey);
    if (table?.dataset.ibCursorKey) url.searchParams.delete(table.dataset.ibCursorKey);
    // Ticks that came back to where they started aren't a place in the history.
    this.#applied = navigateTo(url, false);
    if (!this.#applied) this.#pending = null;
  }

  #canSelectAll(ticks: Ticks, boxes: HTMLInputElement[], search: HTMLInputElement | null): boolean {
    if (this.#failed || (search?.value.trim() ?? "") !== this.#rowsSearch) return false;
    if (!this.#rowsSearch) return true;
    return this.#next === null && !this.#isFull(boxes) && boxes.length > 0 && (ticks.none || ticks.values.length < Number(this.dataset.ibMax));
  }

  #isFull(boxes: HTMLInputElement[]): boolean {
    return boxes.length >= Number(this.dataset.ibMaxRows);
  }

  // Everything a fetched list shows comes from here, as neither the server nor a render can say it.
  #paint(all: HTMLInputElement, search: HTMLInputElement | null, results: HTMLElement, boxes: HTMLInputElement[]): void {
    const ticks = this.#ticks();
    const limit = !ticks.none && ticks.values.length >= Number(this.dataset.ibMax);
    let ticked = 0;
    for (const b of boxes) {
      b.checked = this.#isTicked(ticks, this.#valueOf(b));
      if (b.checked) ticked++;
      b.disabled = limit && b.checked === (ticks.op === "notin");
    }
    const filtered = ticks.none || ticks.values.length > 0;
    const searching = !!this.#rowsSearch;
    all.checked = searching ? ticked > 0 && ticked === boxes.length : !filtered;
    all.indeterminate = searching ? ticked > 0 && ticked < boxes.length : filtered && !ticks.none;
    all.disabled = !this.#canSelectAll(ticks, boxes, search);

    const tooLong = searching && (this.#next !== null || this.#isFull(boxes));
    (results.previousElementSibling as HTMLElement).hidden = searching && !tooLong;
    results.hidden = !searching || tooLong;
    results.textContent = this.dataset.ibResults!.replace("{0}", String(boxes.length));
    const text = this.#failed ? this.dataset.ibTextFailed
      : searching && boxes.length === 0 && !this.#loading ? this.dataset.ibTextNone
      : limit ? this.dataset.ibTextLimit
      : tooLong || this.#isFull(boxes) ? this.dataset.ibTextMore
      : "";
    const status = this.querySelector<HTMLElement>(".ib-filter-list-status")!;
    if (status.textContent !== text) status.textContent = text ?? "";
    this.querySelector<HTMLElement>("[data-ib-list-retry-row]")!.hidden = !this.#failed;
    this.querySelector<HTMLElement>("[data-ib-list-loading]")!.hidden = !(this.#loading && boxes.length === 0);
  }

  // The first page from the address as it is, and later ones from that same address, as a page counts
  // from what was kept when the first was asked. An answer is the page's own HTML, with the list drawn.
  async #load(skip: number, search: HTMLInputElement | null, narrow: () => void): Promise<void> {
    if (skip === 0) clearTimeout(this.#searchTimer);
    this.#request?.abort();
    const request = (this.#request = new AbortController());
    const words = search?.value.trim() ?? "";
    const key = this.dataset.ibValuesKey!;
    const url = new URL(skip > 0 ? this.#first : location.href);
    url.hash = "";
    url.searchParams.set(key, this.dataset.ibValuesField!);
    if (skip === 0) {
      url.searchParams.delete(`${key}-skip`);
      if (words) url.searchParams.set(`${key}-search`, words);
      else url.searchParams.delete(`${key}-search`);
      this.#first = url.href;
    } else {
      url.searchParams.set(`${key}-skip`, String(skip));
    }
    this.#lastSkip = skip;
    this.#loading = true;
    this.#failed = false;
    this.setAttribute("aria-busy", "true");
    narrow();

    let answer: Element | null = null;
    try {
      answer = await fetchPart(url, `ib-filter-list[data-ib-list-key="${CSS.escape(this.dataset.ibListKey!)}"][data-ib-answered]`, request.signal);
    } catch {
      if (request.signal.aborted) return;
    }
    if (request !== this.#request) return;

    this.#loading = false;
    this.removeAttribute("aria-busy");
    if (!answer || answer.hasAttribute("data-ib-load-failed")) {
      this.#failed = true;
      narrow();
      return;
    }

    const items = this.querySelector<HTMLElement>(".ib-filter-list-items")!;
    const next = answer.getAttribute("data-ib-next");
    this.#whole = answer.hasAttribute("data-ib-whole");
    this.#rowsSearch = skip > 0 ? this.#rowsSearch : words;
    const valueOf = (row: Element) => this.#valueOf(row.querySelector("input")!);
    const present = [...items.querySelectorAll<HTMLLabelElement>(":scope > label")];
    const incoming = [...answer.querySelectorAll<HTMLLabelElement>(".ib-filter-list-items > label")];
    const room = Number(this.dataset.ibMaxRows);

    // A row already here stays the same element, with focus and what it's been told, and a later page
    // only adds. Rows taken from another document don't say what's ticked here: painting does that.
    const taken = (row: HTMLLabelElement) => present.find((p) => valueOf(p) === valueOf(row)) ?? (document.importNode(row, true) as HTMLLabelElement);
    const wanted = skip > 0
      ? [...present, ...incoming.filter((row) => !present.some((p) => valueOf(p) === valueOf(row))).map(taken)].slice(0, room)
      : incoming.map(taken);
    this.#next = next !== null && wanted.length < room ? Number(next) : null;

    const active = document.activeElement as HTMLElement | null;
    const hadFocus = !!active && items.contains(active);
    for (const row of present) if (!wanted.includes(row)) row.remove();
    let at: ChildNode | null = items.firstChild;
    for (const row of wanted) {
      if (row === at) at = at.nextSibling;
      else items.insertBefore(row, at);
    }
    const buttons = [...items.querySelectorAll(":scope > button")];
    if (this.#next === null) for (const button of buttons) button.remove();
    else if (buttons.length === 0) items.append(...[...answer.querySelectorAll(".ib-filter-list-items > button")].map((node) => document.importNode(node, true)));
    // Moving a row drops focus, and its row may have gone: back on it, or on the box.
    if (hadFocus && document.activeElement !== active) {
      if (active.isConnected) active.focus({ preventScroll: true });
      else search?.focus({ preventScroll: true });
    }
    narrow();
  }
}

defineElement("ib-filter-list", FilterList);
