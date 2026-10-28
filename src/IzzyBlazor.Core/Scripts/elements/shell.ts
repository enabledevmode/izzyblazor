import { IzzyElement, defineElement } from "../izzy-element";
import { writeCookie } from "../cookies";

/**
 * <ib-shell> upgrades AppShell's mobile drawer. Without it the drawer still works through
 * CSS :target links; with it, opening does not touch the URL, Escape and the backdrop close
 * it, following a link closes it, and focus moves into the drawer and back to the toggle.
 *
 * It only sets data-sidebar-open, and role="button" and aria-expanded on the menu and close links
 * (with no script they stay links: nothing could keep aria-expanded in step). Enhanced navigation
 * re-syncs the shell's attributes with the server's markup, which also closes the drawer after
 * navigating; syncShells puts the roles back.
 *
 * For a collapsible sidebar it saves the collapse checkbox in a cookie, which the server reads
 * on the next request, and lets a group in the icon rail expand the sidebar to show its items.
 *
 * An AppAside with SmallScreen="Drawer" opens and closes the same way, with data-aside-open and
 * AsideToggle. Any aside's scroll box is a Tab stop while it scrolls, so the keyboard can scroll it.
 */
class Shell extends IzzyElement {
  #lastToggle: HTMLElement | null = null;

  private get sidebar(): HTMLElement | null {
    return this.querySelector<HTMLElement>(":scope > .ib-shell-sidebar");
  }

  private get sidebarId(): string {
    return this.sidebar?.id ?? "";
  }

  /** An aside that is a drawer below its breakpoint: the page's own when it has one (it hides the layout's). */
  private get aside(): HTMLElement | null {
    return (
      this.querySelector<HTMLElement>(":scope > .ib-shell-aside-drawer.ib-shell-aside-page") ??
      this.querySelector<HTMLElement>(":scope > .ib-shell-aside-drawer")
    );
  }

  private get asideId(): string {
    return this.aside?.id ?? "";
  }

  private togglesFor(id: string): HTMLElement[] {
    return id ? [...this.querySelectorAll<HTMLElement>(`[data-ib-shell-toggle][aria-controls="${CSS.escape(id)}"]`)] : [];
  }

  private get toggles(): HTMLElement[] {
    return this.togglesFor(this.sidebarId);
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

  private get asideOpen(): boolean {
    return this.hasAttribute("data-aside-open");
  }

  /** The menu and close links become buttons: Space presses them (below) and aria-expanded follows the drawer. */
  syncRoles(): void {
    this.querySelectorAll<HTMLElement>("a[data-ib-shell-toggle], a[data-ib-shell-close]").forEach((control) => {
      control.setAttribute("role", "button");
      // A close button in the header is shown only while the drawer is open.
      if (control.hasAttribute("data-ib-shell-close") && control.hasAttribute("aria-controls")) control.setAttribute("aria-expanded", "true");
    });
    this.toggles.forEach((t) => t.setAttribute("aria-expanded", String(this.isOpen)));
    this.togglesFor(this.asideId).forEach((t) => t.setAttribute("aria-expanded", String(this.asideOpen)));
  }

  protected setup(signal: AbortSignal): void {
    this.syncRoles();
    revealActiveNav(this);
    this.#setupAsideScroll(signal);
    this.addEventListener(
      "click",
      (event) => {
        const target = event.target as Element;
        const toggle = target.closest<HTMLElement>("[data-ib-shell-toggle]");
        if (toggle) {
          event.preventDefault();
          this.#lastToggle = toggle;
          if (this.asideId && toggle.getAttribute("aria-controls") === this.asideId) {
            this.asideOpen ? this.closeAside() : this.openAside();
          } else {
            this.isOpen ? this.close() : this.open();
          }
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

        const closer = target.closest<HTMLElement>("[data-ib-shell-close], .ib-shell-backdrop");
        if (closer) {
          event.preventDefault();
          // The aside's own close button, or a backdrop while the aside is the open drawer.
          const forAside = closer.matches(".ib-shell-backdrop")
            ? this.asideShown()
            : !!this.asideId && (!!closer.closest(".ib-shell-aside") || closer.getAttribute("aria-controls") === this.asideId);
          forAside ? this.closeAside() : this.close();
          return;
        }

        // Following a link inside the drawer closes it.
        if (this.isOpen && target.closest(".ib-shell-sidebar a[href]")) {
          this.close(false);
        }
        if (this.asideOpen && target.closest(".ib-shell-aside a[href]")) {
          this.closeAside(false);
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

    // While the window is being resized, crossing a breakpoint must not animate: the sidebar turning
    // into a closed drawer would slide away (and back, dragging to and fro). Transitions are off
    // until resizing stops.
    let resizing = 0;
    window.addEventListener(
      "resize",
      () => {
        this.setAttribute("data-ib-resizing", "");
        clearTimeout(resizing);
        resizing = window.setTimeout(() => this.removeAttribute("data-ib-resizing"), 150);
      },
      { signal }
    );
    signal.addEventListener("abort", () => clearTimeout(resizing));

    // Grown past the breakpoint (a rotated tablet) with the drawer open: it's a column again,
    // so close it and lift the inert state.
    window.addEventListener(
      "resize",
      () => {
        const sidebar = this.sidebar;
        if (this.isOpen && sidebar && getComputedStyle(sidebar).position !== "fixed") this.close(false);
        const aside = this.aside;
        if (this.asideOpen && aside && getComputedStyle(aside).position !== "fixed") {
          // Focus on the drawer's close button, now hidden, would fall to the page: keep it in the column.
          // The browser may already have dropped it to the body when the button was hidden: while the
          // drawer was open nothing else could hold focus.
          const active = document.activeElement as HTMLElement | null;
          const lost = !active || active === document.body || (aside.contains(active) && active.checkVisibility?.() === false);
          this.closeAside(false);
          if (lost) {
            // Its first control, else the aside itself (focusable from script only, until it's left).
            const first = aside.querySelector<HTMLElement>(":scope > .ib-shell-aside-inner :is(a[href], button, input, select, textarea, summary, [tabindex])");
            if (first) {
              first.focus();
            } else {
              aside.setAttribute("tabindex", "-1");
              aside.addEventListener("blur", () => aside.removeAttribute("tabindex"), { once: true });
              aside.focus({ preventScroll: true });
            }
          }
        }
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
        const aside = this.asideShown();
        if (!(aside || this.isOpen || this.hashOpen())) return;
        // Escape belongs to an open menu, popover or dialog first.
        if (document.querySelector(":modal, [popover]:popover-open")) return;
        aside ? this.closeAside() : this.close();
      },
      { signal }
    );
  }

  private hashOpen(): boolean {
    return !!this.sidebarId && location.hash === `#${this.sidebarId}`;
  }

  /** The aside drawer is open: by the script, or by a link to it before the script ran. */
  private asideShown(): boolean {
    return this.asideOpen || (!!this.asideId && location.hash === `#${this.asideId}`);
  }

  openAside(): void {
    const aside = this.aside;
    if (!aside) return;
    this.syncRoles();
    this.setAttribute("data-aside-open", "");
    this.togglesFor(aside.id).forEach((t) => t.setAttribute("aria-expanded", "true"));
    this.setInert(true, aside);
    const shown = (el: HTMLElement) => (el.checkVisibility ? el.checkVisibility() : el.offsetParent !== null);
    const close = [...aside.querySelectorAll<HTMLElement>("[data-ib-shell-close]")].find(shown);
    (close ?? [...aside.querySelectorAll<HTMLElement>("a[href], button, input, select, textarea, summary")].find(shown))?.focus();
  }

  closeAside(restoreFocus = true): void {
    const id = this.asideId;
    this.syncRoles();
    this.removeAttribute("data-aside-open");
    this.setInert(false, this.aside);
    const toggles = this.togglesFor(id);
    toggles.forEach((t) => t.setAttribute("aria-expanded", "false"));
    if (id && location.hash === `#${id}`) {
      history.replaceState(history.state, "", location.href.split("#")[0]);
    }
    if (restoreFocus) {
      (this.#lastToggle && toggles.includes(this.#lastToggle) ? this.#lastToggle : toggles[0])?.focus();
    }
  }

  /**
   * An aside's scroll box is a Tab stop only while it scrolls (a long panel beside a short page),
   * so the keyboard can scroll it when nothing in it takes focus. Asides come and go with the page
   * (an aside written in a page), so the shell's children are watched for new ones.
   */
  #setupAsideScroll(signal: AbortSignal): void {
    const update = (box: HTMLElement) => {
      if (box.scrollHeight > box.clientHeight + 1) {
        if (box.getAttribute("tabindex") !== "0") box.setAttribute("tabindex", "0");
      } else if (box.hasAttribute("tabindex") && document.activeElement !== box) {
        box.removeAttribute("tabindex");
      }
    };
    const boxes = () => [...this.querySelectorAll<HTMLElement>(":scope > .ib-shell-aside > .ib-shell-aside-inner")];
    // The box resizes with the window; its children with their content.
    const sizes = new ResizeObserver((entries) => {
      for (const { target } of entries) {
        const box = target.closest<HTMLElement>(".ib-shell-aside-inner");
        if (box) update(box);
      }
    });
    const watched = new Set<Element>();
    const scan = () => {
      // Boxes and children gone with their page are let go.
      for (const el of watched) {
        if (!el.isConnected) {
          sizes.unobserve(el);
          watched.delete(el);
        }
      }
      for (const box of boxes()) {
        for (const el of [box, ...box.children]) {
          if (!watched.has(el)) {
            sizes.observe(el);
            watched.add(el);
          }
        }
        update(box);
      }
    };
    // New asides and new content in one; enhanced navigation's merge puts the server's tabindex back.
    const changes = new MutationObserver((records) => {
      // The open drawer's aside went with its page (interactive navigation): lift the inert state.
      if (this.asideOpen && !this.aside) this.closeAside(false);
      // Toggles Blazor added since setup get their role and aria-expanded.
      if (this.querySelector("a[data-ib-shell-toggle]:not([role]), a[data-ib-shell-close]:not([role])")) this.syncRoles();
      if (records.some((r) => r.target === this || (r.target as Element).closest?.(".ib-shell-aside"))) scan();
    });
    changes.observe(this, { childList: true, subtree: true, attributes: true, attributeFilter: ["tabindex"] });
    this.addEventListener(
      "focusout",
      (event) => {
        const box = event.target as HTMLElement;
        if (box.matches?.(".ib-shell-aside-inner")) update(box);
      },
      { signal }
    );
    signal.addEventListener("abort", () => {
      sizes.disconnect();
      changes.disconnect();
    });
    scan();
  }

  open(): void {
    // A menu button Blazor added since setup has no role yet.
    this.syncRoles();
    this.setAttribute("data-sidebar-open", "");
    revealActiveNav(this);
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
   * While the drawer is open the rest of the shell is inert, so focus and screen readers stay in it;
   * the skip link too, since the main area it leads to is inert. A panel leaves the header usable:
   * its menu button has become the close button.
   */
  private setInert(on: boolean, part: HTMLElement | null = this.sidebar): void {
    const panel = part === this.sidebar && this.isPanel;
    for (const child of this.children) {
      if (child === part || child.classList.contains("ib-shell-backdrop")) continue;
      if (panel && child.classList.contains("ib-shell-header")) continue;
      child.toggleAttribute("inert", on);
    }
  }

  close(restoreFocus = true): void {
    this.syncRoles();
    this.removeAttribute("data-sidebar-open");
    this.setInert(false);
    this.toggles.forEach((t) => t.setAttribute("aria-expanded", "false"));
    if (this.hashOpen()) {
      history.replaceState(history.state, "", location.href.split("#")[0]);
    }
    if (restoreFocus) {
      (this.#lastToggle ?? this.toggles[0])?.focus();
    }
  }
}

/**
 * Brings the current page's link into view in the sidebar: on a long menu it's often below the
 * fold when a page opens (a shared link, a search result, a reload), so nothing on screen shows
 * where you are. Scrolls only the sidebar's own list, only when the link is out of view, and
 * centers it there. After nav groups holding the current page have opened.
 */
export function revealActiveNav(root: ParentNode = document): void {
  requestAnimationFrame(() => {
    root.querySelectorAll<HTMLElement>(".ib-shell-sidebar-body").forEach((list) => {
      const link =
        list.querySelector<HTMLElement>('.ib-nav-item[aria-current="page"]') ??
        list.querySelector<HTMLElement>(".ib-nav-item-active");
      if (!link) return;
      const box = list.getBoundingClientRect();
      const r = link.getBoundingClientRect();
      if (!r.height || (r.top >= box.top && r.bottom <= box.bottom)) return;
      list.scrollTop += r.top - box.top - (box.height - r.height) / 2;
    });
  });
}

/** Writes a collapsible sidebar's state to its cookie, which the server reads on the next request. */
function remember(input: HTMLInputElement, collapsed: boolean): void {
  const name = input.dataset.ibRemember;
  if (name) writeCookie(name, collapsed ? "1" : "0");
}

defineElement("ib-shell", Shell);

/** Puts the menu and close buttons' roles back after enhanced navigation reset the attributes. */
export function syncShells(): void {
  document.querySelectorAll<HTMLElement & { syncRoles?: () => void }>("ib-shell").forEach((shell) => shell.syncRoles?.());
}

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
