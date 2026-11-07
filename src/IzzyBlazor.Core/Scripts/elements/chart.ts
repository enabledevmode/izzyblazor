import { IzzyElement, defineElement } from "../izzy-element";
import type { DrawData, draw as Draw } from "./chart-draw";

// The redraw (chart-draw.ts), loaded the first time a legend hides a series.
let drawing: Promise<typeof Draw> | undefined;
const loadDraw = () => (drawing ??= import("./parts/chart-draw.js" as string).then((m: { draw: typeof Draw }) => m.draw));

/**
 * <ib-chart> adds to a Chart the server drew: a tooltip for the item under the pointer, the
 * keyboard (the plot is one Tab stop; the arrow keys move between items and the tooltip's text
 * goes to a live region), legend items that hide and show their series, and labels fitted to the
 * width they really have.
 *
 * The server draws each item's tooltip (from the chart's TooltipTemplate, if any) in a hidden
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
    changed.observe(this, { attributeFilter: ["data-ib-draw", "data-ib-pos", "data-ib-count", "data-ib-pie"] });
    signal.addEventListener("abort", () => changed.disconnect());

    if (this.hasAttribute("data-ib-pie")) {
      this.pie(signal);
      return;
    }

    const frame = this.querySelector<HTMLElement>(".ib-chart-frame");
    const plot = this.querySelector<HTMLElement>(".ib-chart-plot");
    const bottom = this.querySelector<HTMLElement>(".ib-chart-bottom");
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
      if (same || !data || !jsSvg || !jsLabels) {
        this.removeAttribute("data-ib-redrawn");
        for (const mark of plot.querySelectorAll<HTMLElement>("[data-ib-v]")) {
          mark.style.removeProperty("--jp");
          mark.style.removeProperty("--jq");
        }
        return;
      }
      const draw = await loadDraw();
      if (signal.aborted) return;
      const out = draw(data, hidden, { w: plot.clientWidth, h: plot.clientHeight }, measure);
      jsSvg.innerHTML = out.svg;
      jsLabels.innerHTML = out.labels;
      if (jsDl) jsDl.innerHTML = out.dl;
      // Reference marks on the value axis move to the new scale.
      for (const mark of plot.querySelectorAll<HTMLElement>("[data-ib-v]")) {
        mark.style.setProperty("--jp", String(out.pos(Number(mark.dataset.ibV))));
        if (mark.dataset.ibVto) mark.style.setProperty("--jq", String(out.pos(Number(mark.dataset.ibVto))));
      }
      this.setAttribute("data-ib-redrawn", "");
      if (bottom) this.fit(bottom);
      refresh();
    };
    signal.addEventListener("abort", () => this.removeAttribute("data-ib-redrawn"));

    // Labels: show as many as fit, measured. Across, the value axis is drawn again for its width.
    let frameId = 0;
    const refit = () => {
      cancelAnimationFrame(frameId);
      frameId = requestAnimationFrame(() => {
        if (this.hasAttribute("data-ib-redrawn") && (jsDl || this.hasAttribute("data-ib-horizontal"))) redraw();
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
    const horizontal = this.hasAttribute("data-ib-horizontal");
    const scale = this.hasAttribute("data-ib-scale");
    const positions = scale
      ? (this.dataset.ibPos ?? "").split(",").map((p) => (p === "" ? NaN : Number(p)))
      : Array.from({ length: count }, (_, i) => (i + 0.5) / count);
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

    // The keyboard's order: the items, or on a scatter chart its points from left to right.
    const order = Array.from({ length: count }, (_, i) => i).filter((i) => !Number.isNaN(positions[i]));
    if (pointsMode) order.sort((a, b) => positions[a] - positions[b]);

    let current = -1;
    let lastY: number | null = null;

    const along = (event: PointerEvent) => {
      const r = plot.getBoundingClientRect();
      return horizontal ? (event.clientY - r.top) / r.height : (event.clientX - r.left) / r.width;
    };

    const nearest = (f: number) => {
      if (!scale) return Math.min(count - 1, Math.max(0, Math.floor(f * count)));
      let best = -1;
      for (let i = 0; i < positions.length; i++) {
        if (!Number.isNaN(positions[i]) && (best < 0 || Math.abs(positions[i] - f) < Math.abs(positions[best] - f))) best = i;
      }
      return best;
    };

    const show = (index: number, pointerY: number | null) => {
      if (index < 0 || index >= tips.length) return;
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
      const size = scale ? 0 : 1 / count;
      const start = scale ? pos : index / count;
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
      const k = order.indexOf(current);
      return order[k < 0 ? (by > 0 ? 0 : order.length - 1) : Math.min(order.length - 1, Math.max(0, k + by))];
    };
    // Focus from the keys shows the tooltip; a click's focus leaves it with the pointer.
    frame.addEventListener(
      "focus",
      () => {
        if (!frame.matches(":focus-visible")) return;
        byKeys = true;
        show(current >= 0 ? current : order[0], null);
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
            next = step((event.key === "PageDown" ? 1 : -1) * Math.max(1, Math.round(order.length / 10)));
            break;
          case "Home":
            next = order[0];
            break;
          case "End":
            next = order[order.length - 1];
            break;
          case "Escape":
            hide();
            return;
          default:
            return;
        }
        event.preventDefault();
        show(next, null);
        announce();
      },
      { signal }
    );
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
    if (!plot || !frame || !tooltip || tips.length === 0) return;

    const hole = Number(this.dataset.ibPie) || 0;
    const spans = (this.dataset.ibPos ?? "").split(",").map((p) => (p ? p.split(" ").map(Number) : null));
    const order = spans.flatMap((s, i) => (s ? [i] : []));
    const slices = plot.querySelectorAll<SVGPathElement>("path[data-ib-i]");
    let current = -1;

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
        const r = (size / 2) * (hole ? (1 + hole) / 2 : 0.6);
        x = size / 2 + r * Math.sin(turn);
        y = size / 2 - r * Math.cos(turn);
      }
      const gap = 12;
      const tw = tooltip.offsetWidth;
      const th = tooltip.offsetHeight;
      const box = this.getBoundingClientRect();
      const offset = plot.getBoundingClientRect().left - box.left;
      const left = x + gap + tw <= box.width - offset ? x + gap : x - gap - tw;
      tooltip.style.left = `${Math.min(Math.max(left, -offset), box.width - offset - tw)}px`;
      tooltip.style.top = `${Math.min(Math.max(y - th / 2, 0), Math.max(0, size - th))}px`;
    };

    const hide = () => {
      tooltip.hidden = true;
      for (const s of slices) s.removeAttribute("data-ib-active");
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
        const index = spans.findIndex((s) => s !== null && turn >= s[0] && turn < s[1]);
        if (index >= 0) show(index, x, y);
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
        const k = order.indexOf(current);
        let next: number;
        switch (event.key) {
          case "ArrowRight":
          case "ArrowDown":
            next = order[Math.min(order.length - 1, k + 1)];
            break;
          case "ArrowLeft":
          case "ArrowUp":
            next = order[Math.max(0, k - 1)];
            break;
          case "Home":
            next = order[0];
            break;
          case "End":
            next = order[order.length - 1];
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
      if (described) frame.setAttribute("aria-describedby", described);
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
    // Names wrapped in their bands (ArgumentLabels All) all show.
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

    // Names that turn (ArgumentLabels Rotate) stay flat only when every one fits its band; the
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
  }
}

defineElement("ib-chart", Chart);
