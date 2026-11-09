import { arrived, followDrill } from "./chart-drill";

/**
 * A TreemapChart's script, loaded by <ib-chart> when it holds one (data-ib-treemap): the tooltip
 * for the tile or group name under the pointer, the tile outlined; and the keyboard (the tiles
 * are one Tab stop; the arrow keys move to the nearest tile that way and the tooltip's text goes
 * to a live region).
 *
 * The boxes take no pointer events while this runs (treemap.css), so their own hover titles stay
 * quiet: what's under the pointer is found by the boxes' bounds.
 *
 * Nothing Blazor renders is changed but attributes Blazor doesn't set (data-ib-active, role,
 * tabindex) and the tooltip's and live region's contents.
 */

// What a chart keeps when the server draws it again (an interactive page's new data): the tile the
// keys were on and whether the tiles had focus. Only a restart from this chart's own observer
// keeps them; enhanced navigation may reuse the element for another chart.
type Kept = { tile: string; focused: boolean };
const keptBy = new WeakMap<HTMLElement, Kept>();
const keeping = new WeakSet<HTMLElement>();

export function treemap(chart: HTMLElement, signal: AbortSignal, keyboard: (frame: HTMLElement) => void, restart: () => void): void {
  const frame = chart.querySelector<HTMLElement>(".ib-treemap-plot");
  const tooltip = frame?.querySelector<HTMLElement>(".ib-chart-tooltip");
  const live = chart.querySelector<HTMLElement>("[data-ib-live]");
  if (!frame) return;
  const kept = keeping.delete(chart) ? keptBy.get(chart) : undefined;
  const state: Kept = { tile: "", focused: false };
  keptBy.set(chart, state);

  // A new draw from the server sets up again: what it draws (boxes, their text and colors, the
  // tooltips' contents, parts that come and go), not the tooltip's own moves.
  const changed = new MutationObserver(() => {
    keeping.add(chart);
    restart();
  });
  changed.observe(chart, { childList: true });
  changed.observe(frame, { childList: true });
  for (const part of chart.querySelectorAll(".ib-treemap-group, .ib-chart-tips")) changed.observe(part, { childList: true, subtree: true, characterData: true, attributeFilter: ["style", "class"] });
  signal.addEventListener("abort", () => changed.disconnect());

  const tips = new Map<string, HTMLElement>();
  for (const tip of chart.querySelectorAll<HTMLElement>(".ib-chart-tips > [data-ib-tip]")) tips.set(tip.dataset.ibTip!, tip);
  // Tiles in the order the keys visit them (group by group, largest first), and the group names.
  const tiles = [...frame.querySelectorAll<HTMLElement>("[data-ib-tile]")];
  const heads = [...frame.querySelectorAll<HTMLElement>("[data-ib-group]")];
  if (tiles.length === 0 || !tooltip) return;
  chart.setAttribute("data-ib-tooltips", "");
  signal.addEventListener("abort", () => chart.removeAttribute("data-ib-tooltips"));

  let current: HTMLElement | undefined;
  const hide = () => {
    current?.removeAttribute("data-ib-active");
    current = undefined;
    tooltip.hidden = true;
  };
  const key = (el: HTMLElement) => (el.dataset.ibTile !== undefined ? `t${el.dataset.ibTile}` : `g${el.dataset.ibGroup}`);

  // Outlines a tile or group name and shows its tooltip by it: beside the pointer at (x, y) in the
  // plot, or (from the keys) under the box, or over it where there's no room below.
  const show = (el: HTMLElement, x?: number, y?: number) => {
    const tip = tips.get(key(el));
    if (el !== current) {
      current?.removeAttribute("data-ib-active");
      current = el;
      el.setAttribute("data-ib-active", "");
      if (tip) tooltip.innerHTML = tip.innerHTML;
    }
    if (!tip) {
      tooltip.hidden = true;
      return;
    }
    tooltip.hidden = false;
    const gap = 14;
    const width = frame.clientWidth;
    const height = frame.clientHeight;
    const tw = tooltip.offsetWidth;
    const th = tooltip.offsetHeight;
    let left: number;
    let top: number;
    if (x !== undefined && y !== undefined) {
      left = x + gap + tw > width ? x - gap - tw : x + gap;
      top = y - th / 2;
    } else {
      const box = frame.getBoundingClientRect();
      const r = el.getBoundingClientRect();
      left = r.left - box.left + r.width / 2 - tw / 2;
      top = r.bottom - box.top + 6 + th > height ? r.top - box.top - 6 - th : r.bottom - box.top + 6;
    }
    tooltip.style.left = `${Math.max(0, Math.min(left, width - tw))}px`;
    tooltip.style.top = `${Math.max(0, Math.min(top, height - th))}px`;
  };

  const under = (cx: number, cy: number) => {
    for (const el of [...heads, ...tiles]) {
      const r = el.getBoundingClientRect();
      if (cx >= r.left && cx < r.right && cy >= r.top && cy < r.bottom) return el;
    }
    return undefined;
  };

  // Who moved the tooltip last: the keys keep it when the pointer leaves; the pointer doesn't.
  let byKeys = false;
  frame.addEventListener(
    "pointermove",
    (event) => {
      byKeys = false;
      const el = under(event.clientX, event.clientY);
      if (!el) return hide();
      const box = frame.getBoundingClientRect();
      show(el, event.clientX - box.left, event.clientY - box.top);
    },
    { signal }
  );
  frame.addEventListener("pointerleave", () => byKeys || hide(), { signal });
  // Drill-down: a tile selected shows the level below it.
  frame.addEventListener(
    "click",
    (event) => {
      const el = under(event.clientX, event.clientY);
      if (el) followDrill(chart, Number(el.dataset.ibTile), false);
    },
    { signal }
  );

  let at = -1;
  const go = (i: number) => {
    at = i;
    show(tiles[i]);
    if (live) {
      // "Tokyo, 37,400,000, 31% of Asia, 9% of all": what the tooltip says.
      live.textContent = [...tooltip.querySelectorAll("*")]
        .filter((e) => e.children.length === 0)
        .map((e) => (e.textContent ?? "").trim())
        .filter(Boolean)
        .join(", ");
    }
  };

  // The nearest tile that way: past this one's edge, closest by the gap between them, then by how
  // far apart they are across (a tile beside this one before one further up or down).
  const toward = (i: number, dir: "left" | "right" | "up" | "down") => {
    const a = tiles[i].getBoundingClientRect();
    let best = -1;
    let score = Infinity;
    tiles.forEach((el, k) => {
      if (k === i) return;
      const b = el.getBoundingClientRect();
      const along = dir === "right" ? b.left - a.right : dir === "left" ? a.left - b.right : dir === "down" ? b.top - a.bottom : a.top - b.bottom;
      if (along < -1) return;
      const across =
        dir === "left" || dir === "right"
          ? Math.max(0, b.top - a.bottom, a.top - b.bottom) * 2 + Math.abs(b.top + b.height / 2 - (a.top + a.height / 2)) / 10
          : Math.max(0, b.left - a.right, a.left - b.right) * 2 + Math.abs(b.left + b.width / 2 - (a.left + a.width / 2)) / 10;
      const s = Math.max(0, along) + across;
      if (s < score) {
        score = s;
        best = k;
      }
    });
    return best;
  };

  keyboard(frame);
  arrived(chart, frame, live);
  document.addEventListener("keydown", (event) => event.key === "Escape" && !tooltip.hidden && hide(), { signal });
  frame.addEventListener(
    "focus",
    () => {
      if (!frame.matches(":focus-visible")) return;
      byKeys = true;
      go(at >= 0 ? at : 0);
    },
    { signal }
  );
  frame.addEventListener("blur", hide, { signal });
  frame.addEventListener(
    "keydown",
    (event) => {
      const i = at >= 0 ? at : 0;
      // Physical directions, as the tiles are: the plot doesn't turn for a right-to-left page.
      const dirs: Record<string, "left" | "right" | "up" | "down"> = { ArrowRight: "right", ArrowLeft: "left", ArrowDown: "down", ArrowUp: "up" };
      let next: number;
      if (event.key in dirs) next = toward(i, dirs[event.key]);
      else if (event.key === "Home") next = 0;
      else if (event.key === "End") next = tiles.length - 1;
      else if (event.key === "Escape") return hide();
      else if (event.key === "Enter") {
        if (at >= 0 && followDrill(chart, Number(tiles[at].dataset.ibTile), true)) event.preventDefault();
        return;
      } else return;
      event.preventDefault();
      byKeys = true;
      go(next >= 0 ? next : i);
    },
    { signal }
  );

  // Before a new draw: nothing left outlined, and where the keys and focus were, to restore.
  signal.addEventListener("abort", () => {
    state.tile = byKeys && !tooltip.hidden && at >= 0 ? (tiles[at].dataset.ibTile ?? "") : "";
    state.focused = document.activeElement === frame;
    hide();
  });
  if (kept?.focused) {
    byKeys = true;
    frame.focus();
  }
  const back = kept?.tile ? tiles.findIndex((t) => t.dataset.ibTile === kept.tile) : -1;
  if (back >= 0) go(back);
}
