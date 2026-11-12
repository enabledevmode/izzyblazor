import { IzzyElement, defineElement } from "../izzy-element";

/**
 * <ib-col-picker> is a DataGrid's column picker, in the popover of its Columns button. Its rows are
 * in the grid's order: a checkbox, a handle and the title, a band's row holding its columns in a
 * list of their own. This element does what's the picker's own: the search, opening and closing
 * bands, the tri-state boxes, moving rows (Alt+arrows, Alt+Home and End, the handle's menu and
 * dragging it), and keeping all that, and focus, across the grid drawing each change. What changes
 * the grid goes to <ib-grid-columns> (showPicked, movePicked), which keeps the grid's rules (a
 * band's columns stay together) and saves the layout; a single column's box goes to it directly.
 */

interface GridColumns extends Element {
  movePicked(name: string, to: number | string): boolean;
  showPicked(keys: string[], on: boolean): void;
}

const DRAG_START = 6;
const FAR = 1e9;

// Kept across the grid drawing a change (which can replace this element, or move a row's name onto
// another row's elements), for each grid by its layout key: the moved row whose handle gets focus
// back, the search, and the bands closed.
interface Kept {
  refocus: string | null;
  query: string;
  closed: Set<string>;
}
const kept = new Map<string, Kept>();

class ColPicker extends IzzyElement {
  protected setup(signal: AbortSignal): void {
    this.#redrawn();

    // Capture: a handle menu's move is the picker's, before the grid's own listener takes it.
    this.addEventListener("click", (event) => this.#onClick(event), { signal, capture: true });
    this.addEventListener("change", (event) => this.#onChange(event), { signal });
    this.addEventListener("input", (event) => {
      if (!(event.target as Element).matches("[data-ib-picker-search]")) return;
      this.#kept.query = (event.target as HTMLInputElement).value;
      this.#search();
    }, { signal });
    this.addEventListener("keydown", (event) => this.#onKey(event), { signal });
    this.addEventListener("pointerdown", (event) => {
      const handle = (event.target as Element).closest<HTMLElement>("[data-ib-picker-handle]");
      if (handle && event.button === 0 && event.pointerType !== "touch") this.#drag(event, handle, signal);
    }, { signal });

    const changes = new MutationObserver(() => this.#redrawn());
    changes.observe(this, { childList: true, subtree: true, attributes: true, attributeFilter: ["data-ib-picker-item", "data-ib-mixed"] });
    signal.addEventListener("abort", () => changes.disconnect());
  }

  get #kept(): Kept {
    const key = this.#grid?.getAttribute("data-ib-layout-key") ?? "";
    let state = kept.get(key);
    if (!state) kept.set(key, (state = { refocus: null, query: "", closed: new Set() }));
    return state;
  }

  get #grid(): GridColumns | null {
    // Its grid's, or outside it (a tile's header), the grid its part names.
    const part = this.closest<HTMLElement>(".ib-part")?.dataset.ibGrid;
    const table = this.closest("ib-table") ?? (part ? document.querySelector(`ib-table[data-ib-grid-id="${CSS.escape(part)}"]`) : null);
    return table?.querySelector<GridColumns>("ib-grid-columns") ?? null;
  }

  #name(el: Element): string | null {
    return el.closest<HTMLElement>("[data-ib-picker-item]")?.dataset.ibPickerItem ?? null;
  }

  #row(name: string): HTMLElement | null {
    return this.querySelector<HTMLElement>(`[data-ib-picker-item="${CSS.escape(name)}"]`);
  }

  // After each drawing: the tri-state boxes, closed bands, the search, and focus on a moved row.
  #redrawn(): void {
    for (const box of this.querySelectorAll<HTMLInputElement>("[data-ib-picker-all], [data-ib-picker-keys]")) box.indeterminate = box.hasAttribute("data-ib-mixed");
    for (const toggle of this.querySelectorAll<HTMLElement>("[data-ib-picker-toggle]")) {
      const name = this.#name(toggle);
      if (name) this.#expand(toggle, !this.#kept.closed.has(name), false);
    }
    const search = this.querySelector<HTMLInputElement>("[data-ib-picker-search]");
    const state = this.#kept;
    if (search && search.value !== state.query) search.value = state.query;
    this.#search();
    if (!state.refocus) return;
    const handle = this.#row(state.refocus)?.querySelector<HTMLElement>(":scope > .ib-col-picker-row [data-ib-picker-handle]");
    if (handle) {
      state.refocus = null;
      handle.focus();
    }
  }

  // Moves a row: a step, an end, or to another row's place. Focus comes back to it if it moved.
  #move(name: string, to: number | string): void {
    const state = this.#kept;
    state.refocus = name;
    if (!this.#grid?.movePicked(name, to)) state.refocus = null;
  }

  #onClick(event: Event): void {
    const target = event.target as Element;
    const action = target.closest<HTMLElement>("[data-ib-col-action]");
    const step = ({ left: -1, right: 1, first: -FAR, last: FAR } as Record<string, number>)[action?.dataset.ibColAction ?? ""];
    const name = action && this.#name(action);
    if (step !== undefined && name) {
      event.stopPropagation();
      this.#move(name, step);
      return;
    }
    const toggle = target.closest<HTMLElement>("[data-ib-picker-toggle]");
    if (toggle) return this.#expand(toggle, toggle.getAttribute("aria-expanded") !== "true", true);
    const all = target.closest<HTMLElement>("[data-ib-picker-expand-all]");
    if (all) {
      const open = !all.hasAttribute("data-ib-open");
      for (const each of this.querySelectorAll<HTMLElement>("[data-ib-picker-toggle]")) this.#expand(each, open, true);
    }
  }

  #expand(toggle: HTMLElement, open: boolean, keep: boolean): void {
    toggle.setAttribute("aria-expanded", String(open));
    const list = this.querySelector<HTMLElement>(`#${CSS.escape(toggle.getAttribute("aria-controls") ?? "")}`);
    if (list) list.hidden = !open;
    const name = this.#name(toggle);
    if (keep && name) {
      if (open) this.#kept.closed.delete(name);
      else this.#kept.closed.add(name);
    }
    // Expand all is named for what it does next: open them all if any is closed.
    const all = this.querySelector<HTMLElement>("[data-ib-picker-expand-all]");
    if (all) {
      const anyClosed = [...this.querySelectorAll("[data-ib-picker-toggle]")].some((t) => t.getAttribute("aria-expanded") !== "true");
      all.toggleAttribute("data-ib-open", !anyClosed);
      all.setAttribute("aria-label", (anyClosed ? all.dataset.ibTextOpen : all.dataset.ibTextClose) ?? "");
    }
  }

  #onChange(event: Event): void {
    const box = event.target as HTMLInputElement;
    let keys: string[] | null = null;
    // Show all acts on the rows a search leaves.
    if (box.matches("[data-ib-picker-all]")) keys = [...this.querySelectorAll<HTMLInputElement>("[data-ib-col-show]")].filter((b) => !b.closest("li[hidden]")).map((b) => b.dataset.ibColShow!);
    else if (box.dataset.ibPickerKeys) keys = JSON.parse(box.dataset.ibPickerKeys) as string[];
    if (keys?.length) this.#grid?.showPicked(keys, box.checked);
  }

  // By title: a band shows when its title matches (with all its columns) or any of its columns
  // does, and opens while it holds a match.
  #search(): void {
    const text = this.#kept.query.trim().toLowerCase();
    const visit = (row: HTMLElement, matched: boolean): boolean => {
      const match = matched || (row.dataset.ibTitle ?? "").toLowerCase().includes(text);
      const any = [...row.querySelectorAll<HTMLElement>(":scope > ul > li")].map((child) => visit(child, match)).some(Boolean);
      row.hidden = !(match || any);
      const toggle = row.querySelector<HTMLElement>(":scope > .ib-col-picker-row [data-ib-picker-toggle]");
      if (toggle && text && any) this.#expand(toggle, true, false);
      return !row.hidden;
    };
    for (const row of this.querySelectorAll<HTMLElement>(".ib-col-picker-list > li")) visit(row, text === "");
  }

  #onKey(event: KeyboardEvent): void {
    const step = event.altKey ? ({ ArrowUp: -1, ArrowDown: 1, Home: -FAR, End: FAR } as Record<string, number>)[event.key] : undefined;
    if (step === undefined) return;
    const row = (event.target as Element).closest("li");
    const name = row && this.#name(row);
    if (!name || !row.querySelector(":scope > .ib-col-picker-row [data-ib-picker-handle]")) return;
    event.preventDefault();
    this.#move(name, step);
  }

  // Dragging a handle: a line shows where the row would land among the rows beside it that move.
  #drag(down: PointerEvent, handle: HTMLElement, signal: AbortSignal): void {
    const row = handle.closest("li")!;
    const rows = [...row.parentElement!.children].filter((li) => !(li as HTMLElement).hidden && li.querySelector(":scope > .ib-col-picker-row [data-ib-picker-handle]")) as HTMLElement[];
    const from = rows.indexOf(row);
    let to = from;
    let dragging = false;
    const abort = new AbortController();
    signal.addEventListener("abort", () => abort.abort(), { once: true, signal: abort.signal });
    const options = { signal: abort.signal };
    const mark = () => {
      for (const [i, li] of rows.entries()) {
        if (i === to && to !== from) li.dataset.ibDrop = to > from ? "after" : "before";
        else delete li.dataset.ibDrop;
      }
    };
    document.addEventListener("pointermove", (event) => {
      if (!dragging) {
        if (Math.abs(event.clientY - down.clientY) < DRAG_START) return;
        dragging = true;
        row.toggleAttribute("data-ib-dragging", true);
      }
      const index = rows.findIndex((li) => event.clientY < li.getBoundingClientRect().top + li.getBoundingClientRect().height / 2);
      to = index < 0 ? rows.length - 1 : index > from ? index - 1 : index;
      mark();
    }, options);
    const finish = (commit: boolean) => {
      abort.abort();
      row.removeAttribute("data-ib-dragging");
      const target = rows[to];
      to = from;
      mark();
      if (!dragging) return;
      // The click a drag ends with doesn't open the handle's menu.
      const swallow = (event: Event) => {
        event.preventDefault();
        event.stopPropagation();
      };
      document.addEventListener("click", swallow, { capture: true, once: true });
      setTimeout(() => document.removeEventListener("click", swallow, { capture: true }), 0);
      const name = this.#name(row);
      const over = target && target !== row ? this.#name(target) : null;
      if (commit && name && over) this.#move(name, over);
    };
    document.addEventListener("pointerup", () => finish(true), options);
    document.addEventListener("pointercancel", () => finish(false), options);
    document.addEventListener("keydown", (event) => {
      if (event.key !== "Escape") return;
      event.preventDefault();
      finish(false);
    }, { ...options, capture: true });
  }
}

defineElement("ib-col-picker", ColPicker);
