import { format, spread, type NumberShape } from "./chart-draw";

// ---- Pies (ChartShapes.Pie, PieLabels, PieInside), drawn again without the slices the legend hides. ----

/** What a PieChart or NestedDonutChart sends (PieChartBase.DrawData). */
export interface PieDrawData {
  v: (number | null)[][]; // each ring's values, in item order
  r: [number, number][]; // each ring's inner and outer radius
  f: number; // the turn the first slice starts at
  p0: NumberShape; // shares of 9.5% and up
  p1: NumberShape; // shares under it
  l: number; // labels: 0 none, 1 outside, 2 inside
  s: number; // the circle's size in pixels, the outside labels are spread for
}

export interface PieLabelAt {
  end: boolean; // on the right side
  x: number; // where it starts, 0 to 1 of the circle's box
  y: number; // its middle
  leader: string;
}

export interface PieDrawing {
  slices: ({ start: number; end: number; d: string } | null)[]; // by ring * count + item
  shares: (string | null)[]; // the same
  labels: Map<number, PieLabelAt>; // labels beside the circle, by item
  inside: Map<number, { x: number; y: number }>; // shares in their slices, by item
}

const n4 = (v: number) => String(Math.round(v * 1e4) / 1e4 + 0);
const at = (turn: number, r: number): [number, number] => [r * Math.sin(turn * 2 * Math.PI), -r * Math.cos(turn * 2 * Math.PI)];

function slicePath(start: number, end: number, hole: number, outer: number): string {
  const o = n4(outer);
  if (end - start >= 0.99999) {
    let d = `M0,-${o}A${o},${o} 0 1,1 0,${o}A${o},${o} 0 1,1 0,-${o}Z`;
    if (hole > 0) {
      const h = n4(hole);
      d += `M0,${n4(-hole)}A${h},${h} 0 1,0 0,${h}A${h},${h} 0 1,0 0,${n4(-hole)}Z`;
    }
    return d;
  }
  const large = end - start > 0.5 ? 1 : 0;
  const [x0, y0] = at(start, outer);
  const [x1, y1] = at(end, outer);
  let d = `M${n4(x0)},${n4(y0)}A${o},${o} 0 ${large},1 ${n4(x1)},${n4(y1)}`;
  if (hole > 0) {
    const [ix1, iy1] = at(end, hole);
    const [ix0, iy0] = at(start, hole);
    d += `L${n4(ix1)},${n4(iy1)}A${n4(hole)},${n4(hole)} 0 ${large},0 ${n4(ix0)},${n4(iy0)}Z`;
  } else d += "L0,0Z";
  return d;
}

/** A share as the server writes it (ChartMembers.Share): one decimal under 9.5%, "<0.1%" for slivers. */
function share(d: PieDrawData, x: number): string {
  if (x > 0 && x < 0.0005) return "<" + format(d.p1, 0.001, 1);
  return x > 0 && x < 0.095 ? format(d.p1, x, 1) : format(d.p0, x, 1);
}

/**
 * The circle without the hidden items' slices: each ring's shown slices share it by their values,
 * clockwise from the start; their shares are of what's shown. Labels beside the circle are spread
 * again for the items in labeled (outer ring only, as a PieChart's).
 */
export function drawPie(d: PieDrawData, hidden: Set<number>, labeled: number[]): PieDrawing {
  const count = d.v[0]?.length ?? 0;
  const slices: PieDrawing["slices"] = [];
  const shares: PieDrawing["shares"] = [];
  d.v.forEach((values, ring) => {
    const [hole, outer] = d.r[ring];
    const shown = values.map((v, i) => (!hidden.has(i) && v !== null && v > 0 ? v : 0));
    const total = shown.reduce((a, b) => a + b, 0);
    let start = d.f;
    for (let i = 0; i < count; i++) {
      if (total <= 0 || shown[i] <= 0) {
        slices.push(null);
        shares.push(null);
        continue;
      }
      const end = Math.min(d.f + 1, start + shown[i] / total);
      slices.push({ start, end, d: slicePath(start, end, hole, outer) });
      shares.push(share(d, shown[i] / total));
      start = end;
    }
  });

  // Beside the circle: level with a point a little out from the slice's middle, nudged apart a
  // line each on its side (ChartShapes.PieLabels).
  const labels = new Map<number, PieLabelAt>();
  const reach = 1.3;
  const span = d.s * reach;
  const middle = (i: number) => (slices[i]!.start + slices[i]!.end) / 2;
  const withSlice = labeled.filter((i) => slices[i]);
  for (const end of [true, false]) {
    const side = withSlice.filter((i) => middle(i) % 1 < 0.5 === end);
    const centers = spread(side.map((i) => ((at(middle(i), 1.12)[1] + reach) / (2 * reach)) * span), span, 20);
    side.forEach((i, k) => {
      const [ex, ey] = at(middle(i), 1);
      const [bx, by] = at(middle(i), 1.08);
      const y = (centers[k] / span) * 2 * reach - reach;
      const x = Math.max(Math.sqrt(Math.max(0, 1.15 * 1.15 - y * y)), Math.abs(bx)) * (end ? 1 : -1);
      const tip = x + (end ? 0.06 : -0.06);
      labels.set(i, { end, x: (tip + 1) / 2, y: (y + 1) / 2, leader: `M${n4(ex)},${n4(ey)}L${n4(bx)},${n4(by)}L${n4(x)},${n4(y)}H${n4(tip)}` });
    });
  }

  const inside = new Map<number, { x: number; y: number }>();
  if (d.l === 2) {
    const hole = d.r[0]?.[0] ?? 0;
    for (let i = 0; i < count; i++) {
      if (!slices[i]) continue;
      const [x, y] = at(middle(i), hole > 0 ? (1 + hole) / 2 : 0.62);
      inside.set(i, { x: (x + 1) / 2, y: (y + 1) / 2 });
    }
  }
  return { slices, shares, labels, inside };
}
