import { IzzyElement, defineElement } from "../izzy-element";

/**
 * <ib-speed-dial> sits beside every SpeedDial's button.
 *
 * Arrow keys follow the screen. The actions are in the markup nearest the button first, and the
 * line can run up, down, toward the start or toward the end, so the menu's own keys (Down for the
 * next in the markup) would run backwards upward. Here the arrow pointing away from the button
 * moves to the next action, and the opposite one back toward the button, wrapping as a menu does.
 *
 * With data-ib-hover (OpenOn="Hover"), the actions show as a mouse comes over the button and go a
 * moment after it leaves both. The button's click still opens them, so touch and keyboard work as
 * with a click-to-open SpeedDial, and actions opened that way, or reached with a key, stay until
 * closed as usual.
 */
class SpeedDial extends IzzyElement {
  protected setup(signal: AbortSignal): void {
    const root = this.parentElement;
    const panel = root?.querySelector<HTMLElement>(":scope > [popover]");
    if (!root || !panel) return;
    const open = () => panel.matches(":popover-open");
    let byHover = false;

    // Before the menu's own keys (on the same root, in the bubble phase).
    root.addEventListener(
      "keydown",
      (event) => {
        const onButton = (event.target as Element).matches(".ib-sd-button");
        if (event.altKey || event.ctrlKey || event.metaKey || !(onButton || panel.contains(event.target as Node))) return;
        byHover = false;
        const rtl = getComputedStyle(panel).direction === "rtl";
        const [away, back] = panel.classList.contains("ib-sd-down") ? ["ArrowDown", "ArrowUp"]
          : panel.classList.contains("ib-sd-start") ? (rtl ? ["ArrowRight", "ArrowLeft"] : ["ArrowLeft", "ArrowRight"])
          : panel.classList.contains("ib-sd-end") ? (rtl ? ["ArrowLeft", "ArrowRight"] : ["ArrowRight", "ArrowLeft"])
          : ["ArrowUp", "ArrowDown"];
        if (event.key !== away && event.key !== back) return;
        // On the button, the arrow toward the actions opens them at the nearest; the menu's own
        // keys would open the farthest for Up.
        if (onButton) {
          if (event.key !== away) return;
          event.preventDefault();
          event.stopPropagation();
          if (!open()) panel.showPopover();
          return;
        }
        const items = [...panel.querySelectorAll<HTMLElement>(".ib-sd-action")].filter((item) => !item.matches(":disabled, [aria-disabled='true']"));
        if (items.length === 0) return;
        const index = items.indexOf(document.activeElement as HTMLElement);
        const step = event.key === away ? 1 : -1;
        items[(index + step + items.length) % items.length].focus();
        event.preventDefault();
        event.stopPropagation();
      },
      { signal, capture: true }
    );

    if (!this.hasAttribute("data-ib-hover")) return;
    let timer = 0;
    root.addEventListener(
      "pointerenter",
      (event) => {
        if (event.pointerType !== "mouse") return;
        clearTimeout(timer);
        if (!open()) {
          byHover = true;
          panel.showPopover();
        }
      },
      { signal }
    );
    root.addEventListener(
      "pointerleave",
      (event) => {
        if (event.pointerType !== "mouse" || !byHover) return;
        // A short pause, so a pointer crossing the gap to an action doesn't close them.
        timer = window.setTimeout(() => {
          if (byHover && open()) panel.hidePopover();
        }, 300);
      },
      { signal }
    );
    panel.addEventListener(
      "toggle",
      (event) => {
        if ((event as Event & { newState?: string }).newState === "closed") byHover = false;
      },
      { signal }
    );
    signal.addEventListener("abort", () => clearTimeout(timer));
  }
}

defineElement("ib-speed-dial", SpeedDial);
