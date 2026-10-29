import { IzzyElement, defineElement } from "../izzy-element";
import { trackDrag } from "../sortable";

const HANDLE = ".ib-sortable-handle";
const ITEM = ".ib-sortable-item";

type Move = "up" | "down" | "top" | "bottom";

/**
 * <ib-sortable> moves a SortableList's items. Every way of moving goes through the handle's menu
 * items (data-ib-move), so each announces and focuses the same way:
 * - A menu item: on a static page it moves the item here instead of posting (the hidden key
 *   inputs move with it, so the form posts the new order); data-ib-submit posts after each move.
 *   On an interactive page C# moves it, and focus goes back to the handle after the render.
 * - Alt+arrows and Alt+Home/End on a focused handle click the matching menu item.
 * - Dragging a handle (static pages; interactive pages come with the ibmove handshake).
 * With JavaScript off, the menu items post the form and the server moves the item.
 */
class Sortable extends IzzyElement {
  private get list(): HTMLElement | null {
    return this.querySelector(":scope > .ib-sortable-list");
  }

  private get live(): boolean {
    return this.hasAttribute("data-ib-interactive");
  }

  protected setup(signal: AbortSignal): void {
    this.addEventListener("click", (event) => this.onClick(event), { signal });
    this.addEventListener("keydown", (event) => this.onKey(event), { signal });
    this.addEventListener("pointerdown", (event) => this.onPointerDown(event), { signal });
  }

  private itemOf(el: Element | null): HTMLElement | null {
    const item = el?.closest<HTMLElement>(ITEM);
    return item && item.parentElement === this.list ? item : null;
  }

  private onClick(event: MouseEvent): void {
    const button = (event.target as Element).closest<HTMLButtonElement>("button[data-ib-move]");
    const item = this.itemOf(button);
    if (!button || !item || button.disabled) return;
    if (this.live) {
      // C# reorders the items; the handle's node moves, which drops focus.
      this.refocusAfterRender(item.dataset.ibKey);
      return;
    }

    event.preventDefault();
    const items = this.items();
    const from = items.indexOf(item);
    const last = items.length - 1;
    const to = ({ up: from - 1, down: from + 1, top: 0, bottom: last } as Record<Move, number>)[button.dataset.ibMove as Move];
    this.move(item, to, true);
  }

  private onKey(event: KeyboardEvent): void {
    if (!event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) return;
    const handle = (event.target as Element).closest(HANDLE);
    const item = this.itemOf(handle);
    if (!handle || !item || event.target !== handle) return;
    const across = this.classList.contains("ib-sortable-horizontal");
    const rtl = getComputedStyle(this).direction === "rtl";
    const back = rtl ? "ArrowRight" : "ArrowLeft";
    const kind: Move | null =
      event.key === "Home" ? "top"
      : event.key === "End" ? "bottom"
      : event.key === (across ? back : "ArrowUp") ? "up"
      : across ? (event.key === (rtl ? "ArrowLeft" : "ArrowRight") ? "down" : null)
      : event.key === "ArrowDown" ? "down"
      : null;
    if (!kind) return;
    // Alt+Left is the browser's Back: always ours on a handle, even at the end of the list.
    event.preventDefault();
    item.querySelector<HTMLButtonElement>(`button[data-ib-move="${kind}"]`)?.click();
  }

  private onPointerDown(event: PointerEvent): void {
    if (event.button !== 0 || this.live) return;
    const handle = (event.target as Element).closest(HANDLE);
    const item = this.itemOf(handle);
    const list = this.list;
    if (!handle || !item || !list) return;
    trackDrag({
      list,
      item,
      event,
      wrap: this.classList.contains("ib-sortable-horizontal"),
      onDrop: (_, to) => this.move(item, to, false),
    });
  }

  private items(): HTMLElement[] {
    return [...(this.list?.children ?? [])] as HTMLElement[];
  }

  /** Moves an item's node to a place in the list (static pages only), then announces it. */
  private move(item: HTMLElement, to: number, focus: boolean): void {
    const list = this.list;
    const items = this.items();
    const from = items.indexOf(item);
    if (!list || to < 0 || to >= items.length || to === from) return;
    const rest = items.filter((el) => el !== item);
    const handle = item.querySelector<HTMLElement>(HANDLE);
    // Moving a node drops focus from inside it.
    const hadFocus = item.contains(document.activeElement);
    list.insertBefore(item, rest[to] ?? null);
    if (focus || hadFocus) handle?.focus();
    this.refreshEnds();
    this.announce(item, to, items.length);
    if (this.hasAttribute("data-ib-submit")) list.closest("form")?.requestSubmit();
  }

  /** Each item's menu: no moving up from the top or down from the bottom. */
  private refreshEnds(): void {
    const items = this.items();
    items.forEach((el, i) => {
      for (const button of el.querySelectorAll<HTMLButtonElement>("button[data-ib-move]")) {
        const kind = button.dataset.ibMove;
        button.disabled = kind === "up" || kind === "top" ? i === 0 : i === items.length - 1;
      }
    });
  }

  private announce(item: HTMLElement, index: number, count: number): void {
    const region = this.querySelector(":scope > [role=status]");
    const text = (this.dataset.ibMoved ?? "")
      .replace("{0}", item.dataset.ibLabel ?? "")
      .replace("{1}", String(index + 1))
      .replace("{2}", String(count));
    if (!region) return;
    // Cleared first, so moving the same item twice in a row is announced again.
    region.textContent = "";
    requestAnimationFrame(() => (region.textContent = text));
  }

  /** On an interactive page, focuses the moved item's handle once Blazor has moved its node. */
  private refocusAfterRender(key: string | undefined): void {
    const list = this.list;
    if (!list || key === undefined) return;
    const indexOf = () => [...list.children].findIndex((el) => (el as HTMLElement).dataset.ibKey === key);
    const before = indexOf();
    const observer = new MutationObserver(() => {
      observer.disconnect();
      const at = indexOf();
      if (at >= 0 && at !== before) list.children[at].querySelector<HTMLElement>(HANDLE)?.focus();
    });
    observer.observe(list, { childList: true });
    setTimeout(() => observer.disconnect(), 2000);
  }
}

defineElement("ib-sortable", Sortable);
