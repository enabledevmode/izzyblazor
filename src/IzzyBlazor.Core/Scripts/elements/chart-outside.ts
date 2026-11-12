/**
 * A chart's buttons placed outside it, in a Tile's or Card's header (ChartResetZoom, ChartZoom,
 * ChartPause). Loaded by chart.ts where a page has one. A press on one is a press on the chart's
 * own matching button, which stays in the chart, hidden, with its logic; and each follows it:
 * Reset zoom shown while zoomed, zoom out and in disabled at their ends, Pause pressed.
 * (ChartDownload needs none of this: chart-export.ts finds its chart.)
 */

const INNER: Record<string, string> = {
  reset: ".ib-chart-reset",
  "zoom-out": '[data-ib-zoom-by="2"]',
  "zoom-in": '[data-ib-zoom-by="0.5"]',
  pause: ".ib-chart-pause",
};

/** The chart a button outside it acts on: the one data-ib-chart-for names, or the one in its Tile or Card. */
export function chartOf(button: Element, warn = false): HTMLElement | null {
  const id = button.getAttribute("data-ib-chart-for") || button.getAttribute("data-ib-chart-download");
  if (id) return document.getElementById(id);
  const own = button.closest<HTMLElement>("figure");
  if (own) return own;
  const charts = button.closest(".ib-tile, .ib-card")?.querySelectorAll<HTMLElement>("figure:has(> ib-chart)") ?? [];
  if (charts.length === 1) return charts[0];
  if (warn) console.warn(`IzzyBlazor: a chart button found ${charts.length ? "more than one chart" : "no chart"} in its tile or card; give it For, the chart's id.`, button);
  return null;
}

document.addEventListener("click", (event) => {
  const button = (event.target as Element).closest?.<HTMLButtonElement>("[data-ib-chart-act]");
  const inner = button && INNER[button.dataset.ibChartAct ?? ""];
  if (!inner || button.disabled) return;
  chartOf(button, true)?.querySelector<HTMLButtonElement>(`ib-chart ${inner}`)?.click();
});

/** Keeps the buttons outside a chart in step with its own. */
export function outside(chart: HTMLElement, signal: AbortSignal): void {
  const figure = chart.closest("figure");
  const mirror = () => {
    for (const button of document.querySelectorAll<HTMLButtonElement>("[data-ib-chart-act]")) {
      const act = button.dataset.ibChartAct ?? "";
      if (!INNER[act] || chartOf(button) !== figure) continue;
      const inner = chart.querySelector<HTMLButtonElement>(INNER[act]);
      button.hidden = !inner || (act === "reset" && !chart.hasAttribute("data-ib-zoomed"));
      if (!inner) continue;
      button.disabled = inner.disabled;
      if (act === "pause") button.setAttribute("aria-pressed", inner.getAttribute("aria-pressed") ?? "false");
    }
  };
  const watch = new MutationObserver(mirror);
  watch.observe(chart, { attributes: true, subtree: true, attributeFilter: ["data-ib-zoomed", "disabled", "aria-pressed"] });
  signal.addEventListener("abort", () => watch.disconnect());
  mirror();
}
