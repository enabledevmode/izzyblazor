import { fetchPart } from "../fetch-part";
import type { View } from "./chart-draw";
import { scaleValue, viewIn, viewOut, type SyncAt } from "./chart-sync";

/** What a Chart gives its live part: its view and the call that sets it, once set up. */
export type LiveHost = HTMLElement & { ibView?: [() => View, (from: number, to: number, report?: boolean, share?: boolean) => void] };

// Paused by the reader, kept across each new drawing of the same chart.
const paused = new WeakMap<HTMLElement, boolean>();

const keysOf = (el: HTMLElement): SyncAt | null => (el.dataset.ibSyncAt ? JSON.parse(el.dataset.ibSyncAt) : null);

/**
 * Live data on a static page (Chart Refresh): fetches the page again (or RefreshFrom) every
 * interval and puts the server's new drawing in place, in this same element, so the legend's
 * choices, the item shown and the zoom carry over (by argument). Not while paused, the tab is
 * hidden or the chart is off-screen; a failed fetch keeps the drawing and tries again next time.
 * Each new drawing sets the chart up again, and this with it.
 */
export function live(host: LiveHost, signal: AbortSignal): void {
  const figure = host.closest("figure");
  const every = Number(host.dataset.ibRefresh);
  if (!figure?.id || !(every >= 1000)) return;
  const url = new URL(host.dataset.ibRefreshFrom || location.href, location.href);

  // The Pause button: pressed, nothing is fetched; released, the chart catches up at once.
  const pause = host.querySelector<HTMLElement>(".ib-chart-pause");
  pause?.setAttribute("aria-pressed", String(!!paused.get(host)));
  pause?.addEventListener(
    "click",
    () => {
      const now = !paused.get(host);
      paused.set(host, now);
      pause.setAttribute("aria-pressed", String(now));
      if (!now) tick(0);
    },
    { signal }
  );

  let onScreen = true;
  const seen = new IntersectionObserver(([e]) => (onScreen = e.isIntersecting));
  seen.observe(host);
  signal.addEventListener("abort", () => seen.disconnect());

  let timer = 0;
  const tick = (wait: number) => {
    clearTimeout(timer);
    timer = window.setTimeout(run, wait);
  };
  signal.addEventListener("abort", () => clearTimeout(timer));

  const run = async () => {
    if (signal.aborted) return;
    if (paused.get(host) || document.hidden || !onScreen) return tick(every);
    let part: Element | null = null;
    try {
      part = await fetchPart(url, `#${CSS.escape(figure.id)}`, signal, { cache: "no-store" });
    } catch {
      // Offline or the server down: the drawing stays.
    }
    if (signal.aborted) return;
    const next = part?.querySelector<HTMLElement>("ib-chart");
    if (!part || !next || paused.get(host)) return tick(every);
    swap(host, figure, part, next);
  };
  tick(every);
}

// The child at this path of indexes under root, or the path to a node.
const pathTo = (root: Element, node: Element | null): number[] | null => {
  const path: number[] = [];
  for (let n = node; n && n !== root; n = n.parentElement) {
    if (!n.parentElement) return null;
    path.unshift([...n.parentElement.children].indexOf(n));
  }
  return node && root.contains(node) ? path : null;
};
const atPath = (root: Element, path: number[]) => path.reduce<Element | undefined>((n, i) => n?.children[i], root);

/** Puts the fetched drawing in place: the chart in its own element, the rest beside it as it comes. */
function swap(host: LiveHost, figure: HTMLElement, part: Element, next: HTMLElement): void {
  // What carries over: focus inside the chart, the view (by argument), the data table left open.
  const focused = pathTo(host, document.activeElement);
  const before = keysOf(host);
  const view = host.ibView?.[0]();
  const n0 = before?.k.length ?? 0;

  for (const [i, child] of [...part.children].entries()) {
    const old = figure.children[i];
    if (child.localName === "link") continue;
    if (child === next || child.contains(next)) {
      if (old !== host) old?.replaceWith(child);
      continue;
    }
    if (old && old !== host && old.localName === child.localName) {
      if (old instanceof HTMLDetailsElement && child instanceof HTMLDetailsElement) child.open = old.open;
      old.replaceWith(child);
    }
  }
  // The chart itself in place: its attributes and contents, so it sets up again keeping its state.
  for (const a of [...host.attributes]) if (!next.hasAttribute(a.name)) host.removeAttribute(a.name);
  host.innerHTML = next.innerHTML;
  for (const a of [...next.attributes]) host.setAttribute(a.name, a.value);

  // Once set up again (the attributes' observer runs first): the view and focus back.
  queueMicrotask(() =>
    requestAnimationFrame(() => {
      const after = keysOf(host);
      const set = host.ibView?.[1];
      if (view && set && before && after && (view[0] > 1e-6 || view[1] < 1 - 1e-6)) {
        let v: View | undefined;
        if (view[1] >= 1 - 1e-6) {
          // At the latest item: follow new data, keeping the span (bands: as many items).
          if (after.c) {
            const k = Math.max(1, Math.round((view[1] - view[0]) * n0));
            v = [Math.max(0, 1 - k / after.k.length), 1];
          } else if (before.d && after.d) {
            const span = scaleValue(before, 1) - scaleValue(before, view[0]);
            const end = scaleValue(after, 1);
            v = viewIn(after, { t: false, a: end - span, b: end });
          }
        } else {
          const m = viewOut(before, view);
          if (m) v = viewIn(after, m);
        }
        if (v) set(v[0], v[1], false);
      }
      if (focused) (atPath(host, focused) as HTMLElement | undefined)?.focus({ preventScroll: true });
    })
  );
}
