import { fetchPart } from "../fetch-part";
import type { View } from "./chart-draw";
import { viewIn, viewOut, type SyncAt } from "./chart-sync";

/** What a Chart gives its live part: its view and the call that sets it, once set up. */
export type LiveHost = HTMLElement & { ibView?: [() => View, (from: number, to: number, report?: boolean, share?: boolean) => void] };

// Paused by the reader, by page and chart id: kept across each new drawing of the same chart, and
// not carried to another page's chart that enhanced navigation puts in the same element.
const paused = new Map<string, boolean>();

const keysOf = (el: HTMLElement): SyncAt | null => (el.dataset.ibSyncAt ? JSON.parse(el.dataset.ibSyncAt) : null);
const drawnView = (el: HTMLElement): View => (el.dataset.ibDraw ? JSON.parse(el.dataset.ibDraw).zv : null) ?? [0, 1];
const same = (a: View, b: View) => Math.abs(a[0] - b[0]) < 1e-6 && Math.abs(a[1] - b[1]) < 1e-6;

/**
 * Live data on a static page (Chart Refresh): fetches the page again (or RefreshFrom) every
 * interval and puts the server's new drawing in place, in this same element, so the legend's
 * choices, the item shown and the zoom carry over (by argument). Not while paused, the tab is
 * hidden or the chart is off-screen; and it waits while the pointer or focus is in the chart, so
 * nothing moves under the reader (a tooltip, a drag, the data table, the keys). A failed fetch
 * keeps the drawing and tries again next time. Each new drawing sets the chart up again, and
 * this with it.
 */
export function live(host: LiveHost, signal: AbortSignal): void {
  // The chart: its figure, or a Sparkline's or Gauge's own element (with a hidden <ib-chart> in it).
  const figure = host.parentElement?.closest<HTMLElement>("[id]");
  const every = Number(host.dataset.ibRefresh);
  if (!figure?.id || !(every >= 1000)) return;
  const url = new URL(host.dataset.ibRefreshFrom || location.href, location.href);
  const key = `${location.pathname} ${figure.id}`;
  if (!paused.has(key)) paused.set(key, host.hasAttribute("data-ib-paused"));

  // The Pause button: pressed, nothing is fetched; released, the chart catches up at once.
  const pause = host.querySelector<HTMLElement>(".ib-chart-pause");
  pause?.setAttribute("aria-pressed", String(!!paused.get(key)));
  pause?.addEventListener(
    "click",
    () => {
      const now = !paused.get(key);
      paused.set(key, now);
      pause.setAttribute("aria-pressed", String(now));
      if (!now) tick(0);
    },
    { signal }
  );

  let onScreen = true;
  const seen = new IntersectionObserver(([e]) => (onScreen = e.isIntersecting));
  seen.observe(figure);
  signal.addEventListener("abort", () => seen.disconnect());

  let timer = 0;
  const tick = (wait: number) => {
    clearTimeout(timer);
    timer = window.setTimeout(run, wait);
  };
  signal.addEventListener("abort", () => clearTimeout(timer));

  // The reader is in the chart: pointing, dragging or on its keys.
  const busy = () => figure.matches(":hover") || figure.contains(document.activeElement) || host.hasAttribute("data-ib-brushing");

  const run = async () => {
    if (signal.aborted) return;
    if (paused.get(key) || document.hidden || !onScreen) return tick(every);
    if (busy()) return tick(1000);
    let part: Element | null = null;
    try {
      part = await fetchPart(url, `#${CSS.escape(figure.id)}`, signal, { cache: "no-store" });
    } catch {
      // Offline or the server down: the drawing stays.
    }
    if (signal.aborted) return;
    const next = part?.querySelector<HTMLElement>("ib-chart");
    if (!part || !next || paused.get(key)) return tick(every);
    if (busy()) return tick(1000);
    swap(host, figure, part, next);
  };
  tick(every);
}

/** Puts the fetched drawing in place: the chart in its own element, the rest beside it as it comes. */
function swap(host: LiveHost, figure: HTMLElement, part: Element, next: HTMLElement): void {
  // What carries over: the reader's view (by argument) where it isn't the one the server drew.
  const before = keysOf(host);
  const view = host.ibView?.[0]();
  const was = drawnView(host);

  // The chart's own attributes (a Sparkline's name says its values), then the caption and data
  // table beside the chart, child for child; when the two don't line up
  // (the chart elsewhere among them, or a different count), only the chart is drawn again.
  for (const a of [...part.attributes]) if (a.name !== "id") figure.setAttribute(a.name, a.value);
  const old = [...figure.children];
  const fresh = [...part.children];
  const at = fresh.findIndex((c) => c === next || c.contains(next));
  if (fresh.length === old.length && old[at] === host) {
    fresh.forEach((child, i) => {
      if (i === at || child.localName === "link" || old[i].localName !== child.localName) return;
      if (old[i] instanceof HTMLDetailsElement && child instanceof HTMLDetailsElement) child.open = old[i].open;
      old[i].replaceWith(child);
    });
  }
  // The chart itself in place: its attributes and contents, so it sets up again keeping its state.
  for (const a of [...host.attributes]) if (!next.hasAttribute(a.name)) host.removeAttribute(a.name);
  host.innerHTML = next.innerHTML;
  for (const a of [...next.attributes]) host.setAttribute(a.name, a.value);

  // Once set up again (the attributes' observer runs first): the reader's view back.
  queueMicrotask(() =>
    requestAnimationFrame(() => {
      const after = keysOf(host);
      const set = host.ibView?.[1];
      if (!view || !set || !before || !after || same(view, was)) return;
      const v = restore(view, before, after);
      if (v) set(v[0], v[1], false);
    })
  );
}

/** The reader's view on the new data: all of it, the latest (following it), or the same arguments. */
function restore(view: View, before: SyncAt, after: SyncAt): View | undefined {
  if (same(view, [0, 1])) return [0, 1];
  const n0 = before.k.length;
  const n1 = after.k.length;
  if (after.c) {
    // Bands: as many items, ending at the latest or at the same last item as before.
    const count = Math.max(1, Math.round((view[1] - view[0]) * n0));
    const last = view[1] >= 1 - 1e-6 ? n1 - 1 : after.k.indexOf(before.k[Math.min(n0 - 1, Math.ceil(view[1] * n0 - 1e-6) - 1)]);
    return last < 0 ? undefined : [Math.max(0, last + 1 - count) / n1, (last + 1) / n1];
  }
  // A scale: at the latest, the same share of the axis ending there (a log axis grows by decades);
  // otherwise the same arguments.
  if (view[1] >= 1 - 1e-6) return [Math.max(0, 1 - (view[1] - view[0]) * spanOf(before) / spanOf(after)), 1];
  const m = viewOut(before, view);
  return m ? viewIn(after, m) : undefined;
}

// A scale's length in its own units (a log axis's in decades).
const spanOf = (s: SyncAt) => (s.d ? s.d[1] - s.d[0] || 1 : 1);
