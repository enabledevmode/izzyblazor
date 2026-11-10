import { IzzyElement, defineElement } from "../izzy-element";
import type { DrawData, View, draw as Draw, placeMarks as PlaceMarks, zoomArgs as ZoomArgs } from "./chart-draw";
import type { drawPie as DrawPie } from "./chart-draw-pie";
import type { heat as Heat } from "./chart-heat";
import type { pie as Pie } from "./chart-pie";
import type { gestures as Gestures } from "./chart-gesture";
import type { live as Live } from "./chart-live";
import type { selector as Selector } from "./chart-select";
import type { Peer, SyncAt } from "./chart-sync";
import type { radar as Radar } from "./chart-radar";
import type { sankey as Sankey } from "./chart-sankey";
import type { treemap as Treemap } from "./chart-treemap";

// The redraw (chart-draw.ts), loaded the first time a legend hides a series (or a pie's slices).
type DrawModule = { draw: typeof Draw; placeMarks: typeof PlaceMarks; zoomArgs: typeof ZoomArgs };
let drawing: Promise<DrawModule> | undefined;
/** A view kept within the whole axis and at least `least` wide; too narrow or wide, it keeps its middle. */
const clampView = (from: number, to: number, least: number): View => {
  const span = Math.min(1, Math.max(least, to - from));
  const start = Math.min(Math.max(0, to - from === span ? from : (from + to) / 2 - span / 2), 1 - span);
  return [start, start + span];
};

const loadModule = () => (drawing ??= import("./parts/chart-draw.js" as string) as Promise<DrawModule>);
// The RangeSelector's part (chart-select.ts), loaded where there's one.
const loadSelector = () => import("./parts/chart-select.js" as string) as Promise<{ selector: typeof Selector }>;

// Synced charts (Chart Sync): their part (chart-sync.ts), loaded where a chart has a Sync name.
type SyncModule = typeof import("./chart-sync");
let syncing: Promise<SyncModule> | undefined;
const loadSync = () => (syncing ??= import("./parts/chart-sync.js" as string) as Promise<SyncModule>);

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
  // The item the keys were on, and whether the tooltip was theirs, kept over a new draw of the same chart.
  #current = -1;
  #byKeys = false;
  // That item's argument (live and synced charts): new data keeps the item, not its place.
  #key: unknown;
  // Live (Refresh): the view and the call that sets it, for the live part across new drawings.
  ibView?: [() => View, (from: number, to: number, report?: boolean, share?: boolean) => void];

  protected setup(signal: AbortSignal): void {
    // An interactive page can draw the chart again with new data in this same element: set up
    // again from what the server sent, keeping what the legend hides. (Enhanced navigation sets
    // up again too, for what may be another chart: that starts over.)
    const changed = new MutationObserver(() => {
      this.#keep = true;
      this.connectedCallback();
    });
    // Live data on a static page (Chart Refresh): its own part (chart-live.ts).
    if (this.hasAttribute("data-ib-refresh")) {
      (import("./parts/chart-live.js" as string) as Promise<{ live: typeof Live }>).then((m) => !signal.aborted && m.live(this, signal));
    }
    changed.observe(this, { attributeFilter: ["data-ib-draw", "data-ib-pos", "data-ib-count", "data-ib-pie", "data-ib-sankey", "data-ib-treemap", "data-ib-sync", "data-ib-sync-at", "data-ib-sync-view", "data-ib-sync-hover", "data-ib-follow"] });
    signal.addEventListener("abort", () => changed.disconnect());

    if (this.hasAttribute("data-ib-pie")) {
      // A PieChart or NestedDonutChart: its own part (chart-pie.ts).
      (import("./parts/chart-pie.js" as string) as Promise<{ pie: typeof Pie }>).then((m) => {
        if (!signal.aborted) m.pie(this, signal, (frame) => this.keyboard(frame, signal), () => import("./parts/chart-draw-pie.js" as string) as Promise<{ drawPie: typeof DrawPie }>);
      });
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
    // A RangeSelector on its own: the strip only (chart-select.ts).
    const lone = this.querySelector<HTMLElement>(".ib-chart-sel");
    if (!plot && lone && this.dataset.ibDraw) {
      loadSelector().then((m) => !signal.aborted && m.lone(this, lone, signal, loadSync, (layout) => this.emit("iblayout", { layout })));
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
    const kept = this.#keep;
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
    // Synced by view with no Zoom of its own, it follows the others and takes no gestures.
    const gestures = zoomOn && !this.hasAttribute("data-ib-follow");
    // Synced (Sync): the charts named alike, which it zooms and hovers with.
    const syncAt: SyncAt | null = this.dataset.ibSyncAt ? JSON.parse(this.dataset.ibSyncAt) : null;
    const peer: Peer = {};
    let sync: SyncModule | undefined;
    let others: ((call: (p: Peer) => void) => void) | null = null;
    const syncName = syncAt ? this.dataset.ibSync : undefined;
    if (syncName) {
      loadSync().then((m) => {
        if (signal.aborted) return;
        sync = m;
        others = m.joinSync(syncName, peer, signal);
      });
    }
    const syncView = !!syncName && zoomOn && this.hasAttribute("data-ib-sync-view");
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
    // The argument marks back in the server's places; chart-draw.ts places them in another view.
    const clearMarks = () => {
      for (const mark of plot.querySelectorAll<HTMLElement>("[data-ib-za]")) {
        for (const v of ["--jn", ...(mark.dataset.ibV ? [] : ["--jp", "--jq"])]) mark.style.removeProperty(v);
        mark.removeAttribute("data-ib-zout");
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
        clearMarks();
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
      clearMarks();
      if (own) module.placeMarks(plot, placeOf, horizontal);
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

    // A drag box or a pinch under way: the tooltip doesn't follow it.
    let gesturing = false;
    // Said after the next settled view, joined to it (an arrow that panned says the item, then the range).
    let joinNext = false;

    // The RangeSelector, if any, kept on the view (set up below).
    let showSelector = () => {};

    // Sets the view, kept to the whole axis and the fewest items' span, and draws it; once it
    // settles, says the range and (interactive) reports it to .NET for ViewChanged.
    const zoomLive = this.querySelector<HTMLElement>("[data-ib-live]");
    let settle = 0;
    // Moved here since the last settle, not only by another synced chart: said, and passed on.
    let local = false;
    let pending = false;
    // Reset zoom: the others show everything too, not this chart's ends.
    let resetting = false;
    const settled = (wait = 0) => {
      clearTimeout(settle);
      pending = true;
      settle = window.setTimeout(() => {
        pending = false;
        if (signal.aborted) return;
        const inView = (data!.za ?? []).map((p, i) => [p, i] as const).filter(([p]) => p !== null && p >= view[0] - 1e-9 && p <= view[1] + 1e-9).sort((a, b) => a[0]! - b[0]!);
        if (local && zoomLive && data!.zs && data!.zn && inView.length > 0) {
          const range = data!.zs.replace("{0}", data!.zn[inView[0][1]]).replace("{1}", data!.zn[inView[inView.length - 1][1]]);
          zoomLive.textContent = joinNext && zoomLive.textContent ? `${zoomLive.textContent}. ${range}` : range;
        }
        if (local && syncView) others?.((p) => p.settle?.());
        local = false;
        joinNext = false;
        if (this.hasAttribute("data-ib-interactive")) {
          this.emit("iblayout", { layout: isZoomed() ? `${Math.round(view[0] * 1e6) / 1e6} ${Math.round(view[1] * 1e6) / 1e6}` : "" });
        }
      }, wait);
    };
    const setView = (from: number, to: number, report = true, share = true) => {
      view = clampView(from, to, data?.zl ?? 0);
      this.toggleAttribute("data-ib-zoomed", isZoomed());
      redraw();
      showSelector();
      if (share) local = true;
      if (share && syncView && sync) {
        const m = resetting ? null : sync.viewOut(syncAt!, view);
        if (m !== undefined) others?.((p) => p.view?.(m));
      }
      if (report) settled();
    };
    const mine: Chart["ibView"] = (this.ibView = [() => view, setView]);
    signal.addEventListener("abort", () => this.ibView === mine && (this.ibView = undefined));
    if (syncView) {
      peer.view = (m) => {
        const v = sync!.viewIn(syncAt!, m);
        if (v) setView(v[0], v[1], false, false);
      };
      // Its own settle on the way says it all.
      peer.settle = () => {
        if (!(pending && local)) settled();
      };
    }
    const resetAll = () => {
      resetting = true;
      setView(0, 1);
      resetting = false;
    };
    // Reset zoom (a chart that only follows has it too: it resets the group).
    this.querySelector(".ib-chart-reset")?.addEventListener(
      "click",
      () => {
        resetAll();
        // The button hides once reset: focus goes to the plot, when the keys can reach it.
        if (frame.hasAttribute("tabindex")) frame.focus();
      },
      { signal }
    );
    const zoomTo = (from: number, to: number) => setView(from, to);
    const zoomAround = (at: number, factor: number, report = true) => {
      const span = Math.min(1, Math.max(data?.zl ?? 0, (view[1] - view[0]) * factor));
      const k = (at - view[0]) / (view[1] - view[0] || 1);
      setView(at - k * span, at - k * span + span, report);
    };

    // A RangeSelector under the plot.
    const sel = this.querySelector<HTMLElement>(".ib-chart-sel");
    if (zoomOn && sel && data) {
      loadSelector().then((m) => {
        if (signal.aborted) return;
        showSelector = m.selector(this, sel, data, () => view, setView, settled, signal, plot);
        // A view taken in while it loaded.
        if (!sameView()) showSelector();
      });
    }
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
    const tooltip = plot.querySelector<HTMLElement>(".ib-chart-tooltip")!;
    const hover = plot.querySelector<HTMLElement>(".ib-chart-hover")!;
    const tips = this.querySelectorAll<HTMLElement>(".ib-chart-tips > [data-ib-tip]");
    const live = this.querySelector<HTMLElement>("[data-ib-live]");
    // With no tooltips (every series labeled), a chart that zooms still takes its keys and gestures.
    const tipsOn = !!tooltip && !!hover && tips.length > 0;
    if (!tipsOn && !gestures) return;

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
    // Showing an item for another synced chart (byPeer, during show), and whether the one shown is.
    let byPeer = false;
    let peerShown = false;

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

    let current = kept ? (syncAt && this.#key !== undefined ? syncAt.k.indexOf(this.#key as string) : this.#current) : -1;
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
      current = this.#current = index;
      this.#key = syncAt?.k[index];
      lastY = pointerY;
      if (!tipsOn) return;
      // Shown for another synced chart, screen readers leave it to that one.
      if (byPeer) tooltip.setAttribute("aria-hidden", "true");
      else tooltip.removeAttribute("aria-hidden");
      peerShown = byPeer;
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
      // Zoomed, an item at the view's edge has its band cut there.
      const start = Math.max(0, scale ? pos : pos - size / 2);
      const length = scale ? 0 : Math.min(1, pos + size / 2) - start;
      hover.classList.toggle("ib-chart-hover-line", scale);
      hover.style.cssText = horizontal
        ? `top:${start * 100}%;height:${length * 100}%;left:0;width:100%`
        : `left:${start * 100}%;width:${length * 100}%;top:0;height:100%`;
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
      if (!tipsOn || current < 0 || tooltip.hidden) return;
      byPeer = peerShown;
      show(current, lastY);
      byPeer = false;
    };

    const hide = () => {
      if (!tipsOn) return;
      tooltip.hidden = true;
      hover.hidden = true;
      active?.removeAttribute("data-ib-active");
      active = undefined;
    };

    // Synced hover (Sync): the item shown here shows in the charts named alike, at its argument.
    const syncHover = !!syncName && tipsOn && this.hasAttribute("data-ib-sync-hover");
    let told: number | string | null | undefined;
    const tellHover = () => {
      if (!syncHover) return;
      const at = !tooltip.hidden && current >= 0 ? (syncAt!.k[current] ?? null) : null;
      if (at === told) return;
      told = at;
      others?.((p) => p.hover?.(syncAt!.t, at));
    };
    // Hidden here by the reader: hidden in the others too.
    const drop = () => {
      hide();
      tellHover();
    };
    if (syncHover) {
      peer.hover = (t, at) => {
        // Another chart has the pointer now: this one tells again next time, even the same item.
        told = undefined;
        const i = sync!.itemAt(syncAt!, t, at);
        // The reader's own item (by the keys) stays; one shown for another chart follows it.
        if (byKeys && document.activeElement === frame) return;
        if (i < 0) {
          if (peerShown) hide();
          return;
        }
        byPeer = true;
        show(i, null);
        byPeer = false;
      };
    }

    // The plot's shapes stop taking the pointer (chart.css), so their native <title> tips, there
    // for when there's no script, don't show over this one.
    if (tipsOn) this.setAttribute("data-ib-tooltips", "");
    signal.addEventListener("abort", () => {
      this.removeAttribute("data-ib-tooltips");
      this.#byKeys = byKeys;
      hide();
    });

    // Who moved the tooltip last: the keys keep it when the pointer leaves; the pointer doesn't.
    let byKeys = kept && this.#byKeys;
    plot.addEventListener(
      "pointermove",
      (event) => {
        if (!tipsOn || gesturing) return;
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
          tellHover();
          return;
        }
        show(nearest(along(event)), horizontal ? event.clientX - r.left : event.clientY - r.top);
        tellHover();
      },
      { signal }
    );
    plot.addEventListener(
      "pointerleave",
      () => {
        if (!byKeys) drop();
      },
      { signal }
    );

    // The keyboard: one Tab stop on the plot. Screen readers pass the arrow keys to an application,
    // not an image, so the frame becomes one, described by the keys' hint.
    this.keyboard(frame, signal, gestures);
    document.addEventListener("keydown", (event) => event.key === "Escape" && tipsOn && !tooltip.hidden && drop(), { signal });
    const announce = () => {
      if (!live || !tipsOn) return;
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
        tellHover();
      },
      { signal }
    );
    frame.addEventListener("blur", drop, { signal });
    // Arrived by the keys from the level above: the plot has focus (a new level's first item), and says where it is.
    try {
      if (this.dataset.ibDrilled && sessionStorage.getItem("ib-drill") === `${location.pathname} ${this.dataset.ibDrill}`) {
        sessionStorage.removeItem("ib-drill");
        current = this.#current = -1;
        frame.focus();
        if (live) live.textContent = this.dataset.ibDrilled;
      }
    } catch {}
    // Drawn again (an interactive page after a zoom or new data) with the keys or the pointer on an item: it stays shown.
    if (kept && current >= 0 && ((byKeys && document.activeElement === frame) || plot.matches(":hover"))) show(current, null);
    frame.addEventListener(
      "keydown",
      (event) => {
        if (event.key !== "Escape" && event.key !== "Tab") byKeys = true;
        // With no tooltips there's no item to move to: the arrows pan a zoomed view a tenth of
        // itself, at least an item on a category axis (Right and Down show what's further on),
        // and the zoom's keys.
        if (!tipsOn && gestures && isZoomed() && event.key.startsWith("Arrow")) {
          event.preventDefault();
          const span = view[1] - view[0];
          // "ArrowDown" or "ArrowRight" goes on.
          const by = ("DR".includes(event.key[5]) ? 1 : -1) * (data?.ar ? -1 : 1) * Math.max(span / 10, scale ? 0 : 1 / count);
          const from = Math.min(1 - span, Math.max(0, view[0] + by));
          setView(from, from + span);
          return;
        }
        if (!tipsOn && !["+", "=", "-", "_", "0"].includes(event.key)) return;
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
            drop();
            return;
          case "Enter": {
            // Drill-down: the item's level below, back at this chart's plot when it arrives.
            const link = this.querySelector<HTMLAnchorElement>(`.ib-chart-drills [data-ib-drill="${current}"]`);
            if (!link) return;
            try {
              sessionStorage.setItem("ib-drill", `${location.pathname} ${this.dataset.ibDrill}`);
            } catch {}
            link.click();
            return;
          }
          case "+":
          case "=":
          case "-":
          case "_":
          case "0":
            if (!gestures) return;
            event.preventDefault();
            if (event.key === "0") resetAll();
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
          joinNext = true;
          setView(at - span / 2, at + span / 2);
          placed();
        }
        show(next, null);
        announce();
        tellHover();
      },
      { signal }
    );

    // Zoom's gestures: their part (chart-gesture.ts), loaded where a chart zooms.
    if (!gestures) return;
    (import("./parts/chart-gesture.js" as string) as Promise<{ gestures: typeof Gestures }>).then((m) => {
      if (signal.aborted) return;
      m.gestures({
        el: this, plot, data: data!, horizontal, signal,
        view: () => view, isZoomed, shareAt, setView, zoomTo, zoomAround, settled, drop,
        gesture: (on) => (gesturing = on),
      });
    });
  }

  /**
   * Makes the plot one Tab stop that takes the arrow keys: an application to screen readers (an
   * image takes no keys in their browse mode), named as before and described by the keys' hint.
   */
  private keyboard(frame: HTMLElement, signal: AbortSignal, zoom = false): void {
    frame.tabIndex = 0;
    frame.setAttribute("role", "application");
    // Keys beyond moving between items, announced: the zoom's and a drill-down's.
    const shortcuts = [zoom ? "Plus - 0" : "", this.querySelector(".ib-chart-drills [data-ib-drill]") ? "Enter" : ""].filter(Boolean).join(" ");
    if (shortcuts) frame.setAttribute("aria-keyshortcuts", shortcuts);
    const hint = this.querySelector<HTMLElement>("[data-ib-keys]");
    // The server's description (the reference marks) stays, with the keys' hint after it.
    const described = frame.getAttribute("aria-describedby");
    if (hint) {
      frame.setAttribute("aria-roledescription", hint.dataset.ibKeys!);
      const rest = (described ?? "").split(" ").filter((id) => id && id !== hint.id);
      frame.setAttribute("aria-describedby", [...rest, hint.id].join(" "));
    }
    signal.addEventListener("abort", () => {
      // Set up again for new data from the server (an interactive page, after each zoom): the
      // frame keeps its Tab stop, so a focused chart keeps its focus.
      if (this.#keep) return;
      frame.setAttribute("role", "img");
      frame.removeAttribute("aria-roledescription");
      frame.removeAttribute("aria-keyshortcuts");
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
    // Zoomed by the script, names have the room and band count of the view (on .ib-chart-zargs).
    const zoomed = this.hasAttribute("data-ib-zargs") ? axis.querySelector<HTMLElement>(".ib-chart-zargs") : null;
    const style = getComputedStyle(zoomed ?? axis);
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
    const band = width / Math.max(1, (zoomed ? parseFloat(style.getPropertyValue("--ib-chart-n")) : Number(this.dataset.ibCount)) || labels.length);
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

// Chart Download: a press on a download button (data-ib-chart-download) loads chart-export.ts,
// which saves the chart; so does a form with a chart's PNG in it (data-ib-chart-png) being sent,
// held until the PNG is in (before Blazor's enhanced forms see it). Here, not in the main module:
// every chart has an <ib-chart>.
const exporter = () => import("./parts/chart-export.js" as string) as Promise<typeof import("./chart-export")>;
document.addEventListener("click", (event) => {
  const button = (event.target as Element).closest?.("[data-ib-chart-download]");
  if (button) exporter().then((m) => m.press(button));
});
document.addEventListener(
  "submit",
  (event) => {
    // Only a form that posts: an interactive EditForm's submit stays Blazor's.
    const form = event.target as HTMLFormElement;
    const submitter = (event as SubmitEvent).submitter;
    if (form.method == "post" && form.dataset.ibPng != "ready" && form.querySelector("[data-ib-chart-png]")) {
      event.preventDefault();
      // The code can't load: the form goes without the PNG.
      if (!form.dataset.ibPng) exporter().then((m) => m.send(form, submitter), () => (form.dataset.ibPng = "ready") && form.requestSubmit());
    }
  },
  true
);

defineElement("ib-chart", Chart);
