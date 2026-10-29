/**
 * What <ib-table> and <ib-listview> share: the select-all checkbox, and loading the next page as
 * the last item comes into view.
 */

/**
 * Select all on this page. Items are plain checkboxes that post with the form (and report their
 * own changes on interactive pages), so select-all only checks or clears the ones shown and fires
 * `change` on each it flips, then shows itself as checked, cleared or partly selected
 * (indeterminate). Marks the root data-ready while it runs, so CSS can show the checkbox.
 */
export function setupSelectAll(root: HTMLElement, allSelector: string, itemSelector: string, signal: AbortSignal): void {
  if (!root.querySelector(allSelector)) return;
  root.dataset.ready = "";

  const items = () => [...root.querySelectorAll<HTMLInputElement>(itemSelector)].filter((box) => !box.disabled);
  const sync = () => {
    const all = root.querySelector<HTMLInputElement>(allSelector);
    if (!all) return;
    const boxes = items();
    const checked = boxes.filter((box) => box.checked).length;
    all.checked = boxes.length > 0 && checked === boxes.length;
    all.indeterminate = checked > 0 && checked < boxes.length;
    all.disabled = boxes.length === 0;
  };

  root.addEventListener(
    "change",
    (event) => {
      const target = event.target as HTMLInputElement;
      if (target.matches(allSelector)) {
        // Read once: each item's change event below syncs select-all, which may change it.
        const on = target.checked;
        for (const box of items()) {
          if (box.checked === on) continue;
          box.checked = on;
          box.dispatchEvent(new Event("change", { bubbles: true }));
        }
        sync();
      } else if (target.matches(itemSelector)) {
        sync();
      }
    },
    { signal }
  );

  // Items replaced by paging, sorting, filtering or an interactive render.
  const observer = new MutationObserver(sync);
  observer.observe(root, { childList: true, subtree: true, attributes: true, attributeFilter: ["checked"] });
  root.closest("form")?.addEventListener("reset", () => setTimeout(sync, 0), { signal });
  signal.addEventListener("abort", () => {
    observer.disconnect();
    delete root.dataset.ready;
  });

  sync();
}

/**
 * Loading on scroll (Paging LoadOnScroll): when the last item comes into view, presses the "load
 * next" trigger after the items, a load that doesn't move focus. On interactive pages it's a hidden
 * button; on static pages the Load more link, which enhanced navigation follows, keeping the scroll.
 * A static page that grew focuses the first new item on arrival, as after a press of Load more:
 * not after a scroll, so this tells focus.ts to skip that once.
 */
export function setupLoadOnScroll(root: HTMLElement, lastSelector: string, triggerSelector: string, signal: AbortSignal): void {
  if (!root.hasAttribute("data-ib-load-on-scroll")) return;
  let last: Element | null = null;
  let inView = false;
  // The trigger is drawn only on interactive pages, and not while a page loads: one at a time.
  let following: Element | null = null;
  const loadNext = () => {
    const trigger = root.querySelector<HTMLElement>(triggerSelector);
    // A link followed once: its page is on its way, and the patch brings a new last item.
    if (!trigger || trigger === following) return;
    if (trigger instanceof HTMLAnchorElement) {
      following = trigger;
      (document as unknown as Record<symbol, boolean>)[Symbol.for("ib.skipFocusMark")] = true;
    }
    trigger.click();
  };
  // Viewport-rooted, so a scroll box of its own (MaxHeight) clips it too.
  const visibility = new IntersectionObserver((entries) => {
    // Only the item watched now: a report queued for the one before may still arrive.
    const entry = entries.filter((e) => e.target === last).pop();
    if (!entry) return;
    inView = entry.isIntersecting;
    if (inView) loadNext();
  });
  // New items move the last one; a trigger that comes back (a page loaded) while it's still in
  // view loads the next, until the items fill the view.
  const watch = () => {
    const item = root.querySelector(lastSelector);
    if (item !== last) {
      following = null;
      if (last) visibility.unobserve(last);
      last = item;
      inView = false;
      if (item) visibility.observe(item);
    } else if (inView) {
      loadNext();
    }
  };
  const changes = new MutationObserver(watch);
  changes.observe(root, { childList: true, subtree: true });
  signal.addEventListener("abort", () => {
    changes.disconnect();
    visibility.disconnect();
  });
  watch();
}
