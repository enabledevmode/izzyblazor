import { IzzyElement, defineElement } from "../izzy-element";

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
  protected setup(signal: AbortSignal): void {
    const frame = this.querySelector<HTMLElement>(".ib-chart-frame");
    const plot = this.querySelector<HTMLElement>(".ib-chart-plot");
    const bottom = this.querySelector<HTMLElement>(".ib-chart-bottom");
    if (!frame || !plot) return;

    // Labels: show as many as fit, measured.
    if (bottom) {
      let frameId = 0;
      const refit = () => {
        cancelAnimationFrame(frameId);
        frameId = requestAnimationFrame(() => this.fit(bottom));
      };
      const observer = new ResizeObserver(refit);
      observer.observe(bottom);
      signal.addEventListener("abort", () => {
        observer.disconnect();
        cancelAnimationFrame(frameId);
      });
      refit();
    }

    // Legend: each item hides and shows its series.
    const hidden = new Set<number>();
    for (const toggle of this.querySelectorAll<HTMLElement>(".ib-chart-toggle")) {
      const series = Number(toggle.dataset.ibSeries);
      if (toggle.hasAttribute("data-ib-hidden")) hidden.add(series);
      toggle.setAttribute("role", "button");
      toggle.tabIndex = 0;
      toggle.setAttribute("aria-pressed", String(!hidden.has(series)));
    }

    // Draws the open tooltip again once it's set up below (a chart can have no tooltips).
    let refresh = () => {};
    const setHidden = (series: number, off: boolean) => {
      if (off) hidden.add(series);
      else hidden.delete(series);
      for (const el of this.querySelectorAll(`[data-ib-series="${series}"]:not(.ib-chart-tooltip-row)`)) {
        el.toggleAttribute("data-ib-hidden", off);
      }
      this.querySelector(`.ib-chart-toggle[data-ib-series="${series}"]`)?.setAttribute("aria-pressed", String(!off));
      refresh();
    };

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
    const marks = [...plot.querySelectorAll<SVGUseElement>("use[data-ib-i]")];
    const visible = (m: Element) => !m.closest("[data-ib-hidden]");
    const markFor = (index: number) => marks.find((m) => m.dataset.ibI === String(index) && visible(m));
    const at = (m: SVGUseElement) => [
      (parseFloat(m.getAttribute("x") ?? "0") / 100) * plot.clientWidth,
      (parseFloat(m.getAttribute("y") ?? "0") / 100) * plot.clientHeight,
    ];
    let active: SVGUseElement | undefined;

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

    plot.addEventListener(
      "pointermove",
      (event) => {
        const r = plot.getBoundingClientRect();
        if (pointsMode) {
          const px = event.clientX - r.left;
          const py = event.clientY - r.top;
          let best: SVGUseElement | undefined;
          let distance = Infinity;
          for (const m of marks) {
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
        if (document.activeElement !== frame) hide();
      },
      { signal }
    );

    // The keyboard: one Tab stop on the plot.
    frame.tabIndex = 0;
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
    frame.addEventListener(
      "focus",
      () => {
        show(current >= 0 ? current : order[0], null);
        announce();
      },
      { signal }
    );
    frame.addEventListener("blur", hide, { signal });
    frame.addEventListener(
      "keydown",
      (event) => {
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
   * Picks the most labels that fit along the bottom: the server marked each with the narrowest of
   * eight container widths it shows at (ib-chart-fromN), from an estimate; this measures them and
   * sets data-ib-fit to the widest set whose labels have room (chart.css shows that set).
   */
  private fit(axis: HTMLElement): void {
    const labels = [...axis.children] as HTMLElement[];
    if (labels.length === 0) return;
    const from = (l: HTMLElement) => Number(/ib-chart-from(\d)/.exec(l.className)?.[1] ?? 0);
    const width = axis.clientWidth;
    if (width === 0) return;

    // Show them all to measure them.
    this.dataset.ibFit = "7";
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

    this.dataset.ibFit = String(chosen);
  }
}

defineElement("ib-chart", Chart);
