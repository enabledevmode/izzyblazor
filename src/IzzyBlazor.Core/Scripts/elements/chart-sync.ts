import type { View } from "./chart-draw";

// Synced charts (Chart Sync): what they match by (data-ib-sync-at): bands or a scale, names (text)
// or values, each item's argument (names as text, dates as ticks, numbers), and zooming on a
// scale its ends in its units, a log axis's as powers of ten.
export type SyncAt = { c: boolean; t: boolean; k: (number | string | null)[]; d?: [number, number]; g?: boolean };
// A view passed between them: its first and last arguments (names or values), or null for everything.
export type SyncView = { t: boolean; a: number | string; b: number | string } | null;
export type Peer = { view?: (m: SyncView) => void; settle?: () => void; hover?: (t: boolean, at: number | string | null) => void };
const groups = new Map<string, Set<Peer>>();
/** Joins the charts named alike on the page; returns a call to each of the others. */
export const joinSync = (name: string, peer: Peer, signal: AbortSignal) => {
  const group = groups.get(name) ?? new Set<Peer>();
  groups.set(name, group);
  group.add(peer);
  signal.addEventListener("abort", () => {
    group.delete(peer);
    if (group.size === 0) groups.delete(name);
  });
  return (call: (p: Peer) => void) => {
    for (const p of [...group]) if (p !== peer) call(p);
  };
};
const scaleShare = (s: SyncAt, x: number) => ((s.g ? Math.log10(x) : x) - s.d![0]) / (s.d![1] - s.d![0] || 1);
export const scaleValue = (s: SyncAt, f: number) => ((c) => (s.g ? 10 ** c : c))(s.d![0] + f * (s.d![1] - s.d![0]));
/** A chart's view as its first and last arguments; undefined when it can't say (a scale with no zoom). */
export const viewOut = (s: SyncAt, v: View): SyncView | undefined => {
  if (s.c) {
    const n = s.k.length;
    const i = Math.min(n - 1, Math.max(0, Math.floor(v[0] * n + 1e-6)));
    const j = Math.min(n - 1, Math.max(i, Math.ceil(v[1] * n - 1e-6) - 1));
    return s.k[i] === null || s.k[j] === null ? undefined : { t: s.t, a: s.k[i]!, b: s.k[j]! };
  }
  return s.d ? { t: false, a: scaleValue(s, v[0]), b: scaleValue(s, v[1]) } : undefined;
};
/** Another chart's view on this one's axis, cut to its ends; undefined when they don't meet. */
export const viewIn = (s: SyncAt, m: SyncView): View | undefined => {
  if (m === null) return [0, 1];
  if (m.t !== s.t) return;
  const n = s.k.length;
  if (s.c) {
    // Bands: from the first item in the range to the last, by name or by value.
    // By value, with room for rounding: dates as ticks come back from shares a little off.
    const values = s.k.filter((k): k is number => typeof k === "number");
    const eps = values.length > 1 ? 1e-6 * (Math.max(...values) - Math.min(...values)) : 0;
    const [lo, hi] = m.t ? [m.a, m.b] : [Math.min(m.a as number, m.b as number) - eps, Math.max(m.a as number, m.b as number) + eps];
    const inside = s.k.flatMap((k, i) => (m.t ? k === lo || k === hi : typeof k === "number" && k >= (lo as number) && k <= (hi as number)) ? [i] : []);
    return inside.length === 0 ? undefined : [Math.min(...inside) / n, (Math.max(...inside) + 1) / n];
  }
  if (!s.d) return;
  // On a log axis, a value at or below zero is before its start.
  const share = (x: number) => (s.g && x <= 0 ? -Infinity : scaleShare(s, x));
  const [a, b] = [share(m.a as number), share(m.b as number)].sort((x, y) => x - y);
  return Math.min(1, b) - Math.max(0, a) > 1e-9 ? [Math.max(0, a), Math.min(1, b)] : undefined;
};
/** The item at another chart's argument: the same name, or by value the nearest within its ends. */
export const itemAt = (s: SyncAt, t: boolean, at: number | string | null) => {
  if (at === null || t !== s.t) return -1;
  if (s.t) return s.k.indexOf(at);
  let best = -1;
  let [lo, hi] = [Infinity, -Infinity];
  s.k.forEach((k, i) => {
    if (typeof k !== "number") return;
    [lo, hi] = [Math.min(lo, k), Math.max(hi, k)];
    if (best < 0 || Math.abs(k - (at as number)) < Math.abs((s.k[best] as number) - (at as number))) best = i;
  });
  return (at as number) < lo || (at as number) > hi ? -1 : best;
};
