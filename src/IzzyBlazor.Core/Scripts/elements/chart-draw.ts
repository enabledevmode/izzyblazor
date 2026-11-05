/**
 * Draws a Chart again without the series its legend hides, as the server would have: stacks close
 * up, grouped bars share the band among those left, and the value axis rescales. A port of
 * ChartLayout.Build's value side (ChartLayout.cs, ChartScale.cs); the argument axis doesn't change.
 * The server sends what it needs as JSON (Chart.razor's DrawData).
 */

/** A number's look, read by the server from the chart's .NET format: prefix, suffix, separators. */
export interface NumberShape {
  p: string; // before the digits
  s: string; // after them
  np: string; // before a negative number's digits
  ns: string; // after them
  g: string; // group separator, or "" for none
  d: string; // decimal separator
  n: number; // decimals; -1 picks them from the tick step
  t: boolean; // trailing zeros dropped ("#,##0.##")
  k: number; // scale (100 for percent)
}

export interface DrawSeries {
  t: number; // 0 bar, 1 line, 2 area, 3 scatter
  v: (number | null)[];
  c: number; // curve: 0 straight, 1 smooth, 2 step
  j: boolean; // connect nulls
  m: boolean; // markers
  k: number; // palette slot, 1 to 8
  y: number; // style: dash and marker shape
  d: number; // dash, 0 to 3
  col?: string; // the series' own color
}

export interface DrawData {
  h: boolean; // horizontal
  st: number; // 0 none, 1 stacked, 2 full
  lo: number | null; // ValueMin
  hi: number | null; // ValueMax
  f: NumberShape;
  a: (number | null)[]; // each item's place along the argument axis, 0 to 1
  mk: string; // the marker shapes' id prefix
  s: DrawSeries[];
}

// ---- Ticks: D3's (ChartScale.cs). ----

const E10 = Math.sqrt(50), E5 = Math.sqrt(10), E2 = Math.sqrt(2);

interface Spec { i1: number; i2: number; inc: number }

function tickSpec(start: number, stop: number, count: number): Spec {
  const step = (stop - start) / Math.max(0, count);
  const power = Math.floor(Math.log10(step));
  const error = step / Math.pow(10, power);
  const factor = error >= E10 ? 10 : error >= E5 ? 5 : error >= E2 ? 2 : 1;
  let inc: number, i1: number, i2: number;
  if (power < 0) {
    inc = Math.pow(10, -power) / factor;
    i1 = Math.round(start * inc);
    i2 = Math.round(stop * inc);
    if (i1 / inc < start) ++i1;
    if (i2 / inc > stop) --i2;
    inc = -inc;
  } else {
    inc = Math.pow(10, power) * factor;
    i1 = Math.round(start / inc);
    i2 = Math.round(stop / inc);
    if (i1 * inc < start) ++i1;
    if (i2 * inc > stop) --i2;
  }
  if (i2 < i1 && count >= 0.5 && count < 2) return tickSpec(start, stop, count * 2);
  return { i1, i2, inc };
}

const tickValue = (s: Spec, i: number) => (s.inc < 0 ? i / -s.inc : i * s.inc);
const tickStep = (s: Spec) => (s.inc < 0 ? 1 / -s.inc : s.inc);

function nice(min: number, max: number, count: number): [number, number] {
  if (!isFinite(min) || !isFinite(max)) return [0, 1];
  if (min === max) {
    const pad = min === 0 ? 1 : Math.abs(min) * 0.1;
    [min, max] = [min >= 0 && min - pad < 0 ? 0 : min - pad, max + pad];
  }
  let previous: number | undefined;
  for (let i = 0; i < 10; i++) {
    const spec = tickSpec(min, max, count);
    if (previous === spec.inc) break;
    previous = spec.inc;
    if (spec.inc > 0) {
      min = Math.floor(min / spec.inc) * spec.inc;
      max = Math.ceil(max / spec.inc) * spec.inc;
    } else {
      min = Math.ceil(min * spec.inc) / spec.inc;
      max = Math.floor(max * spec.inc) / spec.inc;
    }
  }
  return [min, max];
}

const decimalsFor = (step: number) => (step >= 1 || step <= 0 ? 0 : Math.min(10, Math.ceil(-Math.log10(step) - 1e-9)));

/** Formats a value as the chart's .NET format would, from the shape the server read off it. */
export function format(f: NumberShape, value: number, step: number): string {
  const decimals = f.n < 0 ? decimalsFor(step) : f.n;
  let text = (Math.abs(value) * f.k).toFixed(decimals);
  if (f.t && decimals > 0) text = text.replace(/\.?0+$/, "");
  let [whole, fraction] = text.split(".");
  if (f.g) whole = whole.replace(/\B(?=(\d{3})+(?!\d))/g, f.g);
  const digits = fraction ? whole + f.d + fraction : whole;
  return value < 0 && Number(text) !== 0 ? f.np + digits + f.ns : f.p + digits + f.s;
}

// ---- Curves (ChartLayout.Curve). Points are [along, value], 0 to 1. ----

type Pt = [number, number];
type Seg = [Pt | null, Pt | null, Pt];

function curve(p: Pt[], kind: number): { start: Pt; segs: Seg[] } {
  const segs: Seg[] = [];
  if (kind === 2) {
    for (let i = 1; i < p.length; i++) {
      const mid = (p[i - 1][0] + p[i][0]) / 2;
      segs.push([null, null, [mid, p[i - 1][1]]], [null, null, [mid, p[i][1]]], [null, null, p[i]]);
    }
  } else if (kind === 1 && p.length > 2) {
    // Monotone cubic (Fritsch–Carlson): never overshoots a value.
    const t = new Array<number>(p.length).fill(0);
    for (let i = 1; i < p.length - 1; i++) {
      const h0 = p[i][0] - p[i - 1][0], h1 = p[i + 1][0] - p[i][0];
      const s0 = h0 === 0 ? 0 : (p[i][1] - p[i - 1][1]) / h0, s1 = h1 === 0 ? 0 : (p[i + 1][1] - p[i][1]) / h1;
      const m = (s0 * h1 + s1 * h0) / (h0 + h1);
      t[i] = (Math.sign(s0) + Math.sign(s1)) * Math.min(Math.abs(s0), Math.abs(s1), 0.5 * Math.abs(m));
    }
    const end = (a: number, b: number, tb: number) => {
      const h = p[b][0] - p[a][0];
      return h === 0 ? tb : (3 * (p[b][1] - p[a][1]) / h - tb) / 2;
    };
    t[0] = end(0, 1, t[1]);
    t[p.length - 1] = end(p.length - 2, p.length - 1, t[p.length - 2]);
    for (let i = 1; i < p.length; i++) {
      const dx = (p[i][0] - p[i - 1][0]) / 3;
      segs.push([[p[i - 1][0] + dx, p[i - 1][1] + dx * t[i - 1]], [p[i][0] - dx, p[i][1] - dx * t[i]], p[i]]);
    }
  } else {
    for (let i = 1; i < p.length; i++) segs.push([null, null, p[i]]);
  }
  return { start: p[0], segs };
}

function reverse(c: { start: Pt; segs: Seg[] }): { start: Pt; segs: Seg[] } {
  const points = [c.start, ...c.segs.map((s) => s[2])];
  const segs: Seg[] = [];
  for (let i = c.segs.length - 1; i >= 0; i--) segs.push([c.segs[i][1], c.segs[i][0], points[i]]);
  return { start: points[points.length - 1], segs };
}

const r1 = (n: number) => Math.round(n * 10) / 10;

function path(c: { start: Pt; segs: Seg[] }, horizontal: boolean, move: boolean): string {
  const pt = ([a, v]: Pt) => (horizontal ? `${r1(v * 1000)},${r1(a * 1000)}` : `${r1(a * 1000)},${r1((1 - v) * 1000)}`);
  let d = (move ? "M" : "L") + pt(c.start);
  for (const [c1, c2, to] of c.segs) d += (c1 && c2 ? `C${pt(c1)} ${pt(c2)} ` : "L") + pt(to);
  return d;
}

const esc = (s: string) => s.replace(/&/g, "&amp;").replace(/"/g, "&quot;").replace(/</g, "&lt;");
const pct = (n: number) => `${Math.round(n * 100000) / 1000}%`;

/** What a redraw puts in the chart's own layers: the plot's SVG, and the value labels' HTML. */
export interface Drawing {
  svg: string;
  labels: string;
}

/**
 * The chart with the visible series only. plotPx is the plot's size, for the number of ticks:
 * one per 56px up the side, or per 90px across.
 */
export function draw(d: DrawData, hidden: Set<number>, plotPx: { w: number; h: number }): Drawing {
  const n = d.a.length;
  const visible = d.s.map((_, i) => i).filter((i) => !hidden.has(i));
  const full = d.st === 2;
  const stacked = d.st !== 0;

  // Stacks: bars with bars, areas with areas.
  const lows: (number | null)[][] = d.s.map(() => new Array(n).fill(null));
  const highs: (number | null)[][] = d.s.map((s) => s.v.map((v) => (v === null || !isFinite(v) ? null : v)));
  if (stacked) {
    for (const type of [0, 2]) {
      const group = visible.filter((s) => d.s[s].t === type);
      for (let i = 0; i < n; i++) {
        let total = full ? group.reduce((sum, s) => sum + Math.abs(highs[s][i] ?? 0), 0) : 1;
        if (total === 0) total = 1;
        let up = 0, down = 0;
        for (const s of group) {
          const v = (highs[s][i] ?? 0) / total;
          const missing = highs[s][i] === null;
          if (v >= 0) {
            lows[s][i] = up;
            up += v;
            highs[s][i] = missing && type === 0 ? null : up;
          } else {
            lows[s][i] = down;
            down += v;
            highs[s][i] = missing && type === 0 ? null : down;
          }
        }
      }
    }
  }

  // The value domain.
  let min = Infinity, max = -Infinity;
  for (const s of visible) {
    for (let i = 0; i < n; i++) {
      for (const x of [highs[s][i], lows[s][i]]) {
        if (x !== null) {
          min = Math.min(min, x);
          max = Math.max(max, x);
        }
      }
    }
  }
  if (visible.some((s) => d.s[s].t === 0 || d.s[s].t === 2)) {
    min = Math.min(min, 0);
    max = Math.max(max, 0);
  }
  if (!isFinite(min)) [min, max] = [0, 1];
  if (d.lo != null) min = d.lo;
  if (d.hi != null) max = d.hi;

  const count = d.h ? Math.max(2, plotPx.w / 90) : Math.max(2, Math.round(plotPx.h / 56));
  let [dmin, dmax] = d.lo != null && d.hi != null ? [min, max] : nice(min, max, Math.round(count));
  if (d.lo != null) dmin = d.lo;
  if (d.hi != null) dmax = d.hi;
  const span = dmax - dmin || 1;
  const scale = (v: number) => Math.min(1, Math.max(0, (v - dmin) / span));

  // Ticks, labels and grid lines.
  const spec = tickSpec(dmin, dmax, count);
  let svg = "";
  let labels = "";
  for (let i = spec.i1; i <= spec.i2; i++) {
    const v = tickValue(spec, i);
    const pos = (v - dmin) / span;
    const text = esc(format(d.f, v, tickStep(spec)));
    const zero = v === 0 ? " ib-chart-zero" : "";
    if (d.h) {
      svg += `<line class="ib-chart-grid${zero}" x1="${pct(pos)}" x2="${pct(pos)}" y1="0" y2="100%"/>`;
      labels += `<span style="left:${pct(pos)}">${text}</span>`;
    } else {
      svg += `<line class="ib-chart-grid${zero}" x1="0" x2="100%" y1="${pct(1 - pos)}" y2="${pct(1 - pos)}"/>`;
      labels += `<span style="top:${pct(1 - pos)}">${text}</span>`;
    }
  }

  const attrs = (s: number, kind: string) => {
    const color = d.s[s].col ? ` style="--ib-chart-color:${esc(d.s[s].col!)}"` : "";
    return `class="${kind} ib-chart-s${d.s[s].k}"${color} data-ib-series="${s}"`;
  };

  // Bars: Chart.js's proportions, the group 80% of its band and each bar 90% of its share.
  const bars = visible.filter((s) => d.s[s].t === 0);
  const band = 1 / Math.max(1, n);
  const slotWidth = stacked ? band * 0.8 : (band * 0.8) / Math.max(1, bars.length);
  const width = slotWidth * 0.9;
  let barSvg = "";
  bars.forEach((s, j) => {
    barSvg += `<g ${attrs(s, "ib-chart-bars")}>`;
    for (let i = 0; i < n; i++) {
      const top = highs[s][i];
      if (top === null) continue;
      const a0 = i * band + band * 0.1 + (stacked ? 0 : j * slotWidth) + slotWidth * 0.05;
      const v0 = scale(lows[s][i] ?? 0), v1 = scale(top);
      const lo = Math.min(v0, v1), hi = Math.max(v0, v1);
      const [x, y, w, h] = d.h ? [lo, a0, hi - lo, width] : [a0, 1 - hi, width, hi - lo];
      const rect = (rx: number, ry: number, rw: number, rh: number, tip: boolean) =>
        `<rect${tip ? ' class="ib-chart-tip"' : ""} x="${pct(rx)}" y="${pct(ry)}" width="${pct(rw)}" height="${pct(rh)}"/>`;
      // The value end is rounded on the outermost bar: the last segment of a stack each way.
      const below = v1 < v0;
      const outermost = !stacked || !bars.slice(j + 1).some((o) => {
        const x2 = d.s[o].v[i];
        return x2 !== null && isFinite(x2) && x2 !== 0 && x2 < 0 === (highs[s][i] ?? 0) < (lows[s][i] ?? 0);
      });
      if (outermost && hi > lo) {
        barSvg += rect(x, y, w, h, true);
        barSvg += d.h
          ? rect(below ? x + w / 2 : x, y, w / 2, h, false)
          : rect(x, below ? y : y + h / 2, w, h / 2, false);
      } else {
        barSvg += rect(x, y, w, h, false);
      }
    }
    barSvg += "</g>";
  });

  // Lines, areas and markers.
  let areas = "", lines = "", marks = "";
  for (const s of visible) {
    const data = d.s[s];
    if (data.t === 0) continue;
    const points: ([number, number, number] | null)[] = [];
    for (let i = 0; i < n; i++) {
      const v = highs[s][i], a = d.a[i];
      if (v !== null && a !== null) points.push([a, scale(v), i]);
      else if (!data.j) points.push(null);
    }
    if (data.t !== 3) {
      const runs: [number, number, number][][] = [];
      let run: [number, number, number][] | null = null;
      for (const p of points) {
        if (p) {
          if (!run) runs.push((run = []));
          run.push(p);
        } else run = null;
      }
      let line = "", area = "";
      for (const r of runs) {
        const upper = curve(r.map((p) => [p[0], p[1]]), data.c);
        line += path(upper, d.h, true);
        if (data.t === 2) {
          const lower = curve(r.map((p) => [p[0], lows[s][p[2]] !== null ? scale(lows[s][p[2]]!) : scale(0)]), stacked ? data.c : 0);
          area += path(upper, d.h, true) + path(reverse(lower), d.h, false) + "Z";
        }
      }
      if (area) areas += `<path ${attrs(s, "ib-chart-area")} d="${area}"/>`;
      if (line) lines += `<path ${attrs(s, `ib-chart-line ib-chart-dash${data.d}`)} d="${line}"/>`;
    }
    if (data.m) {
      marks += `<g ${attrs(s, data.t === 3 ? "ib-chart-marks ib-chart-scatter" : "ib-chart-marks")}>`;
      for (const p of points) {
        if (!p) continue;
        const [x, y] = d.h ? [p[1], p[0]] : [p[0], 1 - p[1]];
        marks += `<use href="#${d.mk}${data.y % 4}" x="${pct(x)}" y="${pct(y)}" data-ib-i="${p[2]}"/>`;
      }
      marks += "</g>";
    }
  }

  const paths = (inner: string) => (inner ? `<svg class="ib-chart-paths" viewBox="0 0 1000 1000" preserveAspectRatio="none">${inner}</svg>` : "");
  svg += paths(areas) + barSvg + paths(lines) + marks;
  return { svg, labels };
}
