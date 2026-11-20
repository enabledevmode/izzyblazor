import { OWN_MENU, openPanelAt, setupContextOpener } from "../context-menu";
import { forSelector } from "../for";
import { IzzyElement, defineElement } from "../izzy-element";

/** One <ib-context-menu>: what it's for, and how it opens in the one pressed. */
interface Entry {
  selector: string;
  open(region: HTMLElement, x: number, y: number): void;
}

const entries = new Set<Entry>();
let listening = false;

/** The menu whose element is nearest the press: a card's, before its list's. */
function nearest(target: Element): ((x: number, y: number) => void) | null {
  let found: { entry: Entry; region: HTMLElement } | null = null;
  for (const entry of entries) {
    const region = target.closest<HTMLElement>(entry.selector);
    if (region && (!found || (found.region !== region && found.region.contains(region)))) found = { entry, region };
  }
  return found && ((x, y) => found.entry.open(found.region, x, y));
}

/** One set of listeners for every menu, on the page: what a menu is for may come and go. */
function listen(): void {
  if (listening) return;
  listening = true;
  const root = document.documentElement;
  setupContextOpener(root, nearest, () => true, new AbortController().signal);

  // Shift+F10 fires no contextmenu event in every browser: under what has focus.
  root.addEventListener("keydown", (event) => {
    const target = event.target as Element;
    if (event.defaultPrevented || !event.shiftKey || event.key !== "F10" || target.closest(OWN_MENU)) return;
    const open = nearest(target);
    if (!open) return;
    event.preventDefault();
    const { left, bottom } = target.getBoundingClientRect();
    open(left, bottom);
  });
}

/**
 * <ib-context-menu data-for="…"> sits beside a Menu's panel (Menu ContextFor) and opens that menu
 * at the pointer by right-click, the menu key, Shift+F10 or a long press anywhere in what it's for:
 * the element with that id, or each one a selector matches when it starts with "#", "." or "[". A
 * menu whose element is nearer the press (a row's, another ContextFor's) wins. Escape or a choice
 * puts focus back where it was when the menu opened.
 *
 * The pressed element's data-menu-value says which it is: it replaces "{0}" in the items' value
 * and href, and goes to C# as `ibcontext` (ContextMenu OnOpen). A menu that uses it stays shut in
 * an element without one.
 */
class ContextMenu extends IzzyElement {
  protected setup(signal: AbortSignal): void {
    const panel = this.parentElement?.querySelector<HTMLElement>(":scope > [popover]");
    const target = this.dataset.for ?? "";
    if (!panel || !target) return;
    const selector = forSelector(target);
    try {
      document.querySelector(selector);
    } catch {
      console.warn(`IzzyBlazor: a context menu's For, "${target}", isn't an id or a selector.`, this);
      return;
    }

    // An item's Value or Href with "{0}" in it, as rendered: the pressed element's value goes there.
    const templates = new WeakMap<Element, string>();
    const fill = (item: Element, attribute: string, value: string): boolean => {
      const now = item.getAttribute(attribute) ?? "";
      // Rendered again since the last fill, or never filled.
      if (/\{0\}|%7B0%7D/i.test(now)) templates.set(item, now);
      const template = templates.get(item);
      if (template === undefined) return false;
      item.setAttribute(attribute, template.replace(/\{0\}|%7B0%7D/gi, attribute === "href" ? encodeURIComponent(value) : value));
      return true;
    };

    let back: HTMLElement | null = null;
    const entry: Entry = {
      selector,
      open: (region, x, y) => {
        const value = region.dataset.menuValue;
        let asks = this.dataset.needsValue !== undefined;
        for (const item of panel.querySelectorAll("a[href], button[value]")) {
          asks = fill(item, item instanceof HTMLAnchorElement ? "href" : "value", value ?? "") || asks;
        }
        if (asks && value === undefined) {
          console.warn("IzzyBlazor: this context menu uses the pressed element's value ({0} in an item, or OnOpen), and the element has no data-menu-value.", region);
          return;
        }
        if (value !== undefined) this.emit("ibcontext", { value });
        // What had focus in the element; else the menu's own rule (its button) applies.
        const active = document.activeElement;
        back = active instanceof HTMLElement && region.contains(active) ? active : null;
        openPanelAt(panel, region, x, y);
      },
    };
    entries.add(entry);
    signal.addEventListener("abort", () => entries.delete(entry));
    listen();

    // After the popover's own focus return (to the trigger, if the menu has one), which runs first.
    panel.addEventListener(
      "toggle",
      (event) => {
        if ((event as Event & { newState?: string }).newState !== "closed" || !back) return;
        const active = document.activeElement;
        if (back.isConnected && (!active || active === document.body || panel.contains(active) || this.parentElement!.contains(active))) back.focus();
        back = null;
      },
      { signal }
    );
  }
}

defineElement("ib-context-menu", ContextMenu);
