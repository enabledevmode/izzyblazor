import { IzzyElement, defineElement } from "../izzy-element";

const TILE = ".ib-tile";
const HANDLE = ".ib-tile-handle";
const GRIP = ".ib-tile-grip";
// A press on these stays theirs: the tile's actions, links, fields and the overlays they open.
const CONTROL = "a[href], button:not(.ib-tile-handle), input, select, textarea, label, summary, video, audio, iframe, [contenteditable]:not([contenteditable=false]), [popover], dialog, [role=dialog], [role=menu], [data-ib-role=menu], [role=listbox], [role=slider], [role=tab]";
// A press moving less than this is a click (the handle's menu), not a drag.
const THRESHOLD = 4;
// A touch on the tile (not its handle) is a long press this long, so the page still scrolls.
const LONG_PRESS = 250;
const SLOP = 8;
// Within this distance of the scroll area's edge it scrolls, faster nearer the edge.
const EDGE = 48;
const MAX_SPEED = 16;

type Kind = "left" | "right" | "up" | "down" | "wider" | "narrower" | "taller" | "shorter";

/** A tile's place at the widest column count. */
interface Place {
  col: number;
  row: number;
  cs: number;
  rs: number;
}

/**
 * <ib-tile-layout> moves and resizes the tiles of an editable TileLayout, at its widest column
 * count only (the handles hide below it). Every place is in cells: a tile snaps from cell to cell
 * as the pointer crosses them, and without data-ib-overlap it stays at its last free spot rather
 * than covering another tile.
 * - Dragging a tile moves it, from anywhere but the controls in it; dragging its corner grip
 *   resizes it. A mouse drags after a few pixels, a touch after a long press (the handle and grip
 *   at once). Escape puts it back.
 * - Alt+arrows on the handle move it a cell, Alt+Shift+arrows resize it, by clicking the matching
 *   item of its menu (data-ib-move).
 * - On a static page a menu item moves the tile here instead of posting: its place variables and
 *   the hidden {Name}.Tiles input change, the menus offer what now fits, and the tile announces it.
 * - On an interactive page a menu item runs C#'s move. A drag or resize raises `ibtiles` (the
 *   layout, the tile and seq); the tile keeps its new place (--ib-drag-*) until the render that has
 *   handled it (data-ib-seq) arrives.
 * With JavaScript off, the menu items post the form and the server moves the tile.
 */
class TileLayout extends IzzyElement {
  private seq = 0;
  private busy = false;
  // A drag just ended: the touchend that follows mustn't become a tap.
  private dropped = false;

  private get live(): boolean {
    return this.hasAttribute("data-ib-interactive");
  }

  private get suffix(): string {
    const at = this.dataset.ibAt ?? "";
    return at ? `-${at}` : "";
  }

  private get columns(): number {
    return Number(this.dataset.ibCols ?? 1);
  }

  private get rows(): number {
    return Number(this.dataset.ibRows ?? 0);
  }

  protected setup(signal: AbortSignal): void {
    this.addEventListener("click", (event) => this.onClick(event), { signal });
    this.addEventListener("keydown", (event) => this.onKey(event), { signal });
    this.addEventListener("pointerdown", (event) => this.onPointerDown(event), { signal });
    this.addEventListener("touchend", (event) => {
      if (!this.dropped) return;
      this.dropped = false;
      if (event.cancelable) event.preventDefault();
    }, { signal, passive: false });
  }

  private tiles(): HTMLElement[] {
    return [...this.children].filter((el): el is HTMLElement => el instanceof HTMLElement && el.matches(TILE));
  }

  private tileOf(el: Element | null): HTMLElement | null {
    const tile = el?.closest<HTMLElement>(TILE);
    return tile && tile.parentElement === this ? tile : null;
  }

  /** A tile's place: a drag's, else the one drawn. */
  private placeOf(tile: HTMLElement): Place {
    const read = (name: string) =>
      Number(tile.style.getPropertyValue(`--ib-drag-${name}`) || tile.style.getPropertyValue(`--ib-tile-${name}${this.suffix}`) || 1);
    return { col: read("col"), row: read("row"), cs: read("cs"), rs: read("rs") };
  }

  /** Whether a tile fits at a place: inside the grid, within its limits, over no other tile (unless allowed). */
  private fits(tile: HTMLElement, p: Place): boolean {
    const [minCs, maxCs, minRs, maxRs] = (tile.dataset.ibLimits ?? "1,0,1,0").split(",").map(Number);
    const cols = this.columns;
    const rows = this.rows;
    if (p.col < 1 || p.row < 1 || p.col + p.cs - 1 > cols || (rows > 0 && p.row + p.rs - 1 > rows)) return false;
    if (p.cs < minCs || (maxCs > 0 && p.cs > maxCs) || p.rs < minRs || (maxRs > 0 && p.rs > maxRs)) return false;
    if (this.hasAttribute("data-ib-overlap")) return true;
    return this.tiles().every((other) => {
      if (other === tile) return true;
      const o = this.placeOf(other);
      return p.col >= o.col + o.cs || o.col >= p.col + p.cs || p.row >= o.row + o.rs || o.row >= p.row + p.rs;
    });
  }

  /** Whether editing is on at this width: the handles show only at the widest column count. */
  private editingHere(tile: HTMLElement): boolean {
    const handle = tile.querySelector<HTMLElement>(HANDLE);
    return !!handle && getComputedStyle(handle).display !== "none";
  }

  private onClick(event: MouseEvent): void {
    const button = (event.target as Element).closest<HTMLButtonElement>("button[data-ib-move]");
    const tile = this.tileOf(button);
    if (!button || !tile || button.disabled || this.live) return;
    // A static page: the move happens here instead of posting the form.
    event.preventDefault();
    const p = step(this.placeOf(tile), button.dataset.ibMove as Kind);
    if (!this.fits(tile, p)) return;
    this.commit(tile, p, isResize(button.dataset.ibMove as Kind));
    tile.querySelector<HTMLElement>(HANDLE)?.focus();
  }

  private onKey(event: KeyboardEvent): void {
    if (!event.altKey || event.ctrlKey || event.metaKey) return;
    const target = event.target as Element;
    const tile = this.tileOf(target);
    if (!tile || !target.matches(HANDLE)) return;
    const rtl = getComputedStyle(this).direction === "rtl";
    const left = rtl ? "ArrowRight" : "ArrowLeft";
    const right = rtl ? "ArrowLeft" : "ArrowRight";
    const kind: Kind | null = event.shiftKey
      ? event.key === right ? "wider" : event.key === left ? "narrower" : event.key === "ArrowDown" ? "taller" : event.key === "ArrowUp" ? "shorter" : null
      : event.key === right ? "right" : event.key === left ? "left" : event.key === "ArrowDown" ? "down" : event.key === "ArrowUp" ? "up" : null;
    if (!kind) return;
    // Alt+Left is the browser's Back: always ours on a handle, even when the move doesn't fit.
    event.preventDefault();
    tile.querySelector<HTMLButtonElement>(`button[data-ib-move="${kind}"]`)?.click();
  }

  /** A move made here: the tile's place, the posted layout, the menus and the announcement. */
  private commit(tile: HTMLElement, p: Place, resized: boolean): void {
    for (const [name, value] of Object.entries(p)) tile.style.setProperty(`--ib-tile-${name}${this.suffix}`, String(value));
    clearDrag(tile);
    const input = this.querySelector<HTMLInputElement>(':scope > input[type=hidden][name$=".Tiles"]');
    if (input) input.value = this.serialize();
    this.refreshMenus();
    this.say(tile, p, resized);
  }

  /** The layout as TileLayoutState writes it: each tile's id and place, by row, then column. */
  private serialize(): string {
    return this.tiles()
      .filter((tile) => tile.dataset.tileId)
      .map((tile) => ({ id: tile.dataset.tileId!, p: this.placeOf(tile) }))
      .sort((a, b) => a.p.row - b.p.row || a.p.col - b.p.col)
      .map(({ id, p }) => `${encodeURIComponent(id)}:${p.col},${p.row},${p.cs},${p.rs}`)
      .join(";");
  }

  /** Each tile's menu offers only the moves that now fit. */
  private refreshMenus(): void {
    for (const tile of this.tiles()) {
      const p = this.placeOf(tile);
      for (const button of tile.querySelectorAll<HTMLButtonElement>("button[data-ib-move]")) {
        button.disabled = !this.fits(tile, step(p, button.dataset.ibMove as Kind));
      }
    }
  }

  private say(tile: HTMLElement, p: Place, resized: boolean): void {
    const region = tile.querySelector(":scope > [role=status]");
    if (!region) return;
    const text = resized
      ? (this.dataset.ibResized ?? "").replace("{1}", String(p.cs)).replace("{2}", String(p.rs))
      : (this.dataset.ibMoved ?? "").replace("{1}", String(p.col)).replace("{2}", String(p.row));
    // Cleared first, so the same move twice is announced again.
    region.textContent = "";
    requestAnimationFrame(() => (region.textContent = text.replace("{0}", tile.dataset.ibLabel ?? "")));
  }

  private onPointerDown(event: PointerEvent): void {
    if (event.button !== 0 || !event.isPrimary || this.busy) return;
    const target = event.target as Element;
    const tile = this.tileOf(target);
    if (!tile || !this.editingHere(tile)) return;
    const grip = target.closest(GRIP);
    const handle = target.closest(HANDLE);
    // The grip resizes. The handle moves, and so does the rest of the tile: not a control in it,
    // a press its content took (a chart's zoom), or a scrollbar.
    if (!grip && !handle && (target.closest(CONTROL) || event.defaultPrevented || onScrollbar(target, event))) return;
    this.dropped = false;
    this.track(tile, event, grip ? "resize" : "move", !grip && !handle && event.pointerType !== "mouse" ? LONG_PRESS : 0);
  }

  /** Follows a press: becomes a drag once it moves a few pixels, or once held for the delay. */
  private track(tile: HTMLElement, event: PointerEvent, mode: "move" | "resize", delay: number): void {
    const pressed = event.target as Element;
    const abort = new AbortController();
    const signal = abort.signal;
    const startX = event.clientX;
    const startY = event.clientY;
    const from = this.placeOf(tile);
    let current = from;
    let x = startX;
    let y = startY;
    let dragging = false;
    let frame = 0;
    let held = 0;
    // The cell of the tile that was grabbed, so the tile keeps it under the pointer.
    let grabCol = 0;
    let grabRow = 0;
    const scroller = scrollParent(this);

    try {
      pressed.setPointerCapture(event.pointerId);
    } catch {
      // A synthetic pointer: moves still arrive while over what was pressed.
    }

    const begin = () => {
      dragging = true;
      const cell = this.cellAt(startX, startY);
      grabCol = cell.col - from.col;
      grabRow = cell.row - from.row;
      tile.classList.add(mode === "move" ? "ib-tile-dragging" : "ib-tile-resizing");
      this.classList.add("ib-tiles-dragging");
      frame = requestAnimationFrame(autoScroll);
    };

    if (delay) {
      held = window.setTimeout(() => {
        begin();
        update();
      }, delay);
    }

    const update = () => {
      const cell = this.cellAt(x, y);
      const next: Place = mode === "move"
        ? { ...current, col: clamp(cell.col - grabCol, 1, this.columns - from.cs + 1), row: Math.max(1, cell.row - grabRow) }
        : { ...current, cs: Math.max(1, cell.col - from.col + 1), rs: Math.max(1, cell.row - from.row + 1) };
      if (this.rows > 0 && mode === "move") next.row = Math.min(next.row, this.rows - from.rs + 1);
      if (sameAs(next, current) || !this.fits(tile, next)) return;
      current = next;
      for (const [name, value] of Object.entries(next)) tile.style.setProperty(`--ib-drag-${name}`, String(value));
    };

    const autoScroll = () => {
      if (!dragging) return;
      const area = scroller === document.scrollingElement || !scroller
        ? { top: 0, bottom: window.innerHeight }
        : scroller.getBoundingClientRect();
      const speed =
        y < area.top + EDGE ? -Math.ceil(((area.top + EDGE - y) / EDGE) * MAX_SPEED)
        : y > area.bottom - EDGE ? Math.ceil(((y - (area.bottom - EDGE)) / EDGE) * MAX_SPEED)
        : 0;
      if (speed) {
        (scroller ?? document.scrollingElement)?.scrollBy(0, speed);
        update();
      }
      frame = requestAnimationFrame(autoScroll);
    };

    const finish = (drop: boolean, escaped = false) => {
      abort.abort();
      cancelAnimationFrame(frame);
      clearTimeout(held);
      try {
        pressed.releasePointerCapture(event.pointerId);
      } catch {
        // Already released.
      }
      if (!dragging) return;
      tile.classList.remove("ib-tile-dragging", "ib-tile-resizing");
      this.classList.remove("ib-tiles-dragging");
      this.dropped = event.pointerType !== "mouse";
      // The click after the pointerup would open the handle's menu.
      const swallowClick = () => {
        tile.addEventListener("click", swallow, { capture: true, once: true });
        setTimeout(() => tile.removeEventListener("click", swallow, { capture: true }), 0);
      };
      if (escaped) document.addEventListener("pointerup", swallowClick, { capture: true, once: true });
      else swallowClick();
      if (!drop || sameAs(current, from)) {
        clearDrag(tile);
        return;
      }
      if (this.live) this.report(tile, current);
      else this.commit(tile, current, mode === "resize");
    };

    document.addEventListener("pointermove", (e) => {
      if (e.pointerId !== event.pointerId) return;
      x = e.clientX;
      y = e.clientY;
      if (!dragging) {
        const moved = Math.hypot(x - startX, y - startY);
        if (delay) {
          // Moving before the long press is up: the finger is scrolling the page.
          if (moved > SLOP / (window.visualViewport?.scale ?? 1)) finish(false);
          return;
        }
        if (moved < THRESHOLD) return;
        begin();
      }
      e.preventDefault();
      update();
    }, { signal });
    document.addEventListener("pointerup", (e) => e.pointerId === event.pointerId && finish(true), { signal });
    document.addEventListener("pointercancel", (e) => e.pointerId === event.pointerId && finish(false), { signal });
    pressed.addEventListener("lostpointercapture", () => pressed.isConnected && finish(false), { signal });
    document.addEventListener("keydown", (e) => {
      if (e.key !== "Escape" || !dragging) return;
      e.preventDefault();
      e.stopPropagation();
      finish(false, true);
    }, { signal, capture: true });
    // A held touch would open the context menu.
    this.addEventListener("contextmenu", (e) => dragging && e.preventDefault(), { signal });
    this.addEventListener("touchmove", (e) => dragging && e.cancelable && e.preventDefault(), { signal, passive: false });
  }

  /** The cell under a point, from 1, at the widest column count (past the last row, the rows go on). */
  private cellAt(x: number, y: number): { col: number; row: number } {
    const box = this.getBoundingClientRect();
    const style = getComputedStyle(this);
    const colGap = parseFloat(style.columnGap) || 0;
    const rowGap = parseFloat(style.rowGap) || 0;
    const width = (box.width - colGap * (this.columns - 1)) / this.columns;
    const height = parseFloat(style.gridAutoRows) || parseFloat(style.gridTemplateRows) || 1;
    const along = style.direction === "rtl" ? box.right - x : x - box.left;
    return {
      col: Math.floor(along / (width + colGap)) + 1,
      row: Math.floor((y - box.top) / (height + rowGap)) + 1,
    };
  }

  /** An interactive page: C# takes the new layout; the tile holds its place until that render. */
  private report(tile: HTMLElement, p: Place): void {
    const id = tile.dataset.tileId;
    if (!id) {
      clearDrag(tile);
      return;
    }
    for (const [name, value] of Object.entries(p)) tile.style.setProperty(`--ib-drag-${name}`, String(value));
    const tiles = this.serialize();
    const seq = (this.seq = Math.max(this.seq, Number(this.dataset.ibSeq ?? 0)) + 1);
    this.busy = true;
    let timer = 0;
    const done = () => {
      observer.disconnect();
      clearTimeout(timer);
      this.busy = false;
      // The render drew the new place (or, if C# kept the old one, that shows again).
      clearDrag(tile);
    };
    const observer = new MutationObserver(() => {
      if (Number(this.dataset.ibSeq ?? 0) >= seq) done();
    });
    observer.observe(this, { attributes: true, attributeFilter: ["data-ib-seq"] });
    // No answer (the circuit is down): the tile goes back.
    timer = window.setTimeout(done, 3000);
    this.dispatchEvent(new CustomEvent("ibtiles", { bubbles: true, detail: { tiles, id, seq } }));
  }
}

/** A place one step along: moved a cell, or a cell bigger or smaller. */
function step(p: Place, kind: Kind): Place {
  switch (kind) {
    case "left": return { ...p, col: p.col - 1 };
    case "right": return { ...p, col: p.col + 1 };
    case "up": return { ...p, row: p.row - 1 };
    case "down": return { ...p, row: p.row + 1 };
    case "wider": return { ...p, cs: p.cs + 1 };
    case "narrower": return { ...p, cs: p.cs - 1 };
    case "taller": return { ...p, rs: p.rs + 1 };
    case "shorter": return { ...p, rs: p.rs - 1 };
    default: return p;
  }
}

function isResize(kind: Kind): boolean {
  return kind === "wider" || kind === "narrower" || kind === "taller" || kind === "shorter";
}

function sameAs(a: Place, b: Place): boolean {
  return a.col === b.col && a.row === b.row && a.cs === b.cs && a.rs === b.rs;
}

function clamp(value: number, min: number, max: number): number {
  return Math.max(min, Math.min(max, value));
}

function clearDrag(tile: HTMLElement): void {
  for (const name of ["col", "row", "cs", "rs"]) tile.style.removeProperty(`--ib-drag-${name}`);
}

/** Whether a press is on an element's own scrollbar, past its content box. */
function onScrollbar(el: Element, event: PointerEvent): boolean {
  if (!(el instanceof HTMLElement) || (el.scrollHeight <= el.clientHeight && el.scrollWidth <= el.clientWidth)) return false;
  const box = el.getBoundingClientRect();
  const rtl = getComputedStyle(el).direction === "rtl";
  const x = rtl ? box.right - event.clientX : event.clientX - box.left;
  return x > el.clientLeft + el.clientWidth || event.clientY - box.top > el.clientTop + el.clientHeight;
}

/** The nearest ancestor that scrolls vertically, or the page. */
function scrollParent(el: Element): Element | null {
  for (let node = el.parentElement; node; node = node.parentElement) {
    const overflow = getComputedStyle(node).overflowY;
    if ((overflow === "auto" || overflow === "scroll") && node.scrollHeight > node.clientHeight) return node;
  }
  return document.scrollingElement;
}

function swallow(event: Event): void {
  event.preventDefault();
  event.stopImmediatePropagation();
}

defineElement("ib-tile-layout", TileLayout);
