// Measures every docs example's preview height at a ladder of frame widths, in each of its modes,
// and writes site/IzzyBlazor.Site/frame-heights.json. The docs site sizes each example frame from
// it with container queries, so a frame starts at its content's height and the page doesn't move
// as examples load (site.js still corrects a frame whose content changes later).
//
// Run against the docs site after changing an example:
//   dotnet run --project site/IzzyBlazor.Site            (http://localhost:5190)
//   npm run measure-frames -- [--url http://localhost:5190] [--browser <chrome or chromium>]
//                             [--only <id>,<id>]
// --browser defaults to Playwright's own Chromium, or Chrome when that isn't installed.
//
// A frame's content height depends only on its width (its own page lays it out), so each example
// is loaded once per mode and the window resized through the ladder. Heights are kept only where
// they change: [[width, height], ...], each pair holding from that width up to the next.

import { readFile, writeFile } from "node:fs/promises";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { chromium } from "playwright-core";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const site = join(root, "site", "IzzyBlazor.Site");
const args = process.argv.slice(2);
const arg = (name) => {
  const i = args.indexOf(`--${name}`);
  return i >= 0 ? args[i + 1] : undefined;
};
const base = (arg("url") ?? "http://localhost:5190").replace(/\/$/, "");
const only = arg("only")?.split(",");

// Frame widths on the site run from 271px (a 320px phone) to 1110px (a wide demo page). Every
// 16px, then each change narrowed to the exact width it happens at.
const ladder = [];
for (let w = 256; w <= 1120; w += 16) ladder.push(w);

// components.json has comments (// lines) before its JSON.
const registry = JSON.parse((await readFile(join(site, "components.json"), "utf8")).replace(/^\s*\/\/.*$/gm, ""));
const examples = registry.components.flatMap((c) => [...(c.examples ?? []), ...(c.scenarios ?? []).map((s) => s.example)]);
const todo = examples.filter((e) => !only || only.includes(e.id));

const launch = async () => {
  const path = arg("browser");
  if (path) return chromium.launch({ executablePath: path });
  try {
    return await chromium.launch();
  } catch {
    return chromium.launch({ channel: "chrome" });
  }
};
const browser = await launch();

async function measure(example, mode) {
  const viewHeight = example.height ?? 160;
  const context = await browser.newContext({ javaScriptEnabled: mode !== "off", viewport: { width: ladder[0], height: viewHeight } });
  const page = await context.newPage();
  try {
    await page.goto(`${base}/frame/${example.id}?mode=${mode}`, { waitUntil: "load" });
    await page.evaluate(() => document.fonts.ready);
    if (mode === "live") await page.waitForTimeout(1500);
    // Scripts can change an example after load (a filter form hides its Apply button, a code
    // block is highlighted): wait until its height holds for half a second, three at most. Polled
    // from here, since with JavaScript off (Off) the page runs no timers.
    const height = () =>
      page.evaluate(() => {
        const frame = document.querySelector(".frame");
        return frame ? frame.getBoundingClientRect().height : 0;
      });
    for (let last = -1, steady = 0, waited = 0; steady < 500 && waited < 3000; waited += 100) {
      const h = await height();
      steady = h === last ? steady + 100 : 0;
      last = h;
      await page.waitForTimeout(100);
    }
    const at = async (width) => {
      await page.setViewportSize({ width, height: viewHeight });
      // Reading the box lays the page out at the new width.
      return page.evaluate(() => {
        const frame = document.querySelector(".frame");
        return frame ? Math.ceil(frame.getBoundingClientRect().height) : 0;
      });
    };
    // Every 16px first.
    const coarse = [];
    for (const width of ladder) {
      const h = await at(width);
      if (h > 0) coarse.push([width, h]);
    }
    // Then each change narrowed to the exact width (all of them, where two fall between steps),
    // so a frame between two steps starts right.
    const fine = [];
    const refine = async (lo, loH, hi, hiH) => {
      if (hi - lo <= 1) {
        fine.push([hi, hiH]);
        return;
      }
      const mid = (lo + hi) >> 1;
      const midH = await at(mid);
      if (midH === loH) await refine(mid, midH, hi, hiH);
      else if (midH === hiH) await refine(lo, loH, mid, midH);
      else {
        await refine(lo, loH, mid, midH);
        await refine(mid, midH, hi, hiH);
      }
    };
    if (coarse.length) fine.push(coarse[0]);
    for (let i = 1; i < coarse.length; i++) {
      if (coarse[i][1] !== coarse[i - 1][1]) await refine(...coarse[i - 1], ...coarse[i]);
    }
    const dedupe = (list) => list.filter((s, i) => i === 0 || s[1] !== list[i - 1][1]);
    // Content that scales with the width (an image) changes at nearly every pixel: keep the 16px
    // steps for it, or the CSS would hold a rule per pixel.
    const steps = fine.length > 24 ? dedupe(coarse) : dedupe(fine);
    // The narrowest step holds below the ladder too.
    if (steps.length) steps[0][0] = 0;
    return steps;
  } finally {
    await context.close();
  }
}

const out = only ? JSON.parse(await readFile(join(site, "frame-heights.json"), "utf8").catch(() => "{}")) : {};
const queue = todo.flatMap((e) => (e.modes ?? ["off", "static", "live"]).map((m) => [e, m]));
let done = 0;
const results = new Map();
async function worker() {
  for (let job = queue.shift(); job; job = queue.shift()) {
    const [example, mode] = job;
    try {
      results.set(`${example.id}|${mode}`, await measure(example, mode));
    } catch (error) {
      console.error(`${example.id} (${mode}): ${error.message.split("\n")[0]}`);
    }
    if (++done % 50 === 0) console.log(`${done} measured`);
  }
}
await Promise.all(Array.from({ length: 4 }, worker));
await browser.close();

for (const example of todo) {
  const entry = {};
  for (const mode of example.modes ?? ["off", "static", "live"]) {
    const steps = results.get(`${example.id}|${mode}`);
    if (steps?.length) entry[mode] = steps;
  }
  // A mode measured the same as Static is left out; the site uses Static's.
  for (const mode of ["off", "live"]) {
    if (entry[mode] && entry.static && JSON.stringify(entry[mode]) === JSON.stringify(entry.static)) delete entry[mode];
  }
  if (Object.keys(entry).length) out[example.id] = entry;
}

const sorted = Object.fromEntries(Object.keys(out).sort().map((k) => [k, out[k]]));
// One example per line keeps diffs readable.
const text = "{\n" + Object.entries(sorted).map(([k, v]) => `  ${JSON.stringify(k)}: ${JSON.stringify(v)}`).join(",\n") + "\n}\n";
await writeFile(join(site, "frame-heights.json"), text);
console.log(`Wrote ${Object.keys(sorted).length} examples to frame-heights.json`);
