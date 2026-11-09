import type { DrawData, View } from "./chart-draw";

/**
 * A RangeSelector's strip: kept on the view (at), moved by its handles, its window, a click on
 * the strip, the keys on a handle and its range buttons, all through setView. Returns what
 * draws it again for a new view.
 */
export function selector(el: HTMLElement, sel: HTMLElement, data: DrawData, at: () => View, setView: (from: number, to: number, report?: boolean) => void, settled: () => void, signal: AbortSignal, plot: HTMLElement | null): () => void {
  const track = sel.querySelector<HTMLElement>(".ib-chart-sel-track")!;
  const za = data.za!;
  const names = data.zn ?? [];
  const least = data.zl ?? 0;
  const handles = [...sel.querySelectorAll<HTMLElement>(".ib-chart-sel-handle")];
  // Tab stops now that they move (with no script they'd do nothing).
  for (const h of handles) h.tabIndex = 0;
  signal.addEventListener("abort", () => {
    for (const h of handles) h.removeAttribute("tabindex");
  });
  const nearest = (share: number, end = false) => {
    if (data.zc) return Math.min(za.length - 1, Math.max(0, Math.round(share * za.length) - (end ? 1 : 0)));
    let best = 0;
    za.forEach((p, i) => {
      if (p !== null && (za[best] === null || Math.abs(p - share) < Math.abs(za[best]! - share))) best = i;
    });
    return best;
  };
  // The strip spans the chart's width (Israel, Nov 8: lined up with the plot, it looked
  // indented and its labels, which start at their periods, looked shifted from the axis's).
  const line = () => flags();
  // The flags: each centered over its handle but kept over the strip; when they'd overlap (a
  // narrow window), one flag over the window says both ends.
  const flags = () => {
    const strip = track.getBoundingClientRect();
    const tags = handles.map((h) => h.querySelector<HTMLElement>(".ib-chart-sel-flag.ib-chart-js"));
    if (!strip.width || tags.some((t) => !t)) return;
    const [a, b] = tags as HTMLElement[];
    const texts = [a.dataset.ibText ?? "", b.dataset.ibText ?? ""];
    a.textContent = texts[0];
    b.textContent = texts[1];
    b.style.display = "";
    const center = (k: number) => {
      const r = handles[k].getBoundingClientRect();
      return r.left + r.width / 2 - strip.left;
    };
    const put = (tag: HTMLElement, k: number, middle: number) => {
      const w = Math.ceil(tag.getBoundingClientRect().width);
      const left = Math.min(Math.max(middle - w / 2, 0), Math.max(0, strip.width - w));
      // From the handle's padding edge, inside its border.
      tag.style.left = `${left - (handles[k].getBoundingClientRect().left + handles[k].clientLeft - strip.left)}px`;
      tag.style.right = "auto";
      return [left, left + w];
    };
    const [x, y] = [put(a, 0, center(0)), put(b, 1, center(1))];
    const [left, right] = x[0] <= y[0] ? [x, y] : [y, x];
    if (left[1] + 4 > right[0]) {
      a.textContent = `${texts[0]} – ${texts[1]}`;
      b.style.display = "none";
      put(a, 0, (center(0) + center(1)) / 2);
    }
  };
  const show = () => {
    const placed = (share: number) => String(data.ar ? 1 - share : share);
    track.style.setProperty("--ib-sel-js", placed(at()[0]));
    track.style.setProperty("--ib-sel-je", placed(at()[1]));
    handles.forEach((h, k) => {
      const i = nearest(at()[k], k === 1);
      h.setAttribute("aria-valuenow", String(i));
      h.setAttribute("aria-valuetext", names[i] ?? "");
      const flag = h.querySelector(".ib-chart-sel-flag.ib-chart-js");
      if (flag) flag.setAttribute("data-ib-text", names[i] ?? "");
    });
    flags();
    for (const b of sel.querySelectorAll<HTMLElement>("[data-ib-range]")) {
      const [a, c] = b.dataset.ibRange!.split(" ").map(Number);
      b.setAttribute("aria-pressed", String(Math.abs(a - at()[0]) < 1e-3 && Math.abs(c - at()[1]) < 1e-3));
    }
    el.setAttribute("data-ib-sel", "");
  };
  show();
  line();
  const lining = new ResizeObserver(line);
  if (plot) lining.observe(plot);
  lining.observe(sel.parentElement!);
  signal.addEventListener("abort", () => {
    lining.disconnect();
    el.removeAttribute("data-ib-sel");
    el.removeAttribute("data-ib-sel-drag");
  });

  // A place on the strip, as a share of the whole axis.
  const shareOf = (x: number) => {
    const r = track.getBoundingClientRect();
    const f = Math.min(1, Math.max(0, (x - r.left) / r.width));
    return data.ar ? 1 - f : f;
  };
  let drag: { kind: "start" | "end" | "pan"; at: number; from: View; id: number } | null = null;
  track.addEventListener(
    "pointerdown",
    (event) => {
      if (event.pointerType === "mouse" && event.button !== 0) return;
      const handle = (event.target as Element).closest(".ib-chart-sel-handle");
      const share = shareOf(event.clientX);
      if (!handle && (share < at()[0] || share > at()[1])) {
        // Outside the window: it moves there, centered, and can be dragged on.
        const span = at()[1] - at()[0];
        setView(share - span / 2, share + span / 2, false);
      }
      const kind = handle ? (handle.classList.contains("ib-chart-sel-start") ? "start" : "end") : "pan";
      drag = { kind, at: share, from: [at()[0], at()[1]], id: event.pointerId };
      // What's dragged sets the cursor for the whole drag (chart.css), wherever the pointer goes.
      el.setAttribute("data-ib-sel-drag", kind === "pan" ? "pan" : "handle");
      try {
        track.setPointerCapture(event.pointerId);
      } catch {
        // A pointer already gone (released as it went down): the drag ends at pointerup anyway.
      }
      if (handle) (handle as HTMLElement).focus();
      event.preventDefault();
    },
    { signal }
  );
  track.addEventListener(
    "pointermove",
    (event) => {
      if (!drag || event.pointerId !== drag.id) return;
      const share = shareOf(event.clientX);
      if (drag.kind === "start") setView(Math.min(share, at()[1] - least), at()[1], false);
      else if (drag.kind === "end") setView(at()[0], Math.max(share, at()[0] + least), false);
      else setView(drag.from[0] + share - drag.at, drag.from[1] + share - drag.at, false);
    },
    { signal }
  );
  const drop = (event: PointerEvent) => {
    if (!drag || event.pointerId !== drag.id) return;
    drag = null;
    el.removeAttribute("data-ib-sel-drag");
    settled();
  };
  track.addEventListener("pointerup", drop, { signal });
  track.addEventListener("pointercancel", drop, { signal });

  // Keys on a handle: by one item, a tenth, or to its furthest; the other end stays.
  const step = 1 / Math.max(1, data.zc ? za.length : za.length - 1);
  handles.forEach((h, k) =>
    h.addEventListener(
      "keydown",
      (event) => {
        const back = data.ar ? -1 : 1;
        const by: Record<string, number> = { ArrowRight: step * back, ArrowUp: step, ArrowLeft: -step * back, ArrowDown: -step, PageUp: 0.1, PageDown: -0.1 };
        let to: number;
        if (event.key in by) to = at()[k] + by[event.key];
        else if (event.key === "Home") to = k === 0 ? 0 : at()[0] + least;
        else if (event.key === "End") to = k === 0 ? at()[1] - least : 1;
        else return;
        event.preventDefault();
        if (k === 0) setView(Math.max(0, Math.min(to, at()[1] - least)), at()[1]);
        else setView(at()[0], Math.min(1, Math.max(to, at()[0] + least)));
      },
      { signal }
    )
  );
  sel.addEventListener(
    "click",
    (event) => {
      const b = (event.target as Element).closest<HTMLElement>("[data-ib-range]");
      if (!b) return;
      const [a, c] = b.dataset.ibRange!.split(" ").map(Number);
      setView(a, c);
    },
    { signal }
  );
  return show;
}

