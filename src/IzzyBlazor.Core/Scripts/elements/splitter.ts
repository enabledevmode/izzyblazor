import { IzzyElement, defineElement } from "../izzy-element";
import { writeCookie } from "../cookies";

const KEY_STEP = 16;
// Held arrow keys report the layout once they stop, so a server render answering an earlier step
// can't land between two and undo one.
const KEY_REPORT_MS = 300;
// A collapsible pane dragged this far below its minimum collapses.
const SNAP = 0.5;

/**
 * <ib-splitter> moves the bars between a Splitter's panes: dragging, arrow keys, Home and End,
 * and Enter or a double-click to collapse a collapsible pane, as does its button on the bar. The
 * server draws the sizes; this changes a pane's --ib-pane-size (in its unit: a percentage stays
 * one, other sizes are held in pixels) and its hidden attribute. When a move ends it writes the
 * layout to the LayoutKey's cookie and reports it as `iblayout` for an interactive page.
 */
class Splitter extends IzzyElement {
  #report = 0;

  get #vertical(): boolean {
    return this.classList.contains("ib-splitter-v");
  }

  get #disabled(): boolean {
    return this.hasAttribute("data-ib-disabled");
  }

  protected setup(signal: AbortSignal): void {
    this.addEventListener("pointerdown", (e) => this.#onPointerDown(e, signal), { signal });
    this.addEventListener("keydown", (e) => this.#onKeyDown(e), { signal });
    this.addEventListener("dblclick", (e) => {
      const gutter = this.#gutter(e.target);
      if (gutter && (e.target as Element).closest(".ib-split-bar")) this.#toggle(gutter);
    }, { signal });
    this.addEventListener("click", (e) => {
      const button = (e.target as Element).closest<HTMLButtonElement>(".ib-split-collapse");
      const gutter = this.#gutter(button);
      if (button && gutter) this.#toggle(gutter, button.dataset.ibTarget === "after" ? "after" : "before");
    }, { signal });
    const sizes = new ResizeObserver(() => this.#describe());
    sizes.observe(this);
    signal.addEventListener("abort", () => {
      sizes.disconnect();
      clearTimeout(this.#report);
    });
    this.#describe();
  }

  // The gutter of this splitter (not a nested one) that holds an element.
  #gutter(target: EventTarget | null): HTMLElement | null {
    const gutter = (target as Element | null)?.closest<HTMLElement>(".ib-split-gutter");
    return gutter?.parentElement === this ? gutter : null;
  }

  #panes(): HTMLElement[] {
    return [...this.querySelectorAll<HTMLElement>(":scope > .ib-split-pane")];
  }

  #sides(gutter: HTMLElement): [HTMLElement, HTMLElement] {
    return [gutter.previousElementSibling as HTMLElement, gutter.nextElementSibling as HTMLElement];
  }

  #movable(gutter: HTMLElement): boolean {
    return !this.#disabled && !!gutter.querySelector(".ib-split-bar[role='separator']");
  }

  #extent(): number {
    return this.#vertical ? this.clientHeight : this.clientWidth;
  }

  #size(pane: HTMLElement): number {
    if (pane.hidden) return 0;
    const r = pane.getBoundingClientRect();
    return this.#vertical ? r.height : r.width;
  }

  // A pane's min and max in pixels, from its computed style (a percentage is of the splitter).
  #limits(pane: HTMLElement): [number, number] {
    const style = getComputedStyle(pane);
    const px = (value: string): number => (value.endsWith("%") ? (parseFloat(value) / 100) * this.#extent() : parseFloat(value));
    const min = px(this.#vertical ? style.minHeight : style.minWidth) || 0;
    const max = px(this.#vertical ? style.maxHeight : style.maxWidth);
    return [min, Number.isFinite(max) ? max : Infinity];
  }

  // Where the bar can go: the pane before's size, given both panes' limits and their total.
  #range(before: HTMLElement, after: HTMLElement, total: number): [number, number] {
    const [minA, maxA] = this.#limits(before);
    const [minB, maxB] = this.#limits(after);
    return [Math.max(minA, total - maxB), Math.min(maxA, total - minB)];
  }

  #sized(pane: HTMLElement): boolean {
    return pane.hasAttribute("data-ib-unit");
  }

  #setSize(pane: HTMLElement, px: number): void {
    const value = pane.dataset.ibUnit === "%" ? `${round((px / this.#extent()) * 100)}%` : `${Math.round(px)}px`;
    pane.style.setProperty("--ib-pane-size", value);
  }

  // Moves the bar so the pane before is `a` pixels; the pane after takes the rest of the two. Of
  // two panes that fill, the one before takes a share, in percent, and the other fills the rest.
  #place(before: HTMLElement, after: HTMLElement, a: number, total: number): void {
    const sizedBefore = this.#sized(before);
    const sizedAfter = this.#sized(after);
    if (!sizedBefore && !sizedAfter) before.dataset.ibUnit = "%";
    if (sizedBefore || !sizedAfter) this.#setSize(before, a);
    if (sizedAfter) this.#setSize(after, total - a);
  }

  #onPointerDown(down: PointerEvent, signal: AbortSignal): void {
    if (down.button !== 0) return;
    const bar = (down.target as Element).closest<HTMLElement>(".ib-split-bar");
    const gutter = this.#gutter(bar);
    if (!bar || !gutter || !this.#movable(gutter)) return;
    down.preventDefault();
    bar.focus({ preventScroll: true });
    const [before, after] = this.#sides(gutter);
    if (before.hidden || after.hidden) return;
    const start = [this.#size(before), this.#size(after)];
    const total = start[0] + start[1];
    const saved = [before.style.getPropertyValue("--ib-pane-size"), after.style.getPropertyValue("--ib-pane-size")];
    const [low, high] = this.#range(before, after, total);
    const flip = !this.#vertical && getComputedStyle(this).direction === "rtl" ? -1 : 1;
    let snapped: HTMLElement | null = null;

    bar.setPointerCapture(down.pointerId);
    bar.toggleAttribute("data-ib-dragging", true);
    this.toggleAttribute("data-ib-resizing", true);
    const abort = new AbortController();
    signal.addEventListener("abort", () => abort.abort(), { once: true, signal: abort.signal });
    const options = { signal: abort.signal };

    bar.addEventListener("pointermove", (event) => {
      const delta = (this.#vertical ? event.clientY - down.clientY : event.clientX - down.clientX) * flip;
      const want = start[0] + delta;
      // Past a collapsible pane's minimum by half of it, the pane collapses.
      const snap = want < low - this.#limits(before)[0] * SNAP && before.hasAttribute("data-ib-collapsible") ? before
        : want > high + this.#limits(after)[0] * SNAP && after.hasAttribute("data-ib-collapsible") ? after : null;
      if (snap !== snapped) {
        if (snapped) snapped.hidden = false;
        snapped = snap;
        if (snap) snap.hidden = true;
      }
      if (!snap) this.#place(before, after, clamp(want, low, high), total);
    }, options);

    const end = (commit: boolean) => {
      abort.abort();
      bar.removeAttribute("data-ib-dragging");
      this.removeAttribute("data-ib-resizing");
      if (snapped) {
        // A collapsed pane keeps the size it had, for when it's restored.
        before.style.setProperty("--ib-pane-size", saved[0]);
        after.style.setProperty("--ib-pane-size", saved[1]);
        if (!commit) snapped.hidden = false;
      } else if (!commit) {
        before.style.setProperty("--ib-pane-size", saved[0]);
        after.style.setProperty("--ib-pane-size", saved[1]);
      }
      this.#changed();
    };
    bar.addEventListener("pointerup", () => end(true), options);
    bar.addEventListener("pointercancel", () => end(false), options);
    document.addEventListener("keydown", (event) => {
      if (event.key !== "Escape") return;
      event.preventDefault();
      event.stopPropagation();
      end(false);
    }, { ...options, capture: true });
  }

  #onKeyDown(event: KeyboardEvent): void {
    const bar = (event.target as Element).closest<HTMLElement>(".ib-split-bar");
    const gutter = this.#gutter(bar);
    if (!gutter || !this.#movable(gutter) || event.altKey || event.ctrlKey || event.metaKey) return;
    if (event.key === "Enter") {
      event.preventDefault();
      this.#toggle(gutter);
      return;
    }
    const [before, after] = this.#sides(gutter);
    if (before.hidden || after.hidden) return;
    const a = this.#size(before);
    const total = a + this.#size(after);
    const [low, high] = this.#range(before, after, total);
    const back = this.#vertical ? "ArrowUp" : "ArrowLeft";
    const forward = this.#vertical ? "ArrowDown" : "ArrowRight";
    const rtl = !this.#vertical && getComputedStyle(this).direction === "rtl";
    const step = event.shiftKey ? this.#extent() * 0.1 : KEY_STEP;
    let to: number;
    if (event.key === back || event.key === forward) to = a + ((event.key === forward) !== rtl ? step : -step);
    else if (event.key === "Home") to = low;
    else if (event.key === "End") to = high;
    else return;
    event.preventDefault();
    this.#place(before, after, clamp(to, low, high), total);
    this.#changed(true);
  }

  // Collapses or restores the collapsible pane beside a bar: the one before, unless only the one after is.
  #toggle(gutter: HTMLElement, side?: "before" | "after"): void {
    if (this.#disabled) return;
    const [before, after] = this.#sides(gutter);
    const pane = side === "after" ? after : side === "before" ? before
      : before.hasAttribute("data-ib-collapsible") ? before : after;
    if (!pane.hasAttribute("data-ib-collapsible")) return;
    pane.hidden = !pane.hidden;
    this.#changed();
  }

  // After a move: the bars' values and buttons, the cookie, and the page (for a key, once they stop).
  #changed(soon = false): void {
    this.#describe();
    const layout = this.#panes()
      .map((pane) => {
        const size = pane.style.getPropertyValue("--ib-pane-size").trim();
        return (pane.hidden ? "!" : "") + (/^\d+(\.\d+)?(px|%)$/.test(size) && pane.hasAttribute("data-ib-unit") ? size : "");
      })
      .join("|");
    const key = this.dataset.ibLayoutKey;
    if (key) writeCookie(`ib-split-${key}`, layout);
    clearTimeout(this.#report);
    if (soon) this.#report = window.setTimeout(() => this.emit("iblayout", { layout }), KEY_REPORT_MS);
    else this.emit("iblayout", { layout });
  }

  // Each bar's value (its pane's share of the splitter, as APG has it: the pane it's named after
  // and controls, which is the one Enter collapses) and collapse button.
  #describe(): void {
    const extent = this.#extent();
    if (!extent) return;
    for (const gutter of this.querySelectorAll<HTMLElement>(":scope > .ib-split-gutter")) {
      const [before, after] = this.#sides(gutter);
      const bar = gutter.querySelector<HTMLElement>(".ib-split-bar[role='separator']");
      if (bar) {
        const a = this.#size(before);
        const total = a + this.#size(after);
        let [low, high] = before.hidden || after.hidden ? [0, total] : this.#range(before, after, total);
        let value = a;
        if (bar.getAttribute("aria-controls") === after.id) [value, low, high] = [total - a, total - high, total - low];
        bar.setAttribute("aria-valuenow", String(Math.round((value / extent) * 100)));
        bar.setAttribute("aria-valuemin", String(Math.round((Math.min(low, value) / extent) * 100)));
        bar.setAttribute("aria-valuemax", String(Math.round((Math.max(high, value) / extent) * 100)));
      }
      const button = gutter.querySelector<HTMLElement>(".ib-split-collapse");
      if (button) {
        const collapsed = (button.dataset.ibTarget === "after" ? after : before).hidden;
        button.toggleAttribute("data-ib-collapsed", collapsed);
        const label = collapsed ? button.dataset.ibExpandLabel : button.dataset.ibCollapseLabel;
        if (label) button.setAttribute("aria-label", label);
      }
    }
  }
}

function clamp(value: number, low: number, high: number): number {
  return Math.min(Math.max(value, low), Math.max(low, high));
}

function round(value: number): number {
  return Math.round(value * 100) / 100;
}

defineElement("ib-splitter", Splitter);
