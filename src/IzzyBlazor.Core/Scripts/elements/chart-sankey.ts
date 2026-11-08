/**
 * A SankeyChart's script, loaded by <ib-chart> when it holds one (data-ib-sankey, the nodes in
 * the order the keys visit them: column by column, top to bottom): the tooltip for the flow or
 * node under the pointer, with its flows stronger; and the keyboard (the diagram is one Tab stop; the
 * arrow keys move from node to node and the tooltip's text goes to a live region).
 *
 * The shapes take no pointer events while this runs (sankey.css), so their own hover titles stay
 * quiet: flows are found under the pointer by their fill, nodes and labels by their boxes.
 *
 * Nothing Blazor renders is changed but attributes Blazor doesn't set (data-ib-active,
 * role, tabindex) and the tooltip's and live region's contents.
 */

// What a chart keeps when the server draws it again (an interactive page's new data): the node
// the keys were on and whether the diagram had focus. Only a restart from this chart's own
// observer keeps them; enhanced navigation may reuse the element for another chart.
type Kept = { node: number; focused: boolean };
const keptBy = new WeakMap<HTMLElement, Kept>();
const keeping = new WeakSet<HTMLElement>();

export function sankey(chart: HTMLElement, signal: AbortSignal, keyboard: (frame: HTMLElement) => void, restart: () => void): void {
  const frame = chart.querySelector<HTMLElement>(".ib-sankey-plot");
  const svg = frame?.querySelector<SVGSVGElement>(".ib-sankey-links");
  const tooltip = frame?.querySelector<HTMLElement>(".ib-chart-tooltip");
  const live = chart.querySelector<HTMLElement>("[data-ib-live]");
  if (!frame || !svg) return;
  const kept = keeping.delete(chart) ? keptBy.get(chart) : undefined;
  const state: Kept = { node: -1, focused: false };
  keptBy.set(chart, state);

  // A new draw from the server sets up again.
  const changed = new MutationObserver(() => {
    keeping.add(chart);
    restart();
  });
  // Only what the server draws (the tooltip's own moves, its style, mustn't count): the flows, the
  // bars and labels, their text, the tooltips' contents, and parts that come and go (a chart drawn
  // empty first, or Tooltip turned on).
  changed.observe(chart, { childList: true });
  changed.observe(frame, { childList: true });
  changed.observe(svg, { childList: true, subtree: true, attributeFilter: ["d"] });
  for (const part of chart.querySelectorAll(".ib-sankey-nodes, .ib-sankey-labels, .ib-chart-tips")) changed.observe(part, { childList: true, subtree: true, characterData: true, attributeFilter: ["style"] });
  signal.addEventListener("abort", () => changed.disconnect());

  const tips = new Map<string, HTMLElement>();
  for (const tip of chart.querySelectorAll<HTMLElement>(".ib-chart-tips > [data-ib-tip]")) tips.set(tip.dataset.ibTip!, tip);
  const links = [...svg.querySelectorAll<SVGPathElement>("[data-ib-link]")];
  // A flow, its gradient (LinkColor Gradient), which holds its color, and its copy over the rest.
  const band = (link: Element | undefined) => {
    const i = (link as SVGElement | undefined)?.dataset.ibLink;
    mark(link);
    mark(svg.querySelector(`[data-ib-grad="${i}"]`) ?? undefined);
    mark(svg.querySelector(`[data-ib-raise="${i}"]`) ?? undefined);
  };
  const nodes = new Map<number, HTMLElement>();
  for (const el of frame.querySelectorAll<HTMLElement>(".ib-sankey-node")) nodes.set(Number(el.dataset.ibNode), el);
  const labels = new Map<number, HTMLElement>();
  for (const el of frame.querySelectorAll<HTMLElement>(".ib-sankey-label")) labels.set(Number(el.dataset.ibNode), el);
  const order = (chart.dataset.ibSankey ?? "").split(" ").filter(Boolean).map(Number);
  if (order.length === 0) return;
  chart.setAttribute("data-ib-tooltips", "");
  signal.addEventListener("abort", () => chart.removeAttribute("data-ib-tooltips"));

  const active: Element[] = [];
  const mark = (el: Element | undefined) => {
    if (!el) return;
    el.setAttribute("data-ib-active", "");
    active.push(el);
  };
  let current = "";
  const hide = () => {
    current = "";
    if (tooltip) tooltip.hidden = true;
    for (const el of active.splice(0)) el.removeAttribute("data-ib-active");
  };

  // Strengthens a node's ("n3") flows or a flow ("l5"), and shows its tooltip, if the chart has
  // them, at (x, y) in the plot, on the side with room.
  const show = (key: string, x: number, y: number, before = false) => {
    const tip = tips.get(key);
    if (key !== current) {
      current = key;
      if (tooltip && tip) tooltip.innerHTML = tip.innerHTML;
      for (const el of active.splice(0)) el.removeAttribute("data-ib-active");
      const i = Number(key.slice(1));
      if (key[0] === "l") {
        band(links.find((l) => l.dataset.ibLink === String(i)));
      } else {
        for (const link of links) if (link.dataset.ibFrom === String(i) || link.dataset.ibTo === String(i)) band(link);
      }
    }
    if (!tooltip || !tip) return;
    tooltip.hidden = false;
    const gap = 14;
    const width = frame.clientWidth;
    const height = frame.clientHeight;
    const tw = tooltip.offsetWidth;
    const th = tooltip.offsetHeight;
    const right = x + gap;
    const left = before || right + tw > width ? x - gap - tw : right;
    tooltip.style.left = `${Math.max(0, Math.min(left, width - tw))}px`;
    tooltip.style.top = `${Math.max(0, Math.min(y - th / 2, height - th))}px`;
  };

  const inside = (el: HTMLElement | undefined, cx: number, cy: number, pad: number) => {
    if (!el) return false;
    const r = el.getBoundingClientRect();
    return cx >= r.left - pad && cx <= r.right + pad && cy >= r.top - pad && cy <= r.bottom + pad;
  };

  // Who moved the tooltip last: the keys keep it when the pointer leaves; the pointer doesn't.
  let byKeys = false;
  frame.addEventListener(
    "pointermove",
    (event) => {
      byKeys = false;
      const box = frame.getBoundingClientRect();
      const x = event.clientX - box.left;
      const y = event.clientY - box.top;
      for (const [i, el] of nodes) if (inside(el, event.clientX, event.clientY, 3) || inside(labels.get(i), event.clientX, event.clientY, 0)) return show(`n${i}`, x, y);
      const ctm = svg.getScreenCTM();
      if (ctm) {
        const point = new DOMPoint(event.clientX, event.clientY).matrixTransform(ctm.inverse());
        // The narrowest on top, as drawn.
        for (let k = links.length - 1; k >= 0; k--) if (links[k].isPointInFill(point)) return show(`l${links[k].dataset.ibLink}`, x, y);
      }
      hide();
    },
    { signal }
  );
  frame.addEventListener("pointerleave", () => byKeys || hide(), { signal });

  // The keys come with the tooltips: a node at a time, its tooltip beside its bar.
  if (!tooltip) {
    signal.addEventListener("abort", hide);
    return;
  }
  let at = -1;
  const column = (i: number) => Math.round(nodes.get(i)?.getBoundingClientRect().left ?? 0);
  const middle = (i: number) => {
    const r = nodes.get(i)?.getBoundingClientRect();
    return r ? r.top + r.height / 2 : 0;
  };
  const go = (i: number) => {
    at = i;
    const el = nodes.get(i);
    if (!el) return;
    const box = frame.getBoundingClientRect();
    const r = el.getBoundingClientRect();
    const last = column(i) === Math.max(...order.map(column));
    show(`n${i}`, (last ? r.left : r.right) - box.left, r.top + r.height / 2 - box.top, last);
    if (live) {
      // "Housing, $1,700, from Budget, $1,700": what the tooltip says.
      live.textContent = [...tooltip.querySelectorAll("*")]
        .filter((e) => e.children.length === 0)
        .map((e) => (e.textContent ?? "").trim())
        .filter(Boolean)
        .join(", ");
    }
  };

  keyboard(frame);
  document.addEventListener("keydown", (event) => event.key === "Escape" && !tooltip.hidden && hide(), { signal });
  frame.addEventListener(
    "focus",
    () => {
      if (!frame.matches(":focus-visible")) return;
      byKeys = true;
      go(at >= 0 ? at : order[0]);
    },
    { signal }
  );
  frame.addEventListener("blur", hide, { signal });
  frame.addEventListener(
    "keydown",
    (event) => {
      const i = at >= 0 ? at : order[0];
      const here = column(i);
      const sameColumn = order.filter((k) => column(k) === here);
      const columns = [...new Set(order.map(column))].sort((a, b) => a - b);
      const nearest = (x: number) => {
        const there = order.filter((k) => column(k) === x);
        const y = middle(i);
        return there.reduce((best, k) => (Math.abs(middle(k) - y) < Math.abs(middle(best) - y) ? k : best), there[0]);
      };
      let next: number | undefined;
      switch (event.key) {
        case "ArrowDown":
          next = sameColumn[sameColumn.indexOf(i) + 1];
          break;
        case "ArrowUp":
          next = sameColumn[sameColumn.indexOf(i) - 1];
          break;
        case "ArrowRight": {
          const c = columns[columns.indexOf(here) + 1];
          next = c === undefined ? undefined : nearest(c);
          break;
        }
        case "ArrowLeft": {
          const c = columns[columns.indexOf(here) - 1];
          next = c === undefined ? undefined : nearest(c);
          break;
        }
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
      byKeys = true;
      go(next ?? i);
    },
    { signal }
  );

  // Before a new draw: nothing left highlighted, and where the keys and focus were, to restore.
  signal.addEventListener("abort", () => {
    state.node = byKeys && !tooltip.hidden ? at : -1;
    state.focused = document.activeElement === frame;
    hide();
  });
  if (kept?.focused) {
    byKeys = true;
    frame.focus();
  }
  if (kept && kept.node >= 0 && nodes.has(kept.node)) go(kept.node);
}
