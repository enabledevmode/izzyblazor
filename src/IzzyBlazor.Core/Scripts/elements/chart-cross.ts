/**
 * Chart's Crosshair, loaded where a chart has one: a line at the item shown and one at the value
 * nearest the pointer (snapped to it), with the item's argument and that value in badges on their
 * axes. The server writes each item's (data-ib-c on its tooltip): its argument, then each
 * series' place on its axis (0 to 1), plotted value, that value in the axis's format, 1 on a second axis (or 0), and on a stack its own value and text.
 */
type Cross = [string, ([number, number, string, number?, number?, string?] | null)[]];

/** What the chart drew last itself (chart-draw.ts): its value axis, and each series' plotted values. */
export type Drawn = { pos: (v: number) => number; hi: (number | null)[][] } | undefined;

// The elements reuse .ib-chart-hover (placed, no pointer, left out of downloads and print).
const LINE = "opacity:1;background:var(--ib-color-outline);";
const BADGE =
  "opacity:1;z-index:4;padding:0 6px;border-radius:var(--ib-radius-sm);background:var(--ib-color-on-surface);color:var(--ib-color-surface);font-size:12px;line-height:20px;white-space:nowrap;";

export function crosshair(chart: HTMLElement, plot: HTMLElement, tips: NodeListOf<HTMLElement>, hidden: Set<number>, drawn: () => Drawn, signal: AbortSignal) {
  const across = chart.hasAttribute("data-ib-horizontal");
  const right = chart.dataset.ibCross === "r";
  const parts = [0, 1, 2, 3].map(() => {
    const e = document.createElement("div");
    e.className = "ib-chart-hover";
    e.setAttribute("aria-hidden", "true");
    e.hidden = true;
    plot.append(e);
    return e;
  });
  signal.addEventListener("abort", () => parts.forEach((e) => e.remove()));
  const [argLine, valueLine, argBadge, valueBadge] = parts;
  // The series the value line was last on: the keys keep it as they move.
  let series = -1;

  return (index: number, pointerAt?: number | null, mark?: SVGElement, pos = 0) => {
    const c: Cross | null = index >= 0 && tips[index]?.dataset.ibC ? JSON.parse(tips[index].dataset.ibC!) : null;
    parts.forEach((e) => (e.hidden = !c));
    if (!c) return;
    const w = plot.clientWidth, h = plot.clientHeight;
    const d = drawn();
    // Each shown series' value: its place along the value axis in pixels, and its text. Drawn
    // again by the script, the first axis is its; a value it changed (a stack closed up) has no text here.
    const values = c[1].map((p, s) => {
      if (!p || hidden.has(s)) return null;
      const v = d ? (d.hi[s]?.[index] ?? null) : p[1];
      if (v === null) return null;
      const f = d && !p[3] ? d.pos(v) : p[0];
      return [across ? f * w : (1 - f) * h, v === p[1] ? p[2] : v === p[4] ? p[5]! : null, s, !!p[3]] as const;
    });
    // A point (scatter, bubble): both lines through it. Otherwise the series nearest the pointer,
    // or with the keys the one the line was on.
    let a: number, at: (typeof values)[number] | undefined;
    if (mark) {
      const x = (parseFloat(mark.getAttribute("x") ?? mark.getAttribute("cx") ?? "0") / 100) * w;
      const y = (parseFloat(mark.getAttribute("y") ?? mark.getAttribute("cy") ?? "0") / 100) * h;
      const s = Number(mark.closest<HTMLElement>("[data-ib-series]")?.dataset.ibSeries);
      a = across ? y : x;
      at = values[s] && [across ? x : y, values[s]![1], s, values[s]![3]];
    } else {
      a = pos * (across ? h : w);
      const shown = values.filter((v) => v !== null);
      at = pointerAt != null ? shown.sort((p, q) => Math.abs(p![0] - pointerAt) - Math.abs(q![0] - pointerAt))[0] : (values[series] ?? shown[0]);
    }
    if (at) series = at[2];
    // The argument's line and badge (along the bottom, or down the side across).
    argLine.style.cssText = LINE + (across ? `left:0;width:100%;height:1px;top:${a}px` : `top:0;height:100%;width:1px;left:${a}px`);
    argBadge.textContent = c[0];
    argBadge.style.cssText = BADGE + (across ? `right:100%;top:${a}px;transform:translate(-4px,-50%)` : `top:100%;left:${a}px;transform:translate(-50%,4px)`);
    // The value's, on its axis's side: a second axis's at the end.
    valueLine.hidden = valueBadge.hidden = !at;
    if (!at) return;
    const v = at[0];
    valueLine.style.cssText = LINE + (across ? `top:0;height:100%;width:1px;left:${v}px` : `left:0;width:100%;height:1px;top:${v}px`);
    valueBadge.hidden = at[1] === null;
    valueBadge.textContent = at[1];
    const end = right !== at[3];
    valueBadge.style.cssText =
      BADGE + (across ? `top:100%;left:${v}px;transform:translate(-50%,4px)` : `${end ? "left" : "right"}:100%;top:${v}px;transform:translate(${end ? 4 : -4}px,-50%)`);
  };
}
