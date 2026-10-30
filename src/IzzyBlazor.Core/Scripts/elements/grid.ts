import { IzzyElement, defineElement } from "../izzy-element";
import { extendRange, type RangeRun } from "../data-view";
import { openMenuAt, ownTrigger, setupContextMenu } from "../context-menu";

/**
 * <ib-grid> is an empty element inside a DataGrid's <ib-table>, and makes its table an ARIA grid: one
 * Tab stop and arrow keys between cells, with real focus (roving tabindex). With no script the table
 * is a plain table, with its controls in the Tab order.
 *
 * Where focus lands depends on the cell, worked out each time from what's in it: a cell of text is
 * focused itself; one link, button or checkbox is focused directly; several controls, or one that
 * uses the arrows itself (a text field, a select), leave focus on the cell, and Enter or F2 goes in.
 * Inside, Tab moves among them, and Escape, F2 or tabbing past either end comes back. There's no
 * stored mode: where focus is decides. Arrows still move on from a control that doesn't use them.
 *
 * Selection keys click the row's own checkbox, so the bar, ranges and binding stay in one place.
 */

const CONTROLS = "a[href], button, input:not([type=hidden]), select, textarea, summary, [contenteditable]:not([contenteditable=false]), [tabindex]:not(td, th, tr)";
const KEEPS_ARROWS = "input:not([type=checkbox], [type=button], [type=submit], [type=reset]), select, textarea, [contenteditable]:not([contenteditable=false]), [role=combobox], [role=slider], [role=spinbutton]";
const CELLS = "td, th";

class Grid extends IzzyElement {
  #table: HTMLTableElement | null = null;
  #box: HTMLElement | null = null;
  #current: HTMLTableCellElement | null = null;
  // Whether focus has been in the grid: until then the stop is the first row's, as rows arrive.
  #chosen = false;
  // Tab stops this element took away, to put back: each control and the tabindex it had.
  #controls = new Map<HTMLElement, string | null>();
  #inside: HTMLTableCellElement | null = null;
  // Focus is in the grid, on this row (its key) and column: followed when the rows change.
  #inGrid = false;
  #key: string | null = null;
  #rowIndex = 0;
  #column = 0;
  // PageUp or PageDown asked for a page: where focus goes, once this first row has gone.
  #arrive: { to: "first" | "last"; from: string | null } | null = null;
  #run: RangeRun | null = null;
  // The frozen columns' widths at each edge, as a scroll padding.
  #clearOf = "";

  protected setup(signal: AbortSignal): void {
    const table = this.closest("ib-table")?.querySelector<HTMLTableElement>(":scope > .ib-table-scroll > table");
    if (!table) return;
    this.#table = table;
    table.setAttribute("role", "grid");

    // Cells scroll into view by focus, so the scroll box needs no Tab stop of its own. Still
    // focusable: focus goes there after Retry, and on to a cell.
    this.#box = table.closest<HTMLElement>(".ib-table-scroll");
    this.#box?.setAttribute("tabindex", "-1");
    this.#on(this.#box!, "focus", () => this.#focusCell(this.#current ?? undefined), signal);

    this.#prepare();
    const changes = new MutationObserver(() => this.#rowsChanged());
    changes.observe(table, { childList: true, subtree: true, attributes: true, attributeFilter: ["data-ib-key", "disabled", "hidden"] });
    // Shown (a closed tab or dialog opening), or a column hidden at a new width: the stop looks again.
    const sizes = new ResizeObserver(() => {
      this.#prepare();
      this.#frozen();
    });
    sizes.observe(table);
    if (this.#box) sizes.observe(this.#box);
    signal.addEventListener("abort", () => {
      changes.disconnect();
      sizes.disconnect();
    });

    // Capture: a menu button's own key handler would open its menu on Up and Down, and in a grid
    // those move between cells. Enter and Space still open it.
    this.#on(table, "keydown", (event) => this.#onKey(event as KeyboardEvent), signal, true);
    this.#on(table, "focusin", (event) => this.#onFocusIn(event as FocusEvent), signal);
    this.#on(table, "focusout", (event) => this.#onFocusOut(event as FocusEvent), signal);
    this.#on(table, "change", () => this.#syncSelected(), signal);
    setupContextMenu(table, "tbody > tr, thead > tr > th", "[data-ib-row-menu], [data-ib-menu]", () => true, signal);
    this.#on(this.closest("ib-table")!, "ib-selection", () => this.#syncSelected(), signal);
    this.#on(document, "pointerdown", (event) => {
      this.#run = null;
      this.#arrive = null;
      if (!table.contains(event.target as Node)) this.#inGrid = false;
    }, signal);
    this.#rowsChanged();
  }

  /** Puts focus where the keys would in this cell, for a header that has just been rearranged. */
  focusCell(cell: HTMLTableCellElement): void {
    this.#focusCell(cell);
  }

  // Focus stops clear of the frozen columns; if they'd leave too little room for the rows (a phone),
  // they scroll like the rest.
  #frozen(): void {
    const table = this.#table;
    const box = this.#box;
    const cells = table?.tHead?.rows[0]?.cells;
    if (!table || !box || !cells || box.clientWidth === 0) return;
    let [start, end] = [0, 0];
    for (const cell of cells) {
      if (cell.matches(".ib-frozen-start")) start += cell.offsetWidth;
      else if (cell.matches(".ib-frozen-end")) end += cell.offsetWidth;
    }
    // Room for at least a column's minimum beside them; below 640px the stylesheet has done this.
    const room = 8 * parseFloat(getComputedStyle(document.documentElement).fontSize);
    const unfrozen = start + end > 0 && (box.clientWidth - start - end < room || !matchMedia("(min-width: 640px)").matches);
    table.toggleAttribute("data-ib-unfrozen", unfrozen);
    this.#clearOf = unfrozen ? "" : `${start}px ${end}px`;
  }

  protected teardown(): void {
    for (const [control, tabindex] of this.#controls) {
      if (tabindex === null) control.removeAttribute("tabindex");
      else control.setAttribute("tabindex", tabindex);
    }
    this.#controls.clear();
    this.#box?.setAttribute("tabindex", "0");
    if (this.#table) {
      this.#table.removeAttribute("role");
      this.#table.removeAttribute("aria-multiselectable");
      this.#table.removeAttribute("data-ib-unfrozen");
      for (const cell of this.#table.querySelectorAll(CELLS)) cell.removeAttribute("tabindex");
    }
    this.#table = this.#box = this.#current = this.#inside = null;
  }

  #on(target: EventTarget, type: string, handler: (event: Event) => void, signal: AbortSignal, capture = false): void {
    target.addEventListener(type, handler, { signal, capture });
  }

  // The table's own rows, without placeholders.
  get #rows(): HTMLTableRowElement[] {
    return this.#table ? [...this.#table.rows].filter((row) => !row.hasAttribute("aria-hidden")) : [];
  }

  get #bodyRows(): HTMLTableRowElement[] {
    return this.#rows.filter((row) => row.parentElement === this.#table!.tBodies[0]);
  }

  #cellOf(target: Element): HTMLTableCellElement | null {
    const cell = target.closest<HTMLTableCellElement>(CELLS);
    return cell && cell.closest("table") === this.#table ? cell : null;
  }

  // In an overlay of the grid's own (a cell's menu, a filter panel), not one the grid itself is in.
  #inOverlay(el: Element): boolean {
    const overlay = el.closest("[popover], dialog");
    return !!overlay && !!this.#table?.contains(overlay);
  }

  /** The cell's own controls, usable now: none in its menus or dialogs, or a grid inside it. */
  #controlsOf(cell: Element): HTMLElement[] {
    return [...cell.querySelectorAll<HTMLElement>(CONTROLS)].filter(
      (el) => el.closest("table") === this.#table && !this.#inOverlay(el) && !el.matches(":disabled") && el.checkVisibility()
    );
  }

  // A row's radio doesn't count: in a grid its arrows would change the selection.
  #keepsArrows(el: Element): boolean {
    return el.matches(KEEPS_ARROWS) && !el.matches(".ib-table-select");
  }

  /** Where arrows land in a cell: its one control that leaves them alone, else the cell. */
  #targetOf(cell: HTMLTableCellElement): HTMLElement {
    const controls = this.#controlsOf(cell);
    return controls.length === 1 && !this.#keepsArrows(controls[0]) ? controls[0] : cell;
  }

  #prepare(): void {
    const table = this.#table;
    if (!table) return;
    if (table.closest("ib-table")?.querySelector(".ib-table-select-all")) table.setAttribute("aria-multiselectable", "true");
    else table.removeAttribute("aria-multiselectable");

    // Whatever their state now: a disabled or hidden control can come back with no mutation here.
    const seen = new Set<HTMLElement>();
    for (const control of table.querySelectorAll<HTMLElement>(CONTROLS)) {
      if (control.closest("table") !== table || this.#inOverlay(control)) continue;
      seen.add(control);
      if (!this.#controls.has(control)) this.#controls.set(control, control.getAttribute("tabindex"));
      if (control.closest(CELLS) !== this.#inside && control.tabIndex !== -1) control.tabIndex = -1;
    }
    for (const control of this.#controls.keys()) {
      if (!seen.has(control)) this.#controls.delete(control);
    }
    for (const row of this.#rows) {
      for (const cell of row.cells) {
        if (cell.tabIndex !== -1 || !cell.hasAttribute("tabindex")) cell.tabIndex = -1;
      }
    }

    const stale = !this.#current?.isConnected || this.#current.closest("table") !== table || !!this.#current.closest("[aria-hidden]");
    if (stale || !this.#chosen) {
      const body = this.#bodyRows[0] ?? this.#rows[0];
      this.#current = (body && (this.#visibleCells(body)[0] ?? body.cells[0])) ?? null;
    } else if (this.#current && !this.#current.checkVisibility() && table.checkVisibility()) {
      this.#current = this.#atColumn(this.#current.parentElement as HTMLTableRowElement, this.#columnOf(this.#current)) ?? this.#current;
    }
    if (this.#current && !this.#inside) this.#targetOf(this.#current).tabIndex = 0;
    this.#syncSelected();
  }

  /** Makes this cell the grid's Tab stop. */
  #rove(cell: HTMLTableCellElement): void {
    if (this.#current && this.#current !== cell) {
      this.#targetOf(this.#current).tabIndex = -1;
      this.#current.tabIndex = -1;
    }
    this.#current = cell;
    this.#targetOf(cell).tabIndex = 0;
  }

  #focusCell(cell: HTMLTableCellElement | undefined): void {
    if (!cell) return;
    this.#rove(cell);
    this.#focusInCell(cell, this.#targetOf(cell));
  }

  #focusInCell(cell: HTMLTableCellElement, target: HTMLElement): void {
    // Clear of the frozen columns, only while the keys move focus: a frozen cell would count as
    // under them and move the grid, and a click on one would too.
    const box = this.#box;
    const padding = !cell.matches(".ib-frozen") && box ? this.#clearOf : "";
    if (padding) box!.style.scrollPaddingInline = padding;
    target.focus({ preventScroll: true });
    // The browser leaves a partly visible cell as it is; the ring and text should show.
    cell.scrollIntoView({ block: "nearest", inline: "nearest" });
    if (padding) box!.style.removeProperty("scroll-padding-inline");
  }

  // Not the cells of a column hidden at this width.
  #visibleCells(row: HTMLTableRowElement): HTMLTableCellElement[] {
    return [...row.cells].filter((cell) => cell.checkVisibility());
  }

  /** The visible cell of a row nearest a column, counting spans. */
  #atColumn(row: HTMLTableRowElement, column: number): HTMLTableCellElement | undefined {
    const cells = this.#visibleCells(row);
    let best = cells[cells.length - 1];
    for (const cell of cells) {
      const start = this.#columnOf(cell);
      if (column < start + cell.colSpan) {
        // Past the column (it's hidden here): whichever side is nearer, before on a tie.
        best = start > column && best && best !== cell && cells.indexOf(cell) > 0 && column - this.#columnOf(cells[cells.indexOf(cell) - 1]) <= start - column ? cells[cells.indexOf(cell) - 1] : cell;
        break;
      }
    }
    return best;
  }

  #columnOf(cell: HTMLTableCellElement): number {
    let at = 0;
    for (const other of (cell.parentElement as HTMLTableRowElement).cells) {
      if (other === cell) return at;
      at += other.colSpan;
    }
    return at;
  }

  // Each row's aria-selected follows its checkbox: the server renders it, and on a static page the
  // script owns the selection and changes boxes with no event of their own.
  #syncSelected(): void {
    for (const row of this.#rows) {
      const box = row.querySelector<HTMLInputElement>(":scope > td > .ib-table-select");
      if (box) row.setAttribute("aria-selected", String(box.checked));
    }
  }

  #onFocusIn(event: FocusEvent): void {
    const target = event.target as HTMLElement;
    if (this.#inOverlay(target)) return;
    // A row can have focus itself: Load more puts it on the first row it added.
    if (target instanceof HTMLTableRowElement) {
      if (target.closest("table") === this.#table) this.#focusCell(this.#atColumn(target, this.#column));
      return;
    }
    const cell = this.#cellOf(target);
    if (!cell) return;
    this.#chosen = true;
    this.#rove(cell);
    const row = cell.parentElement as HTMLTableRowElement;
    this.#inGrid = true;
    this.#key = row.dataset.ibKey ?? null;
    this.#rowIndex = this.#rows.indexOf(row);
    const start = this.#columnOf(cell);
    // A cell that spans the wanted column (an empty row, a detail row) leaves it as it was.
    if (this.#column < start || this.#column >= start + cell.colSpan) this.#column = start;
    if (target === cell || target === this.#targetOf(cell)) return;
    // In a cell: its controls take Tab until focus leaves them.
    this.#inside = cell;
    for (const control of this.#controlsOf(cell)) control.tabIndex = 0;
    cell.tabIndex = -1;
  }

  #onFocusOut(event: FocusEvent): void {
    const to = event.relatedTarget as Element | null;
    if (to && !this.#table?.contains(to)) {
      this.#inGrid = false;
      this.#arrive = null;
    }
    const inside = this.#inside;
    if (inside && !(to && to !== inside && inside.contains(to) && !this.#inOverlay(to))) {
      this.#inside = null;
      for (const control of this.#controlsOf(inside)) control.tabIndex = -1;
      if (inside === this.#current) this.#targetOf(inside).tabIndex = 0;
    }
  }

  // New rows: every control leaves the Tab order; and focus follows its row, or goes where a
  // requested page should land.
  #rowsChanged(): void {
    if (!this.#table) return;
    this.#prepare();
    this.#frozen();
    const rows = this.#rows;
    const data = this.#bodyRows.filter((row) => row.cells.length > 1);
    if (this.#arrive && data[0] && data[0].dataset.ibKey !== this.#arrive.from) {
      const row = this.#arrive.to === "first" ? data[0] : data[data.length - 1];
      this.#arrive = null;
      this.#focusCell(this.#atColumn(row, this.#column));
      return;
    }
    if (this.#arrive && data.length === 0) {
      // The page failed: Retry takes focus. An empty row with nothing in it may be a page on its way.
      const message = this.#bodyRows[0];
      const active = document.activeElement;
      if (message && this.#controlsOf(message).length > 0 && (!active || active === document.body)) {
        this.#arrive = null;
        this.#focusCell(message.cells[0]);
      }
      return;
    }
    if (!this.#inGrid || rows.length === 0 || this.#arrive) return;
    // Lost: its row went, or enhanced navigation took the cell's tabindex (a header's too).
    const active = document.activeElement;
    const lost = !active || active === document.body;
    const row = this.#key === null ? null : rows.find((r) => r.dataset.ibKey === this.#key);
    if (row && !row.contains(active)) {
      this.#focusCell(this.#atColumn(row, this.#column));
    } else if (!row && lost) {
      this.#focusCell(this.#atColumn(rows[Math.min(this.#rowIndex, rows.length - 1)], this.#column));
    }
  }

  #onKey(event: KeyboardEvent): void {
    if (event.key !== "Shift" && !event.shiftKey) this.#run = null;
    if (event.defaultPrevented || event.isComposing) return;
    const target = event.target as HTMLElement;
    if (this.#inOverlay(target)) return;
    const cell = this.#cellOf(target);
    if (!cell) return;
    if (this.#isOpen(target)) return;
    this.#arrive = null;
    const inside = target !== cell && target !== this.#targetOf(cell);
    if (inside && this.#onKeyInside(event, target, cell)) return;
    const handled = inside
      ? !this.#keepsArrows(target) && ((event.altKey && this.#openMenu(event, cell)) || this.#onMove(event, cell))
      : this.#onAction(event, target, cell) || this.#onMove(event, cell);
    if (handled) {
      event.preventDefault();
      event.stopPropagation();
    }
  }

  // An open list or picker has the keys first: Escape closes it, and focus stays.
  #isOpen(target: HTMLElement): boolean {
    if (target.getAttribute("aria-expanded") === "true") return true;
    const select = target.closest("select");
    if (!select) return false;
    try {
      return select.matches(":open");
    } catch {
      return false;
    }
  }

  /** On a control in a cell it had to be entered for: the ways out. Whether this was one. */
  #onKeyInside(event: KeyboardEvent, target: HTMLElement, cell: HTMLTableCellElement): boolean {
    if (event.key === "Escape" || event.key === "F2") {
      event.preventDefault();
      cell.focus();
      return true;
    }
    if (event.key === "Tab") {
      const controls = this.#controlsOf(cell);
      if (target === (event.shiftKey ? controls[0] : controls[controls.length - 1])) {
        event.preventDefault();
        cell.focus();
      }
      return true;
    }
    if (event.key === " " && event.shiftKey && !this.#keepsArrows(target)) {
      this.#boxOf(cell)?.click();
      event.preventDefault();
      return true;
    }
    return false;
  }

  #boxOf(cell: Element): HTMLInputElement | null {
    return cell.closest("tr")?.querySelector<HTMLInputElement>(":scope > td > .ib-table-select:not(:disabled)") ?? null;
  }

  #openMenu(event: KeyboardEvent, cell: HTMLTableCellElement): boolean {
    if (event.key !== "ArrowDown") return false;
    const menu = cell.querySelector<HTMLElement>("[data-ib-menu]") ?? cell.querySelector<HTMLElement>("[popovertarget]:not(:disabled)");
    menu?.click();
    return !!menu;
  }

  /** Keys that do something rather than move. Whether this was one. */
  #onAction(event: KeyboardEvent, target: HTMLElement, cell: HTMLTableCellElement): boolean {
    const modifier = event.ctrlKey || event.metaKey;
    if (event.altKey) return this.#openMenu(event, cell);
    // The menu key is the browser's event, which setupContextMenu answers. Shift+F10 has none everywhere.
    if (event.shiftKey && event.key === "F10") {
      const menu = ownTrigger(cell.parentElement as Element, "tr", "[data-ib-row-menu]") ?? ownTrigger(cell, "th, td", "[data-ib-menu]");
      if (!menu) return false;
      const { left, bottom } = cell.getBoundingClientRect();
      openMenuAt(menu, left, bottom);
      return true;
    }
    if (modifier && !event.shiftKey && event.key.toLowerCase() === "a") {
      const all = this.closest("ib-table")?.querySelector<HTMLInputElement>(".ib-table-select-all");
      if (!all) return false;
      if (!all.checked) all.click();
      return true;
    }
    if (modifier) return false;

    switch (event.key) {
      case " ": {
        const box = this.#boxOf(cell);
        if (event.shiftKey) {
          if (!event.repeat) box?.click();
          return !!box;
        }
        // On a control, Space is the control's.
        if (target !== cell) return false;
        if (!event.repeat) box?.click();
        return true;
      }
      case "Enter": {
        if (target !== cell) return false;
        if (event.repeat) return true;
        const primary = cell.querySelector<HTMLElement>("[data-ib-primary]");
        if (primary) {
          // Carrying Shift, which adds a column to the sort.
          primary.dispatchEvent(new MouseEvent("click", { bubbles: true, cancelable: true, shiftKey: event.shiftKey }));
          return true;
        }
        const control = this.#controlsOf(cell)[0];
        if (control) this.#focusInCell(cell, control);
        else cell.closest("tr")?.querySelector<HTMLElement>(".ib-table-row-link")?.click();
        return true;
      }
      case "F2":
        if (target === cell && this.#controlsOf(cell)[0]) this.#focusInCell(cell, this.#controlsOf(cell)[0]);
        return true;
      case "ArrowUp":
      case "ArrowDown":
        return event.shiftKey ? this.#extend(event.key, cell) : false;
      default:
        return false;
    }
  }

  // Shift+Up or Down: the rows from where it started are selected, and going back shrinks them.
  #extend(key: string, cell: HTMLTableCellElement): boolean {
    const box = this.#boxOf(cell);
    if (!box || box.type !== "checkbox") return false;
    const rows = this.#rows.filter((row) => row.querySelector(":scope > td > .ib-table-select:not(:disabled)"));
    const next = rows[rows.indexOf(cell.parentElement as HTMLTableRowElement) + (key === "ArrowDown" ? 1 : -1)];
    if (!next) return true;
    this.#run = extendRange(this.#run, rows.map((row) => this.#boxOf(row.cells[0])!), box, this.#boxOf(next.cells[0])!);
    this.#focusCell(this.#atColumn(next, this.#column));
    return true;
  }

  /** Keys that move between cells. Whether this was one. */
  #onMove(event: KeyboardEvent, cell: HTMLTableCellElement): boolean {
    if (event.shiftKey || event.altKey) return false;
    const modifier = event.ctrlKey || event.metaKey;
    const rows = this.#rows;
    const row = cell.parentElement as HTMLTableRowElement;
    const cells = this.#visibleCells(row);
    const body = this.#bodyRows;
    if (modifier) {
      if (event.key === "Home") {
        const first = body[0] ?? rows[0];
        this.#focusCell(first && this.#visibleCells(first)[0]);
      } else if (event.key === "End") {
        const last = body[body.length - 1] ?? rows[rows.length - 1];
        this.#focusCell(last && this.#visibleCells(last).at(-1));
      } else return false;
      return true;
    }

    const rtl = getComputedStyle(this.#table!).direction === "rtl";
    switch (event.key) {
      case "ArrowDown":
      case "ArrowUp": {
        const next = rows[rows.indexOf(row) + (event.key === "ArrowDown" ? 1 : -1)];
        if (next) this.#focusCell(this.#atColumn(next, this.#column));
        return true;
      }
      case "ArrowRight":
      case "ArrowLeft": {
        const forward = (event.key === "ArrowRight") !== rtl;
        this.#focusCell(cells[cells.indexOf(cell) + (forward ? 1 : -1)]);
        return true;
      }
      case "Home":
        this.#focusCell(cells[0]);
        return true;
      case "End":
        this.#focusCell(cells.at(-1));
        return true;
      case "PageDown":
      case "PageUp":
        this.#page(event, cell, body);
        return true;
      default:
        return false;
    }
  }

  // By the rows in view; from the page's edge, to the next page if there is one, by its own control.
  #page(event: KeyboardEvent, cell: HTMLTableCellElement, body: HTMLTableRowElement[]): void {
    if (body.length === 0) return;
    const down = event.key === "PageDown";
    const row = cell.parentElement as HTMLTableRowElement;
    const at = body.indexOf(row);
    if (at < 0) {
      // From a header, down to the first row; from a footer, up to the last.
      const inHead = row.parentElement === this.#table!.tHead;
      if (inHead === down) this.#focusCell(this.#atColumn(down ? body[0] : body[body.length - 1], this.#column));
      return;
    }
    if (at === (down ? body.length - 1 : 0)) {
      if (event.repeat) return;
      const control = this.closest("ib-table")?.querySelector<HTMLElement>(`:scope > .ib-data-footer ${down ? ".ib-pagination-next" : ".ib-pagination-prev"}`);
      if (control && control.getAttribute("aria-disabled") !== "true") {
        this.#arrive = { to: down ? "first" : "last", from: body[0].dataset.ibKey ?? null };
        control.click();
      }
      return;
    }
    const box = this.#box;
    const view = box && box.scrollHeight > box.clientHeight ? box.clientHeight : innerHeight;
    const step = Math.max(1, Math.floor(view / Math.max(1, row.offsetHeight)) - 1);
    this.#focusCell(this.#atColumn(body[Math.min(body.length - 1, Math.max(0, at + (down ? step : -step)))], this.#column));
  }
}

defineElement("ib-grid", Grid);
