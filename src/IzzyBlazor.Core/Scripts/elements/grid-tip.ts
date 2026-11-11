import { track } from "../position";

/**
 * A grid's tooltip for text cut off at one line: a part of <ib-grid>. A header's title, or a cell
 * that keeps to one line (WrapCells, Wrap, numbers), shows its whole text in a tooltip, only when
 * it's cut off: on hover after a moment, and at once when the cell takes focus, by keys or a tap.
 * It stays while the pointer moves onto it and Escape hides it (WCAG 1.4.13). It's one element in
 * the top layer, anchored to the cell, so the grid's scrolling never clips it.
 */
const CELLS = "td, th";
// A cell of buttons or fields shows those, not a tooltip.
const CONTROLS = "a[href], button, input:not([type=hidden], .ib-table-select-inline), select, textarea, [contenteditable]:not([contenteditable=false])";
let tips = 0;

export function setupCellTips(table: HTMLTableElement, signal: AbortSignal): void {
  if (!("popover" in HTMLElement.prototype)) return;
  const name = `--ib-cell-tip-${++tips}`;
  let tip: HTMLElement | null = null;
  let shownFor: HTMLElement | null = null;
  let timer = 0;
  let untrack: (() => void) | null = null;

  // The element of a cell or header whose text one line cuts off, if it does.
  const clipped = (cell: HTMLElement) => {
    const text = cell.localName === "th"
      ? cell.querySelector<HTMLElement>(".ib-table-title")
      : cell.matches(".ib-cell-clip, .ib-cell-end")
        ? cell.querySelector<HTMLElement>(":scope > .ib-treelist-cell > .ib-treelist-value") ?? cell
        : null;
    if (!text || text.scrollWidth <= text.clientWidth || (text === cell && cell.querySelector(CONTROLS))) return null;
    return text;
  };

  const show = (cell: HTMLElement | null) => {
    const text = cell ? clipped(cell)?.textContent?.trim() : null;
    if (shownFor && shownFor !== cell) shownFor.style.removeProperty("anchor-name");
    if (!cell || !text) {
      if (tip?.matches(":popover-open")) tip.hidePopover();
      untrack?.();
      untrack = null;
      shownFor = null;
      return;
    }
    if (!tip?.isConnected) {
      // In the body, which enhanced navigation leaves alone.
      tip = document.createElement("span");
      tip.className = "ib-tooltip ib-tooltip-top";
      tip.setAttribute("role", "tooltip");
      tip.setAttribute("popover", "manual");
      tip.style.setProperty("position-anchor", name);
      tip.addEventListener("pointerenter", () => clearTimeout(timer));
      tip.addEventListener("pointerleave", () => later(null, 100));
      document.body.append(tip);
    }
    tip.textContent = text;
    cell.style.setProperty("anchor-name", name);
    shownFor = cell;
    if (!tip.matches(":popover-open")) tip.showPopover();
    untrack?.();
    untrack = track(tip, cell, "top");
  };

  // Shows the tooltip for a cell after a wait, or hides it (null), unless the next call comes first.
  const later = (cell: HTMLElement | null, ms: number) => {
    clearTimeout(timer);
    timer = window.setTimeout(() => show(cell), ms);
  };

  table.addEventListener("pointerover", (event) => {
    const cell = (event.target as Element).closest<HTMLElement>(CELLS);
    if (cell && cell !== shownFor && cell.closest("table") === table) later(cell, 300);
  }, { signal });
  table.addEventListener("pointerout", (event) => {
    const to = event.relatedTarget as Element | null;
    if (!to || to.closest(CELLS) !== shownFor) later(null, 100);
  }, { signal });
  table.addEventListener("focusin", (event) => {
    const cell = event.target as HTMLElement;
    if (cell.matches(CELLS) && cell.closest("table") === table) later(cell, 0);
  }, { signal });
  table.addEventListener("focusout", () => later(null, 0), { signal });
  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape") later(null, 0);
  }, { signal });
  signal.addEventListener("abort", () => {
    clearTimeout(timer);
    untrack?.();
    shownFor?.style.removeProperty("anchor-name");
    tip?.remove();
  });
}
