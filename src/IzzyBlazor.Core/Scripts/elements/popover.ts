import { IzzyElement, defineElement } from "../izzy-element";
import { track } from "../position";

/**
 * <ib-popover> wraps a Menu or Popover: the trigger button and its [popover] panel.
 * Opening, Escape and light dismiss are native; this element adds:
 *   - placement in browsers without CSS anchor positioning,
 *   - data-open: the server or C# (@bind-Open) can open and close the panel,
 *   - for menus: focus on open, arrow keys, Home and End, type-ahead, and closing after a choice.
 *   - for submenus (an <ib-popover> inside a menu): opening on hover and with the arrow key toward
 *     it, closing with the arrow key back; choosing an item closes the whole menu.
 *   - for popovers (role="dialog"): focus moves into the panel when the user opens it.
 *   - on close, focus goes back to the trigger if it was inside the panel.
 *
 * Menu roles promise arrow keys, so static pages render them as data-ib-role, data-ib-checked
 * and data-ib-haspopup; applyMenuRoles turns them into ARIA here, before the keys are added.
 * With JavaScript off the items stay plain links and buttons.
 */

const ITEM = "[role='menuitem'], [role='menuitemcheckbox'], [role='menuitemradio']";
const MENU_PANEL = "[popover]:is([role='menu'], [data-ib-role='menu'])";

/** Turns the server's data-ib-* menu attributes into ARIA. Safe to run again. */
function applyMenuRoles(root: Element): void {
  root.querySelectorAll<HTMLElement>("[data-ib-role]").forEach((el) => {
    el.setAttribute("role", el.dataset.ibRole!);
  });
  root.querySelectorAll<HTMLElement>("[data-ib-haspopup]").forEach((el) => {
    el.setAttribute("aria-haspopup", el.dataset.ibHaspopup!);
  });
  root.querySelectorAll<HTMLElement>("[data-ib-checked]").forEach((el) => {
    // aria-checked replaces the no-script stand-ins.
    el.setAttribute("aria-checked", el.dataset.ibChecked!);
    el.removeAttribute("aria-pressed");
    if (el.getAttribute("aria-current") === "true") el.removeAttribute("aria-current");
  });
}
class PopoverRoot extends IzzyElement {
  static observedAttributes = ["data-open"];

  #stopTracking: (() => void) | null = null;
  #typed = "";
  #typedAt = 0;
  #focusLast = false;
  // What sync() last asked for, so its own toggle isn't mistaken for the user's.
  #syncTarget: boolean | null = null;
  // The user's latest open or close, so a render that was already on its way can't undo it.
  #userState: boolean | null = null;
  // The data-open sync() last acted on: only a change opens or closes the panel, so a render or
  // enhanced post that leaves it as it was doesn't close a panel the user opened.
  #lastWant: boolean | null = null;
  #userAt = 0;
  // Opened by hovering: don't move focus into it.
  #hoverOpen = false;
  // When focus last left the panel for nowhere (a click on a blank area), to tell a dismiss that
  // took focus from inside the panel from one that didn't.
  #focusLeftAt = 0;
  #hoverTimer = 0;

  private get panel(): HTMLElement | null {
    return this.querySelector<HTMLElement>(":scope > [popover]");
  }

  private get trigger(): HTMLElement | null {
    const id = this.panel?.id;
    return id ? this.querySelector<HTMLElement>(`[popovertarget="${CSS.escape(id)}"]`) : null;
  }

  private get isMenu(): boolean {
    return this.panel?.getAttribute("role") === "menu";
  }

  /** The menu panel this submenu sits in, if it is one. */
  private get parentPanel(): HTMLElement | null {
    return this.parentElement?.closest<HTMLElement>(MENU_PANEL) ?? null;
  }

  /** The outermost menu panel, which closes every submenu with it. */
  private get rootPanel(): HTMLElement | null {
    let root = this.panel;
    let up = this.parentPanel;
    while (up) {
      root = up;
      up = up.parentElement?.closest<HTMLElement>(MENU_PANEL) ?? null;
    }
    return root;
  }

  private items(): HTMLElement[] {
    const panel = this.panel;
    if (!panel) return [];
    return [...panel.querySelectorAll<HTMLElement>(ITEM)].filter(
      (item) => !item.matches(":disabled, [aria-disabled='true']") && item.closest("[popover]") === panel
    );
  }

  protected setup(signal: AbortSignal): void {
    const panel = this.panel;
    // Without the Popover API the panel is ordinary content; there's nothing to enhance.
    if (!panel || typeof panel.showPopover !== "function") return;

    applyMenuRoles(this);

    panel.addEventListener("toggle", (event) => this.onToggle((event as Event & { newState?: string }).newState === "open"), { signal });
    panel.addEventListener(
      "focusout",
      (event) => {
        if (!(event as FocusEvent).relatedTarget) this.#focusLeftAt = Date.now();
      },
      { signal }
    );

    if (this.isMenu) {
      panel.addEventListener("keydown", (event) => this.onMenuKey(event), { signal });
      // A submenu's trigger is an item of the menu around it, which handles its keys.
      if (!this.parentPanel) {
        this.trigger?.addEventListener("keydown", (event) => this.onTriggerKey(event), { signal });
      } else {
        this.setupHover(panel, signal);
      }
      // Links and submit buttons don't close the menu by themselves; choosing one in a submenu
      // closes the whole menu.
      panel.addEventListener(
        "click",
        (event) => {
          const item = (event.target as Element).closest<HTMLElement>(ITEM);
          if (!item || !this.items().includes(item) || item.hasAttribute("aria-haspopup") || item.hasAttribute("data-ib-keep-open")) return;
          const root = this.rootPanel;
          if (root?.matches(":popover-open")) root.hidePopover();
        },
        { signal }
      );
    }

    this.sync();
  }

  protected teardown(): void {
    this.#stopTracking?.();
    this.#stopTracking = null;
  }

  attributeChangedCallback(): void {
    if (this.isConnected) this.sync();
  }

  /** Opens or closes the panel to match data-open. */
  private sync(): void {
    const panel = this.panel;
    if (!panel || typeof panel.showPopover !== "function") return;
    const want = this.hasAttribute("data-open");
    if (want === this.#lastWant) return;
    const open = panel.matches(":popover-open");
    // Until the server has caught up with the user's last open or close, a render that left
    // before it mustn't undo it. Once data-open matches what the user did, C# is in charge again.
    if (this.#userState !== null) {
      if (want === this.#userState || Date.now() - this.#userAt >= 2000) this.#userState = null;
      else return;
    }
    this.#lastWant = want;
    this.#syncTarget = want;
    if (want && !open) {
      try {
        (panel.showPopover as (options?: { source?: HTMLElement | null }) => void)({ source: this.trigger });
      } catch {
        // Not connected yet, or another modal is open; the next change will retry.
      }
    } else if (!want && open) {
      // A panel only the user opens (a Table filter) stays open through renders and enhanced
      // navigations that don't mention it, such as the one its own live filter just made.
      if (panel.hasAttribute("data-ib-user-controlled")) return;
      panel.hidePopover();
    }
  }

  private onToggle(open: boolean): void {
    const panel = this.panel;
    if (!panel) return;
    const fromSync = this.#syncTarget === open;
    if (fromSync) {
      this.#syncTarget = null;
    } else {
      this.#userState = open;
      this.#userAt = Date.now();
    }
    if (open) {
      this.#stopTracking?.();
      this.#stopTracking = track(panel, this, this.dataset.placement ?? "bottom-start");
      // A menu the user opened takes focus; one the server or C# opened (data-open) doesn't.
      if (this.isMenu && !this.#hoverOpen && !fromSync) {
        const items = this.items();
        (this.#focusLast ? items[items.length - 1] : items[0])?.focus();
      } else if (!this.isMenu && !fromSync) {
        // A popover opened by the user: focus its [autofocus] element, or the panel itself so it's
        // read by its title and Tab moves through it. One opened by the server or C# leaves focus.
        const target = panel.querySelector<HTMLElement>("[autofocus]");
        if (target) target.focus();
        else {
          if (!panel.hasAttribute("tabindex")) panel.setAttribute("tabindex", "-1");
          panel.focus();
        }
      }
      this.#focusLast = false;
      this.#hoverOpen = false;
    } else {
      this.#stopTracking?.();
      this.#stopTracking = null;
      // Back to the trigger if focus was in the panel: still inside (Escape, a choice), or just
      // dropped to the page by the click that dismissed it.
      const dropped = document.activeElement === document.body && Date.now() - this.#focusLeftAt < 300;
      if (panel.contains(document.activeElement) || dropped) {
        this.trigger?.focus();
      }
    }
  }

  private onTriggerKey(event: KeyboardEvent): void {
    if (event.key !== "ArrowDown" && event.key !== "ArrowUp") return;
    if (event.altKey || event.ctrlKey || event.metaKey) return;
    const panel = this.panel;
    if (!panel) return;
    event.preventDefault();
    const items = this.items();
    if (panel.matches(":popover-open")) {
      (event.key === "ArrowDown" ? items[0] : items[items.length - 1])?.focus();
    } else {
      // The toggle event focuses an item once the panel is open.
      this.#focusLast = event.key === "ArrowUp";
      panel.showPopover();
    }
  }

  private onMenuKey(event: KeyboardEvent): void {
    // Keys in a submenu belong to the submenu's own element.
    if ((event.target as Element).closest("[popover]") !== this.panel) return;
    const items = this.items();
    if (items.length === 0) return;
    const index = items.indexOf(document.activeElement as HTMLElement);
    let next: HTMLElement | undefined;
    if ((event.altKey || event.ctrlKey || event.metaKey) && event.key !== "Tab") return;

    switch (event.key) {
      case "ArrowDown":
        next = items[(index + 1) % items.length];
        break;
      case "ArrowUp":
        next = items[(index - 1 + items.length) % items.length];
        break;
      case "Home":
        next = items[0];
        break;
      case "End":
        next = items[items.length - 1];
        break;
      case "Tab":
        this.rootPanel?.hidePopover();
        return;
      case "ArrowRight":
      case "ArrowLeft": {
        const rtl = getComputedStyle(this).direction === "rtl";
        const inward = (event.key === "ArrowRight") !== rtl;
        const current = items[index];
        if (inward && current?.hasAttribute("aria-haspopup")) {
          // Open the submenu; its element focuses its first item.
          const sub = document.getElementById(current.getAttribute("popovertarget") ?? "");
          if (sub && !sub.matches(":popover-open")) sub.showPopover();
          else
            [...(sub?.querySelectorAll<HTMLElement>(ITEM) ?? [])]
              .find((item) => !item.matches(":disabled, [aria-disabled='true']") && item.closest("[popover]") === sub)
              ?.focus();
          event.preventDefault();
        } else if (!inward && this.parentPanel) {
          // Back to the item that opened this submenu.
          event.preventDefault();
          this.panel?.hidePopover();
        }
        return;
      }
      default:
        if (event.key.length === 1 && !event.ctrlKey && !event.metaKey && !event.altKey && event.key !== " ") {
          next = this.typeAhead(items, index, event.key);
        }
    }

    if (next) {
      event.preventDefault();
      next.focus();
    }
  }

  /** Submenus open while the pointer rests on their item, and close soon after it leaves. */
  private setupHover(panel: HTMLElement, signal: AbortSignal): void {
    const trigger = this.trigger;
    if (!trigger) return;
    const open = () => {
      clearTimeout(this.#hoverTimer);
      if (trigger.matches(":disabled") || panel.matches(":popover-open")) return;
      this.#hoverTimer = window.setTimeout(() => {
        this.#hoverOpen = true;
        try {
          panel.showPopover();
        } catch {
          this.#hoverOpen = false;
        }
      }, 120);
    };
    const close = () => {
      clearTimeout(this.#hoverTimer);
      this.#hoverTimer = window.setTimeout(() => {
        // Keep it open while focus is inside, such as after an arrow key.
        if (panel.matches(":popover-open") && !panel.contains(document.activeElement)) panel.hidePopover();
      }, 300);
    };
    const mouse = (handler: () => void) => (event: PointerEvent) => {
      if (event.pointerType === "mouse") handler();
    };
    trigger.addEventListener("pointerenter", mouse(open), { signal });
    trigger.addEventListener("pointerleave", mouse(close), { signal });
    panel.addEventListener("pointerenter", mouse(() => clearTimeout(this.#hoverTimer)), { signal });
    panel.addEventListener("pointerleave", mouse(close), { signal });
    signal.addEventListener("abort", () => clearTimeout(this.#hoverTimer));
  }

  /** Moves to the next item whose text starts with what was typed in the last half second. */
  private typeAhead(items: HTMLElement[], index: number, key: string): HTMLElement | undefined {
    const now = Date.now();
    this.#typed = now - this.#typedAt > 500 ? key.toLowerCase() : this.#typed + key.toLowerCase();
    this.#typedAt = now;
    const ordered = [...items.slice(index + 1), ...items.slice(0, index + 1)];
    return ordered.find((item) => (item.textContent ?? "").trim().toLowerCase().startsWith(this.#typed));
  }
}

defineElement("ib-popover", PopoverRoot);
