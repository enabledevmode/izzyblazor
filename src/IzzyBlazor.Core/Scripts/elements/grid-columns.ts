import { IzzyElement, defineElement } from "../izzy-element";
import { readCookie, writeCookie } from "../cookies";
import type { ChipHost } from "./grid-group";

// Moving a grouped-by chip, by drag or keys: its own part (grid-group.ts).
const loadGroup = () => import("./parts/grid-group.js" as string) as Promise<typeof import("./grid-group")>;

/**
 * <ib-grid-columns> is an empty element in a DataGrid with resizable or reorderable columns or a
 * picker. It lets people rearrange the columns, by pointer, keys, each header's menu and the picker,
 * and saves the result as a DataGridLayout: in the cookie (data-ib-layout-key), at an address the app
 * maps (data-ib-save-url), and on interactive pages to C# (iblayout), which draws it. A static page
 * asks for itself again by enhanced navigation, so the server stays the one that draws columns.
 * Only a width changed by dragging needs no round trip: it's a variable the table already uses. A
 * column dropped stays where it was dropped until the server has drawn it there, as it would otherwise
 * go back for as long as the response takes.
 */

interface ColumnEntry {
  k: string;
  w?: string;
  p?: number;
  h?: boolean;
  f?: number;
}

const COOKIE_LIMIT = 4000;
const KEY_STEP = 16;
const DRAG_START = 6;
const FROZEN = { none: 0, start: 1, end: 2 };

class GridColumns extends IzzyElement {
  #host: HTMLElement | null = null;
  #table: HTMLTableElement | null = null;
  // The column whose header takes focus when the new layout has been drawn.
  #refocus: string | null = null;
  // Cells kept where a drop put them, until the layout they're waiting for is drawn.
  #held: HTMLElement[] = [];

  protected setup(signal: AbortSignal): void {
    const host = this.closest<HTMLElement>("ib-table");
    const table = host?.querySelector<HTMLTableElement>(":scope > .ib-table-scroll > table");
    if (!host || !table) return;
    this.#host = host;
    this.#table = table;

    host.addEventListener("click", (event) => this.#onClick(event), { signal });
    // A chip in the bar of what's grouped moves among the others.
    host.addEventListener("pointerdown", (event) => {
      const chip = this.#mine((event.target as Element).closest<HTMLElement>(".ib-grid-group-chip"));
      if (!chip || event.button || event.pointerType === "touch" || (event.target as Element).closest("a, button")) return;
      // Or it selects the chips' text, and a drag of that cancels this one.
      event.preventDefault();
      // Let go before the part arrives: a click, not a drag, which opens the chip's menu. (Left
      // listening, it only sets this flag.)
      let up = false;
      document.addEventListener("pointerup", () => (up = true), { once: true, capture: true });
      loadGroup().then((m) => !signal.aborted && m.dragChip(this.#chipHost(), event, chip, signal, up));
    }, { signal });
    // Or by Alt+Left and Alt+Right on its remove control, the one part of it that takes focus.
    host.addEventListener("keydown", (event) => {
      if (!event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) return;
      const chip = this.#mine((event.target as Element).closest<HTMLElement>(".ib-grid-group-chip"));
      if (!chip || (event.key !== "ArrowLeft" && event.key !== "ArrowRight")) return;
      // Alt+Left is the browser's Back, even at the end of the row.
      event.preventDefault();
      if (!event.repeat) loadGroup().then((m) => !signal.aborted && m.moveChip(this.#chipHost(), chip, event.key === "ArrowRight"));
    }, { signal });
    host.addEventListener("focusin", (event) => {
      const control = (event.target as Element).closest?.(".ib-grid-group-chip a, .ib-grid-group-chip button");
      if (!this.#mine(control)) return;
      // Blazor keeps the control when the other levels go: the keys go with them.
      if (this.#chips.length > 1) control!.setAttribute("aria-keyshortcuts", "Alt+ArrowLeft Alt+ArrowRight");
      else control!.removeAttribute("aria-keyshortcuts");
    }, { signal });
    host.addEventListener("change", (event) => this.#onChange(event), { signal });
    table.addEventListener("pointerdown", (event) => this.#onPointerDown(event, signal), { signal });
    table.addEventListener("dblclick", (event) => this.#onDoubleClick(event), { signal });
    table.addEventListener("keydown", (event) => this.#onKey(event), { signal, capture: true });
    // A sort link would start the browser's own drag.
    table.addEventListener("dragstart", (event) => {
      if ((event.target as Element).closest?.("th[data-ib-reorder], th[data-ib-group]")) event.preventDefault();
    }, { signal });

    const changes = new MutationObserver(() => this.#arrive());
    changes.observe(table, { attributes: true, subtree: true, attributeFilter: ["data-ib-col"] });
    signal.addEventListener("abort", () => changes.disconnect());
    this.#adoptCookie();
    this.#arrive();
  }

  // A first render that couldn't read the cookie (no prerender, or the response had started): C#
  // gets it now, and the next change builds on it rather than replacing it.
  #adoptCookie(): void {
    const key = this.dataset.ibLayoutKey;
    const saved = key && !this.dataset.ibLayout ? readCookie(`ib-grid-${key}`) : null;
    if (!saved) return;
    this.dataset.ibLayout = saved;
    if (this.hasAttribute("data-ib-interactive")) this.dispatchEvent(new CustomEvent("iblayout", { bubbles: true, detail: { layout: saved } }));
  }

  #letGo(): void {
    for (const cell of this.#held) cell.style.transform = "";
    this.#held = [];
  }

  #arrive(): void {
    this.#letGo();
    if (!this.#refocus) return;
    const header = this.#refocus.startsWith("band:") ? this.#bandHeader(this.#refocus.slice(5)) : this.#header(this.#refocus);
    const grid = this.closest("ib-table")?.querySelector("ib-grid") as (Element & { focusCell?: (cell: HTMLTableCellElement) => void }) | null;
    if (header) grid?.focusCell?.(header);
    this.#refocus = null;
  }

  #header(key: string): HTMLTableCellElement | null {
    return [...(this.#table?.querySelectorAll<HTMLTableCellElement>(":scope > thead th[data-ib-col]") ?? [])].find((th) => th.dataset.ibCol === key) ?? null;
  }

  #bandHeader(id: string): HTMLTableCellElement | null {
    return this.#table?.querySelector<HTMLTableCellElement>(`:scope > thead th[data-ib-band="${id}"]`) ?? null;
  }

  // What the new layout's focus goes back to: a column's key, or a band's number.
  #keyOf(th: HTMLTableCellElement): string {
    return th.dataset.ibBand !== undefined ? `band:${th.dataset.ibBand}` : th.dataset.ibCol!;
  }

  get #headers(): HTMLTableCellElement[] {
    return [...(this.#table?.querySelectorAll<HTMLTableCellElement>(":scope > thead th[data-ib-col]") ?? [])];
  }

  #layout(): ColumnEntry[] {
    try {
      return (JSON.parse(this.dataset.ibLayout || "{}") as { c?: ColumnEntry[] }).c ?? [];
    } catch {
      return [];
    }
  }

  #entry(columns: ColumnEntry[], key: string): ColumnEntry {
    let entry = columns.find((c) => c.k.toLowerCase() === key.toLowerCase());
    if (!entry) {
      entry = { k: key };
      columns.push(entry);
    }
    return entry;
  }

  /** Saves a layout, then has it drawn, and says whether it went ahead. */
  async #save(columns: ColumnEntry[], draw: boolean, focus: string | null): Promise<boolean> {
    const kept = columns.filter((c) => c.w !== undefined || c.p !== undefined || c.h !== undefined || c.f !== undefined);
    const json = kept.length > 0 ? JSON.stringify({ c: kept }) : "";
    const key = this.dataset.ibLayoutKey;
    const interactive = this.hasAttribute("data-ib-interactive");
    // As the browser counts it: a cookie too long is dropped with no word.
    if (key && encodeURIComponent(`ib-grid-${key}=${json}`).length > COOKIE_LIMIT) {
      console.warn(`IzzyBlazor: the "${key}" grid's layout is too large for a cookie, and wasn't saved. Bind Layout to keep it elsewhere.`);
      if (!interactive) return false;
    } else if (key) {
      writeCookie(`ib-grid-${key}`, json, true, location.pathname);
    }
    const url = this.dataset.ibSaveUrl;
    if (url) {
      const saved = await fetch(url, { method: "POST", headers: { "Content-Type": "application/json" }, body: json || "{}" }).then((r) => r.ok, () => false);
      if (!saved) {
        console.warn(`IzzyBlazor: a grid's layout couldn't be saved to ${url}.`);
        if (!interactive) return false;
      }
    }
    this.dataset.ibLayout = json;
    this.#refocus = focus;

    if (interactive) {
      this.dispatchEvent(new CustomEvent("iblayout", { bubbles: true, detail: { layout: json } }));
    } else if (draw) {
      const blazor = (window as unknown as { Blazor?: { navigateTo?: (url: string, options: object) => void } }).Blazor;
      // Not to its hash: Blazor would scroll back to that section, from wherever someone has got to.
      if (blazor?.navigateTo) blazor.navigateTo(location.pathname + location.search, { replaceHistoryEntry: true });
      else location.reload();
    }
    return true;
  }

  #say(th: HTMLElement): void {
    const width = Math.round(th.offsetWidth);
    const status = this.#host?.querySelector("[data-ib-col-say]");
    if (status) status.textContent = (this.dataset.ibWidthText ?? "{0}: {1}").replace("{0}", th.dataset.ibName ?? th.querySelector(".ib-table-title")?.textContent?.trim() ?? "").replace("{1}", String(width));
  }

  #side(el: Element): number {
    return el.matches(".ib-frozen-start") ? 1 : el.matches(".ib-frozen-end") ? 2 : 0;
  }

  // The numbers of the bands a header is in, outermost first; a band's header ends with its own.
  #path(el: Element): string[] {
    return (el.getAttribute("data-ib-path") ?? "").split(".").filter(Boolean);
  }

  /**
   * What a header moves among: its column, or for a band's header all its band's columns, is one
   * block, and the others are the columns and bands beside it in the same band and frozen zone
   * (start, the rest and end don't mix) that can move. Each block is its column headers, in order.
   */
  #blocks(th: HTMLTableCellElement): { blocks: HTMLTableCellElement[][]; from: number; depth: number } {
    const band = th.dataset.ibBand;
    const path = this.#path(th);
    const parent = band === undefined ? path : path.slice(0, -1);
    const mine = band === undefined ? `c${th.dataset.ibCol}` : `b${band}`;
    const side = this.#side(th);
    const all: { key: string; headers: HTMLTableCellElement[] }[] = [];
    for (const header of this.#headers) {
      const at = this.#path(header);
      if (this.#side(header) !== side || !parent.every((id, i) => at[i] === id)) continue;
      const key = at.length > parent.length ? `b${at[parent.length]}` : `c${header.dataset.ibCol}`;
      const last = all[all.length - 1];
      if (last?.key === key) last.headers.push(header);
      else all.push({ key, headers: [header] });
    }
    const moves = (block: { key: string; headers: HTMLTableCellElement[] }) =>
      block.key === mine || !!(block.key[0] === "b" ? this.#bandHeader(block.key.slice(1)) : block.headers[0])?.hasAttribute("data-ib-reorder");
    const blocks = all.filter(moves);
    return { blocks: blocks.map((block) => block.headers), from: blocks.findIndex((block) => block.key === mine), depth: parent.length };
  }

  /** Moves a column, or a band with its columns, to a place among the blocks beside it: false if it's there. */
  #moveTo(th: HTMLTableCellElement, to: number, focus: boolean): boolean {
    const { blocks, from } = this.#blocks(th);
    const target = Math.max(0, Math.min(blocks.length - 1, to));
    if (from < 0 || from === target) return false;
    const moving = blocks[from];
    const order = this.#headers.filter((header) => !moving.includes(header));
    const anchor = target > from ? blocks[target][blocks[target].length - 1] : blocks[target][0];
    order.splice(order.indexOf(anchor) + (target > from ? 1 : 0), 0, ...moving);
    const columns = this.#layout();
    this.#withHidden(order.map((header) => header.dataset.ibCol!)).forEach((key, index) => (this.#entry(columns, key).p = index));
    void this.#save(columns, true, focus ? this.#keyOf(th) : null).then((drawing) => {
      if (!drawing) this.#letGo();
    });
    return true;
  }

  // All of an outermost band's columns, hidden ones too, as its header lists them.
  #bandColumns(id: string | undefined): string[] {
    try {
      return id === undefined ? [] : (JSON.parse(this.#bandHeader(id)?.dataset.ibCols ?? "[]") as string[]);
    } catch {
      return [];
    }
  }

  // The columns shown in order, with each band's hidden ones placed beside the band's others, so
  // one shown again comes back inside the band where the band now is.
  #withHidden(keys: string[]): string[] {
    const bands = new Set(this.#headers.map((header) => this.#path(header)[0]).filter((id) => id !== undefined));
    for (const id of bands) {
      const all = this.#bandColumns(id);
      all.forEach((key, i) => {
        if (keys.includes(key)) return;
        const before = all.slice(0, i).reverse().find((other) => keys.includes(other));
        const after = all.slice(i + 1).find((other) => keys.includes(other));
        if (before !== undefined) keys.splice(keys.indexOf(before) + 1, 0, key);
        else if (after !== undefined) keys.splice(keys.indexOf(after), 0, key);
      });
    }
    return keys;
  }

  // The picker's names for what it moves: a column's key, or "band:3".
  #item(name: string): HTMLTableCellElement | null {
    return name.startsWith("band:") ? this.#bandHeader(name.slice(5)) : this.#header(name);
  }

  /**
   * For the column picker: moves a column or band (its name, as #item reads it) by a step, or to
   * the place of another (a column or band beside it).
   */
  movePicked(name: string, to: number | string): boolean {
    const th = this.#item(name);
    if (!th) return false;
    if (typeof to === "number") return this.#move(th, to, false);
    const target = to.startsWith("band:") ? this.#headers.find((h) => this.#path(h).includes(to.slice(5))) : this.#header(to);
    const index = this.#blocks(th).blocks.findIndex((block) => target && block.includes(target));
    return index >= 0 && this.#moveTo(th, index, false);
  }

  /** For the column picker: shows or hides these columns at once, keeping one shown. */
  showPicked(keys: string[], on: boolean): void {
    const columns = this.#layout();
    for (const k of keys) this.#entry(columns, k).h = !on;
    const shown = this.#headers.map((h) => h.dataset.ibCol!).filter((k) => !keys.includes(k));
    if (!on && shown.length === 0) this.#entry(columns, this.#headers[0]?.dataset.ibCol ?? keys[0]).h = false;
    void this.#save(columns, true, null);
  }

  #move(th: HTMLTableCellElement, by: number, focus: boolean): boolean {
    return this.#moveTo(th, this.#blocks(th).from + by, focus);
  }

  // Whatever the unit (a length may be in ch or vw): as the browser works it out.
  #toPixels(css: string | undefined, fallback: number): number {
    if (!css) return fallback;
    const probe = document.createElement("div");
    probe.style.cssText = `position:absolute;visibility:hidden;width:${css}`;
    this.#host!.append(probe);
    const width = probe.offsetWidth;
    probe.remove();
    return width || fallback;
  }

  #clamp(th: HTMLElement, width: number): number {
    const min = this.#toPixels(th.dataset.ibMin, 48);
    const max = this.#toPixels(th.dataset.ibMax, Infinity);
    return Math.max(min, Math.min(max, Math.round(width)));
  }

  // A header's column among the data columns, and its <col>: past the selection and groups' lead cells.
  #place(th: HTMLTableCellElement): [number, number] {
    let cells = 0;
    let cols = 0;
    for (const cell of th.parentElement!.children as HTMLCollectionOf<HTMLTableCellElement>) {
      if (!cell.matches(".ib-table-select-cell, .ib-grid-lead")) break;
      cells++;
      cols += cell.colSpan;
    }
    return [th.cellIndex - cells, th.cellIndex - cells + cols];
  }

  /** Shows a width at once: the variable everything uses, and the column if it had none. */
  #show(th: HTMLTableCellElement, width: number): void {
    const [index, at] = this.#place(th);
    this.#table!.style.setProperty(`--ib-col-${index}`, `${width}px`);
    const col = this.#table!.querySelectorAll(":scope > colgroup > col")[at] as HTMLElement | undefined;
    if (col) col.style.width = `var(--ib-col-${index})`;
  }

  #hasWidth(th: HTMLTableCellElement): boolean {
    return !!this.#table?.style.getPropertyValue(`--ib-col-${this.#place(th)[0]}`);
  }

  #setWidth(th: HTMLTableCellElement, width: number, focus: boolean, hadWidth = this.#hasWidth(th)): void {
    const clamped = this.#clamp(th, width);
    this.#show(th, clamped);
    this.#say(th);
    const columns = this.#layout();
    this.#entry(columns, th.dataset.ibCol!).w = `${clamped}px`;
    // A column with no width of its own was part of the grid's sum: the server works that out again.
    void this.#save(columns, !hadWidth, focus ? th.dataset.ibCol! : null);
  }

  #onClick(event: Event): void {
    const button = this.#mine((event.target as Element).closest<HTMLElement>("[data-ib-col-action]"));
    if (!button) return;
    const action = button.dataset.ibColAction!;
    const key = button.dataset.ibCol;
    if (action === "reset") {
      void this.#save([], true, null);
      return;
    }
    const band = button.dataset.ibBand;
    const th = band !== undefined ? this.#bandHeader(band) : key ? this.#header(key) : null;
    const columns = this.#layout();
    const rem = parseFloat(getComputedStyle(document.documentElement).fontSize);
    if (th && (action === "left" || action === "right")) this.#move(th, action === "left" ? -1 : 1, true);
    else if (th && (action === "first" || action === "last")) this.#moveTo(th, action === "first" ? 0 : 1e9, true);
    else if (th && action === "wider") this.#setWidth(th, th.offsetWidth + rem, true);
    else if (th && action === "narrower") this.#setWidth(th, th.offsetWidth - rem, true);
    else if (th && action === "fit") {
      const widest = this.#fit(th);
      if (widest > 0) this.#setWidth(th, widest, true);
    }
    else if (action === "fit-all") {
      // Measured all first, then saved once and drawn.
      const fits = this.#headers.filter((h) => h.hasAttribute("data-ib-resize")).map((h) => [h, this.#fit(h)] as const);
      for (const [h, widest] of fits) if (widest > 0) this.#entry(columns, h.dataset.ibCol!).w = `${this.#clamp(h, widest)}px`;
      void this.#save(columns, true, key ?? null);
    }
    else if (th && action === "hide") {
      const headers = this.#headers;
      const at = headers.indexOf(th);
      this.#setHidden(columns, key!, true, (headers[at + 1] ?? headers[at - 1])?.dataset.ibCol ?? null);
    }
    else if (action === "choose") {
      // The toolbar's picker, as if its button were pressed.
      this.#host?.querySelector<HTMLElement>(".ib-col-picker [popovertarget]")?.click();
    }
    else if (th && action === "reset-width") {
      delete this.#entry(columns, key!).w;
      void this.#save(columns, true, key!);
    }
    else if (th && (action === "freeze-start" || action === "freeze-end" || action === "unfreeze")) {
      // A line, as a sheet's frozen panes, nothing moving: Pin left pins every column to this one's
      // left, Pin right every one to its right (a band's edges for a column in one, as a band is
      // pinned whole), and pinning again moves the line: what falls outside it on that side scrolls.
      // No pin unpins this column's side, or every pin from one that scrolls. A band's hidden columns
      // go with it, so it stays whole when they're shown again.
      const headers = this.#headers;
      const outer = this.#path(th)[0];
      const own = outer === undefined ? [th] : headers.filter((h) => this.#path(h)[0] === outer);
      const first = headers.indexOf(own[0]);
      const last = headers.indexOf(own[own.length - 1]);
      if (action === "unfreeze") {
        const side = this.#side(th);
        for (const h of headers) if (this.#side(h) && (!side || this.#side(h) === side)) this.#entry(columns, h.dataset.ibCol!).f = FROZEN.none;
        // Hidden ones too.
        for (const entry of columns) if (entry.f && (!side || entry.f === side)) entry.f = FROZEN.none;
      } else {
        const start = action === "freeze-start";
        const range = start ? headers.slice(0, first) : headers.slice(last + 1);
        if (range.length === 0) return;
        const keys = new Set(range.map((h) => h.dataset.ibCol!));
        for (const id of new Set(range.map((h) => this.#path(h)[0]).filter((id) => id !== undefined))) for (const k of this.#bandColumns(id)) keys.add(k);
        const pinned = start ? FROZEN.start : FROZEN.end;
        for (const h of headers) if (!keys.has(h.dataset.ibCol!) && this.#side(h) === pinned) this.#entry(columns, h.dataset.ibCol!).f = FROZEN.none;
        for (const entry of columns) if (entry.f === pinned && !keys.has(entry.k)) entry.f = FROZEN.none;
        // Pinned columns are placed by adding the widths up, so each needs one: a hidden one takes
        // the width of one shown beside it, until it's shown and resized.
        for (const k of keys) {
          const entry = this.#entry(columns, k);
          entry.f = start ? FROZEN.start : FROZEN.end;
          const header = this.#header(k) ?? range[0];
          entry.w ??= `${Math.round(header.offsetWidth)}px`;
        }
      }
      void this.#save(columns, true, this.#keyOf(th));
    }
  }

  #setHidden(columns: ColumnEntry[], key: string, hidden: boolean, focus: string | null = null): void {
    const entry = this.#entry(columns, key);
    if (hidden) {
      // At least one stays.
      if (this.#headers.length <= 1) return;
      entry.h = true;
    } else entry.h = false; // Said outright: a column can start hidden.
    void this.#save(columns, true, focus);
  }

  // This grid's own: not a grid's in one of its details.
  #mine<T extends Element>(el: T | null | undefined): T | null {
    return el?.closest("ib-table") === this.#host ? el : null;
  }

  #onChange(event: Event): void {
    const box = event.target as HTMLInputElement;
    const key = box.dataset?.ibColShow;
    if (!key || !this.#mine(box)) return;
    if (!box.checked && this.#headers.length <= 1) {
      box.checked = true;
      return;
    }
    this.#setHidden(this.#layout(), key, !box.checked);
  }

  #onKey(event: KeyboardEvent): void {
    if ((event.key !== "ArrowLeft" && event.key !== "ArrowRight") || event.ctrlKey || event.metaKey) return;
    if (event.shiftKey === event.altKey) return;
    const th = this.#mine((event.target as Element).closest<HTMLTableCellElement>("thead th[data-ib-col], thead th[data-ib-band]"));
    if (!th || (event.target as Element).closest("[popover], dialog") || (event.target as Element).matches("input, select, textarea")) return;
    const rtl = getComputedStyle(th).direction === "rtl";
    const forward = (event.key === "ArrowRight") !== rtl;
    if (event.repeat) return;
    if (event.altKey) {
      if (!th.hasAttribute("data-ib-reorder")) return;
      // Alt+Left is the browser's Back.
      event.preventDefault();
      event.stopPropagation();
      this.#move(th, forward ? 1 : -1, true);
    } else if (th.hasAttribute("data-ib-resize")) {
      event.preventDefault();
      event.stopPropagation();
      this.#setWidth(th, th.offsetWidth + (forward ? KEY_STEP : -KEY_STEP), true);
    }
  }

  #onPointerDown(event: PointerEvent, signal: AbortSignal): void {
    if (event.button !== 0) return;
    const target = event.target as Element;
    const handle = target.closest<HTMLElement>(".ib-col-resize");
    const th = this.#mine(target.closest<HTMLTableCellElement>("thead th[data-ib-col], thead th[data-ib-band]"));
    if (!th) return;
    if (handle) this.#dragWidth(event, th, handle, signal);
    else if ((th.hasAttribute("data-ib-reorder") || th.hasAttribute("data-ib-group")) && event.pointerType !== "touch" && !target.closest("button, input, [popover]")) this.#dragColumn(event, th, signal);
  }

  #dragWidth(down: PointerEvent, th: HTMLTableCellElement, handle: HTMLElement, signal: AbortSignal): void {
    down.preventDefault();
    const start = th.offsetWidth;
    const rtl = getComputedStyle(th).direction === "rtl";
    let width = start;
    const hadWidth = this.#hasWidth(th);
    handle.setPointerCapture(down.pointerId);
    const abort = this.#until(signal);
    const options = { signal: abort.signal };
    handle.addEventListener("pointermove", (event) => {
      width = this.#clamp(th, start + (event.clientX - down.clientX) * (rtl ? -1 : 1));
      this.#show(th, width);
    }, options);
    const end = (commit: boolean) => {
      abort.abort();
      if (commit && width !== start) this.#setWidth(th, width, false, hadWidth);
      else if (!commit || width === start) {
        if (hadWidth) this.#show(th, start);
        else this.#forget(th);
      }
    };
    handle.addEventListener("pointerup", () => end(true), options);
    handle.addEventListener("pointercancel", () => end(false), options);
    document.addEventListener("keydown", (event) => {
      if (event.key !== "Escape") return;
      event.preventDefault();
      event.stopPropagation();
      end(false);
    }, { ...options, capture: true });
  }

  // Listeners for one drag, gone with it or with the element.
  #until(signal: AbortSignal): AbortController {
    const abort = new AbortController();
    signal.addEventListener("abort", () => abort.abort(), { once: true, signal: abort.signal });
    return abort;
  }

  // A column with no width of its own, after a drag that came to nothing.
  #forget(th: HTMLTableCellElement): void {
    const [index, at] = this.#place(th);
    this.#table?.style.removeProperty(`--ib-col-${index}`);
    (this.#table?.querySelectorAll<HTMLElement>(":scope > colgroup > col")[at])?.style.removeProperty("width");
  }

  // Double-click on a handle: as wide as its header and the rows drawn.
  #onDoubleClick(event: Event): void {
    const handle = (event.target as Element).closest(".ib-col-resize");
    const th = this.#mine(handle?.closest<HTMLTableCellElement>("thead th[data-ib-col]"));
    if (!th || !this.#table) return;
    const widest = this.#fit(th);
    if (widest > 0) this.#setWidth(th, widest, false);
  }

  /** As wide as its header and the rows drawn: 0 if there's nothing to measure. */
  #fit(th: HTMLTableCellElement): number {
    const table = this.#table!;
    const handle = th.querySelector(".ib-col-resize");
    table.toggleAttribute("data-ib-measuring", true);
    const range = document.createRange();
    let widest = 0;
    // A group's row and a band's row span columns differently, and their labels aren't the column's content.
    for (const row of table.rows) {
      if (row.matches(".ib-grid-group, .ib-table-band-row")) continue;
      const cell = row.cells[th.cellIndex];
      if (!cell || cell.colSpan > 1) continue;
      const style = getComputedStyle(cell);
      range.selectNodeContents(cell);
      // Not the handle, which spans the cell's edge.
      if (cell === th && handle) range.setEndBefore(handle);
      widest = Math.max(widest, range.getBoundingClientRect().width + parseFloat(style.paddingInlineStart) + parseFloat(style.paddingInlineEnd));
    }
    table.toggleAttribute("data-ib-measuring", false);
    return widest > 0 ? Math.ceil(widest) + 1 : 0;
  }

  get #bar(): HTMLElement | null {
    return this.#host?.querySelector<HTMLElement>(".ib-grid-group-bar") ?? null;
  }

  get #chips(): HTMLElement[] {
    return [...(this.#bar?.querySelectorAll<HTMLElement>(".ib-grid-group-chip") ?? [])];
  }

  #inside(el: Element, event: PointerEvent): boolean {
    const r = el.getBoundingClientRect();
    return event.clientX >= r.left && event.clientX <= r.right && event.clientY >= r.top && event.clientY <= r.bottom;
  }

  // Where among the chips a drop lands: before the first whose middle is past the pointer.
  #dropAt(event: PointerEvent, slots: DOMRect[]): number {
    const rtl = this.#bar && getComputedStyle(this.#bar).direction === "rtl";
    const index = slots.findIndex((r) => (rtl ? event.clientX > r.left + r.width / 2 : event.clientX < r.left + r.width / 2));
    return index < 0 ? slots.length : index;
  }

  // A chip of what's dragged, where a drop would land; none at -1.
  #mark(at: number, name = ""): void {
    const bar = this.#bar;
    let ghost = bar?.querySelector<HTMLElement>(".ib-grid-group-ghost");
    if (at < 0 || !bar) {
      ghost?.remove();
      return;
    }
    if (!ghost) {
      ghost = document.createElement("span");
      ghost.className = "ib-tag ib-tag-sm ib-grid-group-ghost";
      ghost.setAttribute("aria-hidden", "true");
      ghost.textContent = name;
    }
    const before = this.#chips[at] ?? bar.querySelector(".ib-grid-group-set");
    if (ghost.nextElementSibling !== before || !ghost.isConnected) bar.insertBefore(ghost, before);
  }

  // The new grouping: into the input C# reads on a bound page, or to its address.
  #group(tokens: string[]): void {
    const bar = this.#bar;
    const text = tokens.join(",");
    const input = bar?.querySelector<HTMLInputElement>(".ib-grid-group-set");
    if (input) {
      input.value = text;
      input.dispatchEvent(new Event("change", { bubbles: true }));
      return;
    }
    const href = bar?.dataset.ibGroupHref?.replace("_GROUP_", encodeURIComponent(text));
    if (!href) return;
    const blazor = (window as unknown as { Blazor?: { navigateTo?: (url: string) => void } }).Blazor;
    if (blazor?.navigateTo) blazor.navigateTo(href);
    else location.assign(href);
  }

  #chipHost(): ChipHost {
    return {
      host: this.#host!,
      bar: () => this.#bar,
      chips: () => this.#chips,
      group: (tokens) => this.#group(tokens),
      mark: (at, name) => this.#mark(at, name),
      inside: (el, event) => this.#inside(el, event),
      dropAt: (event, slots) => this.#dropAt(event, slots),
      until: (signal) => this.#until(signal),
    };
  }

  // Dragging a header: its column follows the pointer, and the others slide aside.
  #dragColumn(down: PointerEvent, th: HTMLTableCellElement, signal: AbortSignal): void {
    const table = this.#table!;
    // A header that can be grouped but not reordered only goes to the bar.
    const moves = th.hasAttribute("data-ib-reorder");
    const { blocks: zone, from, depth } = moves ? this.#blocks(th) : { blocks: [[th]], from: 0, depth: 0 };
    if (from < 0) return;
    // A block's span across the table, from its first column to its last.
    const rects = zone.map((block) => {
      const left = Math.min(...block.map((header) => header.getBoundingClientRect().left));
      return { left, width: Math.max(...block.map((header) => header.getBoundingClientRect().right)) - left };
    });
    const columnOf = (header: HTMLTableCellElement) => [...table.rows].filter((row) => !row.matches(".ib-grid-group, .ib-table-band-row")).flatMap((row) => {
      const cell = row.cells[header.cellIndex];
      return cell && cell.colSpan === 1 ? [cell] : [];
    });
    // A band's block takes its own header, and those of the bands in it, along.
    const bandsOf = (block: HTMLTableCellElement[]) => {
      const path = this.#path(block[0]);
      if (path.length <= depth) return [];
      const own = path.slice(0, depth + 1).join(".");
      return [...table.querySelectorAll<HTMLTableCellElement>(":scope > thead th[data-ib-band]")].filter((band) => `${band.dataset.ibPath}.`.startsWith(`${own}.`));
    };
    const cells = zone.map((block) => [...block.flatMap(columnOf), ...bandsOf(block)]);
    const rtl = getComputedStyle(th).direction === "rtl";
    let to = from;
    let dragging = false;
    // Over the bar of what's grouped, a header that can be grouped is dropped there instead, a chip
    // of it showing where. Places are from the chips as they were, as the preview moves them.
    const bar = th.dataset.ibGroup ? this.#bar : null;
    const slots = this.#chips.map((chip) => chip.getBoundingClientRect());
    const name = th.dataset.ibName ?? th.querySelector(".ib-table-title")?.textContent?.trim() ?? "";
    let at = -1;
    const abort = this.#until(signal);
    const options = { signal: abort.signal };
    const paint = (dx: number) => {
      const width = rects[from].width;
      zone.forEach((_, i) => {
        const shift = i === from ? dx : (from < i && i <= to ? -width : to <= i && i < from ? width : 0) * (rtl ? -1 : 1);
        for (const cell of cells[i]) cell.style.transform = shift ? `translateX(${shift}px)` : "";
      });
    };
    const finish = (commit: boolean) => {
      abort.abort();
      const dropped = commit && dragging && to !== from;
      if (dropped) {
        // Exactly where the server will draw it: past the columns it crossed.
        const [low, high] = to > from ? [from + 1, to] : [to, from - 1];
        const crossed = rects.slice(low, high + 1).reduce((sum, r) => sum + r.width, 0);
        paint(crossed * (to > from ? 1 : -1) * (rtl ? -1 : 1));
        this.#held = cells.flat();
      } else {
        for (const column of cells) for (const cell of column) cell.style.transform = "";
      }
      table.removeAttribute("data-ib-dragging");
      for (const cell of cells[from]) cell.removeAttribute("data-ib-lifted");
      this.#mark(-1);
      if (!dragging) return;
      // The click after a drag isn't a sort. It goes to whatever the press and release share, or
      // nowhere, so it's caught on the document, and only for this moment.
      const swallow = (event: Event) => {
        event.preventDefault();
        event.stopPropagation();
      };
      document.addEventListener("click", swallow, { capture: true, once: true });
      setTimeout(() => document.removeEventListener("click", swallow, { capture: true }), 0);
      if (commit && at >= 0) {
        const tokens = this.#chips.map((chip) => chip.dataset.ibGroupToken!);
        tokens.splice(at, 0, th.dataset.ibGroup!);
        this.#group(tokens);
      } else if (commit && to !== from) this.#moveTo(th, to, true);
    };
    document.addEventListener("pointermove", (event) => {
      const dx = event.clientX - down.clientX;
      if (!dragging) {
        if (Math.max(Math.abs(dx), Math.abs(event.clientY - down.clientY)) < DRAG_START) return;
        dragging = true;
        table.setAttribute("data-ib-dragging", "");
        if (moves) for (const cell of cells[from]) cell.setAttribute("data-ib-lifted", "");
      }
      at = bar && this.#inside(bar, event) ? this.#dropAt(event, slots) : -1;
      this.#mark(at, name);
      if (at >= 0 || !moves) {
        to = from;
        paint(0);
        return;
      }
      to = rects.findIndex((r) => (rtl ? event.clientX > r.left + r.width / 2 : event.clientX < r.left + r.width / 2));
      to = to < 0 ? rects.length - 1 : to > from ? to - 1 : to;
      paint(dx);
    }, options);
    document.addEventListener("pointerup", () => finish(true), options);
    document.addEventListener("pointercancel", () => finish(false), options);
    document.addEventListener("keydown", (event) => {
      if (event.key !== "Escape") return;
      event.preventDefault();
      event.stopPropagation();
      finish(false);
    }, { ...options, capture: true });
  }
}

defineElement("ib-grid-columns", GridColumns);
