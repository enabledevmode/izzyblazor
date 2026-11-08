/**
 * A HeatmapChart's script, loaded by <ib-chart> when it holds one (data-ib-heat, the column
 * count): the tooltip for the cell under the pointer, the keyboard (the grid is one Tab stop; the
 * arrow keys move from cell to cell and the tooltip's text goes to a live region), legend keys
 * that hide and show the cells in their class, and the values in cells and the gaps between them
 * fitted to the width the grid really has (the server's container queries only estimate it).
 *
 * A CalendarHeatmap (data-ib-cal) is the same grid, a row per weekday and a column per week:
 * days outside its range (ib-heat-out) are skipped, the tooltip names the day (data-ib-dates),
 * Up and Down move a day and Left and Right a week.
 *
 * The cells are the grid's children in row order. Nothing Blazor renders is changed but
 * attributes Blazor doesn't set (data-ib-hidden, data-ib-active, data-ib-labels, data-ib-gaps,
 * role, tabindex, aria-pressed) and the tooltip's and live region's contents.
 */

// The classes a chart's legend hides, kept when the server draws the same chart again.
const hiddenBy = new WeakMap<HTMLElement, Set<number>>();

export function heat(
  chart: HTMLElement,
  signal: AbortSignal,
  keyboard: (frame: HTMLElement) => void,
  fit: (axis: HTMLElement) => void,
  restart: () => void
): void {
  const heatmap = chart.querySelector<HTMLElement>(".ib-heat");
  const grid = chart.querySelector<HTMLElement>(".ib-heat-grid");
  const frame = chart.querySelector<HTMLElement>(".ib-heat-frame");
  const bottom = chart.querySelector<HTMLElement>(".ib-chart-bottom");
  if (!heatmap || !grid || !frame) return;

  const cols = Number(chart.dataset.ibHeat) || 1;
  const cells = [...grid.children] as HTMLElement[];
  const indexOf = new Map(cells.map((c, i) => [c as Element, i]));
  const rows = [...chart.querySelectorAll<HTMLElement>(".ib-heat-rows > span")].map((r) => r.textContent ?? "");
  const columns: string[] = chart.dataset.ibColumns ? JSON.parse(chart.dataset.ibColumns) : [];
  const auto = chart.hasAttribute("data-ib-auto");
  const calendar = chart.hasAttribute("data-ib-cal");
  const dates: (string | null)[] = chart.dataset.ibDates ? JSON.parse(chart.dataset.ibDates) : [];
  const out = (i: number) => !cells[i] || cells[i].classList.contains("ib-heat-out");

  // A new draw from the server (an interactive page's new data) sets up again.
  const changed = new MutationObserver(restart);
  changed.observe(grid, { childList: true, subtree: true, characterData: true, attributeFilter: ["class", "style"] });
  signal.addEventListener("abort", () => changed.disconnect());

  // Values in cells where the widest fits its cell, and gaps where cells are 6px or wider.
  const canvas = document.createElement("canvas").getContext("2d");
  const measure = () => {
    const width = grid.clientWidth;
    if (width === 0) return;
    // From the grid's width, not a cell's (which the gaps set here change); a calendar's grid
    // already stops growing where its days reach --ib-cal-max.
    const cell = (width - 2 * (cols - 1)) / cols;
    chart.dataset.ibGaps = cell >= 6 ? "1" : "0";
    // Every weekday's name where a row is a line tall, else Monday, Wednesday and Friday where
    // two rows are, else none.
    const pitch = cell + (cell >= 6 ? 2 : 0);
    if (calendar) chart.dataset.ibRows = pitch >= 16 ? "1" : 2 * pitch >= 13 ? "0" : "-1";
    if (bottom) fit(bottom);
    if (!auto || !canvas) return;
    const style = getComputedStyle(chart.querySelector(".ib-heat-rows") ?? grid);
    canvas.font = `${style.fontSize} ${style.fontFamily}`;
    let widest = 0;
    for (const c of cells) if (c.textContent) widest = Math.max(widest, canvas.measureText(c.textContent).width);
    const height = cells[0]?.offsetHeight ?? 0;
    chart.dataset.ibLabels = widest + 8 <= cell && height >= 14 ? "1" : "0";
  };
  const resized = new ResizeObserver(measure);
  resized.observe(grid);
  signal.addEventListener("abort", () => {
    resized.disconnect();
    for (const name of ["data-ib-gaps", "data-ib-labels", "data-ib-rows", "data-ib-fit", "data-ib-turn"]) chart.removeAttribute(name);
  });

  // Legend: each class's key hides and shows its cells.
  const hidden = hiddenBy.get(chart) ?? new Set<number>();
  hiddenBy.set(chart, hidden);
  const keys = [...chart.querySelectorAll<HTMLElement>("[data-ib-class]")];
  const apply = () => {
    for (const c of cells) {
      const k = /ib-k(\d)/.exec(c.className);
      c.toggleAttribute("data-ib-hidden", !!k && hidden.has(Number(k[1]) - 1));
    }
    for (const key of keys) key.setAttribute("aria-pressed", String(!hidden.has(Number(key.dataset.ibClass))));
  };
  const toggle = (key: HTMLElement) => {
    const k = Number(key.dataset.ibClass);
    if (!hidden.delete(k)) hidden.add(k);
    apply();
  };
  for (const key of keys) {
    key.setAttribute("role", "button");
    key.tabIndex = 0;
    key.addEventListener("click", () => toggle(key), { signal });
    key.addEventListener(
      "keydown",
      (event) => {
        if (event.key !== "Enter" && event.key !== " ") return;
        event.preventDefault();
        toggle(key);
      },
      { signal }
    );
  }
  apply();
  signal.addEventListener("abort", () => {
    for (const key of keys) for (const name of ["role", "tabindex", "aria-pressed"]) key.removeAttribute(name);
    for (const c of cells) c.removeAttribute("data-ib-hidden");
  });

  const tooltip = chart.querySelector<HTMLElement>(".ib-chart-tooltip");
  const live = chart.querySelector<HTMLElement>("[data-ib-live]");
  if (!tooltip) return;
  chart.setAttribute("data-ib-tooltips", "");
  signal.addEventListener("abort", () => chart.removeAttribute("data-ib-tooltips"));

  let current = -1;
  // With Tooltip unset, the tooltip shows only while the values aren't in the cells (too narrow,
  // or Labels None); the keys still move from cell to cell and say each one.
  const autoTips = chart.dataset.ibTips === "auto";
  const wanted = () => !autoTips || getComputedStyle(grid).fontSize === "0px";
  // Who moved the tooltip last: the keys keep it when the pointer leaves; the pointer doesn't.
  let byKeys = false;

  // "Tuesday, 2 PM" over "Orders 32" (or your HeatmapTooltip's content).
  const fill = (i: number) => {
    const tip = chart.querySelector(`.ib-chart-tips > [data-ib-tip="${i}"]`);
    if (tip) {
      tooltip.innerHTML = tip.innerHTML;
      return;
    }
    const cell = cells[i];
    const title = document.createElement("div");
    title.className = "ib-chart-tooltip-title";
    title.textContent = calendar ? (dates[i] ?? "") : [rows[Math.floor(i / cols)], columns[i % cols]].filter(Boolean).join(", ");
    const row = document.createElement("div");
    row.className = "ib-chart-tooltip-row";
    const key = document.createElement("span");
    key.className = "ib-chart-tooltip-key";
    key.style.background = getComputedStyle(cell).backgroundColor;
    const name = document.createElement("span");
    name.textContent = chart.dataset.ibValue ?? "";
    const value = document.createElement("span");
    value.className = "ib-chart-tooltip-value";
    value.textContent = cell.textContent || chart.dataset.ibNone || "";
    row.append(key, name, value);
    tooltip.replaceChildren(title, row);
  };

  // At the cell's end side (its right, in a left-to-right page), or its start side where
  // there's no room; level with the cell, kept inside the chart.
  const show = (i: number) => {
    const cell = cells[i];
    if (!cell) return;
    if (i !== current || tooltip.hidden) {
      cells[current]?.removeAttribute("data-ib-active");
      current = i;
      cell.setAttribute("data-ib-active", "");
      fill(i);
    }
    if (!wanted()) {
      tooltip.hidden = true;
      return;
    }
    tooltip.hidden = false;
    const box = chart.getBoundingClientRect();
    const area = frame.getBoundingClientRect();
    const r = cell.getBoundingClientRect();
    const rtl = getComputedStyle(chart).direction === "rtl";
    const gap = 6;
    const tw = tooltip.offsetWidth;
    const th = tooltip.offsetHeight;
    const after = r.right - area.left + gap;
    const before = r.left - area.left - gap - tw;
    const offset = area.left - box.left;
    const fitsAfter = area.left + after + tw <= box.right;
    const fitsBefore = area.left + before >= box.left;
    const left = rtl ? (fitsBefore ? before : after) : fitsAfter || !fitsBefore ? after : before;
    tooltip.style.left = `${Math.max(-offset, Math.min(left, box.width - offset - tw))}px`;
    tooltip.style.top = `${Math.max(0, Math.min(r.top - area.top + r.height / 2 - th / 2, area.height - th))}px`;
  };

  const hide = () => {
    tooltip.hidden = true;
    cells[current]?.removeAttribute("data-ib-active");
  };

  const announce = () => {
    if (!live) return;
    // "Tuesday, 2 PM, Orders, 32": what the tooltip says.
    live.textContent = [...tooltip.querySelectorAll("*")]
      .filter((e) => e.children.length === 0)
      .map((e) => (e.textContent ?? "").trim())
      .filter(Boolean)
      .join(", ");
  };

  grid.addEventListener(
    "pointermove",
    (event) => {
      byKeys = false;
      // Only a new cell redraws the tooltip.
      const i = indexOf.get(event.target as Element) ?? -1;
      if (i === current && !tooltip.hidden) return;
      if (i < 0 || out(i) || !wanted()) return hide();
      show(i);
    },
    { signal }
  );
  grid.addEventListener("pointerleave", () => byKeys || hide(), { signal });

  keyboard(frame);
  document.addEventListener("keydown", (event) => event.key === "Escape" && !tooltip.hidden && hide(), { signal });
  frame.addEventListener(
    "focus",
    () => {
      if (!frame.matches(":focus-visible")) return;
      byKeys = true;
      show(current >= 0 ? current : first());
      announce();
    },
    { signal }
  );
  frame.addEventListener("blur", hide, { signal });

  // A calendar's days in date order: down each week's column, then the next week's.
  const day = (i: number) => (i % cols) * 7 + Math.floor(i / cols);
  const cellOf = (d: number) => (d % 7) * cols + Math.floor(d / 7);
  const first = () => {
    if (!calendar) return 0;
    for (let d = 0; d < cells.length; d++) if (!out(cellOf(d))) return cellOf(d);
    return 0;
  };
  const last = () => {
    if (!calendar) return cells.length - 1;
    for (let d = cells.length - 1; d >= 0; d--) if (!out(cellOf(d))) return cellOf(d);
    return cells.length - 1;
  };
  // The row's first or last day in range.
  const rowEnd = (i: number, end: boolean) => {
    const start = i - (i % cols);
    if (end) {
      for (let j = start + cols - 1; j > start; j--) if (!out(j)) return j;
      return start;
    }
    for (let j = start; j < start + cols - 1; j++) if (!out(j)) return j;
    return start + cols - 1;
  };
  frame.addEventListener(
    "keydown",
    (event) => {
      const rtl = getComputedStyle(chart).direction === "rtl";
      const i = current >= 0 ? current : first();
      const col = i % cols;
      let next: number;
      switch (event.key) {
        case "ArrowRight":
        case "ArrowLeft":
          next = (event.key === "ArrowRight") !== rtl ? (col < cols - 1 ? i + 1 : i) : col > 0 ? i - 1 : i;
          break;
        case "ArrowDown":
          next = calendar ? (day(i) + 1 < cells.length ? cellOf(day(i) + 1) : i) : i + cols < cells.length ? i + cols : i;
          break;
        case "ArrowUp":
          next = calendar ? (day(i) > 0 ? cellOf(day(i) - 1) : i) : i - cols >= 0 ? i - cols : i;
          break;
        case "Home":
          next = event.ctrlKey ? first() : calendar ? rowEnd(i, false) : i - col;
          break;
        case "End":
          next = event.ctrlKey ? last() : calendar ? rowEnd(i, true) : i - col + cols - 1;
          break;
        case "Escape":
          hide();
          return;
        default:
          return;
      }
      event.preventDefault();
      // Days outside a calendar's range aren't there to move to.
      if (out(next)) next = i;
      byKeys = true;
      show(next);
      announce();
    },
    { signal }
  );
}
