import { IzzyElement, defineElement } from "../izzy-element";
import { track } from "../position";

/**
 * <ib-popover> wraps a Menu or Popover: the trigger button and its [popover] panel.
 * Opening, Escape and light dismiss are native; this element adds:
 *   - placement in browsers without CSS anchor positioning,
 *   - data-open: the server or C# (@bind-Open) can open and close the panel,
 *   - for menus: focus on open, arrow keys, Home and End, type-ahead, and closing after a choice.
 */
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
  #userAt = 0;

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

  private items(): HTMLElement[] {
    const panel = this.panel;
    if (!panel) return [];
    return [...panel.querySelectorAll<HTMLElement>("[role='menuitem']")].filter(
      (item) => !item.matches(":disabled, [aria-disabled='true']") && item.closest("[popover]") === panel
    );
  }

  protected setup(signal: AbortSignal): void {
    const panel = this.panel;
    // Without the Popover API the panel is ordinary content; there's nothing to enhance.
    if (!panel || typeof panel.showPopover !== "function") return;

    panel.addEventListener("toggle", (event) => this.onToggle((event as Event & { newState?: string }).newState === "open"), { signal });

    if (this.isMenu) {
      panel.addEventListener("keydown", (event) => this.onMenuKey(event), { signal });
      this.trigger?.addEventListener("keydown", (event) => this.onTriggerKey(event), { signal });
      // Links and submit buttons don't close the menu by themselves.
      panel.addEventListener(
        "click",
        (event) => {
          const item = (event.target as Element).closest<HTMLElement>("[role='menuitem']");
          if (item && this.items().includes(item) && panel.matches(":popover-open")) {
            panel.hidePopover();
          }
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
    const open = panel.matches(":popover-open");
    // Until the server has caught up with the user's last open or close, a render that left
    // before it mustn't undo it. Once data-open matches what the user did, C# is in charge again.
    if (this.#userState !== null) {
      if (want === this.#userState || Date.now() - this.#userAt >= 2000) this.#userState = null;
      else return;
    }
    this.#syncTarget = want;
    if (want && !open) {
      try {
        (panel.showPopover as (options?: { source?: HTMLElement | null }) => void)({ source: this.trigger });
      } catch {
        // Not connected yet, or another modal is open; the next change will retry.
      }
    } else if (!want && open) {
      panel.hidePopover();
    }
  }

  private onToggle(open: boolean): void {
    const panel = this.panel;
    if (!panel) return;
    if (this.#syncTarget === open) {
      this.#syncTarget = null;
    } else {
      this.#userState = open;
      this.#userAt = Date.now();
    }
    if (open) {
      this.#stopTracking?.();
      this.#stopTracking = track(panel, this, this.dataset.placement ?? "bottom-start");
      if (this.isMenu) {
        const items = this.items();
        (this.#focusLast ? items[items.length - 1] : items[0])?.focus();
      }
      this.#focusLast = false;
    } else {
      this.#stopTracking?.();
      this.#stopTracking = null;
      if (this.isMenu && (panel.contains(document.activeElement) || document.activeElement === document.body)) {
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
        this.panel?.hidePopover();
        return;
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
