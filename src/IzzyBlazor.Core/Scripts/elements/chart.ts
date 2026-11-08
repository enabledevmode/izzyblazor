import { IzzyElement, defineElement } from "../izzy-element";
import type { DrawData, PieDrawData, View, draw as Draw, drawPie as DrawPie, zoomArgs as ZoomArgs } from "./chart-draw";
import type { heat as Heat } from "./chart-heat";
import type { radar as Radar } from "./chart-radar";
import type { sankey as Sankey } from "./chart-sankey";
import type { treemap as Treemap } from "./chart-treemap";

// The redraw (chart-draw.ts), loaded the first time a legend hides a series (or a pie's slices).
type DrawModule = { draw: typeof Draw; drawPie: typeof DrawPie; zoomArgs: typeof ZoomArgs };
let drawing: Promise<DrawModule> | undefined;
/** A view kept within the whole axis and at least `least` wide; too narrow or wide, it keeps its middle. */
const clampView = (from: number, to: number, least: number): View => {
  const span = Math.min(1, Math.max(least, to - from));
  const start = Math.min(Math.max(0, to - from === span ? from : (from + to) / 2 - span / 2), 1 - span);
  return [start, start + span];
};

const loadModule = () => (drawing ??= import("./parts/chart-draw.js" as string) as Promise<DrawModule>);

/**
 * <ib-chart> adds to a Chart the server drew: a tooltip for the item under the pointer, the
 * keyboard (the plot is one Tab stop; the arrow keys move between items and the tooltip's text
 * goes to a live region), legend items that hide and show their series, and labels fitted to the
 * width they really have.
 *
 * The server draws each item's tooltip (from the chart's ChartTooltip, if any) in a hidden
 * list; this copies the one in use into the tooltip. Nothing Blazor renders is changed but
 * attributes Blazor doesn't set (data-ib-hidden, data-ib-fit, aria-pressed, tabindex), and the
 * tooltip's and live region's contents, which Blazor leaves empty.
 */
class Chart extends IzzyElement {
  // The series the legend hides, kept when the server sends new data for the same chart.
  #hidden: Set<number> | null = null;
  // The series the server drew hidden, as last read: when they change, the app set Hidden.
  #off = "";
  #keep = false;

  protected setup(signal: AbortSignal): void {
    // An interactive page can draw the chart again with new data in this same element: set up
    // again from what the server sent, keeping what the legend hides. (Enhanced navigation sets
    // up again too, for what may be another chart: that starts over.)
    const changed = new MutationObserver(() => {
      this.#keep = true;
      this.connectedCallback();
    });
    changed.observe(this, { attributeFilter: ["data-ib-draw", "data-ib-pos", "data-ib-count", "data-ib-pie", "data-ib-sankey", "data-ib-treemap"] });
    signal.addEventListener("abort", () => changed.disconnect());

    if (this.hasAttribute("data-ib-pie")) {
      this.pie(signal);
      return;
    }

    // A HeatmapChart: its own part (chart-heat.ts), loaded when one is on the page.
    if (this.hasAttribute("data-ib-heat")) {
      (import("./parts/chart-heat.js" as string) as Promise<{ heat: typeof Heat }>).then((m) => {
        if (!signal.aborted) m.heat(this, signal, (frame) => this.keyboard(frame, signal), (axis) => this.fit(axis), () => this.connectedCallback());
      });
      return;
    }

    // A RadarChart: its own part (chart-radar.ts), loaded when one is on the page.
    if (this.hasAttribute("data-ib-radar")) {
      (import("./parts/chart-radar.js" as string) as Promise<{ radar: typeof Radar }>).then((m) => {
        if (!signal.aborted) m.radar(this, signal, (frame) => this.keyboard(frame, signal), () => this.connectedCallback());
      });
      return;
    }

    // A SankeyChart: its own part (chart-sankey.ts), loaded when one is on the page.
    if (this.hasAttribute("data-ib-sankey")) {
      (import("./parts/chart-sankey.js" as string) as Promise<{ sankey: typeof Sankey }>).then((m) => {
        if (!signal.aborted) m.sankey(this, signal, (frame) => this.keyboard(frame, signal), () => this.connectedCallback());
      });
      return;
    }

    // A TreemapChart: its own part (chart-treemap.ts), loaded when one is on the page.
    if (this.hasAttribute("data-ib-treemap")) {
      (import("./parts/chart-treemap.js" as string) as Promise<{ treemap: typeof Treemap }>).then((m) => {
        if (!signal.aborted) m.treemap(this, signal, (frame) => this.keyboard(frame, signal), () => this.connectedCallback());
      });
      return;
    }

    const frame = this.querySelector<HTMLElement>(".ib-chart-frame");
    const plot = this.querySelector<HTMLElement>(".ib-chart-plot");
    const bottom = this.querySelector<HTMLElement>(".ib-chart-bottom");
    // A RangeSelector on its own: the strip only, its view kept here and, on an interactive
    // page, reported to .NET as it settles.
    const lone = this.querySelector<HTMLElement>(".ib-chart-sel");
    if (!plot && lone && this.dataset.ibDraw) {
      const d: DrawData = JSON.parse(this.dataset.ibDraw);
      let v: View = d.zv ?? [0, 1];
      let timer = 0;
      const report = () => {
        clearTimeout(timer);
        timer = window.setTimeout(() => {
          if (signal.aborted || !this.hasAttribute("data-ib-interactive")) return;
          const all = v[0] <= 1e-6 && v[1] >= 1 - 1e-6;
          this.emit("iblayout", { layout: all ? "" : `${Math.round(v[0] * 1e6) / 1e6} ${Math.round(v[1] * 1e6) / 1e6}` });
        }, 0);
      };
      let show = () => {};
      const set = (from: number, to: number, settle = true) => {
        v = clampView(from, to, d.zl ?? 0);
        show();
        if (settle) report();
      };
      show = this.selector(lone, d, () => v, set, report, signal, null);
      return;
    }

    if (!frame || !plot) return;

    // Legend: each item hides and shows its series. The server says which it drew hidden; the
    // legend's own choice carries over a new draw of the same chart.
    const data: DrawData | null = this.dataset.ibDraw ? JSON.parse(this.dataset.ibDraw) : null;
    const toggles = [...this.querySelectorAll<HTMLElement>(".ib-chart-toggle")];
    const drawn = new Set(data?.off ?? toggles.filter((t) => t.hasAttribute("data-ib-hidden")).map((t) => Number(t.dataset.ibSeries)));
    const off = [...drawn].join();
    if (!this.#keep || !this.#hidden || this.#off !== off) this.#hidden = new Set(drawn);
    this.#keep = false;
    this.#off = off;
    const hidden = this.#hidden;
    const apply = (series: number) => {
      const isOff = hidden.has(series);
      for (const el of this.querySelectorAll(`[data-ib-series="${series}"]:not(.ib-chart-tooltip-row)`)) el.toggleAttribute("data-ib-hidden", isOff);
      this.querySelector(`.ib-chart-toggle[data-ib-series="${series}"]`)?.setAttribute("aria-pressed", String(!isOff));
    };
    for (const toggle of toggles) {
      toggle.setAttribute("role", "button");
      toggle.tabIndex = 0;
      apply(Number(toggle.dataset.ibSeries));
    }

    // With a series hidden that the server drew shown (or the other way round), the chart is
    // drawn again here without it: its own plot and value labels stand in for the server's.
    const jsSvg = plot.querySelector<SVGSVGElement>("svg.ib-chart-js");
    const jsLabels = this.querySelector<HTMLElement>(".ib-chart-values.ib-chart-js");
    // Value labels, placed again at the plot's size: measured in their own font.
    const jsDl = plot.querySelector<HTMLElement>(".ib-chart-dl.ib-chart-js");
    const jsEnds = plot.querySelector<HTMLElement>(".ib-chart-ends.ib-chart-js");
    const horizontal = this.hasAttribute("data-ib-horizontal");

    // Zoom (ChartArgumentAxis Zoom): the view the server drew, and the one shown, as shares of the
    // whole argument axis. The script draws any other view itself (chart-draw.ts).
    const zoomOn = !!data?.za && this.hasAttribute("data-ib-zoom");
    const drawnView: View = data?.zv ?? [0, 1];
    let view: View = [drawnView[0], drawnView[1]];
    const zargs = this.querySelector<HTMLElement>(".ib-chart-zargs");
    const sameView = () => Math.abs(view[0] - drawnView[0]) < 1e-6 && Math.abs(view[1] - drawnView[1]) < 1e-6;
    const isZoomed = () => view[0] > 1e-6 || view[1] < 1 - 1e-6;
    // A share of the whole axis at a place in the plot (0 to 1 along it), and the other way.
    const shareAt = (f: number) => view[0] + (data?.ar ? 1 - f : f) * (view[1] - view[0]);
    const placeOf = (share: number) => ((q) => (data?.ar ? 1 - q : q))((share - view[0]) / (view[1] - view[0] || 1));
    this.toggleAttribute("data-ib-zoomed", zoomOn && isZoomed());
    signal.addEventListener("abort", () => {
      for (const a of ["data-ib-zoomed", "data-ib-zargs", "data-ib-brushing"]) this.removeAttribute(a);
    });
    // The argument marks' places in the view the script draws (with none, the server's).
    const placeMarks = (on: boolean) => {
      for (const mark of plot.querySelectorAll<HTMLElement>("[data-ib-za]")) {
        for (const v of ["--jn", ...(mark.dataset.ibV ? [] : ["--jp", "--jq"])]) mark.style.removeProperty(v);
        mark.removeAttribute("data-ib-zout");
        if (!on) continue;
        const [zp, zq] = mark.dataset.ibZa!.split(" ").map(Number);
        const out = (p: number) => p < -1e-9 || p > 1 + 1e-9;
        if (mark.classList.contains("ib-chart-note")) {
          const at = placeOf(zp);
          mark.toggleAttribute("data-ib-zout", out(at));
          mark.style.setProperty("--jn", String(horizontal ? 1 - at : at));
          continue;
        }
        let [p, q] = [placeOf(zp), placeOf(zq)];
        if (p > q) [p, q] = [q, p];
        // Out of view, a mark is hidden; a band across an end is cut there.
        const band = mark.classList.contains("ib-chart-band");
        mark.toggleAttribute("data-ib-zout", band ? q <= 0 || p >= 1 : out(p));
        if (band) {
          [p, q] = [Math.max(0, p), Math.min(1, q)];
          if (horizontal) [p, q] = [1 - q, 1 - p];
          mark.style.setProperty("--jp", String(p));
          mark.style.setProperty("--jq", String(q));
        } else mark.style.setProperty("--jp", String(horizontal ? 1 - p : p));
      }
    };
    // The tooltip's places for the items, set once the tooltip is (below).
    let placed = () => {};
    let measure: ((text: string) => number) | undefined;
    if (jsDl) {
      const css = getComputedStyle(jsDl);
      const ctx = document.createElement("canvas").getContext("2d");
      if (ctx) {
        ctx.font = `500 ${css.fontSize} ${css.fontFamily}`;
        measure = (text) => ctx.measureText(text).width;
      }
    }
    const redraw = async () => {
      const same = hidden.size === drawn.size && [...hidden].every((s) => drawn.has(s));
      if ((same && (!zoomOn || sameView())) || !data || !jsSvg || !jsLabels) {
        this.removeAttribute("data-ib-redrawn");
        this.removeAttribute("data-ib-zargs");
        for (const mark of plot.querySelectorAll<HTMLElement>("[data-ib-v]")) {
          mark.style.removeProperty("--jp");
          mark.style.removeProperty("--jq");
        }
        placeMarks(false);
        if (zargs) {
          zargs.innerHTML = "";
          zargs.removeAttribute("style");
        }
        placed();
        if (bottom) this.fit(bottom);
        return;
      }
      const module = await loadModule();
      if (signal.aborted) return;
      const out = module.draw(data, hidden, { w: plot.clientWidth, h: plot.clientHeight }, measure, zoomOn ? view : undefined);
      // Another view than the server's: argument labels, grid lines and marks for it too.
      let grid = "";
      const own = zoomOn && !sameView();
      if (own) {
        const args = module.zoomArgs(data, view, horizontal ? plot.clientHeight : plot.clientWidth);
        grid = args.grid;
        if (zargs) {
          zargs.innerHTML = args.labels;
          zargs.setAttribute("style", args.room);
        }
      }
      this.toggleAttribute("data-ib-zargs", own);
      placeMarks(own);
      jsSvg.innerHTML = grid + out.svg;
      jsLabels.innerHTML = out.labels;
      if (jsDl) jsDl.innerHTML = out.dl;
      if (jsEnds) jsEnds.innerHTML = out.ends;
      // Reference marks on the value axis move to the new scale.
      for (const mark of plot.querySelectorAll<HTMLElement>("[data-ib-v]")) {
        mark.style.setProperty("--jp", String(out.pos(Number(mark.dataset.ibV))));
        if (mark.dataset.ibVto) mark.style.setProperty("--jq", String(out.pos(Number(mark.dataset.ibVto))));
      }
      this.setAttribute("data-ib-redrawn", "");
      placed();
      if (bottom) this.fit(bottom);
      refresh();
    };
    signal.addEventListener("abort", () => this.removeAttribute("data-ib-redrawn"));

    // Labels: show as many as fit, measured. Across, the value axis is drawn again for its width.
    let frameId = 0;
    const refit = () => {
      cancelAnimationFrame(frameId);
      frameId = requestAnimationFrame(() => {
        if (this.hasAttribute("data-ib-redrawn") && (jsDl || horizontal || zoomOn)) redraw();
        if (bottom) this.fit(bottom);
      });
    };
    const observer = new ResizeObserver(refit);
    observer.observe(plot);
    signal.addEventListener("abort", () => {
      observer.disconnect();
      cancelAnimationFrame(frameId);
    });
    refit();

    // Draws the open tooltip again once it's set up below (a chart can have no tooltips).
    let refresh = () => {};

    // The RangeSelector, if any, kept on the view (set up below).
    let showSelector = () => {};

    // Sets the view, kept to the whole axis and the fewest items' span, and draws it; once it
    // settles, says the range and (interactive) reports it to .NET for ViewChanged.
    const zoomLive = this.querySelector<HTMLElement>("[data-ib-live]");
    let settle = 0;
    const settled = (wait = 0) => {
      clearTimeout(settle);
      settle = window.setTimeout(() => {
        if (signal.aborted) return;
        const inView = (data!.za ?? []).map((p, i) => [p, i] as const).filter(([p]) => p !== null && p >= view[0] - 1e-9 && p <= view[1] + 1e-9).sort((a, b) => a[0]! - b[0]!);
        if (zoomLive && data!.zs && data!.zn && inView.length > 0) {
          zoomLive.textContent = data!.zs.replace("{0}", data!.zn[inView[0][1]]).replace("{1}", data!.zn[inView[inView.length - 1][1]]);
        }
        if (this.hasAttribute("data-ib-interactive")) {
          this.emit("iblayout", { layout: isZoomed() ? `${Math.round(view[0] * 1e6) / 1e6} ${Math.round(view[1] * 1e6) / 1e6}` : "" });
        }
      }, wait);
    };
    const setView = (from: number, to: number, report = true) => {
      view = clampView(from, to, data?.zl ?? 0);
      this.toggleAttribute("data-ib-zoomed", isZoomed());
      redraw();
      showSelector();
      if (report) settled();
    };
    const zoomTo = (from: number, to: number) => setView(from, to);
    const zoomAround = (at: number, factor: number, report = true) => {
      const span = Math.min(1, Math.max(data?.zl ?? 0, (view[1] - view[0]) * factor));
      const k = (at - view[0]) / (view[1] - view[0] || 1);
      setView(at - k * span, at - k * span + span, report);
    };

    // A RangeSelector under the plot.
    const sel = this.querySelector<HTMLElement>(".ib-chart-sel");
    if (zoomOn && sel && data) showSelector = this.selector(sel, data, () => view, setView, settled, signal, plot);
    const setHidden = (series: number, isOff: boolean) => {
      if (isOff) hidden.add(series);
      else hidden.delete(series);
      apply(series);
      redraw();
      refit();
      refresh();
    };

    // A legend choice kept over new data from the server draws again at once.
    redraw();

    this.addEventListener(
      "click",
      (event) => {
        const toggle = (event.target as Element).closest<HTMLElement>(".ib-chart-toggle");
        if (toggle) {
          const series = Number(toggle.dataset.ibSeries);
          setHidden(series, !hidden.has(series));
        }
      },
      { signal }
    );
    this.addEventListener(
      "keydown",
      (event) => {
        const toggle = (event.target as Element).closest<HTMLElement>(".ib-chart-toggle");
        if (toggle && (event.key === "Enter" || event.key === " ")) {
          event.preventDefault();
          toggle.click();
        }
      },
      { signal }
    );

    // Tooltips.
    const tooltip = plot.querySelector<HTMLElement>(".ib-chart-tooltip");
    const hover = plot.querySelector<HTMLElement>(".ib-chart-hover");
    const tips = this.querySelectorAll<HTMLElement>(".ib-chart-tips > [data-ib-tip]");
    const live = this.querySelector<HTMLElement>("[data-ib-live]");
    if (!tooltip || !hover || tips.length === 0) return;

    const count = Number(this.dataset.ibCount) || tips.length;
    const scale = this.hasAttribute("data-ib-scale");
    const reverse = this.hasAttribute("data-ib-reverse");
    // Each item's place along the plot, and a band's width: the server's, or zoomed the view's.
    let positions: number[] = [];
    let bandSize = 0;
    placed = () => {
      if (zoomOn) {
        positions = data!.za!.map((p) => (p === null ? NaN : placeOf(p)));
        bandSize = scale ? 0 : 1 / count / (view[1] - view[0] || 1);
      } else {
        positions = scale
          ? (this.dataset.ibPos ?? "").split(",").map((p) => (p === "" ? NaN : Number(p)))
          : Array.from({ length: count }, (_, i) => (reverse ? count - i - 0.5 : i + 0.5) / count);
        bandSize = scale ? 0 : 1 / count;
      }
    };
    placed();
    const shown = (i: number) => positions[i] >= -1e-9 && positions[i] <= 1 + 1e-9;
    // A scatter chart's tooltip is for the point nearest the pointer, not the band under it.
    const pointsMode = this.hasAttribute("data-ib-points");
    // The points shown: the server's, or the script's once it has drawn the chart again.
    const marksNow = () =>
      [...plot.querySelectorAll<SVGElement>(this.hasAttribute("data-ib-redrawn") ? "svg.ib-chart-js :is(use, circle)[data-ib-i]" : "svg:not(.ib-chart-js) :is(use, circle)[data-ib-i]")];
    const visible = (m: Element) => !m.closest("[data-ib-hidden]");
    const markFor = (index: number) => marksNow().find((m) => m.dataset.ibI === String(index) && visible(m));
    // A marker's x and y, or a bubble's cx and cy, in percent of the plot.
    const at = (m: SVGElement) => [
      (parseFloat(m.getAttribute("x") ?? m.getAttribute("cx") ?? "0") / 100) * plot.clientWidth,
      (parseFloat(m.getAttribute("y") ?? m.getAttribute("cy") ?? "0") / 100) * plot.clientHeight,
    ];
    let active: SVGElement | undefined;

    // The keyboard's order: the items, or on a scatter chart its points from left to right;
    // reversed, the items from the last, so Right still moves right.
    const items = () => {
      const all = Array.from({ length: count }, (_, i) => i).filter((i) => !Number.isNaN(positions[i]));
      if (pointsMode || zoomOn) all.sort((a, b) => positions[a] - positions[b]);
      else if (reverse) all.reverse();
      return all;
    };
    // A point of a series the legend hid isn't a stop, nor (zoomed) one out of view.
    const keys = () => (pointsMode ? items().filter((i) => shown(i) && markFor(i)) : items());

    let current = -1;
    let lastY: number | null = null;

    const along = (event: PointerEvent) => {
      const r = plot.getBoundingClientRect();
      return horizontal ? (event.clientY - r.top) / r.height : (event.clientX - r.left) / r.width;
    };

    const nearest = (f: number) => {
      if (!scale && !zoomOn) {
        const i = Math.min(count - 1, Math.max(0, Math.floor(f * count)));
        return reverse ? count - 1 - i : i;
      }
      let best = -1;
      for (let i = 0; i < positions.length; i++) {
        if (!Number.isNaN(positions[i]) && shown(i) && (best < 0 || Math.abs(positions[i] - f) < Math.abs(positions[best] - f))) best = i;
      }
      return best;
    };

    const show = (index: number, pointerY: number | null) => {
      // Not a number: no stop left (the legend hid every series).
      if (!(index >= 0) || index >= tips.length) return;
      // Zoomed out of view, it has no tooltip.
      if (zoomOn && !shown(index)) {
        hide();
        return;
      }
      current = index;
      lastY = pointerY;
      tooltip.innerHTML = tips[index].innerHTML;
      for (const row of tooltip.querySelectorAll<HTMLElement>("[data-ib-series]")) {
        if (hidden.has(Number(row.dataset.ibSeries))) row.remove();
      }

      active?.removeAttribute("data-ib-active");
      active = pointsMode ? markFor(index) : undefined;
      active?.setAttribute("data-ib-active", "");

      // The band (or, on a scale, a line) under the item.
      const pos = positions[index];
      const size = bandSize;
      const start = scale ? pos : pos - size / 2;
      hover.classList.toggle("ib-chart-hover-line", scale);
      hover.style.cssText = horizontal
        ? `top:${start * 100}%;height:${size * 100}%;left:0;width:100%`
        : `left:${start * 100}%;width:${size * 100}%;top:0;height:100%`;
      hover.hidden = pointsMode;
      tooltip.hidden = false;

      // Beside the item, flipped to the other side when there's no room; kept inside the chart.
      const pw = plot.clientWidth;
      const ph = plot.clientHeight;
      const tw = tooltip.offsetWidth;
      const th = tooltip.offsetHeight;
      const gap = 12;
      // The frame's edges, in the plot's coordinates.
      const offset = plot.getBoundingClientRect().left - frame.getBoundingClientRect().left;
      const minX = -offset;
      const maxX = frame.clientWidth - offset - tw;
      let x: number;
      let y: number;
      if (active) {
        const [ax, ay] = at(active);
        x = ax + gap + tw <= frame.clientWidth - offset ? ax + gap : ax - gap - tw;
        y = ay - th / 2;
      } else if (!horizontal) {
        const ax = pos * pw;
        const half = (size * pw) / 2;
        x = ax + half + gap + tw <= frame.clientWidth - offset ? ax + half + gap : ax - half - gap - tw;
        y = (pointerY ?? ph / 3) - th / 2;
      } else {
        const ay = pos * ph;
        const half = (size * ph) / 2;
        x = (pointerY ?? pw / 2) - tw / 2;
        y = ay + half + gap + th <= ph ? ay + half + gap : ay - half - gap - th;
      }
      tooltip.style.left = `${Math.min(Math.max(x, minX), Math.max(minX, maxX))}px`;
      tooltip.style.top = `${Math.min(Math.max(y, 0), Math.max(0, ph - th))}px`;
    };

    refresh = () => {
      if (current >= 0 && !tooltip.hidden) show(current, lastY);
    };

    const hide = () => {
      tooltip.hidden = true;
      hover.hidden = true;
      active?.removeAttribute("data-ib-active");
      active = undefined;
    };

    // The plot's shapes stop taking the pointer (chart.css), so their native <title> tips, there
    // for when there's no script, don't show over this one.
    this.setAttribute("data-ib-tooltips", "");
    signal.addEventListener("abort", () => this.removeAttribute("data-ib-tooltips"));

    // Who moved the tooltip last: the keys keep it when the pointer leaves; the pointer doesn't.
    let byKeys = false;
    plot.addEventListener(
      "pointermove",
      (event) => {
        byKeys = false;
        const r = plot.getBoundingClientRect();
        if (pointsMode) {
          const px = event.clientX - r.left;
          const py = event.clientY - r.top;
          let best: SVGElement | undefined;
          let distance = Infinity;
          for (const m of marksNow()) {
            if (!visible(m)) continue;
            const [mx, my] = at(m);
            const d = (mx - px) ** 2 + (my - py) ** 2;
            if (d < distance) [best, distance] = [m, d];
          }
          if (best) show(Number(best.dataset.ibI), null);
          return;
        }
        show(nearest(along(event)), horizontal ? event.clientX - r.left : event.clientY - r.top);
      },
      { signal }
    );
    plot.addEventListener(
      "pointerleave",
      () => {
        if (!byKeys) hide();
      },
      { signal }
    );

    // The keyboard: one Tab stop on the plot. Screen readers pass the arrow keys to an application,
    // not an image, so the frame becomes one, described by the keys' hint.
    this.keyboard(frame, signal);
    document.addEventListener("keydown", (event) => event.key === "Escape" && !tooltip.hidden && hide(), { signal });
    const announce = () => {
      if (!live) return;
      live.textContent = [...tooltip.children]
        .map((c) => (c.textContent ?? "").replace(/\s+/g, " ").trim())
        .filter(Boolean)
        .join(", ");
    };
    const step = (by: number) => {
      const order = keys();
      const k = order.indexOf(current);
      return order[k < 0 ? (by > 0 ? 0 : order.length - 1) : Math.min(order.length - 1, Math.max(0, k + by))];
    };
    // Focus from the keys shows the tooltip; a click's focus leaves it with the pointer.
    frame.addEventListener(
      "focus",
      () => {
        if (!frame.matches(":focus-visible")) return;
        byKeys = true;
        show(current >= 0 ? current : keys()[0], null);
        announce();
      },
      { signal }
    );
    frame.addEventListener("blur", hide, { signal });
    frame.addEventListener(
      "keydown",
      (event) => {
        if (event.key !== "Escape" && event.key !== "Tab") byKeys = true;
        let next = current;
        switch (event.key) {
          case "ArrowRight":
          case "ArrowDown":
            next = step(1);
            break;
          case "ArrowLeft":
          case "ArrowUp":
            next = step(-1);
            break;
          case "PageDown":
          case "PageUp":
            next = step((event.key === "PageDown" ? 1 : -1) * Math.max(1, Math.round(keys().length / 10)));
            break;
          case "Home":
            next = keys()[0];
            break;
          case "End":
            next = keys().slice(-1)[0];
            break;
          case "Escape":
            hide();
            return;
          case "+":
          case "=":
          case "-":
          case "_":
          case "0":
            if (!zoomOn) return;
            event.preventDefault();
            if (event.key === "0") setView(0, 1);
            else {
              const at = current >= 0 && shown(current) ? shareAt(positions[current]) : (view[0] + view[1]) / 2;
              zoomAround(at, event.key === "+" || event.key === "=" ? 0.5 : 2);
            }
            return;
          default:
            return;
        }
        event.preventDefault();
        // Zoomed, an item out of view comes into the middle of it.
        if (zoomOn && next !== undefined && next >= 0 && !shown(next) && data!.za![next] !== null) {
          const at = data!.za![next]!, span = view[1] - view[0];
          setView(at - span / 2, at + span / 2);
          placed();
        }
        show(next, null);
        announce();
      },
      { signal }
    );

    // Zoom's gestures: a drag across the plot draws a box to zoom into, Ctrl (⌘) and the wheel or
    // a trackpad's pinch zoom at the pointer, Shift and the wheel pan; on touch two fingers pinch
    // and pan, and one still scrolls the page.
    if (!zoomOn) return;
    const brush = plot.querySelector<HTMLElement>(".ib-chart-brush");
    const length = () => (horizontal ? plot.clientHeight : plot.clientWidth);
    const fracOf = (x: number, y: number) => {
      const r = plot.getBoundingClientRect();
      return horizontal ? (y - r.top) / r.height : (x - r.left) / r.width;
    };
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
        if ((event.target as Element).closest(".ib-chart-reset")) return;
        if (event.pointerType === "touch") {
          touches.set(event.pointerId, [event.clientX, event.clientY]);
          if (touches.size === 2) {
            const { mid, dist } = pinchState();
            pinch = { share: shareAt(mid), span: view[1] - view[0], dist };
            hide();
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
            const span = Math.min(1, Math.max(data!.zl ?? 0, (pinch.span * pinch.dist) / Math.max(1, dist)));
            const f = data!.ar ? 1 - mid : mid;
            setView(pinch.share - f * span, pinch.share - f * span + span, false);
          }
          return;
        }
        if (!box || event.pointerId !== box.id) return;
        if (!this.hasAttribute("data-ib-brushing")) {
          if (Math.hypot(event.clientX - box.x, event.clientY - box.y) < 4) return;
          plot.setPointerCapture(event.pointerId);
          this.setAttribute("data-ib-brushing", "");
          hide();
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
        settled();
        return;
      }
      if (!box || event.pointerId !== box.id) return;
      const from = box.from;
      box = null;
      if (!this.hasAttribute("data-ib-brushing")) return;
      this.removeAttribute("data-ib-brushing");
      const to = Math.min(1, Math.max(0, fracOf(event.clientX, event.clientY)));
      if (Math.abs(to - from) * length() < 6) return;
      const [a, b] = [shareAt(from), shareAt(to)].sort((x, y) => x - y);
      zoomTo(a, b);
    };
    plot.addEventListener("pointerup", release, { signal });
    plot.addEventListener("pointercancel", release, { signal });
    plot.addEventListener(
      "wheel",
      (event) => {
        if (event.ctrlKey || event.metaKey) {
          event.preventDefault();
          hide();
          zoomAround(shareAt(fracOf(event.clientX, event.clientY)), Math.exp(event.deltaY * 0.002), false);
          settled(300);
        } else if (isZoomed() && (event.shiftKey || Math.abs(event.deltaX) > Math.abs(event.deltaY))) {
          event.preventDefault();
          const by = ((event.shiftKey ? event.deltaY || event.deltaX : event.deltaX) / length()) * (view[1] - view[0]) * (data!.ar ? -1 : 1);
          setView(view[0] + by, view[1] + by, false);
          settled(300);
        }
      },
      { signal, passive: false }
    );
    this.querySelector(".ib-chart-reset")?.addEventListener("click", () => setView(0, 1), { signal });
  }

  /**
   * A PieChart: the tooltip is for the slice under the pointer, found from its angle and distance
   * from the middle (data-ib-pos holds each slice's start and end as shares of the turn); the arrow
   * keys move between slices.
   */
  private pie(signal: AbortSignal): void {
    const plot = this.querySelector<HTMLElement>(".ib-pie-plot");
    const frame = this.querySelector<HTMLElement>(".ib-pie-frame");
    const tooltip = this.querySelector<HTMLElement>(".ib-chart-tooltip");
    const tips = this.querySelectorAll<HTMLElement>(".ib-chart-tips > [data-ib-tip]");
    const live = this.querySelector<HTMLElement>("[data-ib-live]");
    if (!plot || !frame) return;

    const hole = Number(this.dataset.ibPie) || 0;
    // A nested donut's rings, outer first, each as thick as the next; its slices are numbered
    // ring by ring (ring * count + item), and its positions come ring by ring, split by ";".
    const rings = Number(this.dataset.ibRings) || 1;
    const count = Number(this.dataset.ibCount) || 0;
    const thick = (1 - hole) / rings;
    let spans = (this.dataset.ibPos ?? "").split(";").flatMap((ring) => ring.split(",").map((p) => (p ? p.split(" ").map(Number) : null)));
    let order = spans.flatMap((s, i) => (s ? [i] : []));
    const ringOf = (index: number) => (rings > 1 && count ? Math.floor(index / count) : 0);
    const slices = plot.querySelectorAll<SVGPathElement>("path[data-ib-i]");
    let current = -1;
    // Set once the tooltip is: hides it when the legend hides its slice.
    let redrawn = () => {};

    // Legend: each item hides and shows its slices; the rest close up and their shares are of
    // what's shown, drawn here (chart-draw.ts drawPie).
    const data: PieDrawData | null = this.dataset.ibDraw ? JSON.parse(this.dataset.ibDraw) : null;
    const toggles = [...this.querySelectorAll<HTMLElement>(".ib-pie-toggle")];
    const hidden = new Set<number>();
    const labeled = [...plot.querySelectorAll<HTMLElement>("span[data-ib-l]")].map((l) => Number(l.dataset.ibL));
    const redraw = async () => {
      if (!data) return;
      const out = (await loadModule()).drawPie(data, hidden, labeled);
      if (signal.aborted) return;
      for (const path of slices) {
        const slice = out.slices[Number(path.dataset.ibI)];
        path.toggleAttribute("data-ib-hidden", !slice);
        if (slice) path.setAttribute("d", slice.d);
      }
      spans = out.slices.map((s) => (s ? [s.start, s.end] : null));
      order = spans.flatMap((s, i) => (s ? [i] : []));
      // Hidden items' shares go blank: the legend still names them, dimmed.
      for (const el of this.querySelectorAll<HTMLElement>("[data-ib-share]")) el.textContent = out.shares[Number(el.dataset.ibShare)] ?? "";
      for (const el of plot.querySelectorAll<HTMLElement | SVGPathElement>("[data-ib-l]")) {
        const label = out.labels.get(Number(el.dataset.ibL));
        el.toggleAttribute("data-ib-hidden", !label);
        if (!label) continue;
        if (el instanceof SVGPathElement) el.setAttribute("d", label.leader);
        else {
          el.style.left = `${label.x * 100}%`;
          el.style.top = `${label.y * 100}%`;
          el.classList.toggle("ib-pie-label-start", !label.end);
        }
      }
      for (const el of plot.querySelectorAll<HTMLElement>("[data-ib-in]")) {
        const at = out.inside.get(Number(el.dataset.ibIn));
        el.toggleAttribute("data-ib-hidden", !at);
        if (!at) continue;
        el.style.left = `${at.x * 100}%`;
        el.style.top = `${at.y * 100}%`;
      }
      redrawn();
    };
    if (data) {
      const toggle = (el: HTMLElement) => {
        const item = Number(el.dataset.ibSeries);
        if (hidden.has(item)) hidden.delete(item);
        else hidden.add(item);
        el.setAttribute("aria-pressed", String(!hidden.has(item)));
        redraw();
      };
      for (const el of toggles) {
        el.setAttribute("role", "button");
        el.tabIndex = 0;
        el.setAttribute("aria-pressed", "true");
        el.addEventListener("click", () => toggle(el), { signal });
        el.addEventListener(
          "keydown",
          (event) => {
            if (event.key !== "Enter" && event.key !== " ") return;
            event.preventDefault();
            toggle(el);
          },
          { signal }
        );
      }
      signal.addEventListener("abort", () => {
        for (const el of toggles) {
          el.removeAttribute("role");
          el.removeAttribute("tabindex");
          el.removeAttribute("aria-pressed");
        }
      });
    }

    if (!tooltip || tips.length === 0) return;

    this.setAttribute("data-ib-tooltips", "");
    signal.addEventListener("abort", () => this.removeAttribute("data-ib-tooltips"));

    const show = (index: number, x?: number, y?: number) => {
      const span = spans[index];
      if (!span) return;
      current = index;
      tooltip.innerHTML = tips[index].innerHTML;
      tooltip.hidden = false;
      for (const s of slices) s.toggleAttribute("data-ib-active", s.dataset.ibI === String(index));

      // Beside the pointer, or for the keyboard beside the slice's middle; kept inside the chart.
      const size = plot.clientWidth;
      if (x === undefined || y === undefined) {
        const turn = ((span[0] + span[1]) / 2) * 2 * Math.PI;
        const r = (size / 2) * (rings > 1 ? 1 - (ringOf(index) + 0.5) * thick : hole ? (1 + hole) / 2 : 0.6);
        x = size / 2 + r * Math.sin(turn);
        y = size / 2 - r * Math.cos(turn);
      }
      const gap = 12;
      const tw = tooltip.offsetWidth;
      const th = tooltip.offsetHeight;
      const box = this.getBoundingClientRect();
      const offset = plot.getBoundingClientRect().left - box.left;
      const left = x + gap + tw <= box.width - offset ? x + gap : x - gap - tw;
      tooltip.style.left = `${Math.max(-offset, Math.min(left, box.width - offset - tw))}px`;
      tooltip.style.top = `${Math.min(Math.max(y - th / 2, 0), Math.max(0, size - th))}px`;
    };

    const hide = () => {
      tooltip.hidden = true;
      for (const s of slices) s.removeAttribute("data-ib-active");
    };
    // The legend hid the slice the tooltip is for: nothing to show, and the keys start again.
    redrawn = () => {
      if (current >= 0 && !spans[current]) {
        hide();
        current = -1;
      }
    };

    // Who moved the tooltip last: the keys keep it when the pointer leaves; the pointer doesn't.
    let byKeys = false;

    plot.addEventListener(
      "pointermove",
      (event) => {
        const r = plot.getBoundingClientRect();
        const x = event.clientX - r.left;
        const y = event.clientY - r.top;
        const dx = x - r.width / 2;
        const dy = y - r.height / 2;
        const d = Math.hypot(dx, dy) / (r.width / 2);
        byKeys = false;
        if (d > 1 || d < hole) {
          hide();
          return;
        }
        const turn = (Math.atan2(dx, -dy) / (2 * Math.PI) + 1) % 1;
        const ring = Math.min(rings - 1, Math.floor((1 - d) / thick));
        // A slice that crosses the top (StartAngle) ends past 1: measure from its start, round the turn.
        const index = spans.findIndex((s, i) => s !== null && ringOf(i) === ring && (((turn - s[0]) % 1) + 1) % 1 < s[1] - s[0]);
        // An empty ring (every value zero) has no slice to show.
        if (index >= 0) show(index, x, y);
        else hide();
      },
      { signal }
    );
    plot.addEventListener(
      "pointerleave",
      () => {
        if (!byKeys) hide();
      },
      { signal }
    );

    this.keyboard(frame, signal);
    document.addEventListener("keydown", (event) => event.key === "Escape" && !tooltip.hidden && hide(), { signal });
    const announce = () => {
      if (!live) return;
      // "Pro, 412, 33%": the name, the value and the share.
      live.textContent = [...tooltip.querySelectorAll("*")]
        .filter((e) => e.children.length === 0)
        .map((e) => (e.textContent ?? "").trim())
        .filter(Boolean)
        .join(", ");
    };
    frame.addEventListener(
      "focus",
      () => {
        if (!frame.matches(":focus-visible")) return;
        byKeys = true;
        show(current >= 0 ? current : order[0]);
        announce();
      },
      { signal }
    );
    frame.addEventListener("blur", hide, { signal });
    frame.addEventListener(
      "keydown",
      (event) => {
        if (event.key !== "Escape" && event.key !== "Tab") byKeys = true;
        // No slice yet (focus without a visible ring, or the pointer in the hole): any arrow
        // starts at the first slice, as keyboard focus does.
        const moves = event.key.startsWith("Arrow") || event.key === "Home" || event.key === "End";
        if (moves && order.length === 0) return;
        if (moves && current < 0) {
          event.preventDefault();
          show(order[0]);
          announce();
          return;
        }
        // With rings, Left and Right keep to the current one; Up and Down move out and in to
        // the same item (or the nearest one that has a slice there).
        const ring = current >= 0 ? ringOf(current) : 0;
        const along = rings > 1 ? order.filter((i) => ringOf(i) === ring) : order;
        const k = along.indexOf(current);
        const across = (step: number) => {
          const to = Math.max(0, Math.min(rings - 1, ring + step));
          const item = current - ring * count;
          const there = order.filter((i) => ringOf(i) === to);
          return there.reduce((best, i) => (Math.abs(i - to * count - item) < Math.abs(best - to * count - item) ? i : best), there[0] ?? current);
        };
        let next: number;
        switch (event.key) {
          case "ArrowRight":
            next = along[Math.min(along.length - 1, k + 1)];
            break;
          case "ArrowDown":
            next = rings > 1 ? across(1) : along[Math.min(along.length - 1, k + 1)];
            break;
          case "ArrowLeft":
            next = along[Math.max(0, k - 1)];
            break;
          case "ArrowUp":
            next = rings > 1 ? across(-1) : along[Math.max(0, k - 1)];
            break;
          case "Home":
            next = along[0];
            break;
          case "End":
            next = along[along.length - 1];
            break;
          case "Escape":
            hide();
            return;
          default:
            return;
        }
        event.preventDefault();
        show(next);
        announce();
      },
      { signal }
    );
  }

  /**
   * A RangeSelector's strip: kept on the view (at), moved by its handles, its window, a click on
   * the strip, the keys on a handle and its range buttons, all through setView. Returns what
   * draws it again for a new view.
   */
  private selector(sel: HTMLElement, data: DrawData, at: () => View, setView: (from: number, to: number, report?: boolean) => void, settled: () => void, signal: AbortSignal, plot: HTMLElement | null): () => void {
    const track = sel.querySelector<HTMLElement>(".ib-chart-sel-track")!;
    const za = data.za!;
    const names = data.zn ?? [];
    const least = data.zl ?? 0;
    const handles = [...sel.querySelectorAll<HTMLElement>(".ib-chart-sel-handle")];
    const nearest = (share: number) => {
      let best = 0;
      za.forEach((p, i) => {
        if (p !== null && (za[best] === null || Math.abs(p - share) < Math.abs(za[best]! - share))) best = i;
      });
      return best;
    };
    // Beside a plot, it's lined up with it (it sits outside the frame, whose img role would hide
    // its sliders).
    const line = () => {
      if (!plot) return;
      const main = sel.parentElement!.getBoundingClientRect(), r = plot.getBoundingClientRect();
      sel.style.setProperty("--ib-chart-sel-x", `${r.left - main.left}px`);
      sel.style.setProperty("--ib-chart-sel-w", `${r.width}px`);
    };
    const show = () => {
      const placed = (share: number) => String(data.ar ? 1 - share : share);
      sel.style.setProperty("--ib-sel-js", placed(at()[0]));
      sel.style.setProperty("--ib-sel-je", placed(at()[1]));
      handles.forEach((h, k) => {
        const i = nearest(at()[k]);
        h.setAttribute("aria-valuenow", String(i));
        h.setAttribute("aria-valuetext", names[i] ?? "");
        const flag = h.querySelector(".ib-chart-sel-flag.ib-chart-js");
        if (flag) flag.textContent = names[i] ?? "";
      });
      for (const b of sel.querySelectorAll<HTMLElement>("[data-ib-range]")) {
        const [a, c] = b.dataset.ibRange!.split(" ").map(Number);
        b.setAttribute("aria-pressed", String(Math.abs(a - at()[0]) < 1e-3 && Math.abs(c - at()[1]) < 1e-3));
      }
      this.setAttribute("data-ib-sel", "");
    };
    show();
    line();
    const lining = new ResizeObserver(line);
    if (plot) lining.observe(plot);
    lining.observe(sel.parentElement!);
    signal.addEventListener("abort", () => {
      lining.disconnect();
      this.removeAttribute("data-ib-sel");
      this.removeAttribute("data-ib-sel-drag");
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
        track.setPointerCapture(event.pointerId);
        this.setAttribute("data-ib-sel-drag", "");
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
      this.removeAttribute("data-ib-sel-drag");
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

  /**
   * Makes the plot one Tab stop that takes the arrow keys: an application to screen readers (an
   * image takes no keys in their browse mode), named as before and described by the keys' hint.
   */
  private keyboard(frame: HTMLElement, signal: AbortSignal): void {
    frame.tabIndex = 0;
    frame.setAttribute("role", "application");
    const hint = this.querySelector<HTMLElement>("[data-ib-keys]");
    // The server's description (the reference marks) stays, with the keys' hint after it.
    const described = frame.getAttribute("aria-describedby");
    if (hint) {
      frame.setAttribute("aria-roledescription", hint.dataset.ibKeys!);
      frame.setAttribute("aria-describedby", described ? `${described} ${hint.id}` : hint.id);
    }
    signal.addEventListener("abort", () => {
      frame.setAttribute("role", "img");
      frame.removeAttribute("aria-roledescription");
      // Only the hint comes off: a render since may have changed the server's part.
      const rest = (frame.getAttribute("aria-describedby") ?? "").split(" ").filter((id) => id && id !== hint?.id).join(" ");
      if (rest) frame.setAttribute("aria-describedby", rest);
      else frame.removeAttribute("aria-describedby");
      frame.removeAttribute("tabindex");
    });
  }

  /**
   * Picks the most labels that fit along the bottom: the server marked each with the narrowest of
   * eight container widths it shows at (ib-chart-fromN), from an estimate; this measures them and
   * sets data-ib-fit to the widest set whose labels have room (chart.css shows that set).
   */
  private fit(axis: HTMLElement): void {
    const labels = [...axis.querySelectorAll<HTMLElement>(":scope > .ib-chart-ticks > span")].filter((l) => getComputedStyle(l.parentElement!).display !== "none");
    // Names wrapped in their bands (LabelFit Wrap) all show.
    if (labels.length === 0 || axis.classList.contains("ib-chart-wrap")) return;
    const from = (l: HTMLElement) => Number(/ib-chart-from(\d)/.exec(l.className)?.[1] ?? 0);
    const width = axis.clientWidth;
    if (width === 0) return;

    // Show them all, flat, to measure them.
    this.dataset.ibFit = "7";
    this.removeAttribute("data-ib-turn");
    const bands = axis.classList.contains("ib-chart-bands");
    const style = getComputedStyle(axis);
    const sizes = labels.map((l) => l.scrollWidth);
    const centers = labels.map((l) => (parseFloat(l.style.left) / 100) * width);

    let chosen = 0;
    for (let k = 7; k >= 0; k--) {
      const shown = labels.map((l, i) => i).filter((i) => from(labels[i]) <= k);
      let fits: boolean;
      if (bands) {
        const room = (parseFloat(style.getPropertyValue(`--ib-chart-w${k}`)) / 100) * width;
        fits = shown.every((i) => sizes[i] <= room + 0.5);
      } else {
        fits = shown.every((i, j) => j === 0 || centers[i] - sizes[i] / 2 - (centers[shown[j - 1]] + sizes[shown[j - 1]] / 2) >= 6);
      }
      if (fits) {
        chosen = k;
        break;
      }
    }

    // Names that turn (LabelFit Rotate) stay flat only when every one fits its band; the
    // server's sets are for turned names below that. Turned 45°, each needs about a line and a
    // half along the axis.
    const band = width / Math.max(1, Number(this.dataset.ibCount) || labels.length);
    if (/ib-chart-rot\d/.test(axis.parentElement!.className) && sizes.some((s) => s > band - 6)) {
      this.setAttribute("data-ib-turn", "");
      const pitch = 1.5 * (parseFloat(style.lineHeight) || 16);
      chosen = 0;
      for (let k = 7; k >= 0; k--) {
        const shown = labels.map((l, i) => i).filter((i) => from(labels[i]) <= k);
        if (shown.every((i, j) => j === 0 || centers[i] - centers[shown[j - 1]] >= pitch)) {
          chosen = k;
          break;
        }
      }
    }

    this.dataset.ibFit = String(chosen);

    // Zoomed by the script: the first date shown says its month (or year) if it doesn't already.
    for (const lead of axis.querySelectorAll(".ib-chart-zargs .ib-chart-lead")) lead.remove();
    const first = labels.find((l) => from(l) <= chosen && l.parentElement?.classList.contains("ib-chart-zargs"));
    if (first?.dataset.ibLead) {
      const line = document.createElement("span");
      line.className = "ib-chart-context ib-chart-lead";
      line.textContent = first.dataset.ibLead;
      first.append(line);
    }
  }
}

defineElement("ib-chart", Chart);
