/**
 * A RadarChart's script, loaded by <ib-chart> when it holds one (data-ib-radar, the spoke count;
 * data-ib-start, where the first spoke points as a share of the turn): the tooltip for the
 * spoke nearest the pointer, with every series' value there; the keyboard (the plot is one Tab
 * stop; the arrow keys move from spoke to spoke and the tooltip's text goes to a live region);
 * and legend items that hide and show their series.
 *
 * Nothing Blazor renders is changed but attributes Blazor doesn't set (data-ib-hidden,
 * data-ib-active, role, tabindex, aria-pressed) and the tooltip's and live region's contents.
 */

// The series a chart's legend hides, kept when the server draws the same chart again.
const hiddenBy = new WeakMap<HTMLElement, Set<number>>();

export function radar(chart: HTMLElement, signal: AbortSignal, keyboard: (frame: HTMLElement) => void, restart: () => void): void {
  const plot = chart.querySelector<HTMLElement>(".ib-radar-plot");
  const frame = chart.querySelector<HTMLElement>(".ib-radar-frame");
  if (!plot || !frame) return;
  const n = Number(chart.dataset.ibRadar) || 0;
  const start = Number(chart.dataset.ibStart) || 0;

  // A new draw from the server (an interactive page's new data) sets up again.
  const changed = new MutationObserver(restart);
  const svg = frame.querySelector(".ib-radar-svg");
  if (svg) changed.observe(svg, { childList: true, subtree: true, attributeFilter: ["d", "x", "y"] });
  signal.addEventListener("abort", () => changed.disconnect());

  // Legend: each item hides and shows its series. The server says which it drew hidden.
  const toggles = [...chart.querySelectorAll<HTMLElement>(".ib-chart-toggle")];
  const drawn = toggles.filter((t) => t.hasAttribute("data-ib-hidden")).map((t) => Number(t.dataset.ibSeries));
  const hidden = hiddenBy.get(chart) ?? new Set<number>(drawn);
  hiddenBy.set(chart, hidden);
  let refresh = () => {};
  const apply = () => {
    for (const el of frame.querySelectorAll<Element>("[data-ib-series]")) el.toggleAttribute("data-ib-hidden", hidden.has(Number((el as HTMLElement).dataset.ibSeries)));
    for (const t of toggles) t.setAttribute("aria-pressed", String(!hidden.has(Number(t.dataset.ibSeries))));
    refresh();
  };
  const toggle = (t: HTMLElement) => {
    const s = Number(t.dataset.ibSeries);
    if (!hidden.delete(s)) hidden.add(s);
    apply();
  };
  for (const t of toggles) {
    t.setAttribute("role", "button");
    t.tabIndex = 0;
    t.addEventListener("click", () => toggle(t), { signal });
    t.addEventListener(
      "keydown",
      (event) => {
        if (event.key !== "Enter" && event.key !== " ") return;
        event.preventDefault();
        toggle(t);
      },
      { signal }
    );
  }
  apply();
  signal.addEventListener("abort", () => {
    for (const t of toggles) for (const name of ["role", "tabindex", "aria-pressed"]) t.removeAttribute(name);
  });

  const tooltip = frame.querySelector<HTMLElement>(".ib-chart-tooltip");
  const tips = chart.querySelectorAll<HTMLElement>(".ib-chart-tips > [data-ib-tip]");
  const live = chart.querySelector<HTMLElement>("[data-ib-live]");
  if (!tooltip || tips.length === 0 || n === 0) return;
  chart.setAttribute("data-ib-tooltips", "");
  signal.addEventListener("abort", () => chart.removeAttribute("data-ib-tooltips"));

  let current = -1;
  const active: Element[] = [];
  // The visible markers at a spoke.
  const marksAt = (k: number) => [...frame.querySelectorAll<SVGElement>(`.ib-radar-marks:not([data-ib-hidden]) > [data-ib-i="${k}"]`)];

  const show = (k: number) => {
    current = k;
    tooltip.innerHTML = tips[k].innerHTML;
    for (const row of tooltip.querySelectorAll<HTMLElement>("[data-ib-series]")) if (hidden.has(Number(row.dataset.ibSeries))) row.remove();
    for (const el of active.splice(0)) el.removeAttribute("data-ib-active");
    const marks = marksAt(k);
    const spoke = frame.querySelector(`.ib-radar-spoke[data-ib-i="${k}"]`);
    for (const el of spoke ? [spoke, ...marks] : marks) {
      el.setAttribute("data-ib-active", "");
      active.push(el);
    }
    tooltip.hidden = false;

    // Beside the spoke's outermost value (or its middle), on the spoke's outer side; kept inside the chart.
    const size = plot.clientWidth;
    const turn = (start + k / n) * 2 * Math.PI;
    const sin = Math.sin(turn);
    const cos = Math.cos(turn);
    const r = Math.max(0.5, ...marks.map((m) => Math.hypot(parseFloat(m.getAttribute("x") ?? "50") - 50, parseFloat(m.getAttribute("y") ?? "50") - 50) / 50));
    const x = size / 2 + (size / 2) * r * sin;
    const y = size / 2 - (size / 2) * r * cos;
    const gap = 12;
    const tw = tooltip.offsetWidth;
    const th = tooltip.offsetHeight;
    const box = chart.getBoundingClientRect();
    const offset = plot.getBoundingClientRect().left - box.left;
    const right = sin >= 0 ? x + gap : x - gap - tw;
    const left = right + tw > box.width - offset ? x - gap - tw : right < -offset ? x + gap : right;
    tooltip.style.left = `${Math.max(-offset, Math.min(left, box.width - offset - tw))}px`;
    tooltip.style.top = `${Math.min(Math.max(y - th / 2, 0), Math.max(0, size - th))}px`;
  };
  const hide = () => {
    tooltip.hidden = true;
    for (const el of active.splice(0)) el.removeAttribute("data-ib-active");
  };
  refresh = () => {
    if (current >= 0 && !tooltip.hidden) show(current);
  };

  // Who moved the tooltip last: the keys keep it when the pointer leaves; the pointer doesn't.
  let byKeys = false;
  plot.addEventListener(
    "pointermove",
    (event) => {
      byKeys = false;
      const box = frame.getBoundingClientRect();
      const dx = event.clientX - box.left - box.width / 2;
      const dy = event.clientY - box.top - box.height / 2;
      // Over the circle and its names: the spoke whose wedge the pointer is in.
      if (Math.hypot(dx, dy) > (box.width / 2) * 1.25) return hide();
      const turn = (Math.atan2(dx, -dy) / (2 * Math.PI) - start + 2) % 1;
      const k = Math.round(turn * n) % n;
      if (k !== current || tooltip.hidden) show(k);
    },
    { signal }
  );
  plot.addEventListener("pointerleave", () => byKeys || hide(), { signal });

  keyboard(frame);
  document.addEventListener("keydown", (event) => event.key === "Escape" && !tooltip.hidden && hide(), { signal });
  const announce = () => {
    if (!live) return;
    // "Camera, Phone A, 82, Phone B, 64": what the tooltip says.
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
      show(current >= 0 ? current : 0);
      announce();
    },
    { signal }
  );
  frame.addEventListener("blur", hide, { signal });
  frame.addEventListener(
    "keydown",
    (event) => {
      const k = current >= 0 ? current : 0;
      let next: number;
      switch (event.key) {
        case "ArrowRight":
        case "ArrowDown":
          next = (k + 1) % n;
          break;
        case "ArrowLeft":
        case "ArrowUp":
          next = (k - 1 + n) % n;
          break;
        case "Home":
          next = 0;
          break;
        case "End":
          next = n - 1;
          break;
        case "Escape":
          hide();
          return;
        default:
          return;
      }
      event.preventDefault();
      byKeys = true;
      show(next);
      announce();
    },
    { signal }
  );
}
