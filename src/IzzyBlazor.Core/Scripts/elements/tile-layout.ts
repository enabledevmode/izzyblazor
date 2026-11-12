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

/** Every tile's place. */
type Layout = Map<HTMLElement, Place>;

/**
 * <ib-tile-layout> moves and resizes the tiles of an editable TileLayout, at its widest column
 * count only (the handles hide below it). Every place is in cells: a tile snaps from cell to cell
 * as the pointer crosses them. By default a tile in the way takes the moved tile's old cells when
 * it fits there, else moves down, and then every tile rises (not with data-ib-float): arrange(),
 * the same rule as C#'s TileArrange. The other tiles show their new places as the pointer moves.
 * data-ib-collision="block" stops a tile at its last free spot; "overlap" lets it cover others.
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

  private get collision(): string {
    return this.dataset.ibCollision ?? "push";
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

  /** Every tile's place now. */
  private layout(): Layout {
    return new Map(this.tiles().map((tile) => [tile, this.placeOf(tile)]));
  }

  /**
   * The layout with the tile at a place, from a layout: null when the place is outside the grid or
   * the tile's limits, covers a tile (block), pushes one past the rows, or (when a change is
   * required, as for the menus) changes nothing.
   */
  private place(tile: HTMLElement, p: Place, from: Layout, requireChange: boolean, kind?: Kind): Layout | null {
    const [minCs, maxCs, minRs, maxRs] = (tile.dataset.ibLimits ?? "1,0,1,0").split(",").map(Number);
    const cols = this.columns;
    const rows = this.rows || Infinity;
    if (p.col < 1 || p.row < 1 || p.col + p.cs - 1 > cols || p.row + p.rs - 1 > rows) return null;
    if (p.cs < minCs || (maxCs > 0 && p.cs > maxCs) || p.rs < minRs || (maxRs > 0 && p.rs > maxRs)) return null;
    let next: Layout | null;
    const changes = (layout: Layout | null) => !!layout && [...layout].some(([other, q]) => !sameAs(q, from.get(other)!));
    if (this.collision === "push") {
      const compact = !this.hasAttribute("data-ib-float");
      // A menu's step onto tiles jumps past them first; the step itself when that can't be made.
      const leap = kind ? jump(from, tile, kind, p, cols, rows) : null;
      next = leap ? arrange(from, tile, leap, cols, rows, compact) : null;
      if (!changes(next)) next = arrange(from, tile, p, cols, rows, compact);
    } else {
      next = new Map(from).set(tile, p);
      if (this.collision === "block" && [...from].some(([other, o]) => other !== tile && overlaps(p, o))) next = null;
    }
    if (requireChange && !changes(next)) return null;
    return next;
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
    const now = this.layout();
    const kind = button.dataset.ibMove as Kind;
    const next = this.place(tile, step(now.get(tile)!, kind), now, true, kind);
    if (!next) return;
    this.commit(tile, next, isResize(button.dataset.ibMove as Kind));
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

  /** A move made here: every tile's place, the posted layout, the menus and the announcement. */
  private commit(tile: HTMLElement, layout: Layout, resized: boolean): void {
    for (const [each, p] of layout) {
      for (const [name, value] of Object.entries(p)) each.style.setProperty(`--ib-tile-${name}${this.suffix}`, String(value));
    }
    this.clearDrags();
    const input = this.querySelector<HTMLInputElement>(':scope > input[type=hidden][name$=".Tiles"]');
    if (input) input.value = this.serialize();
    this.refreshMenus();
    this.say(tile, layout.get(tile)!, resized);
  }

  /** Shows a layout while dragging, over the places drawn. */
  private preview(layout: Layout): void {
    for (const [tile, p] of layout) {
      for (const [name, value] of Object.entries(p)) tile.style.setProperty(`--ib-drag-${name}`, String(value));
    }
  }

  private clearDrags(): void {
    for (const tile of this.tiles()) clearDrag(tile);
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

  /** Each tile's menu offers only the moves that can be made now. */
  private refreshMenus(): void {
    const now = this.layout();
    for (const tile of this.tiles()) {
      const p = now.get(tile)!;
      for (const button of tile.querySelectorAll<HTMLButtonElement>("button[data-ib-move]")) {
        const kind = button.dataset.ibMove as Kind;
        button.disabled = !this.place(tile, step(p, kind), now, true, kind);
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
    // The layout when the drag started, which every step arranges from (tiles pushed aside come
    // back when the pointer moves on), and the one shown now.
    const start = this.layout();
    let current = from;
    let shown: Layout = start;
    let x = startX;
    let y = startY;
    let dragging = false;
    let frame = 0;
    let held = 0;
    // A cell's width and height with the gap after it, and where the layout was, at the start: the
    // tile moves (or grows) by as many cells as the pointer has moved, past half a cell, wherever
    // it was grabbed (the pill handle sits over the tile's edge), and with the page as it scrolls.
    let pitch = { col: 1, row: 1, rtl: false };
    let startTop = 0;
    const scroller = scrollParent(this);

    try {
      pressed.setPointerCapture(event.pointerId);
    } catch {
      // A synthetic pointer: moves still arrive while over what was pressed.
    }

    const begin = () => {
      dragging = true;
      pitch = this.pitch();
      startTop = this.getBoundingClientRect().top;
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
      const across = Math.round(((x - startX) * (pitch.rtl ? -1 : 1)) / pitch.col);
      const down = Math.round((y - startY + startTop - this.getBoundingClientRect().top) / pitch.row);
      const next: Place = mode === "move"
        ? { ...current, col: clamp(from.col + across, 1, this.columns - from.cs + 1), row: Math.max(1, from.row + down) }
        : { ...current, cs: Math.max(1, from.cs + across), rs: Math.max(1, from.rs + down) };
      if (this.rows > 0 && mode === "move") next.row = Math.min(next.row, this.rows - from.rs + 1);
      if (sameAs(next, current)) return;
      if (sameAs(next, from)) {
        // Back where it started: nothing moves.
        current = next;
        shown = start;
        this.clearDrags();
        return;
      }
      const arranged = this.place(tile, next, start, false);
      if (!arranged) return;
      current = next;
      shown = arranged;
      this.preview(arranged);
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
      if (!drop || sameAs(current, from) || [...shown].every(([each, p]) => sameAs(p, start.get(each)!))) {
        this.clearDrags();
        return;
      }
      if (this.live) this.report(tile);
      else this.commit(tile, shown, mode === "resize");
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

  /** A cell's width and height, each with the gap after it, at the widest column count. */
  private pitch(): { col: number; row: number; rtl: boolean } {
    const style = getComputedStyle(this);
    const colGap = parseFloat(style.columnGap) || 0;
    const width = (this.getBoundingClientRect().width - colGap * (this.columns - 1)) / this.columns;
    const height = parseFloat(style.gridAutoRows) || parseFloat(style.gridTemplateRows) || 1;
    return { col: width + colGap || 1, row: height + (parseFloat(style.rowGap) || 0), rtl: style.direction === "rtl" };
  }

  /** An interactive page: C# takes the layout shown; the tiles hold their places until that render. */
  private report(tile: HTMLElement): void {
    const id = tile.dataset.tileId;
    if (!id) {
      this.clearDrags();
      return;
    }
    const tiles = this.serialize();
    const seq = (this.seq = Math.max(this.seq, Number(this.dataset.ibSeq ?? 0)) + 1);
    this.busy = true;
    let timer = 0;
    const done = () => {
      observer.disconnect();
      clearTimeout(timer);
      this.busy = false;
      // The render drew the new places (or, if C# kept the old ones, those show again).
      this.clearDrags();
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

/**
 * The layout with one tile moved to a place: each other tile, by row then column, that overlaps a
 * tile already placed takes the moved tile's old cells if it fits there, else moves down below what
 * it overlaps. Then, when compacting, every tile rises, by row then column, as far as it can. Null
 * when a tile would be pushed past the rows. The same rule as C#'s TileArrange.Arrange.
 */
function arrange(layout: Layout, moved: HTMLElement, target: Place, columns: number, rows: number, compact: boolean): Layout | null {
  const from = layout.get(moved)!;
  const placed: [HTMLElement, Place][] = [[moved, target]];
  const hits = (p: Place, list: [HTMLElement, Place][]) => list.filter(([, o]) => overlaps(p, o));
  const hitsAny = (p: Place, list: [HTMLElement, Place][]) => list.some(([, o]) => overlaps(p, o));
  for (const [tile, p] of byReading([...layout].filter(([tile]) => tile !== moved))) {
    let at = p;
    if (hitsAny(at, placed)) {
      const swap = { ...at, col: from.col, row: from.row };
      if (swap.col + swap.cs - 1 <= columns && swap.row + swap.rs - 1 <= rows && !hitsAny(swap, placed)) {
        at = swap;
      } else {
        // Down below every tile it overlaps, until it overlaps none.
        for (let over = hits(at, placed); over.length; over = hits(at, placed)) {
          at = { ...at, row: Math.max(...over.map(([, o]) => o.row + o.rs)) };
        }
        if (at.row + at.rs - 1 > rows) return null;
      }
    }
    placed.push([tile, at]);
  }
  if (!compact) return new Map(placed);
  // Each tile rises to just below the lowest tile above it in its columns (the tiles before it,
  // already risen, are all above it), or to the first row.
  const risen: [HTMLElement, Place][] = [];
  for (const [tile, p] of byReading(placed)) {
    let top = 1;
    for (const [, o] of risen) if (p.col < o.col + o.cs && o.col < p.col + p.cs) top = Math.max(top, o.row + o.rs);
    risen.push([tile, { ...p, row: top }]);
  }
  return new Map(risen);
}

/**
 * For a step of one cell onto other tiles, the place just past them: the tile's far edge on theirs.
 * Null for a resize, a step onto no tile, or a place outside the grid. The same as C#'s TileArrange.Jump.
 */
function jump(layout: Layout, moved: HTMLElement, kind: Kind, p: Place, columns: number, rows: number): Place | null {
  const hit = [...layout].filter(([tile, o]) => tile !== moved && overlaps(p, o)).map(([, o]) => o);
  if (!hit.length) return null;
  const j: Place | null =
    kind === "left" ? { ...p, col: Math.min(...hit.map((o) => o.col)) }
    : kind === "right" ? { ...p, col: Math.max(...hit.map((o) => o.col + o.cs)) - p.cs }
    : kind === "up" ? { ...p, row: Math.min(...hit.map((o) => o.row)) }
    : kind === "down" ? { ...p, row: Math.max(...hit.map((o) => o.row + o.rs)) - p.rs }
    : null;
  return j && !sameAs(j, p) && j.col >= 1 && j.row >= 1 && j.col + j.cs - 1 <= columns && j.row + j.rs - 1 <= rows ? j : null;
}

/** By row, then column; a stable sort, as C#'s OrderBy. */
function byReading(list: [HTMLElement, Place][]): [HTMLElement, Place][] {
  return [...list].sort(([, a], [, b]) => a.row - b.row || a.col - b.col);
}

function overlaps(a: Place, b: Place): boolean {
  return a.col < b.col + b.cs && b.col < a.col + a.cs && a.row < b.row + b.rs && b.row < a.row + a.rs;
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
