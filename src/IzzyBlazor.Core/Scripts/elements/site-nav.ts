import { IzzyElement, defineElement } from "../izzy-element";

/**
 * <ib-site-nav> upgrades SiteNav's panel. The panel is a native popover toggled by its menu
 * button, so it opens, light-dismisses and closes on Escape with no script. With script it also
 * moves focus to the first link when it opens, closes when a link in it is followed (enhanced
 * navigation keeps the page, so the popover would stay open), and closes when the screen grows
 * past the breakpoint and the row of links comes back.
 */
class SiteNav extends IzzyElement {
  private get panel(): HTMLElement | null {
    return this.querySelector<HTMLElement>(":scope > .ib-site-nav-panel");
  }

  private get isOpen(): boolean {
    return !!this.panel?.matches(":popover-open");
  }

  protected setup(signal: AbortSignal): void {
    this.addEventListener(
      "click",
      (event) => {
        const link = (event.target as Element).closest("a[href]");
        if (link && this.isOpen && this.panel?.contains(link)) this.panel.hidePopover();
      },
      { signal }
    );

    // toggle doesn't bubble; listening in the capture phase still sees the panel's.
    this.addEventListener(
      "toggle",
      (event) => {
        const panel = event.target as HTMLElement;
        if (panel !== this.panel || (event as ToggleEvent).newState !== "open") return;
        panel.querySelector<HTMLElement>("a[href], button:not([tabindex='-1'], [aria-hidden='true']), [tabindex]:not([tabindex='-1'])")?.focus();
      },
      { signal, capture: true }
    );

    window.addEventListener(
      "resize",
      () => {
        const toggle = this.querySelector<HTMLElement>(":scope > .ib-site-nav-toggle");
        if (this.isOpen && toggle && getComputedStyle(toggle).display === "none") this.panel?.hidePopover();
      },
      { signal }
    );
  }
}

defineElement("ib-site-nav", SiteNav);
