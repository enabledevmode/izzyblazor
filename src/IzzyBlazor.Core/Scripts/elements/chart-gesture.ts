import type { DrawData, View } from "./chart-draw";

/** What a zooming chart gives its gestures: its parts, its view and the calls that change it. */
export type GestureHost = {
  el: HTMLElement;
  plot: HTMLElement;
  data: DrawData;
  horizontal: boolean;
  signal: AbortSignal;
  view: () => View;
  isZoomed: () => boolean;
  shareAt: (f: number) => number;
  setView: (from: number, to: number, report?: boolean) => void;
  zoomTo: (from: number, to: number) => void;
  zoomAround: (at: number, factor: number, report?: boolean) => void;
  settled: (wait?: number) => void;
  drop: () => void;
  gesture: (on: boolean) => void;
};

/**
 * Zoom's gestures (ChartArgumentAxis Zoom): a drag across the plot draws a box to zoom into,
 * Ctrl (⌘) and the wheel or a trackpad's pinch zoom at the pointer, Shift and the wheel pan; on
 * touch two fingers pinch and pan, and one still scrolls the page. Zoom out and Zoom in, by the
 * title, do the same around the middle for a pointer that doesn't drag.
 */
export function gestures(c: GestureHost): void {
  const { el, plot, data, horizontal, signal } = c;
  const brush =plot.querySelector<HTMLElement>(".ib-chart-brush");
  const length = () => (horizontal ? plot.clientHeight : plot.clientWidth);
  const fracOf = (x: number, y: number) => {
    const r = plot.getBoundingClientRect();
    return horizontal ? (y - r.top) / r.height : (x - r.left) / r.width;
  };
  // Zoom out and in: out waits until there's something to zoom out of, in stops at the closest.
  const [out, inn] = el.querySelectorAll<HTMLButtonElement>("[data-ib-zoom]");
  const can = () => {
    if (!out) return;
    out.disabled = !c.isZoomed();
    inn.disabled = c.view()[1] - c.view()[0] <= (data.zl ?? 0) + 1e-6;
  };
  const watch = new MutationObserver(can);
  watch.observe(el, { attributeFilter: ["data-ib-zoomed"] });
  signal.addEventListener("abort", () => watch.disconnect());
  el.addEventListener(
    "click",
    (event) => {
      const b = (event.target as Element).closest<HTMLButtonElement>("[data-ib-zoom]");
      if (!b) return;
      c.drop();
      c.zoomAround((c.view()[0] + c.view()[1]) / 2, Number(b.dataset.ibZoom));
      can();
      // A button that turned off hands focus to the other.
      if (b.disabled) (b === out ? inn : out).focus();
    },
    { signal }
  );
  can();
  const touches = new Map<number, [number, number]>();
  let pinch: { share: number; span: number; dist: number } | null = null;
  let box: { from: number; x: number; y: number; id: number } | null = null;
  const two = () => [...touches.values()];
  const pinchState = () => {
    const [a, b] = two();
    return { mid: fracOf((a[0] + b[0]) / 2, (a[1] + b[1]) / 2), dist: Math.hypot(a[0] - b[0], a[1] - b[1]) };
  };
  plot.addEventListener(
    "pointerdown",
    (event) => {
      if ((event.target as Element).closest(".ib-chart-tools")) return;
      if (event.pointerType === "touch") {
        touches.set(event.pointerId, [event.clientX, event.clientY]);
        if (touches.size === 2) {
          const { mid, dist } = pinchState();
          pinch = { share: c.shareAt(mid), span: c.view()[1] - c.view()[0], dist };
          c.gesture(true);
          c.drop();
        }
        return;
      }
      if (event.button !== 0) return;
      box = { from: fracOf(event.clientX, event.clientY), x: event.clientX, y: event.clientY, id: event.pointerId };
    },
    { signal }
  );
  plot.addEventListener(
    "pointermove",
    (event) => {
      if (touches.has(event.pointerId)) {
        touches.set(event.pointerId, [event.clientX, event.clientY]);
        if (pinch && touches.size === 2) {
          const { mid, dist } = pinchState();
          const span = Math.min(1, Math.max(data.zl ?? 0, (pinch.span * pinch.dist) / Math.max(1, dist)));
          const f = data.ar ? 1 - mid : mid;
          c.setView(pinch.share - f * span, pinch.share - f * span + span, false);
        }
        return;
      }
      if (!box || event.pointerId !== box.id) return;
      // Released outside the plot before it was captured: no box.
      if ((event.buttons & 1) === 0) {
        box = null;
        return;
      }
      if (!el.hasAttribute("data-ib-brushing")) {
        if (Math.hypot(event.clientX - box.x, event.clientY - box.y) < 4) return;
        plot.setPointerCapture(event.pointerId);
        el.setAttribute("data-ib-brushing", "");
        c.gesture(true);
        c.drop();
      }
      const to = Math.min(1, Math.max(0, fracOf(event.clientX, event.clientY)));
      const [a, b] = [Math.min(box.from, to), Math.max(box.from, to)];
      if (brush) brush.style.cssText = horizontal ? `top:${a * 100}%;height:${(b - a) * 100}%;left:0;width:100%` : `left:${a * 100}%;width:${(b - a) * 100}%;top:0;height:100%`;
    },
    { signal }
  );
  const release = (event: PointerEvent) => {
    if (touches.delete(event.pointerId) && pinch) {
      pinch = null;
      c.gesture(false);
      c.settled();
      return;
    }
    if (!box || event.pointerId !== box.id) return;
    const from = box.from;
    box = null;
    if (!el.hasAttribute("data-ib-brushing")) return;
    el.removeAttribute("data-ib-brushing");
    c.gesture(false);
    const to = Math.min(1, Math.max(0, fracOf(event.clientX, event.clientY)));
    if (Math.abs(to - from) * length() < 6) return;
    const [a, b] = [c.shareAt(from), c.shareAt(to)].sort((x, y) => x - y);
    c.zoomTo(a, b);
  };
  plot.addEventListener("pointerup", release, { signal });
  plot.addEventListener("pointercancel", release, { signal });
  plot.addEventListener(
    "wheel",
    (event) => {
      if (event.ctrlKey || event.metaKey) {
        event.preventDefault();
        c.drop();
        c.zoomAround(c.shareAt(fracOf(event.clientX, event.clientY)), Math.exp(event.deltaY * 0.002), false);
        c.settled(300);
      } else if (c.isZoomed() && (event.shiftKey || Math.abs(event.deltaX) > Math.abs(event.deltaY))) {
        event.preventDefault();
        const by = ((event.shiftKey ? event.deltaY || event.deltaX : event.deltaX) / length()) * (c.view()[1] - c.view()[0]) * (data.ar ? -1 : 1);
        c.setView(c.view()[0] + by, c.view()[1] + by, false);
        c.settled(300);
      }
    },
    { signal, passive: false }
  );
}
