import { IzzyElement, defineElement } from "../izzy-element";

/**
 * <ib-speed-dial> sits beside a SpeedDial's button when it opens on hover (OpenOn="Hover"): the
 * actions show as a mouse comes over the button and go a moment after it leaves both. The button's
 * click still opens them, so touch and keyboard work as with a click-to-open SpeedDial, and actions
 * opened that way, or reached with a key, stay until closed as usual.
 */
class SpeedDial extends IzzyElement {
  protected setup(signal: AbortSignal): void {
    const root = this.parentElement;
    const panel = root?.querySelector<HTMLElement>(":scope > [popover]");
    if (!root || !panel) return;
    let timer = 0;
    let byHover = false;
    const open = () => panel.matches(":popover-open");
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
    // A key in the actions means someone is using them: they stay until closed as usual.
    panel.addEventListener("keydown", () => (byHover = false), { signal });
    panel.addEventListener("toggle", (event) => {
      if ((event as Event & { newState?: string }).newState === "closed") byHover = false;
    }, { signal });
    signal.addEventListener("abort", () => clearTimeout(timer));
  }
}

defineElement("ib-speed-dial", SpeedDial);
