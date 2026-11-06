import { IzzyElement, defineElement } from "../izzy-element";

/**
 * <ib-col-picker> is a DataGrid's column picker, in the popover of its Columns button. Its rows are
 * in the grid's order: a checkbox, a handle and the title, a band's row holding its columns in a
 * list of their own. This element does what's the picker's own: the search, opening and closing
 * bands, the tri-state boxes, Alt+arrows and dragging a handle. What changes the grid goes to
 * <ib-grid-columns> (showPicked, movePicked), which keeps the grid's rules (a band's columns stay
 * together) and saves the layout; a single column's box goes to it directly, as before.
 */

interface GridColumns extends Element {
  movePicked(name: string, to: number | string): void;
  showPicked(keys: string[], on: boolean): void;
}

const DRAG_START = 6;

// What gets focus back when the grid has drawn a move: a row's name, across the redraw.
let refocus: string | null = null;

class ColPicker extends IzzyElement {
  protected setup(signal: AbortSignal): void {
    this.#redrawn();

    this.addEventListener("click", (event) => this.#onClick(event), { signal });
    this.addEventListener("change", (event) => this.#onChange(event), { signal });
    this.addEventListener("input", (event) => {
      if ((event.target as Element).matches("[data-ib-picker-search]")) this.#search((event.target as HTMLInputElement).value);
    }, { signal });
    this.addEventListener("keydown", (event) => this.#onKey(event), { signal });
    this.addEventListener("pointerdown", (event) => {
      const handle = (event.target as Element).closest<HTMLElement>("[data-ib-picker-handle]");
      if (handle && event.button === 0 && event.pointerType !== "touch") this.#drag(event, handle, signal);
    }, { signal });

    // The grid draws each change again: by enhanced navigation on a static page, which merges into
    // this element, or in place on an interactive one, which can move a row's name onto another
    // row's elements.
    const changes = new MutationObserver(() => this.#redrawn());
    changes.observe(this, { childList: true, subtree: true, attributes: true, attributeFilter: ["data-ib-picker-item", "data-ib-mixed"] });
    signal.addEventListener("abort", () => changes.disconnect());
  }

  get #grid(): GridColumns | null {
    return this.closest("ib-table")?.querySelector<GridColumns>("ib-grid-columns") ?? null;
  }

  #name(el: Element): string | null {
    return el.closest<HTMLElement>("[data-ib-picker-item]")?.dataset.ibPickerItem ?? null;
  }

  #redrawn(): void {
    for (const box of this.querySelectorAll<HTMLInputElement>("[data-ib-picker-all], [data-ib-picker-keys]")) box.indeterminate = box.hasAttribute("data-ib-mixed");
    if (!refocus) return;
    const handle = this.querySelector<HTMLElement>(`[data-ib-picker-item="${CSS.escape(refocus)}"] > .ib-col-picker-row [data-ib-picker-handle]`);
    if (handle) {
      refocus = null;
      handle.focus();
    }
  }

  #onClick(event: Event): void {
    const target = event.target as Element;
    const toggle = target.closest<HTMLElement>("[data-ib-picker-toggle]");
    if (toggle) return this.#expand(toggle, toggle.getAttribute("aria-expanded") !== "true");
    const all = target.closest<HTMLElement>("[data-ib-picker-expand-all]");
    if (all) {
      const open = all.getAttribute("aria-expanded") !== "true";
      all.setAttribute("aria-expanded", String(open));
      for (const each of this.querySelectorAll<HTMLElement>("[data-ib-picker-toggle]")) this.#expand(each, open);
    }
  }

  #expand(toggle: HTMLElement, open: boolean): void {
    toggle.setAttribute("aria-expanded", String(open));
    const list = this.querySelector<HTMLElement>(`#${CSS.escape(toggle.getAttribute("aria-controls") ?? "")}`);
    if (list) list.hidden = !open;
  }

  #onChange(event: Event): void {
    const box = event.target as HTMLInputElement;
    let keys: string[] | null = null;
    if (box.matches("[data-ib-picker-all]")) keys = [...this.querySelectorAll<HTMLInputElement>("[data-ib-col-show]")].map((b) => b.dataset.ibColShow!);
    else if (box.dataset.ibPickerKeys) keys = JSON.parse(box.dataset.ibPickerKeys) as string[];
    if (keys) this.#grid?.showPicked(keys, box.checked);
  }

  // A band shows when its title matches (with all its columns) or any of its columns does.
  #search(text: string): void {
    const query = text.trim().toLowerCase();
    const visit = (row: HTMLElement, matched: boolean): boolean => {
      const title = row.querySelector(":scope > .ib-col-picker-row > label")?.textContent?.toLowerCase() ?? "";
      const match = matched || title.includes(query);
      const rows = [...row.querySelectorAll<HTMLElement>(":scope > ul > li")];
      const any = rows.map((child) => visit(child, match)).some(Boolean);
      row.hidden = !(match || any);
      return !row.hidden;
    };
    for (const row of this.querySelectorAll<HTMLElement>(".ib-col-picker-list > li")) visit(row, query === "");
  }

  #onKey(event: KeyboardEvent): void {
    if (!event.altKey || (event.key !== "ArrowUp" && event.key !== "ArrowDown")) return;
    const row = (event.target as Element).closest("li");
    const name = row && this.#name(row);
    if (!name || !row.querySelector(":scope > .ib-col-picker-row [data-ib-picker-handle]")) return;
    event.preventDefault();
    refocus = name;
    this.#grid?.movePicked(name, event.key === "ArrowUp" ? -1 : 1);
  }

  // Dragging a handle: a line shows where the row would land among the rows beside it that move.
  #drag(down: PointerEvent, handle: HTMLElement, signal: AbortSignal): void {
    const row = handle.closest("li")!;
    const rows = [...row.parentElement!.children].filter((li) => li.querySelector(":scope > .ib-col-picker-row [data-ib-picker-handle]")) as HTMLElement[];
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
      if (commit && name && over) {
        refocus = name;
        this.#grid?.movePicked(name, over);
      }
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
