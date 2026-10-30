import { IzzyElement, defineElement } from "../izzy-element";
import { readCookie, writeCookie } from "../cookies";

/**
 * <ib-grid-columns> is an empty element in a DataGrid with resizable or reorderable columns or a
 * picker. It lets people rearrange the columns, by pointer, keys, each header's menu and the picker,
 * and saves the result as a DataGridLayout: in the cookie (data-ib-layout-key), at an address the app
 * maps (data-ib-save-url), and on interactive pages to C# (iblayout), which draws it. A static page
 * asks for itself again by enhanced navigation, so the server stays the one that draws columns.
 * Only a width changed by dragging needs no round trip: it's a variable the table already uses.
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

  protected setup(signal: AbortSignal): void {
    const host = this.closest<HTMLElement>("ib-table");
    const table = host?.querySelector<HTMLTableElement>(":scope > .ib-table-scroll > table");
    if (!host || !table) return;
    this.#host = host;
    this.#table = table;

    host.addEventListener("click", (event) => this.#onClick(event), { signal });
    host.addEventListener("change", (event) => this.#onChange(event), { signal });
    table.addEventListener("pointerdown", (event) => this.#onPointerDown(event, signal), { signal });
    table.addEventListener("dblclick", (event) => this.#onDoubleClick(event), { signal });
    table.addEventListener("keydown", (event) => this.#onKey(event), { signal, capture: true });
    // A sort link would start the browser's own drag.
    table.addEventListener("dragstart", (event) => {
      if ((event.target as Element).closest?.("th[data-ib-reorder]")) event.preventDefault();
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

  #arrive(): void {
    if (!this.#refocus) return;
    const header = this.#header(this.#refocus);
    const grid = this.closest("ib-table")?.querySelector("ib-grid") as (Element & { focusCell?: (cell: HTMLTableCellElement) => void }) | null;
    if (header) grid?.focusCell?.(header);
    this.#refocus = null;
  }

  #header(key: string): HTMLTableCellElement | null {
    return [...(this.#table?.querySelectorAll<HTMLTableCellElement>("thead th[data-ib-col]") ?? [])].find((th) => th.dataset.ibCol === key) ?? null;
  }

  get #headers(): HTMLTableCellElement[] {
    return [...(this.#table?.querySelectorAll<HTMLTableCellElement>("thead th[data-ib-col]") ?? [])];
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

  /** Saves a layout, then has it drawn. */
  async #save(columns: ColumnEntry[], draw: boolean, focus: string | null): Promise<void> {
    const kept = columns.filter((c) => c.w !== undefined || c.p !== undefined || c.h !== undefined || c.f !== undefined);
    const json = kept.length > 0 ? JSON.stringify({ c: kept }) : "";
    const key = this.dataset.ibLayoutKey;
    const interactive = this.hasAttribute("data-ib-interactive");
    // As the browser counts it: a cookie too long is dropped with no word.
    if (key && encodeURIComponent(`ib-grid-${key}=${json}`).length > COOKIE_LIMIT) {
      console.warn(`IzzyBlazor: the "${key}" grid's layout is too large for a cookie, and wasn't saved. Bind Layout to keep it elsewhere.`);
      if (!interactive) return;
    } else if (key) {
      writeCookie(`ib-grid-${key}`, json, true, location.pathname);
    }
    const url = this.dataset.ibSaveUrl;
    if (url) {
      const saved = await fetch(url, { method: "POST", headers: { "Content-Type": "application/json" }, body: json || "{}" }).then((r) => r.ok, () => false);
      if (!saved) {
        console.warn(`IzzyBlazor: a grid's layout couldn't be saved to ${url}.`);
        if (!interactive) return;
      }
    }
    this.dataset.ibLayout = json;
    this.#refocus = focus;

    if (interactive) {
      this.dispatchEvent(new CustomEvent("iblayout", { bubbles: true, detail: { layout: json } }));
    } else if (draw) {
      const blazor = (window as unknown as { Blazor?: { navigateTo?: (url: string, options: object) => void } }).Blazor;
      if (blazor?.navigateTo) blazor.navigateTo(location.pathname + location.search + location.hash, { replaceHistoryEntry: true });
      else location.reload();
    }
  }

  #say(th: HTMLElement): void {
    const width = Math.round(th.offsetWidth);
    const status = this.#host?.querySelector("[data-ib-col-say]");
    if (status) status.textContent = (this.dataset.ibWidthText ?? "{0}: {1}").replace("{0}", th.querySelector(".ib-table-title")?.textContent?.trim() ?? "").replace("{1}", String(width));
  }

  // The headers of a column's own zone: start, the rest and end don't mix.
  #zone(th: HTMLElement): HTMLTableCellElement[] {
    const side = (el: Element) => (el.matches(".ib-frozen-start") ? 1 : el.matches(".ib-frozen-end") ? 2 : 0);
    return this.#headers.filter((other) => side(other) === side(th));
  }

  /** Moves a column to a place among the headers, in its zone. */
  #moveTo(th: HTMLTableCellElement, to: number, focus: boolean): void {
    const zone = this.#zone(th).filter((other) => other.hasAttribute("data-ib-reorder") || other === th);
    const from = zone.indexOf(th);
    const target = Math.max(0, Math.min(zone.length - 1, to));
    if (from === target) return;
    const order = this.#headers;
    order.splice(order.indexOf(th), 1);
    order.splice(order.indexOf(zone[target]) + (target > from ? 1 : 0), 0, th);
    const columns = this.#layout();
    order.forEach((header, index) => (this.#entry(columns, header.dataset.ibCol!).p = index));
    void this.#save(columns, true, focus ? th.dataset.ibCol! : null);
  }

  #move(th: HTMLTableCellElement, by: number, focus: boolean): void {
    const zone = this.#zone(th).filter((other) => other.hasAttribute("data-ib-reorder") || other === th);
    this.#moveTo(th, zone.indexOf(th) + by, focus);
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

  /** Shows a width at once: the variable everything uses, and the column if it had none. */
  #show(th: HTMLTableCellElement, width: number): void {
    const select = this.#table?.tHead?.rows[0].cells[0].matches(".ib-table-select-cell") ? 1 : 0;
    const index = th.cellIndex - select;
    this.#table!.style.setProperty(`--ib-col-${index}`, `${width}px`);
    const col = this.#table!.querySelector("colgroup")?.children[th.cellIndex] as HTMLElement | undefined;
    if (col) col.style.width = `var(--ib-col-${index})`;
  }

  #hasWidth(th: HTMLTableCellElement): boolean {
    const select = this.#table?.tHead?.rows[0].cells[0].matches(".ib-table-select-cell") ? 1 : 0;
    return !!this.#table?.style.getPropertyValue(`--ib-col-${th.cellIndex - select}`);
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
    const button = (event.target as Element).closest<HTMLElement>("[data-ib-col-action]");
    if (!button) return;
    const action = button.dataset.ibColAction!;
    const key = button.dataset.ibCol;
    if (action === "reset") {
      void this.#save([], true, null);
      return;
    }
    const th = key ? this.#header(key) : null;
    const columns = this.#layout();
    const rem = parseFloat(getComputedStyle(document.documentElement).fontSize);
    if (th && (action === "left" || action === "right")) this.#move(th, action === "left" ? -1 : 1, true);
    else if (th && action === "wider") this.#setWidth(th, th.offsetWidth + rem, true);
    else if (th && action === "narrower") this.#setWidth(th, th.offsetWidth - rem, true);
    else if (th && action === "reset-width") {
      delete this.#entry(columns, key!).w;
      void this.#save(columns, true, key!);
    } else if (key && th && action === "hide") {
      const headers = this.#headers;
      const next = headers[headers.indexOf(th) + 1] ?? headers[headers.indexOf(th) - 1];
      this.#setHidden(columns, key, true, next?.dataset.ibCol ?? null);
    }
    else if (th && (action === "freeze-start" || action === "freeze-end" || action === "unfreeze")) {
      const entry = this.#entry(columns, key!);
      entry.f = action === "unfreeze" ? FROZEN.none : action === "freeze-start" ? FROZEN.start : FROZEN.end;
      // Frozen columns are placed by adding the widths up, so it needs one.
      entry.w ??= `${Math.round(th.offsetWidth)}px`;
      void this.#save(columns, true, key!);
    }
  }

  #setHidden(columns: ColumnEntry[], key: string, hidden: boolean, focus: string | null = null): void {
    const entry = this.#entry(columns, key);
    if (hidden) {
      // At least one stays.
      if (this.#headers.length <= 1) return;
      entry.h = true;
    } else delete entry.h;
    void this.#save(columns, true, focus);
  }

  #onChange(event: Event): void {
    const box = event.target as HTMLInputElement;
    const key = box.dataset?.ibColShow;
    if (!key) return;
    if (!box.checked && this.#headers.length <= 1) {
      box.checked = true;
      return;
    }
    this.#setHidden(this.#layout(), key, !box.checked);
  }

  #onKey(event: KeyboardEvent): void {
    if ((event.key !== "ArrowLeft" && event.key !== "ArrowRight") || event.ctrlKey || event.metaKey) return;
    if (event.shiftKey === event.altKey) return;
    const th = (event.target as Element).closest<HTMLTableCellElement>("thead th[data-ib-col]");
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
    const th = target.closest<HTMLTableCellElement>("thead th[data-ib-col]");
    if (!th) return;
    if (handle) this.#dragWidth(event, th, handle, signal);
    else if (th.hasAttribute("data-ib-reorder") && event.pointerType !== "touch" && !target.closest("button, input, [popover]")) this.#dragColumn(event, th, signal);
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
    const select = this.#table?.tHead?.rows[0].cells[0].matches(".ib-table-select-cell") ? 1 : 0;
    this.#table?.style.removeProperty(`--ib-col-${th.cellIndex - select}`);
    (this.#table?.querySelector("colgroup")?.children[th.cellIndex] as HTMLElement | undefined)?.style.removeProperty("width");
  }

  // Double-click on a handle: as wide as its header and the rows drawn.
  #onDoubleClick(event: Event): void {
    const handle = (event.target as Element).closest(".ib-col-resize");
    const th = handle?.closest<HTMLTableCellElement>("thead th[data-ib-col]");
    if (!th || !this.#table) return;
    this.#table.toggleAttribute("data-ib-measuring", true);
    const range = document.createRange();
    let widest = 0;
    for (const row of this.#table.rows) {
      const cell = row.cells[th.cellIndex];
      if (!cell || cell.colSpan > 1) continue;
      const style = getComputedStyle(cell);
      range.selectNodeContents(cell);
      // Not the handle, which spans the cell's edge.
      if (cell === th && handle) range.setEndBefore(handle);
      widest = Math.max(widest, range.getBoundingClientRect().width + parseFloat(style.paddingInlineStart) + parseFloat(style.paddingInlineEnd));
    }
    this.#table.toggleAttribute("data-ib-measuring", false);
    if (widest > 0) this.#setWidth(th, Math.ceil(widest) + 1, false);
  }

  // Dragging a header: its column follows the pointer, and the others slide aside.
  #dragColumn(down: PointerEvent, th: HTMLTableCellElement, signal: AbortSignal): void {
    const table = this.#table!;
    const zone = this.#zone(th).filter((other) => other.hasAttribute("data-ib-reorder") || other === th);
    const from = zone.indexOf(th);
    const rects = zone.map((other) => other.getBoundingClientRect());
    const columnOf = (header: HTMLTableCellElement) => [...table.rows].flatMap((row) => {
      const cell = row.cells[header.cellIndex];
      return cell && cell.colSpan === 1 ? [cell] : [];
    });
    const cells = zone.map(columnOf);
    const rtl = getComputedStyle(th).direction === "rtl";
    let to = from;
    let dragging = false;
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
      for (const column of cells) for (const cell of column) cell.style.transform = "";
      table.removeAttribute("data-ib-dragging");
      for (const cell of cells[from]) cell.removeAttribute("data-ib-lifted");
      if (!dragging) return;
      // The click after a drag isn't a sort. It goes to whatever the press and release share, or
      // nowhere, so it's caught on the document, and only for this moment.
      const swallow = (event: Event) => {
        event.preventDefault();
        event.stopPropagation();
      };
      document.addEventListener("click", swallow, { capture: true, once: true });
      setTimeout(() => document.removeEventListener("click", swallow, { capture: true }), 0);
      if (commit && to !== from) this.#moveTo(th, to, true);
    };
    document.addEventListener("pointermove", (event) => {
      const dx = event.clientX - down.clientX;
      if (!dragging) {
        if (Math.abs(dx) < DRAG_START) return;
        dragging = true;
        table.setAttribute("data-ib-dragging", "");
        for (const cell of cells[from]) cell.setAttribute("data-ib-lifted", "");
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
