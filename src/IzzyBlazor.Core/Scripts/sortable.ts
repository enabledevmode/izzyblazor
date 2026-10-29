/**
 * Dragging an item to a new place among its siblings, with pointer events (mouse, pen and touch
 * alike; no HTML5 drag and drop, which is weak on touch and can't style its ghost). Shared by
 * <ib-sortable> and, later, anything else that reorders: it works on any container whose element
 * children are the items.
 *
 * It never moves nodes. While dragging, the item follows the pointer by a transform, and either
 * the others slide aside (a single column, as DevExtreme's "push") or a line marks the drop place
 * (rows that wrap). When the pointer comes up, onDrop gets the item's old and new index and a
 * `settle` that clears the transforms; the caller moves the node itself and settles at once (a
 * static page), or asks the renderer to and settles when it has (an interactive one), so the
 * items stay where they were dropped until their nodes move.
 * Escape, a cancelled pointer or a lost capture put everything back.
 *
 * With a delay (a touch on the item rather than its handle), the press is a long press: the page
 * scrolls as usual if the finger moves first, and the drag begins once it has been held still.
 */

/** Another list the item may be dropped in: a drop zone of the same group. */
export interface DropTarget {
  /** The zone's box, which the pointer is over to drop there, such as its Card. */
  area: HTMLElement;
  /** Its items' container. */
  list: HTMLElement;
}

export interface SortOptions {
  /** The items' container; its element children are the items. */
  list: HTMLElement;
  /** The item being dragged. */
  item: HTMLElement;
  /** The pointerdown that may start the drag, on the item or its handle. */
  event: PointerEvent;
  /** A long press: how long the pointer is held still before the drag begins (ms). */
  delay?: number;
  /** Rows that wrap: a drop line instead of sliding, and the item follows the pointer both ways. */
  wrap: boolean;
  /**
   * Other lists it may move to (drop zones), read when the drag begins: the item follows the
   * pointer both ways, and over one of them a line marks its place there.
   */
  targets?: () => DropTarget[];
  /** Zones that won't take it (they refuse it, or are full): over one, the drop does nothing. */
  refusing?: () => HTMLElement[];
  /** With targets, its own zone's box: over none of the zones, the drop does nothing. */
  home?: HTMLElement;
  /**
   * The drop, when the item lands somewhere new: in its own list, or at `to` in `target`.
   * Call settle to clear the transforms.
   */
  onDrop(from: number, to: number, settle: () => void, target?: DropTarget): void;
  /** After the drag ends, either way (not when the press never became a drag). */
  onEnd?(): void;
}

// A press moving less than this is a click (the handle's menu), not a drag.
const THRESHOLD = 4;
// A long press that moves further than this before its delay is up is a scroll, not a drag.
const SLOP = 8;
// Within this distance of a scroll container's edge, it scrolls; faster nearer the edge.
const EDGE = 48;
const MAX_SPEED = 16;

/** Follows a press; becomes a drag once it moves a few pixels, or once held for the delay. */
export function trackDrag(options: SortOptions): void {
  const { list, item, event, wrap, delay } = options;
  // What was pressed: the handle, or anything on the item.
  const grip = event.target as Element;
  const abort = new AbortController();
  const signal = abort.signal;
  const startX = event.clientX;
  const startY = event.clientY;
  // Where the list was at the press: the item keeps up with the pointer as the list scrolls.
  const startBox = list.getBoundingClientRect();
  let x = startX;
  let y = startY;
  let dragging = false;
  let items: HTMLElement[] = [];
  let from = 0;
  let to = 0;
  // Positions within the list at the start, so scrolling during the drag doesn't skew them.
  let tops: number[] = [];
  let heights: number[] = [];
  let itemTop = 0;
  let itemLeft = 0;
  let step = 0;
  let marked: HTMLElement | null = null;
  let scroller: Element | null = null;
  let frame = 0;
  let held = 0;
  // The item follows the pointer sideways too: rows that wrap, or zones to move to.
  const free = wrap || !!options.targets;
  let targets: DropTarget[] = [];
  let refusing: HTMLElement[] = [];
  // Boxes that clipped the item (a Card's overflow: hidden), opened while it drags to other zones.
  let lifted: Element[] = [];
  let target: DropTarget | undefined;
  let zoneMarked: HTMLElement | null = null;

  try {
    grip.setPointerCapture(event.pointerId);
  } catch {
    // An unknown pointer (synthetic events): moves still arrive while over what was pressed.
  }

  const begin = () => {
    dragging = true;
    items = [...list.children].filter((el): el is HTMLElement => el instanceof HTMLElement);
    from = to = items.indexOf(item);
    const box = list.getBoundingClientRect();
    tops = items.map((el) => el.getBoundingClientRect().top - box.top);
    heights = items.map((el) => el.getBoundingClientRect().height);
    const own = item.getBoundingClientRect();
    itemTop = own.top - box.top;
    itemLeft = own.left - box.left;
    step = own.height + (parseFloat(getComputedStyle(list).rowGap) || 0);
    scroller = scrollParent(list);
    targets = options.targets?.() ?? [];
    refusing = options.refusing?.() ?? [];
    if (options.targets) lifted = unclip(list);
    list.classList.add("ib-sortable-sorting");
    item.classList.add("ib-sortable-dragging");
    frame = requestAnimationFrame(autoScroll);
  };

  if (delay) {
    held = window.setTimeout(() => {
      if (!grip.isConnected) {
        finish(false);
        return;
      }
      begin();
      update();
    }, delay);
  }

  const update = () => {
    const box = list.getBoundingClientRect();
    const dx = free ? x - startX + (startBox.left - box.left) : 0;
    const dy = y - startY + (startBox.top - box.top);
    item.style.transform = `translate(${dx}px, ${dy}px)`;
    zoneMarked?.removeAttribute("data-ib-drop");
    zoneMarked = null;
    target = targets.find((t) => within(t.area, x, y));
    const refused = !target && (refusing.some((area) => within(area, x, y)) || (!!options.home && !within(options.home, x, y)));
    item.toggleAttribute("data-ib-refused", refused);
    if (refused) {
      // Over a zone that won't take it, or over no zone: everything stays put, and the drop does nothing.
      for (const el of items) if (el !== item) el.style.transform = "";
      marked?.removeAttribute("data-ib-drop");
      marked = null;
      to = from;
      return;
    }
    if (target) {
      // Over another zone: its own list closes up, and a line marks the place in the other.
      for (const el of items) if (el !== item) el.style.transform = "";
      marked?.removeAttribute("data-ib-drop");
      marked = null;
      markTarget(target);
      return;
    }
    if (wrap) {
      markDropLine();
      return;
    }

    // A column: the item lands after every other item whose middle it has passed.
    const middle = itemTop + dy + heights[from] / 2;
    to = 0;
    items.forEach((el, i) => {
      if (i !== from && tops[i] + heights[i] / 2 < middle) to++;
    });
    items.forEach((el, i) => {
      if (i === from) return;
      const shift = from < to && i > from && i <= to ? -step : to < from && i >= to && i < from ? step : 0;
      el.style.transform = shift ? `translateY(${shift}px)` : "";
    });
  };

  // Rows that wrap: the nearest other item, and which side of it the pointer is on.
  const markDropLine = () => {
    marked?.removeAttribute("data-ib-drop");
    marked = null;
    // Over the item's own place (a long press not moved yet, or a drag brought back): it stays.
    const box = list.getBoundingClientRect();
    const left = box.left + itemLeft;
    const top = box.top + itemTop;
    if (x >= left && x <= left + item.offsetWidth && y >= top && y <= top + heights[from]) {
      to = from;
      return;
    }
    let best = -1;
    let distance = Infinity;
    items.forEach((el, i) => {
      if (i === from) return;
      const r = el.getBoundingClientRect();
      const d = Math.hypot(x - (r.left + r.width / 2), y - (r.top + r.height / 2));
      if (d < distance) {
        distance = d;
        best = i;
      }
    });
    if (best < 0) return;
    const target = items[best];
    const r = target.getBoundingClientRect();
    const rtl = getComputedStyle(list).direction === "rtl";
    const after = rtl ? x < r.left + r.width / 2 : x > r.left + r.width / 2;
    target.setAttribute("data-ib-drop", after ? "after" : "before");
    marked = target;
    // Its index once the dragged item is out of the list.
    const index = best > from ? best - 1 : best;
    to = after ? index + 1 : index;
  };

  // Another zone: the nearest of its items, and which side of it the pointer is on; an empty
  // zone is marked whole.
  const markTarget = (t: DropTarget) => {
    const others = [...t.list.children].filter((el): el is HTMLElement => el instanceof HTMLElement && el !== item);
    if (!others.length) {
      // The zone's own element (around its list and empty text) is outlined.
      const zone = t.list.parentElement ?? t.area;
      zone.setAttribute("data-ib-drop", "inside");
      zoneMarked = zone;
      to = 0;
      return;
    }
    const across = !!t.list.parentElement?.classList.contains("ib-sortable-horizontal");
    const rtl = getComputedStyle(t.list).direction === "rtl";
    let best = 0;
    let distance = Infinity;
    others.forEach((el, i) => {
      const r = el.getBoundingClientRect();
      const d = Math.hypot(x - (r.left + r.width / 2), y - (r.top + r.height / 2));
      if (d < distance) {
        distance = d;
        best = i;
      }
    });
    const r = others[best].getBoundingClientRect();
    const after = across ? (rtl ? x < r.left + r.width / 2 : x > r.left + r.width / 2) : y > r.top + r.height / 2;
    others[best].setAttribute("data-ib-drop", after ? "after" : "before");
    zoneMarked = others[best];
    to = after ? best + 1 : best;
  };

  const autoScroll = () => {
    if (!dragging) return;
    if (!item.isConnected) {
      finish(false);
      return;
    }
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

  const finish = (drop: boolean) => {
    abort.abort();
    cancelAnimationFrame(frame);
    clearTimeout(held);
    try {
      grip.releasePointerCapture(event.pointerId);
    } catch {
      // Already released.
    }
    if (!dragging) return;
    // Without the sorting class first, so clearing the transforms doesn't animate.
    list.classList.remove("ib-sortable-sorting");
    // The lifted item and, in rows that wrap, the drop line stay until it settles.
    const settle = () => {
      marked?.removeAttribute("data-ib-drop");
      zoneMarked?.removeAttribute("data-ib-drop");
      item.classList.remove("ib-sortable-dragging");
      item.removeAttribute("data-ib-refused");
      for (const el of lifted) el.removeAttribute("data-ib-unclip");
      for (const el of items) el.style.transform = "";
    };
    // The click that follows the pointerup would open the handle's menu, or follow the item's link.
    // (A touch's comes later; <ib-sortable> cancels its touchend instead.)
    item.addEventListener("click", swallow, { capture: true, once: true });
    setTimeout(() => item.removeEventListener("click", swallow, { capture: true }), 0);
    if (drop && target) options.onDrop(from, to, settle, target);
    else if (drop && to !== from) options.onDrop(from, to, settle);
    else settle();
    options.onEnd?.();
  };

  // On the document: what was pressed may be rendered away mid-drag, taking its capture with it.
  document.addEventListener(
    "pointermove",
    (e) => {
      const move = e as PointerEvent;
      if (move.pointerId !== event.pointerId) return;
      x = move.clientX;
      y = move.clientY;
      if (!dragging) {
        const moved = Math.hypot(x - startX, y - startY);
        if (delay) {
          // Moving before the long press is up: the finger is scrolling the page.
          // In CSS pixels, so as many screen pixels on a page zoomed out as in.
          if (moved > SLOP / (window.visualViewport?.scale ?? 1)) finish(false);
          return;
        }
        if (moved < THRESHOLD) return;
        begin();
      }
      move.preventDefault();
      update();
    },
    { signal }
  );
  document.addEventListener("pointerup", (e) => e.pointerId === event.pointerId && finish(true), { signal });
  document.addEventListener("pointercancel", (e) => e.pointerId === event.pointerId && finish(false), { signal });
  grip.addEventListener("lostpointercapture", () => grip.isConnected && finish(false), { signal });
  document.addEventListener(
    "keydown",
    (e) => {
      if (e.key !== "Escape" || !dragging) return;
      e.preventDefault();
      e.stopPropagation();
      finish(false);
    },
    { signal, capture: true }
  );
}

function swallow(event: Event): void {
  event.preventDefault();
  event.stopImmediatePropagation();
}

/**
 * The list's ancestors that clip what overflows them (a Card, a bordered list), up to its drop
 * zone group, marked data-ib-unclip so CSS lets the item out over the other zones. Boxes that
 * scroll keep their overflow: opening them would change the layout.
 */
function unclip(list: Element): Element[] {
  const stop = list.closest(".ib-dropzone-group");
  const marked: Element[] = [];
  for (let node = list.parentElement; node && node !== stop; node = node.parentElement) {
    const style = getComputedStyle(node);
    const clips = [style.overflowX, style.overflowY].some((o) => o === "hidden" || o === "clip");
    const scrolls = [style.overflowX, style.overflowY].some((o) => o === "auto" || o === "scroll");
    if (clips && !scrolls) {
      node.setAttribute("data-ib-unclip", "");
      marked.push(node);
    }
  }
  return marked;
}

function within(el: Element, x: number, y: number): boolean {
  const r = el.getBoundingClientRect();
  return x >= r.left && x <= r.right && y >= r.top && y <= r.bottom;
}

/** The nearest ancestor that scrolls vertically, or the page. */
function scrollParent(el: Element): Element | null {
  for (let node = el.parentElement; node; node = node.parentElement) {
    const overflow = getComputedStyle(node).overflowY;
    if ((overflow === "auto" || overflow === "scroll") && node.scrollHeight > node.clientHeight) return node;
  }
  return document.scrollingElement;
}
