import { IzzyElement, defineElement } from "../izzy-element";
import { track } from "../position";

const FOCUSABLE = "a[href], button, input, select, textarea, summary, [tabindex]:not([tabindex='-1'])";

/** Whether this browser understands popover="hint", checked once. */
const hintSupported = (() => {
  if (typeof HTMLElement === "undefined" || !("popover" in HTMLElement.prototype)) return false;
  const probe = document.createElement("div");
  probe.setAttribute("popover", "hint");
  return (probe as HTMLElement & { popover: string | null }).popover === "hint";
})();

/**
 * <ib-tooltip> upgrades a CSS-only Tooltip:
 *   - links it to its trigger with aria-describedby (unless the trigger's label says the same),
 *   - moves it to the top layer as popover="hint" (or "manual"), so overflow can't clip it,
 *   - shows it after the hover delay or at once on keyboard focus, and hides it on Escape.
 */
class TooltipRoot extends IzzyElement {
  #timer = 0;
  #stopTracking: (() => void) | null = null;

  private get tip(): HTMLElement | null {
    return this.querySelector<HTMLElement>(":scope > .ib-tooltip");
  }

  private get target(): HTMLElement | null {
    const tip = this.tip;
    return [...this.querySelectorAll<HTMLElement>(FOCUSABLE)].find((el) => !tip?.contains(el)) ?? null;
  }

  protected setup(signal: AbortSignal): void {
    const tip = this.tip;
    if (!tip) return;
    const target = this.target;

    if (target && tip.id) {
      const label = target.getAttribute("aria-label")?.trim();
      const described = (target.getAttribute("aria-describedby") ?? "").split(/\s+/);
      if (label !== tip.textContent?.trim() && !described.includes(tip.id)) {
        target.setAttribute("aria-describedby", [...described.filter(Boolean), tip.id].join(" "));
      }
    }

    if (!("popover" in HTMLElement.prototype)) return; // Stay CSS-only.
    tip.setAttribute("popover", hintSupported ? "hint" : "manual");

    const delay = Number(this.dataset.delay ?? 300);
    this.addEventListener("pointerenter", () => this.schedule(() => this.show(), delay), { signal });
    this.addEventListener("pointerleave", () => this.schedule(() => this.hide(), 100), { signal });
    this.addEventListener(
      "focusin",
      (event) => {
        if ((event.target as Element).matches(":focus-visible")) this.schedule(() => this.show(), 0);
      },
      { signal }
    );
    this.addEventListener("focusout", () => this.schedule(() => this.hide(), 0), { signal });
    document.addEventListener(
      "keydown",
      (event) => {
        if (event.key === "Escape") this.hide();
      },
      { signal }
    );
  }

  protected teardown(): void {
    clearTimeout(this.#timer);
    this.#stopTracking?.();
    this.#stopTracking = null;
  }

  private schedule(action: () => void, ms: number): void {
    clearTimeout(this.#timer);
    this.#timer = window.setTimeout(action, ms);
  }

  private show(): void {
    const tip = this.tip;
    if (!tip || tip.matches(":popover-open")) return;
    try {
      tip.showPopover();
    } catch {
      return;
    }
    this.#stopTracking = track(tip, this, this.dataset.placement ?? "top");
  }

  private hide(): void {
    clearTimeout(this.#timer);
    this.#stopTracking?.();
    this.#stopTracking = null;
    const tip = this.tip;
    if (tip?.matches(":popover-open")) tip.hidePopover();
  }
}

defineElement("ib-tooltip", TooltipRoot);
