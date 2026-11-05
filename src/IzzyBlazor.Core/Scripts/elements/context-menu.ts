import { IzzyElement, defineElement } from "../izzy-element";
import { OWN_MENU, openPanelAt, setupContextOpener } from "../context-menu";

/**
 * <ib-context-menu data-for="id"> sits beside a Menu's panel (Menu ContextFor) and opens that menu
 * at the pointer by right-click, the menu key, Shift+F10 or a long press anywhere in the element
 * with that id. A menu inside it that's nearer the press (a row's, another ContextFor's) wins.
 * Escape or a choice puts focus back where it was when the menu opened.
 */
class ContextMenu extends IzzyElement {
  protected setup(signal: AbortSignal): void {
    const region = document.getElementById(this.dataset.for ?? "");
    const panel = this.parentElement?.querySelector<HTMLElement>(":scope > [popover]");
    if (!region || !panel) return;

    let back: HTMLElement | null = null;
    const open = (x: number, y: number) => {
      // What had focus in the element; else the menu's own rule (its button) applies.
      const active = document.activeElement;
      back = active instanceof HTMLElement && region.contains(active) ? active : null;
      openPanelAt(panel, region, x, y);
    };
    setupContextOpener(region, () => open, () => true, signal);

    // Shift+F10 fires no contextmenu event in every browser: under what has focus.
    region.addEventListener(
      "keydown",
      (event) => {
        const target = event.target as Element;
        if (event.defaultPrevented || !event.shiftKey || event.key !== "F10" || target.closest(OWN_MENU)) return;
        event.preventDefault();
        const { left, bottom } = target.getBoundingClientRect();
        open(left, bottom);
      },
      { signal }
    );

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
