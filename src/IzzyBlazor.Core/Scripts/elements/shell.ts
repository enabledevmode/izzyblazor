import { IzzyElement, defineElement } from "../izzy-element";
import { writeCookie } from "../cookies";

/**
 * <ib-shell> upgrades AppShell's mobile drawer. Without it the drawer still works through
 * CSS :target links; with it, opening does not touch the URL, Escape and the backdrop close
 * it, following a link closes it, and focus moves into the drawer and back to the toggle.
 *
 * It only sets data-sidebar-open and aria-expanded. Enhanced navigation re-syncs the shell's
 * attributes with the server's markup, which also closes the drawer after navigating.
 *
 * For a collapsible sidebar it saves the collapse checkbox in a cookie, which the server reads
 * on the next request, and lets a group in the icon rail expand the sidebar to show its items.
 */
class Shell extends IzzyElement {
  #lastToggle: HTMLElement | null = null;

  private get sidebar(): HTMLElement | null {
    return this.querySelector<HTMLElement>(":scope > .ib-shell-sidebar");
  }

  private get sidebarId(): string {
    return this.sidebar?.id ?? "";
  }

  private get toggles(): HTMLElement[] {
    const id = this.sidebarId;
    return id ? [...this.querySelectorAll<HTMLElement>(`[data-ib-shell-toggle][aria-controls="${CSS.escape(id)}"]`)] : [];
  }

  private get collapseInput(): HTMLInputElement | null {
    return this.sidebar?.querySelector<HTMLInputElement>(".ib-shell-collapse-input") ?? null;
  }

  /** The collapse row is hidden below the breakpoint (it may be display: contents above it). */
  private railShown(input: HTMLInputElement): boolean {
    return !!input.parentElement && getComputedStyle(input.parentElement).display !== "none";
  }

  private get isOpen(): boolean {
    return this.hasAttribute("data-sidebar-open");
  }

  protected setup(signal: AbortSignal): void {
    this.addEventListener(
      "click",
      (event) => {
        const target = event.target as Element;
        const toggle = target.closest<HTMLElement>("[data-ib-shell-toggle]");
        if (toggle) {
          event.preventDefault();
          this.#lastToggle = toggle;
          this.isOpen ? this.close() : this.open();
          return;
        }

        // In the icon rail a group's items are hidden, so opening a group expands the sidebar.
        const groupSummary = target.closest<HTMLElement>(".ib-nav-group > summary");
        const collapse = this.collapseInput;
        if (groupSummary && collapse?.checked && this.railShown(collapse)) {
          event.preventDefault();
          (groupSummary.parentElement as HTMLDetailsElement).open = true;
          collapse.checked = false;
          collapse.dispatchEvent(new Event("change", { bubbles: true }));
          return;
        }

        if (target.closest("[data-ib-shell-close], .ib-shell-backdrop")) {
          event.preventDefault();
          this.close();
          return;
        }

        // Following a link inside the drawer closes it.
        if (this.isOpen && target.closest(".ib-shell-sidebar a[href]")) {
          this.close(false);
        }
      },
      { signal }
    );

    // Saved when someone flips it, and when C# sets it on an interactive page (Blazor sets the
    // checked property, which fires no event, so data-ib-collapsed carries the change).
    this.addEventListener(
      "change",
      (event) => {
        const input = event.target as HTMLInputElement;
        if (input.matches?.(".ib-shell-collapse-input")) remember(input, input.checked);
      },
      { signal }
    );
    const observer = new MutationObserver((records) => {
      for (const { target } of records) {
        const input = target as HTMLInputElement;
        if (input.matches(".ib-shell-collapse-input")) remember(input, input.dataset.ibCollapsed === "1");
      }
    });
    observer.observe(this, { subtree: true, attributes: true, attributeFilter: ["data-ib-collapsed"] });
    signal.addEventListener("abort", () => observer.disconnect());

    // Grown past the breakpoint (a rotated tablet) with the drawer open: it's a column again,
    // so close it and lift the inert state.
    window.addEventListener(
      "resize",
      () => {
        const sidebar = this.sidebar;
        if (this.isOpen && sidebar && getComputedStyle(sidebar).position !== "fixed") this.close(false);
      },
      { signal }
    );

    // The toggle and close controls are links with role="button": Space should press them too.
    this.addEventListener(
      "keydown",
      (event) => {
        const control = (event.target as Element).closest<HTMLElement>("a[data-ib-shell-toggle], a[data-ib-shell-close]");
        if (control && event.key === " ") {
          event.preventDefault();
          control.click();
        }
      },
      { signal }
    );

    document.addEventListener(
      "keydown",
      (event) => {
        if (event.key !== "Escape" || event.defaultPrevented || event.isComposing) return;
        if (!(this.isOpen || this.hashOpen())) return;
        // Escape belongs to an open menu, popover or dialog first.
        if (document.querySelector(":modal, [popover]:popover-open")) return;
        this.close();
      },
      { signal }
    );
  }

  private hashOpen(): boolean {
    return !!this.sidebarId && location.hash === `#${this.sidebarId}`;
  }

  open(): void {
    this.setAttribute("data-sidebar-open", "");
    this.toggles.forEach((t) => t.setAttribute("aria-expanded", "true"));
    this.setInert(true);
    // The close button first: with a SidebarHeader it comes after the header's links in the row.
    // A panel has none (its close button is in the header), so focus goes to its first link.
    const shown = (el: HTMLElement) => (el.checkVisibility ? el.checkVisibility() : el.offsetParent !== null);
    const sidebar = this.sidebar;
    const close = sidebar ? [...sidebar.querySelectorAll<HTMLElement>("[data-ib-shell-close]")].find(shown) : undefined;
    (close ?? (sidebar ? [...sidebar.querySelectorAll<HTMLElement>("a[href], button, input, summary")].find(shown) : undefined))?.focus();
  }

  /** Below its breakpoint a SmallScreen="Panel" sidebar opens under the header, which stays usable. */
  private get isPanel(): boolean {
    return !!this.sidebar?.classList.contains("ib-shell-sidebar-panel");
  }

  /**
   * While the drawer is open the rest of the shell is inert, so focus and screen readers stay in it.
   * A panel leaves the header usable: its menu button has become the close button.
   */
  private setInert(on: boolean): void {
    const panel = this.isPanel;
    for (const child of this.children) {
      if (child === this.sidebar || child.classList.contains("ib-shell-backdrop") || child.classList.contains("ib-skip-link")) continue;
      if (panel && child.classList.contains("ib-shell-header")) continue;
      child.toggleAttribute("inert", on);
    }
  }

  close(restoreFocus = true): void {
    this.removeAttribute("data-sidebar-open");
    this.setInert(false);
    this.toggles.forEach((t) => t.setAttribute("aria-expanded", "false"));
    if (this.hashOpen()) {
      history.replaceState(history.state, "", location.pathname + location.search);
    }
    if (restoreFocus) {
      (this.#lastToggle ?? this.toggles[0])?.focus();
    }
  }
}

/** Writes a collapsible sidebar's state to its cookie, which the server reads on the next request. */
function remember(input: HTMLInputElement, collapsed: boolean): void {
  const name = input.dataset.ibRemember;
  if (name) writeCookie(name, collapsed ? "1" : "0");
}

defineElement("ib-shell", Shell);

/**
 * In ScrollMode.Main the page itself never scrolls, so the browser's scroll-to-top after an
 * enhanced navigation doesn't reach the main area. Called after each enhanced navigation.
 */
let lastPath = typeof location === "undefined" ? "" : location.pathname;

export function resetShellScroll(): void {
  // enhancedload also fires for form posts, streaming updates and filter changes on the same
  // page; only a move to another page should scroll back to the top.
  const path = location.pathname;
  const moved = path !== lastPath;
  lastPath = path;
  if (!moved || location.hash) return;
  document.querySelectorAll<HTMLElement>(".ib-shell-scroll-main > .ib-shell-main").forEach((main) => {
    main.scrollTop = 0;
  });
}
