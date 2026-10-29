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
 */

export interface SortOptions {
  /** The items' container; its element children are the items. */
  list: HTMLElement;
  /** The item being dragged. */
  item: HTMLElement;
  /** The pointerdown that may start the drag, on the item's handle. */
  event: PointerEvent;
  /** Rows that wrap: a drop line instead of sliding, and the item follows the pointer both ways. */
  wrap: boolean;
  /** The drop, when the item lands somewhere new. Call settle to clear the transforms. */
  onDrop(from: number, to: number, settle: () => void): void;
  /** After the drag ends, either way (not when the press never became a drag). */
  onEnd?(): void;
}

// A press moving less than this is a click (the handle's menu), not a drag.
const THRESHOLD = 4;
// Within this distance of a scroll container's edge, it scrolls; faster nearer the edge.
const EDGE = 48;
const MAX_SPEED = 16;

/** Follows a press on a handle; becomes a drag once it moves a few pixels. */
export function trackDrag(options: SortOptions): void {
  const { list, item, event, wrap } = options;
  const handle = event.target as Element;
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
  let step = 0;
  let marked: HTMLElement | null = null;
  let scroller: Element | null = null;
  let frame = 0;

  try {
    handle.setPointerCapture(event.pointerId);
  } catch {
    // An unknown pointer (synthetic events): moves still arrive while over the handle.
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
    step = own.height + (parseFloat(getComputedStyle(list).rowGap) || 0);
    scroller = scrollParent(list);
    list.classList.add("ib-sortable-sorting");
    item.classList.add("ib-sortable-dragging");
    frame = requestAnimationFrame(autoScroll);
  };

  const update = () => {
    const box = list.getBoundingClientRect();
    const dx = wrap ? x - startX + (startBox.left - box.left) : 0;
    const dy = y - startY + (startBox.top - box.top);
    item.style.transform = `translate(${dx}px, ${dy}px)`;
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
    marked?.removeAttribute("data-ib-drop");
    marked = null;
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
    try {
      handle.releasePointerCapture(event.pointerId);
    } catch {
      // Already released.
    }
    if (!dragging) return;
    // Without the sorting class first, so clearing the transforms doesn't animate.
    list.classList.remove("ib-sortable-sorting");
    // The lifted item and, in rows that wrap, the drop line stay until it settles.
    const settle = () => {
      marked?.removeAttribute("data-ib-drop");
      item.classList.remove("ib-sortable-dragging");
      for (const el of items) el.style.transform = "";
    };
    // The click that follows the pointerup would open the handle's menu.
    handle.addEventListener("click", swallow, { capture: true, once: true });
    setTimeout(() => handle.removeEventListener("click", swallow, { capture: true }), 0);
    if (drop && to !== from) options.onDrop(from, to, settle);
    else settle();
    options.onEnd?.();
  };

  handle.addEventListener(
    "pointermove",
    (e) => {
      const move = e as PointerEvent;
      if (move.pointerId !== event.pointerId) return;
      x = move.clientX;
      y = move.clientY;
      if (!dragging) {
        if (Math.hypot(x - startX, y - startY) < THRESHOLD) return;
        begin();
      }
      move.preventDefault();
      update();
    },
    { signal }
  );
  handle.addEventListener("pointerup", (e) => (e as PointerEvent).pointerId === event.pointerId && finish(true), { signal });
  handle.addEventListener("pointercancel", () => finish(false), { signal });
  handle.addEventListener("lostpointercapture", () => finish(false), { signal });
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

/** The nearest ancestor that scrolls vertically, or the page. */
function scrollParent(el: Element): Element | null {
  for (let node = el.parentElement; node; node = node.parentElement) {
    const overflow = getComputedStyle(node).overflowY;
    if ((overflow === "auto" || overflow === "scroll") && node.scrollHeight > node.clientHeight) return node;
  }
  return document.scrollingElement;
}
