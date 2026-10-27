/**
 * Tabs that don't fit scroll sideways (tabs.css). The strip hides its scrollbar, so with a mouse
 * a wheel turned over it scrolls it, and the selected tab is scrolled into view when the page
 * loads. Tabs (radios) and TabLinks (a nav of links) both: the links have no element of their own.
 * Without script the strip still scrolls by touch, trackpad and Shift+wheel.
 *
 * A TabLinks strip hidden when the page loads (in a closed dialog, say) shows from its start;
 * Tabs' own element reveals its selected tab once the strip has a size.
 */

const LIST = ".ib-tabs > .ib-tabs-list";

/**
 * Scrolls the list to show its selected (or current) tab, leaving the page alone: only when it's
 * cut off, and then centered, so the tabs on either side show there are more. False when the list
 * isn't laid out (hidden), so there was nothing to measure.
 */
export function revealSelectedTab(list: HTMLElement): boolean {
  if (!list.clientWidth) return false;
  if (list.scrollWidth <= list.clientWidth) return true;
  const tab = list.querySelector<HTMLElement>(":scope > .ib-tab:is(:has(> .ib-tab-input:checked), [aria-current])");
  if (!tab) return true;
  const box = list.getBoundingClientRect();
  const r = tab.getBoundingClientRect();
  if (r.left >= box.left && r.right <= box.right) return true;
  list.scrollLeft += r.left - box.left - (box.width - r.width) / 2;
  return true;
}

/** Reveals the selected tab of every tab strip on the page (on load and after enhanced navigation). */
export function revealSelectedTabs(): void {
  document.querySelectorAll<HTMLElement>(LIST).forEach(revealSelectedTab);
}

// Which of the page and a strip a wheel gesture is scrolling, until when, as the browser's own
// scroll areas latch: a trackpad scrolling the page carries on over a strip, and a fling that
// reaches a strip's end stays on it rather than carrying on into the page.
let latched: { list: HTMLElement | null; until: number } | null = null;
const LATCH_MS = 150;

function onWheel(this: HTMLElement, event: WheelEvent): void {
  const list = this;
  // Horizontal swipes, Shift+wheel and pinch-zoom already do the right thing.
  if (event.ctrlKey || event.shiftKey || Math.abs(event.deltaX) >= Math.abs(event.deltaY)) return;
  const now = event.timeStamp;
  if (latched && latched.list !== list && now < latched.until) return;
  // A vertical list laid out as a column doesn't scroll sideways (overflow: visible).
  if (list.scrollWidth <= list.clientWidth || !/auto|scroll/.test(getComputedStyle(list).overflowX)) return;

  const unit = event.deltaMode === WheelEvent.DOM_DELTA_LINE ? 16 : event.deltaMode === WheelEvent.DOM_DELTA_PAGE ? list.clientWidth : 1;
  // Right-to-left, the strip's end is to the left (scrollLeft runs from 0 down).
  const rtl = getComputedStyle(list).direction === "rtl";
  const before = list.scrollLeft;
  list.scrollLeft += event.deltaY * unit * (rtl ? -1 : 1);

  // Past either end, the wheel scrolls the page again (once a fling on the strip has settled).
  if (list.scrollLeft === before && !(latched?.list === list && now < latched.until)) return;
  event.preventDefault();
  latched = { list, until: now + LATCH_MS };
}

// A wheel no strip took scrolls the page (or another scroll area): passive, so it costs nothing.
function onPageWheel(event: WheelEvent): void {
  if (!event.defaultPrevented) latched = { list: null, until: event.timeStamp + LATCH_MS };
}

// Each strip listens for the wheel itself, once the pointer first comes over it: a listener that
// can cancel the wheel makes the browser wait on it, which it shouldn't do for the rest of the page.
const listening = new WeakSet<HTMLElement>();

export function installTabScroll(): void {
  document.addEventListener("wheel", onPageWheel, { passive: true });
  document.addEventListener(
    "pointerover",
    (event) => {
      const list = (event.target as Element).closest?.<HTMLElement>(LIST);
      if (!list || listening.has(list)) return;
      listening.add(list);
      list.addEventListener("wheel", onWheel, { passive: false });
    },
    { passive: true }
  );

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", revealSelectedTabs, { once: true });
  } else {
    revealSelectedTabs();
  }
}
