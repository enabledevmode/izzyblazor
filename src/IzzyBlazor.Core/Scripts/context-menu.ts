/**
 * Opens a row's or item's own menu at the pointer: by right-click, the menu key, or a long press on
 * touch. It's the same menu its ⋮ button opens, so it acts on that row and works the same however
 * it's opened. Links (a row that is one included), fields and selected text keep the browser's menu,
 * as does Shift+right-click.
 */

const OWN_MENU = "a[href], input, select, textarea, [contenteditable]:not([contenteditable=false]), [popover], dialog";
const LONG_PRESS = 500;
const MOVE_LIMIT = 8;
// The click that ends a long press comes this soon after the finger lifts, or it isn't coming.
const CLICK_WINDOW = 350;

/** A menu trigger of this row or item, not of one nested inside it. */
export function ownTrigger(owner: Element, row: string, trigger: string): HTMLElement | null {
  return [...owner.querySelectorAll<HTMLElement>(trigger)].find((found) => found.closest(row) === owner && !found.matches(":disabled")) ?? null;
}

/** Opens the menu behind a trigger at a point on the screen. */
export function openMenuAt(trigger: HTMLElement, x: number, y: number): void {
  const panelId = trigger.getAttribute("popovertarget");
  const panel = panelId ? document.getElementById(panelId) : null;
  if (!panel || typeof panel.showPopover !== "function") return;
  if (panel.matches(":popover-open")) panel.hidePopover();
  // Not placed by its button, which is what the script does where the browser can't anchor it.
  panel.dataset.ibAtPointer = "";
  panel.showPopover();
  // Kept on the screen; the panel's own placement comes back when it closes.
  const { width, height } = panel.getBoundingClientRect();
  const left = Math.max(0, Math.min(x, innerWidth - width));
  const top = Math.max(0, Math.min(y, innerHeight - height));
  panel.style.cssText += `;position:fixed;inset:auto;margin:0;position-anchor:none;left:${left}px;top:${top}px`;

  // It stays where it opened, so once its row has scrolled away it's closed, not left over another.
  // By where the row is, not that something scrolled: another list may have, or a scroll that had
  // already ended, whose event comes a frame later.
  const opened = trigger.getBoundingClientRect();
  const scrolled = () => {
    const now = trigger.getBoundingClientRect();
    if (now.top !== opened.top || now.left !== opened.left) panel.hidePopover();
  };
  document.addEventListener("scroll", scrolled, { capture: true, passive: true });
  panel.addEventListener("toggle", function reset(event) {
    if ((event as Event & { newState?: string }).newState !== "closed") return;
    panel.removeEventListener("toggle", reset);
    document.removeEventListener("scroll", scrolled, { capture: true });
    delete panel.dataset.ibAtPointer;
    for (const name of ["position", "inset", "margin", "position-anchor", "left", "top"]) panel.style.removeProperty(name);
  });
}

// Events an inner grid or list has already answered, for the one around it.
const handled = new WeakSet<Event>();

/**
 * Right-click, the menu key and long press on what `row` matches open the menu that `trigger` finds
 * in it. A row with none keeps the browser's menu. `longPress` is off where a long press means
 * something else (a list that drags on it).
 */
export function setupContextMenu(root: HTMLElement, row: string, trigger: string, longPress: () => boolean, signal: AbortSignal): void {
  const triggerFor = (target: Element): HTMLElement | null => {
    if (target.closest(OWN_MENU)) return null;
    const owner = target.closest<HTMLElement>(row);
    return owner && root.contains(owner) ? ownTrigger(owner, row, trigger) : null;
  };

  root.addEventListener("contextmenu", (event) => {
    if (handled.has(event) || event.shiftKey || !getSelection()?.isCollapsed) return;
    const target = event.target as Element;
    const button = triggerFor(target);
    if (!button) return;
    handled.add(event);
    event.preventDefault();
    // The menu key has no pointer: under what has focus.
    const rect = target.getBoundingClientRect();
    const fromKey = event.button !== 2 || (event.clientX === 0 && event.clientY === 0);
    openMenuAt(button, fromKey ? rect.left : event.clientX, fromKey ? rect.bottom : event.clientY);
  }, { signal });

  let timer = 0;
  root.addEventListener("pointerdown", (down) => {
    clearTimeout(timer);
    if (handled.has(down) || down.pointerType !== "touch" || !longPress()) return;
    const button = triggerFor(down.target as Element);
    if (!button) return;
    handled.add(down);
    // It opens as the finger lifts: opened under it, the lift would count as a press outside.
    let held = false;
    timer = window.setTimeout(() => (held = true), LONG_PRESS);
    const moved = (move: PointerEvent) => {
      if (Math.hypot(move.clientX - down.clientX, move.clientY - down.clientY) > MOVE_LIMIT) {
        clearTimeout(timer);
        held = false;
      }
    };
    const stop = () => {
      clearTimeout(timer);
      document.removeEventListener("pointermove", moved);
      document.removeEventListener("pointerup", up);
      document.removeEventListener("pointercancel", stop);
    };
    const up = () => {
      stop();
      if (!held) return;
      openMenuAt(button, down.clientX, down.clientY);
      // The tap that ends the press isn't a click on the row, or on the menu now under the finger.
      const swallow = (event: Event) => {
        event.preventDefault();
        event.stopPropagation();
      };
      document.addEventListener("click", swallow, { capture: true, once: true });
      setTimeout(() => document.removeEventListener("click", swallow, { capture: true }), CLICK_WINDOW);
    };
    document.addEventListener("pointermove", moved, { signal });
    document.addEventListener("pointerup", up, { signal });
    document.addEventListener("pointercancel", stop, { signal });
  }, { signal });
}
