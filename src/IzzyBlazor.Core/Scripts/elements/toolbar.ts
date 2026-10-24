import { IzzyElement, defineElement } from "../izzy-element";

/**
 * <ib-toolbar> measures what fits and moves the rest into the "…" menu, and makes the toolbar one
 * tab stop with arrow keys between its controls.
 *
 * Without script, the toolbar's own <style> shows and hides units at widths the server worked out
 * from estimates (Toolbar.razor, ToolbarPlan). Once the fonts are in, this element measures every
 * unit, makes the same reckoning with the real widths (keep plan() in step with ToolbarPlan.cs),
 * marks units data-ib-out (bar) and data-ib-show (menu), and sets data-ib-measured, which switches
 * the server's rules off. For the library's font the two agree, so nothing moves when it arrives.
 * It only changes attributes; both copies of every unit are rendered by the server.
 */

type Region = 0 | 1 | 2;

interface Unit {
  id: string;
  region: Region;
  width: number;
  priority: number;
  overflow: "auto" | "never" | "always";
  separator: boolean;
}

interface Layout {
  barHidden: Set<string>;
  menuShown: Set<string>;
  width: number;
  more: boolean;
}

const REGIONS = [".ib-toolbar-start", ".ib-toolbar-center", ".ib-toolbar-end"];
const CONTROLS = "a[href], button, input, select, textarea";
const TEXT_INPUT = /^(text|search|url|tel|email|password|number|)$/;

/** Units leave lowest priority first; ties from the middle outward (start from its end, center from its end, end from its start). */
function dropOrder(units: Unit[]): Unit[] {
  const droppable = units.filter((u) => !u.separator && u.overflow === "auto");
  const rank = (u: Unit) => {
    const region = droppable.filter((d) => d.region === u.region);
    const index = region.indexOf(u);
    const before = droppable.filter((d) => d.region < u.region).length;
    return before + (u.region === 2 ? index : region.length - 1 - index);
  };
  return droppable
    .map((u) => ({ u, priority: u.priority, rank: rank(u) }))
    .sort((a, b) => a.priority - b.priority || a.rank - b.rank)
    .map((x) => x.u);
}

function hideStraySeparators(units: Unit[], shown: (u: Unit) => boolean, hide: (id: string) => void): void {
  let afterItem = false;
  units.forEach((unit, i) => {
    if (!unit.separator) {
      afterItem ||= shown(unit);
      return;
    }
    const rest = units.slice(i + 1);
    const end = rest.findIndex((u) => u.region !== unit.region);
    const itemAfter = (end < 0 ? rest : rest.slice(0, end)).some((u) => !u.separator && shown(u));
    if (!afterItem || !itemAfter) hide(unit.id);
    else afterItem = false;
  });
}

function menuSeparatorShows(units: Unit[], separator: Unit, menuShown: Set<string>): boolean {
  const region = units.filter((u) => u.region === separator.region);
  const index = region.indexOf(separator);
  const before = region.slice(0, index).filter((u) => !u.separator && menuShown.has(u.id)).pop();
  const after = region.slice(index + 1).some((u) => !u.separator && menuShown.has(u.id));
  if (!before || !after) return false;
  return region.slice(region.indexOf(before) + 1).find((u) => u.separator) === separator;
}

function layout(units: Unit[], dropped: Set<string>, gap: number, moreWidth: number, more: boolean): Layout {
  const inMenu = (u: Unit) => !u.separator && (dropped.has(u.id) || u.overflow === "always");
  const barHidden = new Set(units.filter(inMenu).map((u) => u.id));
  const menuShown = new Set(barHidden);
  for (const region of [0, 1, 2]) {
    hideStraySeparators(
      units.filter((u) => u.region === region),
      (u) => !barHidden.has(u.id),
      (id) => barHidden.add(id)
    );
  }
  for (const separator of units.filter((u) => u.separator)) {
    if (menuSeparatorShows(units, separator, menuShown)) menuShown.add(separator.id);
  }
  const regionWidth = (region: Region) => {
    const shown = units.filter((u) => u.region === region && !barHidden.has(u.id));
    return shown.reduce((sum, u) => sum + u.width, 0) + Math.max(0, shown.length - 1) * gap;
  };
  const start = regionWidth(0);
  let end = regionWidth(2);
  if (more) end += (end > 0 ? gap : 0) + moreWidth;
  const width = units.some((u) => u.region === 1) ? 2 * Math.max(start, end) + regionWidth(1) + 2 * gap : start + gap + end;
  return { barHidden, menuShown, width, more };
}

/**
 * The layout for a toolbar this wide: the first, keeping the most in the bar, that fits (or the
 * last). Until the toolbar is resized, the layout the server's estimates chose (cssDropped)
 * stays when it fits and the one that keeps more is a tight fit (within a few pixels: the
 * estimates leave out kerning), so nothing moves when the script arrives.
 */
function plan(units: Unit[], available: number, gap: number, moreWidth: number, cssDropped?: Set<string>): Layout {
  const order = dropOrder(units);
  const always = units.some((u) => !u.separator && u.overflow === "always");
  const at = (k: number) => layout(units, new Set(order.slice(0, k).map((u) => u.id)), gap, moreWidth, k > 0 || always);
  let k = 0;
  let result = at(0);
  // Half a pixel of slack for rounding, so a toolbar exactly as wide as its content keeps it.
  while (result.width > available + 0.5 && k < order.length) result = at(++k);

  if (cssDropped) {
    const cssK = order.findIndex((u) => !cssDropped.has(u.id));
    const kept = cssK < 0 ? order.length : cssK;
    const matches = order.every((u, i) => cssDropped.has(u.id) === i < kept);
    if (matches && kept > k && result.width > available - 4) return at(kept);
  }
  return result;
}

class Toolbar extends IzzyElement {
  #frame = 0;
  // What the server's rules had moved into the menu when the script first measured, and the width
  // then: until the width changes, that layout stays if it's a tight call.
  #css: { dropped: Set<string>; available: number } | null = null;
  #current: HTMLElement | null = null;
  #touched = new Set<HTMLElement>();

  protected setup(signal: AbortSignal): void {
    if (this.dataset.ibRole) this.setAttribute("role", this.dataset.ibRole);

    const schedule = () => {
      cancelAnimationFrame(this.#frame);
      this.#frame = requestAnimationFrame(() => this.#update());
    };
    const resize = new ResizeObserver(schedule);
    const observeUnits = () => {
      resize.disconnect();
      resize.observe(this);
      for (const unit of this.#barUnits()) resize.observe(unit);
    };
    // Interactive pages change the content; the server's menu copies change with it.
    const mutation = new MutationObserver(() => {
      observeUnits();
      schedule();
    });
    mutation.observe(this, { childList: true, subtree: true, characterData: true });
    signal.addEventListener("abort", () => {
      resize.disconnect();
      mutation.disconnect();
      cancelAnimationFrame(this.#frame);
    });

    this.addEventListener("keydown", (event) => this.#onKey(event), { signal });
    this.addEventListener(
      "focusin",
      (event) => {
        const stop = this.#stops().find((s) => s.includes(event.target as HTMLElement));
        if (stop) this.#roving(stop);
      },
      { signal }
    );

    // Measure once the fonts are in: a fallback font is a different width.
    document.fonts.ready.then(() => {
      if (signal.aborted) return;
      this.#update();
      observeUnits();
    });
    this.#roving();
  }

  protected teardown(): void {
    for (const el of this.#touched) el.removeAttribute("tabindex");
    this.#touched.clear();
  }

  #barUnits(): HTMLElement[] {
    return [...this.querySelectorAll<HTMLElement>(".ib-toolbar-unit")].filter((u) => u.closest("ib-toolbar") === this && !u.closest("[popover]"));
  }

  #moreTrigger(): HTMLElement | null {
    return this.querySelector<HTMLElement>(":scope > .ib-toolbar-end > .ib-toolbar-more [popovertarget]");
  }

  /** Measures the units and shows the layout that fits. */
  #update(): void {
    const more = this.querySelector<HTMLElement>(":scope > .ib-toolbar-end > .ib-toolbar-more");
    if (!more) return;
    const elements = this.#barUnits();
    const menuIds = new Set([...this.querySelectorAll<HTMLElement>(".ib-toolbar-menu-unit")].map((m) => m.dataset.ibUnit));

    // Every unit at full width for one read; no paint happens before the attributes below.
    this.toggleAttribute("data-ib-measuring", true);
    const units: Unit[] = elements.map((el) => {
      const overflow = el.dataset.ibOverflow;
      return {
        id: el.dataset.ibUnit!,
        region: REGIONS.findIndex((r) => el.closest(r)?.parentElement === this) as Region,
        width: el.getBoundingClientRect().width,
        priority: Number(el.dataset.ibPriority) || 0,
        overflow: !menuIds.has(el.dataset.ibUnit) || overflow === "never" ? "never" : overflow === "always" ? "always" : "auto",
        separator: el.classList.contains("ib-toolbar-sep"),
      };
    });
    const moreWidth = more.getBoundingClientRect().width;
    this.toggleAttribute("data-ib-measuring", false);

    const style = getComputedStyle(this);
    const gap = parseFloat(style.columnGap) || 0;
    const available = this.clientWidth - parseFloat(style.paddingLeft) - parseFloat(style.paddingRight);
    // The first time (and after enhanced navigation resets the attributes), what the server's rules show.
    if (!this.hasAttribute("data-ib-measured")) {
      this.#css = { dropped: new Set(elements.filter((el) => !el.checkVisibility()).map((el) => el.dataset.ibUnit!)), available };
    }
    const result = plan(units, available, gap, moreWidth, this.#css?.available === available ? this.#css.dropped : undefined);

    // Focus in a unit that's leaving the bar goes to "…", as it would to nothing otherwise.
    const active = document.activeElement as HTMLElement | null;
    const leaving = active && elements.some((el) => el.contains(active) && result.barHidden.has(el.dataset.ibUnit!));

    for (const el of elements) el.toggleAttribute("data-ib-out", result.barHidden.has(el.dataset.ibUnit!));
    for (const el of this.querySelectorAll<HTMLElement>(".ib-toolbar-menu-unit")) {
      el.toggleAttribute("data-ib-show", result.menuShown.has(el.dataset.ibUnit!));
    }
    this.toggleAttribute("data-ib-more", result.more);
    this.toggleAttribute("data-ib-measured", true);

    if (leaving) this.#moreTrigger()?.focus();
    this.#roving();
  }

  /** The tab stops: each visible control in the bar, a radio group as one, and the "…" button. */
  #stops(): HTMLElement[][] {
    const stops: HTMLElement[][] = [];
    const groups = new Map<string, HTMLElement[]>();
    for (const el of this.querySelectorAll<HTMLElement>(CONTROLS)) {
      if (el.closest("ib-toolbar") !== this || el.closest("[popover]") || el.matches(":disabled, [type='hidden']")) continue;
      if (!el.checkVisibility()) continue;
      if (el instanceof HTMLInputElement && el.type === "radio" && el.name) {
        const group = groups.get(el.name);
        if (group) {
          group.push(el);
          continue;
        }
        groups.set(el.name, [el]);
        stops.push(groups.get(el.name)!);
        continue;
      }
      stops.push([el]);
    }
    return stops;
  }

  /** Makes one stop the toolbar's tab stop: the given one, else the last used, else the first. */
  #roving(stop?: HTMLElement[]): void {
    const stops = this.#stops();
    const wanted = stop?.[0] ?? this.#current;
    const current = stops.find((s) => wanted && s.includes(wanted)) ?? stops[0];
    if (!current) return;
    this.#current = current[0];
    for (const s of stops) {
      for (const el of s) {
        if (s === current) el.removeAttribute("tabindex");
        else el.setAttribute("tabindex", "-1");
        this.#touched.add(el);
      }
    }
  }

  #focus(stop: HTMLElement[]): void {
    const target = stop.find((el) => (el as HTMLInputElement).checked) ?? stop[0];
    this.#roving(stop);
    target.focus();
  }

  #onKey(event: KeyboardEvent): void {
    if (event.altKey || event.ctrlKey || event.metaKey || event.defaultPrevented) return;
    const target = event.target as HTMLElement;
    if (target.closest("[popover]")) return;
    const stops = this.#stops();
    const index = stops.findIndex((s) => s.includes(target));
    if (index < 0) return;

    const rtl = getComputedStyle(this).direction === "rtl";
    let step = 0;
    if (event.key === "ArrowRight") step = rtl ? -1 : 1;
    else if (event.key === "ArrowLeft") step = rtl ? 1 : -1;

    if (target instanceof HTMLInputElement && target.type === "range") return;
    if ((target instanceof HTMLInputElement && TEXT_INPUT.test(target.type)) || target instanceof HTMLTextAreaElement) {
      // A text field keeps the arrows until the caret reaches its start or end, and keeps Home and End.
      if (step === 0) return;
      const { selectionStart: from, selectionEnd: to, value } = target;
      if (from !== null && to !== null) {
        const atStart = from === 0 && to === 0;
        const atEnd = from === value.length && to === value.length;
        if ((event.key === "ArrowLeft" && !atStart) || (event.key === "ArrowRight" && !atEnd)) return;
      }
    } else if (target instanceof HTMLInputElement && target.type === "radio") {
      // Radios keep the arrows until the first or last option.
      const group = stops[index];
      const at = group.indexOf(target);
      if (step !== 0 && at + step >= 0 && at + step < group.length) return;
      if (event.key === "ArrowUp" || event.key === "ArrowDown") return;
    }

    let next: HTMLElement[] | undefined;
    if (step !== 0) next = stops[(index + step + stops.length) % stops.length];
    else if (event.key === "Home") next = stops[0];
    else if (event.key === "End") next = stops[stops.length - 1];
    if (!next) return;
    event.preventDefault();
    this.#focus(next);
  }
}

defineElement("ib-toolbar", Toolbar);
