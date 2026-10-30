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
  // data-ib-keys: the selection bar's copy of the selection, which changes when C# changes it on
  // interactive pages (Blazor sets checked as a property, which isn't seen).
  observer.observe(root, { childList: true, subtree: true, attributes: true, attributeFilter: ["checked", "data-ib-keys", "data-ib-all"] });
  root.closest("form")?.addEventListener("reset", () => setTimeout(sync, 0), { signal });
  // The selection script set the boxes from its own state.
  root.addEventListener("ib-selection", sync, { signal });
  signal.addEventListener("abort", () => {
    observer.disconnect();
    delete root.dataset.ready;
  });

  sync();
}

/** A view's selection as the script keeps it on static pages: as Selection in C#. */
interface SelectionState {
  keys: Set<string>;
  all: boolean;
  filter: string;
  search: string;
}

// Per view (path and selection name), kept while enhanced navigation moves between its pages,
// sorts and filters. A submit hands it to the server, whose answer is the truth after that.
const selections = new Map<string, SelectionState>();

const formatNumber = (n: number) => new Intl.NumberFormat(document.documentElement.lang || undefined).format(n);

/** Fills "{0}" or "{0:N0}" with a number, as the C# strings are written. */
const fill = (format: string, n: number) => format.replace(/\{0(?::N0)?\}/g, formatNumber(n));

/**
 * The selection beyond one page, and Shift-click ranges.
 *
 * - Shift-click on a checkbox selects or clears every box from the last one clicked (any page
 *   kind; on interactive pages each changed box fires `change`, and C# keeps the selection).
 * - On static pages the script owns the selection: it keeps the keys while enhanced navigation
 *   moves between pages, checks the boxes of each page as it arrives, runs the bar (its count,
 *   Clear selection, Select all N) and, when the form posts, writes the whole selection into what
 *   it posts (`formdata`), so keys from other pages and "all matching" post too. With no script,
 *   only the page's own boxes post.
 */
export function setupSelection(root: HTMLElement, itemSelector: string, signal: AbortSignal): void {
  const bar = root.querySelector<HTMLElement>(":scope > .ib-selection-bar");
  if (!bar) return;
  root.dataset.ready = "";
  signal.addEventListener("abort", () => delete root.dataset.ready);

  const boxes = () => [...root.querySelectorAll<HTMLInputElement>(itemSelector)];
  // On interactive pages C# keeps the selection; the script adds only the ranges.
  if (root.hasAttribute("data-ib-interactive")) {
    setupRanges(root, boxes, signal);
    return;
  }

  const name = bar.dataset.ibName ?? "selected";
  const id = `${location.pathname}|${name}`;
  const scope = { filter: bar.dataset.ibFilter ?? "", search: bar.dataset.ibSearch ?? "" };
  const fromServer = (): SelectionState => ({
    keys: new Set<string>(JSON.parse(bar.dataset.ibKeys || "[]")),
    all: bar.hasAttribute("data-ib-all"),
    filter: bar.dataset.ibScopeFilter ?? "",
    search: bar.dataset.ibScopeSearch ?? "",
  });
  const empty = (): SelectionState => ({ keys: new Set(), all: false, filter: "", search: "" });

  let state = selections.get(id) ?? fromServer();
  // All of another filter or search isn't these items (the server clears it the same way).
  if (state.all && (state.filter !== scope.filter || state.search !== scope.search)) state = empty();
  const has = (key: string) => state.all !== state.keys.has(key);
  const save = () => selections.set(id, state);
  const multiple = () => boxes().some((box) => box.type === "checkbox");

  const render = () => {
    const shown = boxes();
    for (const box of shown) box.checked = has(box.value);
    const total = bar.dataset.ibTotal ? Number(bar.dataset.ibTotal) : null;
    const count = state.all ? (total === null ? null : Math.max(0, total - state.keys.size)) : state.keys.size;
    const isEmpty = !state.all && state.keys.size === 0;
    bar.hidden = isEmpty;
    const text = isEmpty
      ? ""
      : count === null
        ? (bar.dataset.ibAllOpen ?? "")
        : fill((state.all ? bar.dataset.ibAllCount : bar.dataset.ibCount) ?? "{0}", count);
    const status = bar.querySelector<HTMLElement>(".ib-selection-count");
    if (status && status.textContent !== text) status.textContent = text;
    const selectable = shown.filter((box) => !box.disabled);
    const pageAll = selectable.length > 0 && selectable.every((box) => box.checked);
    const more = bar.querySelector<HTMLElement>(".ib-selection-more");
    if (more) more.hidden = !(multiple() && !state.all && pageAll && bar.hasAttribute("data-ib-more"));
    // The page's select-all box follows the boxes (setupSelectAll).
    root.dispatchEvent(new Event("ib-selection"));
  };

  root.addEventListener(
    "change",
    (event) => {
      const box = event.target as HTMLInputElement;
      if (!box.matches(itemSelector)) return;
      if (box.type === "radio") {
        state = { ...empty(), keys: new Set([box.value]) };
      } else {
        state = { ...state, keys: new Set(state.keys) };
        // Under All the keys are the ones left out.
        if (box.checked !== state.all) state.keys.add(box.value);
        else state.keys.delete(box.value);
      }
      save();
      render();
    },
    { signal }
  );

  bar.addEventListener(
    "click",
    (event) => {
      const button = (event.target as Element).closest<HTMLElement>("[data-ib-selection]");
      if (!button) return;
      state = button.dataset.ibSelection === "all" ? { keys: new Set(), all: true, ...scope } : empty();
      save();
      render();
      if (button.dataset.ibSelection === "clear") boxes()[0]?.focus();
      else bar.querySelector<HTMLElement>(".ib-selection-clear")?.focus();
    },
    { signal }
  );

  // The whole selection posts, not only this page's boxes. Once posted, the server's answer
  // (the page's Selection) is the truth.
  const form = root.closest("form");
  form?.addEventListener(
    "formdata",
    (event) => {
      const data = event.formData;
      data.delete(`${name}.Keys`);
      for (const key of state.keys) data.append(`${name}.Keys`, key);
      if (state.all) {
        data.set(`${name}.All`, "true");
        if (state.filter) data.set(`${name}.Filter`, state.filter);
        if (state.search) data.set(`${name}.Search`, state.search);
      }
    },
    { signal }
  );
  form?.addEventListener("submit", () => selections.delete(id), { signal });
  form?.addEventListener("reset", () => { state = empty(); selections.delete(id); setTimeout(render, 0); }, { signal });

  setupRanges(root, boxes, signal);
  render();
}

/**
 * Shift-click on a checkbox sets every box between it and the last one changed to its state. It
 * runs on the clicked box's own change, after the selection has taken it (so set it up after the
 * selection's listeners), and fires `change` on each box it flips.
 */
function setupRanges(root: HTMLElement, boxes: () => HTMLInputElement[], signal: AbortSignal): void {
  let anchor: HTMLInputElement | null = null;
  let shift = false;
  let applying = false;
  root.addEventListener(
    "click",
    (event) => {
      const box = event.target as HTMLInputElement;
      if (box.type === "checkbox" && boxes().includes(box)) shift = (event as MouseEvent).shiftKey;
    },
    { signal }
  );
  root.addEventListener(
    "change",
    (event) => {
      const box = event.target as HTMLInputElement;
      if (applying || box.type !== "checkbox" || !boxes().includes(box)) return;
      const all = boxes();
      const from = shift && anchor?.isConnected ? all.indexOf(anchor) : -1;
      anchor = box;
      shift = false;
      if (from < 0) return;
      const to = all.indexOf(box);
      const [start, end] = from < to ? [from, to] : [to, from];
      applying = true;
      try {
        for (const other of all.slice(start, end + 1)) {
          if (other === box || other.disabled || other.checked === box.checked) continue;
          other.checked = box.checked;
          other.dispatchEvent(new Event("change", { bubbles: true }));
        }
      } finally {
        applying = false;
      }
    },
    { signal }
  );
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
