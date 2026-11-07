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
  t: number; // the fewest decimals kept: trailing zeros past these drop ("#,##0.##" keeps 0)
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
  lm?: number; // value labels: 1 auto, 2 inside, 3 outside
  lt?: (string | null)[]; // each item's label text
}

export interface DrawData {
  h: boolean; // horizontal
  st: number; // 0 none, 1 stacked, 2 full
  lo: number | null; // ValueMin
  hi: number | null; // ValueMax
  vs: number | null; // ValueStep
  lg: boolean; // ValueLog
  gl: boolean; // grid lines at the values
  // With a ValueFormatter, the server's axis as drawn, kept as it is: its range, and each tick's
  // place (0 to 1), text, first width it shows at, and whether it's zero.
  rl?: number;
  rh?: number;
  tk?: [number, string, number, boolean][];
  f: NumberShape;
  a: (number | null)[]; // each item's place along the argument axis, 0 to 1
  mk: string; // the marker shapes' id prefix
  vc: number; // the tick count up the side, from the chart's Height
  off: number[]; // the series the server drew hidden
  s: DrawSeries[];
}

// ---- Ticks: D3's (ChartScale.cs). ----

const E10 = Math.sqrt(50), E5 = Math.sqrt(10), E2 = Math.sqrt(2);

interface Spec { i1: number; i2: number; inc: number }

function tickSpec(start: number, stop: number, count: number): Spec {
  // No span, or a backward or endless one: no ticks (and no endless loop over them).
  if (!(stop > start) || !isFinite(start) || !isFinite(stop) || !(count > 0)) return { i1: 0, i2: -1, inc: 1 };
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

// ---- Set steps and log scales (ChartLayout.StepDomain, StepTicks, LogDomain, LogTicks). ----

const power = (m: number, e: number) => Number(`${m}e${e}`);

function logTicks(min: number, max: number, count: number): [number, number][] {
  const lo = Math.floor(Math.log10(min) + 1e-9), hi = Math.ceil(Math.log10(max) - 1e-9);
  const multiples = (hi - lo) * 3 <= count + 1 ? [1, 2, 5] : [1];
  const ticks: [number, number][] = [];
  for (let e = lo; e <= hi; e++) {
    for (const m of multiples) {
      const v = power(m, e);
      if (v < min * (1 - 1e-9) || v > max * (1 + 1e-9)) continue;
      let zeros = 0;
      while (e !== 0 && zeros < 4 && (Math.abs(e) >> zeros) % 2 === 0) zeros++;
      ticks.push([v, m !== 1 ? 0 : e === 0 ? 6 : 1 + zeros]);
    }
  }
  return ticks;
}

function stepTicks(min: number, max: number, step: number, decimals: number): [number, number][] {
  const i1 = Math.ceil(min / step - 1e-9), i2 = Math.floor(max / step + 1e-9);
  // Levels as ChartScale.Ladder's from a step of 1: every 1, 2, 10, 20, 100… steps.
  const ladder = [1];
  for (let k = 1, j = 0; k < Math.max(1, i2 - i1) && ladder.length < 40; j++) ladder.push((k *= j % 2 === 0 ? 2 : 5));
  const ticks: [number, number][] = [];
  for (let i = i1; i <= i2; i++) {
    let level = 0;
    while (level < ladder.length - 1 && i % ladder[level + 1] === 0) level++;
    ticks.push([Number((i * step).toFixed(decimals)), level]);
  }
  return ticks;
}

/** The ticks to keep: those of the finest level whose ticks are needPx apart. */
function thin(ticks: [number, number][], pos: (v: number) => number, px: number, needPx: number): [number, number][] {
  const top = Math.max(0, ...ticks.map((t) => t[1]));
  for (let level = 0; ; level++) {
    const kept = ticks.filter((t) => t[1] >= level);
    let gap = Infinity;
    for (let i = 1; i < kept.length; i++) gap = Math.min(gap, Math.abs(pos(kept[i][0]) - pos(kept[i - 1][0])));
    if (level >= top || gap * px >= needPx) return kept;
  }
}

const decimalsFor = (step: number) => (step >= 1 || step <= 0 ? 0 : Math.min(10, Math.ceil(-Math.log10(step) - 1e-9)));

/** Formats a value as the chart's .NET format would, from the shape the server read off it. */
export function format(f: NumberShape, value: number, step: number): string {
  const decimals = f.n < 0 ? decimalsFor(step) : f.n;
  let text = (Math.abs(value) * f.k).toFixed(decimals);
  // Optional decimals ("#,##0.##"): trailing zeros drop down to the ones the format keeps.
  // The axis default (n -1) keeps them all, as .NET's N does.
  for (let keep = decimals; keep > (f.n < 0 ? decimals : f.t) && /\.\d*0$/.test(text); keep--) text = text.slice(0, -1);
  text = text.replace(/\.$/, "");
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
  dl: string; // the value labels' HTML
}

// ---- Value labels (ChartLayout.PlaceLabels), placed at the plot's real size. ----

const GAP = 4, PAD = 2, LINE = 16;

/** A spot: the label's center in pixels, and the bar it must stay inside, if any. */
type Spot = [number, number, [number, number, number, number] | null];

interface LabelAt {
  s: number;
  text: string;
  w: number;
  spots: Spot[];
}

function placeLabels(all: LabelAt[], W: number, H: number, slot: (s: number) => number, color: (s: number) => string): string {
  const boxes: [number, number, number, number][] = [];
  let html = "";
  for (const { s, text, w, spots } of all) {
    for (const [cx, cy, bar] of spots) {
      const box: [number, number, number, number] = [cx - w / 2 - PAD, cy - LINE / 2, cx + w / 2 + PAD, cy + LINE / 2];
      const room = bar
        ? box[0] >= bar[0] && box[2] <= bar[2] && box[1] >= bar[1] - 0.5 && box[3] <= bar[3] + 0.5
        : box[0] >= 0 && box[2] <= W && box[1] >= 0 && box[3] <= H;
      if (!room) continue;
      if (boxes.some((o) => box[0] < o[2] && o[0] < box[2] && box[1] < o[3] && o[1] < box[3])) break;
      boxes.push(box);
      html += `<span${bar ? ` class="ib-chart-dl-in ib-chart-s${slot(s)}"` : ""} style="left:${Math.round(cx * 10) / 10}px;top:${Math.round(cy * 10) / 10}px;${color(s)}" data-ib-series="${s}">${esc(text)}</span>`;
      break;
    }
  }
  return html;
}


/**
 * The chart with the visible series only. plotPx is the plot's size, for the number of ticks:
 * one per 56px up the side, or per 90px across.
 */
export function draw(d: DrawData, hidden: Set<number>, plotPx: { w: number; h: number }, measure?: (text: string) => number): Drawing {
  const W = plotPx.w, H = plotPx.h;
  const labelsAt: LabelAt[] = [];
  const widthOf = (text: string) => (measure ? measure(text) : text.length * 7);
  const n = d.a.length;
  const visible = d.s.map((_, i) => i).filter((i) => !hidden.has(i));
  const full = d.st === 2;
  const stacked = d.st !== 0;

  // Stacks: bars with bars, areas with areas.
  const lows: (number | null)[][] = d.s.map(() => new Array(n).fill(null));
  const log = d.lg;
  const highs: (number | null)[][] = d.s.map((s) => s.v.map((v) => (v === null || !isFinite(v) || (log && v <= 0) ? null : v)));
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
        if (x !== null && (!log || x > 0)) {
          min = Math.min(min, x);
          max = Math.max(max, x);
        }
      }
    }
  }
  if (!log && visible.some((s) => d.s[s].t === 0 || d.s[s].t === 2)) {
    min = Math.min(min, 0);
    max = Math.max(max, 0);
  }
  if (!isFinite(min)) [min, max] = log ? [1, 10] : [0, 1];
  // A set end the values cross moves the other end past it, so the range never runs backward.
  if (d.lo != null) [min, max] = [d.lo, Math.max(max, d.lo)];
  if (d.hi != null) [min, max] = [Math.min(min, d.hi), d.hi];

  const count = d.h ? Math.max(2, plotPx.w / 90) : d.vc;
  let dmin: number, dmax: number;
  let ticks: [number, number][] | null = null;
  // A step's decimals, as 10 to minus them, for format to pick.
  const stepDecimals = d.vs != null && d.vs < 1 ? Math.min(10, (String(d.vs).split(".")[1] ?? "").length) : 0;
  let step = d.vs;
  if (log) {
    dmin = d.lo ?? power(1, Math.floor(Math.log10(min) + 1e-9));
    dmax = d.hi ?? power(1, Math.ceil(Math.log10(max) - 1e-9));
    if (!(dmax > dmin)) dmax = dmin * 10;
    ticks = logTicks(dmin, dmax, count);
  } else if (step != null) {
    dmin = d.lo ?? Math.floor(min / step + 1e-9) * step;
    dmax = d.hi ?? Math.ceil(max / step - 1e-9) * step;
    if (!(dmax > dmin)) dmax = dmin + step;
    if ((dmax - dmin) / step > 500) step = null;
    else ticks = stepTicks(dmin, dmax, step, stepDecimals);
  }
  if (!log && step == null) {
    [dmin, dmax] = d.lo != null && d.hi != null ? [min, max] : nice(min, max, Math.round(count));
    if (d.lo != null) dmin = d.lo;
    if (d.hi != null) dmax = d.hi;
  }
  if (d.tk) [dmin, dmax] = [d.rl!, d.rh!];
  const span = dmax! - dmin! || 1;
  const [l0, l1] = log ? [Math.log10(dmin!), Math.log10(dmax!)] : [0, 1];
  const scale = (v: number) =>
    log ? (v > 0 ? Math.min(1, Math.max(0, (Math.log10(v) - l0) / (l1 - l0))) : 0) : Math.min(1, Math.max(0, (v - dmin!) / span));

  // Ticks, labels and grid lines: thinned to a line apart up the side, or to a label's width across.
  if (ticks) {
    const text = (v: number) => format(d.f, v, log ? v : Math.pow(10, -stepDecimals));
    const widest = Math.max(0, ...ticks.map((t) => text(t[0]).length)) * 7 + 8;
    ticks = thin(ticks, scale, d.h ? plotPx.w : plotPx.h, d.h ? widest : 20);
  } else {
    const spec = tickSpec(dmin!, dmax!, count);
    ticks = [];
    for (let i = spec.i1; i <= spec.i2; i++) ticks.push([tickValue(spec, i), 0]);
    step = tickStep(spec);
  }
  const axisTicks: [number, string, number, boolean][] = d.tk ??
    ticks.map(([v]) => [scale(v), format(d.f, v, log ? v : d.vs != null ? Math.pow(10, -stepDecimals) : step!), 0, v === 0]);
  let svg = "";
  let labels = "";
  for (const [pos, raw, from, isZero] of axisTicks) {
    const text = esc(raw);
    const cls = (isZero ? " ib-chart-zero" : "") + (from > 0 ? ` ib-chart-from${from}` : "");
    const span = from > 0 ? ` class="ib-chart-from${from}"` : "";
    const line = d.gl || isZero;
    if (d.h) {
      if (line) svg += `<line class="ib-chart-grid${cls}" x1="${pct(pos)}" x2="${pct(pos)}" y1="0" y2="100%"/>`;
      labels += `<span${span} style="left:${pct(pos)}">${text}</span>`;
    } else {
      if (line) svg += `<line class="ib-chart-grid${cls}" x1="0" x2="100%" y1="${pct(1 - pos)}" y2="${pct(1 - pos)}"/>`;
      labels += `<span${span} style="top:${pct(1 - pos)}">${text}</span>`;
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
      const rect = (end: string) =>
        `<rect${end ? ` class="ib-chart-end-${end}"` : ""} x="${pct(x)}" y="${pct(y)}" width="${pct(w)}" height="${pct(h)}"/>`;
      // The value end is rounded on the outermost bar: the last segment of a stack each way.
      const below = v1 < v0;
      const outermost = !stacked || !bars.slice(j + 1).some((o) => {
        const x2 = d.s[o].v[i];
        return x2 !== null && isFinite(x2) && x2 !== 0 && x2 < 0 === (highs[s][i] ?? 0) < (lows[s][i] ?? 0);
      });
      barSvg += rect(outermost && hi > lo ? (d.h ? (below ? "l" : "r") : below ? "b" : "t") : "");
      const text = d.s[s].lt?.[i];
      if (d.s[s].lm && text != null) {
        const tw = widthOf(text);
        const b: [number, number, number, number] = [x * W, y * H, (x + w) * W, (y + h) * H];
        const mx = (b[0] + b[2]) / 2, my = (b[1] + b[3]) / 2, dw = GAP + tw / 2, dh = GAP + LINE / 2;
        const dir = d.h ? (below ? "l" : "r") : below ? "b" : "t";
        const out: Spot = dir === "t" ? [mx, b[1] - dh, null] : dir === "b" ? [mx, b[3] + dh, null] : dir === "r" ? [b[2] + dw, my, null] : [b[0] - dw, my, null];
        const end: Spot = dir === "t" ? [mx, b[1] + dh, b] : dir === "b" ? [mx, b[3] - dh, b] : dir === "r" ? [b[2] - dw, my, b] : [b[0] + dw, my, b];
        const mode = d.s[s].lm;
        labelsAt.push({ s, text, w: tw, spots: stacked && mode !== 3 ? [[mx, my, b]] : mode === 2 ? [end] : [out, end] });
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
        const text = data.lt?.[p[2]];
        if (data.lm && text != null) {
          const dy = 6 + GAP + LINE / 2;
          const above: Spot = [x * W, y * H - dy, null], under: Spot = [x * W, y * H + dy, null];
          labelsAt.push({ s, text, w: widthOf(text), spots: data.lm === 2 ? [under, above] : [above, under] });
        }
      }
      marks += "</g>";
    }
  }

  const paths = (inner: string) => (inner ? `<svg class="ib-chart-paths" viewBox="0 0 1000 1000" preserveAspectRatio="none">${inner}</svg>` : "");
  svg += paths(areas) + barSvg + paths(lines) + marks;
  return { svg, labels, dl: placeLabels(labelsAt, W, H, (s) => d.s[s].k, (s) => (d.s[s].col ? `--ib-chart-color:${esc(d.s[s].col!)}` : "")) };
}
