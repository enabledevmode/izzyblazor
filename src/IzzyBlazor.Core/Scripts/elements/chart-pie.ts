import type { PieDrawData, drawPie as DrawPie } from "./chart-draw-pie";

/**
 * A PieChart: the tooltip is for the slice under the pointer, found from its angle and distance
 * from the middle (data-ib-pos holds each slice's start and end as shares of the turn); the arrow
 * keys move between slices.
 */
export function pie(host: HTMLElement, signal: AbortSignal, keyboard: (frame: HTMLElement) => void, loadDraw: () => Promise<{ drawPie: typeof DrawPie }>): void {
  const plot = host.querySelector<HTMLElement>(".ib-pie-plot");
  const frame = host.querySelector<HTMLElement>(".ib-pie-frame");
  const tooltip = host.querySelector<HTMLElement>(".ib-chart-tooltip");
  const tips = host.querySelectorAll<HTMLElement>(".ib-chart-tips > [data-ib-tip]");
  const live = host.querySelector<HTMLElement>("[data-ib-live]");
  if (!plot || !frame) return;

  const hole = Number(host.dataset.ibPie) || 0;
  // A nested donut's rings, outer first, each as thick as the next; its slices are numbered
  // ring by ring (ring * count + item), and its positions come ring by ring, split by ";".
  const rings = Number(host.dataset.ibRings) || 1;
  const count = Number(host.dataset.ibCount) || 0;
  const thick = (1 - hole) / rings;
  let spans = (host.dataset.ibPos ?? "").split(";").flatMap((ring) => ring.split(",").map((p) => (p ? p.split(" ").map(Number) : null)));
  let order = spans.flatMap((s, i) => (s ? [i] : []));
  const ringOf = (index: number) => (rings > 1 && count ? Math.floor(index / count) : 0);
  const slices = plot.querySelectorAll<SVGPathElement>("path[data-ib-i]");
  let current = -1;
  // Set once the tooltip is: hides it when the legend hides its slice.
  let redrawn = () => {};

  // Legend: each item hides and shows its slices; the rest close up and their shares are of
  // what's shown, drawn here (chart-draw-pie.ts).
  const data: PieDrawData | null = host.dataset.ibDraw ? JSON.parse(host.dataset.ibDraw) : null;
  const toggles = [...host.querySelectorAll<HTMLElement>(".ib-pie-toggle")];
  const hidden = new Set<number>();
  const labeled = [...plot.querySelectorAll<HTMLElement>("span[data-ib-l]")].map((l) => Number(l.dataset.ibL));
  const redraw = async () => {
    if (!data) return;
    const out = (await loadDraw()).drawPie(data, hidden, labeled);
    if (signal.aborted) return;
    for (const path of slices) {
      const slice = out.slices[Number(path.dataset.ibI)];
      path.toggleAttribute("data-ib-hidden", !slice);
      if (slice) path.setAttribute("d", slice.d);
    }
    spans = out.slices.map((s) => (s ? [s.start, s.end] : null));
    order = spans.flatMap((s, i) => (s ? [i] : []));
    // Hidden items' shares go blank: the legend still names them, dimmed.
    for (const el of host.querySelectorAll<HTMLElement>("[data-ib-share]")) el.textContent = out.shares[Number(el.dataset.ibShare)] ?? "";
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

  host.setAttribute("data-ib-tooltips", "");
  signal.addEventListener("abort", () => host.removeAttribute("data-ib-tooltips"));

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
    const box = host.getBoundingClientRect();
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

  keyboard(frame);
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
