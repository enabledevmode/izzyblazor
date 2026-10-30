import { IzzyElement, defineElement } from "../izzy-element";
import { extendRange, setupLoadOnScroll, setupSelectAll, setupSelection, type RangeRun } from "../data-view";

/**
 * <ib-listview> adds a ListView's selection (select-all, Shift ranges, the bar, and on static pages a selection across pages) and loading on scroll (Paging
 * LoadOnScroll), both shared with <ib-table>, and arrow keys between items. Everything else works without it.
 *
 * Arrows are a shortcut on top of the Tab order, which stays as it is: Up and Down go to the same
 * kind of control in the item above or below, Left and Right to the one before or after where
 * items sit side by side, Home and End to the first and last item. Disabled items and items with
 * nothing to focus are passed over. At an end the key does what it did. A menu button keeps its
 * own arrows (they open its menu), so it's not a stop.
 */

const CONTROLS = "a[href], button:not([popovertarget]), input:not([type=hidden]), select, textarea, summary";
// Controls that use these keys themselves.
const KEEPS_KEYS = "input:not([type=checkbox], [type=button], [type=submit], [type=reset]), select, textarea, [contenteditable]:not([contenteditable=false]), [role=combobox], [role=slider], [role=spinbutton]";
const KINDS = [".ib-listview-action", ".ib-listview-select"];
const BOX = ".ib-listview-select[type=checkbox]";

class ListView extends IzzyElement {
  private run: RangeRun | null = null;

  protected setup(signal: AbortSignal): void {
    setupSelectAll(this, ".ib-listview-select-all", ".ib-listview-list > li > .ib-listview-select", signal);
    setupSelection(this, ".ib-listview-list > li > .ib-listview-select", signal);
    setupLoadOnScroll(this, ":scope > .ib-listview-body > .ib-listview-list > li:last-child", ":scope > .ib-listview-body > .ib-data-more [data-ib-load-next]", signal);
    this.addEventListener("keydown", (event) => this.onKey(event), { signal });
    this.addEventListener("pointerdown", () => (this.run = null), { signal });
    // An item's stretched link has no height of its own, so the browser scrolls only that much of
    // the item into view. The whole item, for arrows and Tab alike.
    this.addEventListener(
      "focusin",
      (event) => {
        const link = event.target as HTMLElement;
        if (link.matches(".ib-listview-action:focus-visible")) link.closest(".ib-listview-item")?.scrollIntoView({ block: "nearest" });
      },
      { signal }
    );
  }

  private get items(): HTMLElement[] {
    return [...this.querySelectorAll<HTMLElement>(".ib-listview-list > .ib-listview-item:not(.ib-listview-skeleton)")].filter((item) => item.closest("ib-listview") === this);
  }

  /** The item's own controls that arrows can reach: none in its menus or dialogs, or a list inside it. */
  private controls(item: Element): HTMLElement[] {
    return [...item.querySelectorAll<HTMLElement>(CONTROLS)].filter(
      (el) => el.closest("ib-listview") === this && el.closest(".ib-listview-item") === item && !el.closest("[popover], dialog") && !el.matches(":disabled") && el.checkVisibility()
    );
  }

  /** The control in another item that matches this one: the same kind, or the same place among the rest, or the item's first. */
  private match(control: HTMLElement, item: Element): HTMLElement | undefined {
    const there = this.controls(item);
    const kind = KINDS.find((k) => control.matches(k));
    if (kind) return there.find((el) => el.matches(kind)) ?? there[0];
    const here = control.closest(".ib-listview-item");
    const others = (list: HTMLElement[]) => list.filter((el) => !KINDS.some((k) => el.matches(k)));
    const index = here ? others(this.controls(here)).indexOf(control) : -1;
    return others(there)[index] ?? there[0];
  }

  /** The nearest of these items in the next row up or down, closest across. */
  private rowNeighbour(items: HTMLElement[], from: HTMLElement, down: boolean): HTMLElement | undefined {
    const at = from.getBoundingClientRect();
    const middle = (r: DOMRect) => r.left + r.width / 2;
    let best: HTMLElement | undefined;
    let bestRow = Infinity;
    let bestAcross = Infinity;
    for (const item of items) {
      if (item === from) continue;
      const r = item.getBoundingClientRect();
      // A pixel of slack: dividers and rounding can make neighbours overlap.
      if (down ? r.top < at.bottom - 1 : r.bottom > at.top + 1) continue;
      const row = Math.round(down ? r.top - at.bottom : at.top - r.bottom);
      const across = Math.abs(middle(r) - middle(at));
      if (row < bestRow || (row === bestRow && across < bestAcross)) {
        best = item;
        bestRow = row;
        bestAcross = across;
      }
    }
    return best;
  }

  /** The item a key goes to from `from`, among those `can` lets it land on. */
  private neighbour(key: string, from: HTMLElement, can: (item: HTMLElement) => boolean): HTMLElement | undefined {
    const all = this.items;
    const allowed = all.filter((item) => item !== from && can(item));
    const index = all.indexOf(from);
    // Whether items sit side by side: one starts above where the one before it ends.
    const sideBySide = () => all.some((item, i) => i > 0 && item.getBoundingClientRect().top < all[i - 1].getBoundingClientRect().bottom - 1);
    const rtl = getComputedStyle(this).direction === "rtl";
    const before = allowed.filter((item) => all.indexOf(item) < index);
    const after = allowed.filter((item) => all.indexOf(item) > index);
    switch (key) {
      case "ArrowDown":
        return this.rowNeighbour(allowed, from, true);
      case "ArrowUp":
        return this.rowNeighbour(allowed, from, false);
      case "ArrowRight":
        return !sideBySide() ? undefined : rtl ? before[before.length - 1] : after[0];
      case "ArrowLeft":
        return !sideBySide() ? undefined : rtl ? after[0] : before[before.length - 1];
      case "Home":
        return before[0];
      case "End":
        return after[after.length - 1];
      default:
        return undefined;
    }
  }

  private onKey(event: KeyboardEvent): void {
    if (event.key !== "Shift" && !event.shiftKey) this.run = null;
    if (event.defaultPrevented || event.altKey) return;
    const control = event.target as HTMLElement;
    const item = control.closest<HTMLElement>(".ib-listview-item");
    if (!item || item.closest("ib-listview") !== this || control.matches(KEEPS_KEYS)) return;
    // An item can have focus itself: Load more puts it on the first new one.
    if (control !== item && !this.controls(item).includes(control)) return;

    if (control.matches(BOX) && this.selectionKey(event, control as HTMLInputElement, item)) return;
    if (event.shiftKey || event.ctrlKey || event.metaKey) return;

    // A link goes to the next link, a checkbox to the next checkbox, past items without one if there are others.
    const enabled = (el: HTMLElement) => !el.matches(".ib-listview-disabled") && this.controls(el).length > 0;
    const kind = KINDS.find((k) => control.matches(k));
    const hasKind = (el: HTMLElement) => enabled(el) && this.controls(el).some((c) => c.matches(kind!));
    const next = (kind && this.neighbour(event.key, item, hasKind)) || this.neighbour(event.key, item, enabled);
    const to = next && this.match(control, next);
    if (!to) return;
    event.preventDefault();
    to.focus();
  }

  /** Shift+arrows and Ctrl+A on a selection checkbox. Whether it was one of those. */
  private selectionKey(event: KeyboardEvent, box: HTMLInputElement, item: HTMLElement): boolean {
    if ((event.ctrlKey || event.metaKey) && !event.shiftKey && event.key.toLowerCase() === "a") {
      event.preventDefault();
      const all = this.querySelector<HTMLInputElement>(".ib-listview-select-all");
      if (all && !all.checked) all.click();
      return true;
    }
    if (!event.shiftKey || event.ctrlKey || event.metaKey || !event.key.startsWith("Arrow")) return false;

    const boxOf = (el: Element) => el.querySelector<HTMLInputElement>(`:scope > ${BOX}:not(:disabled)`);
    const next = this.neighbour(event.key, item, (el) => !el.matches(".ib-listview-disabled") && !!boxOf(el));
    const to = next && boxOf(next);
    if (!to) return false;
    event.preventDefault();

    this.run = extendRange(this.run, this.items.map(boxOf).filter((b): b is HTMLInputElement => !!b), box, to);
    to.focus();
    return true;
  }
}

defineElement("ib-listview", ListView);
