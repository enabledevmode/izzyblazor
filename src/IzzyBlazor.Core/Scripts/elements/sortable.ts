import { IzzyElement, defineElement } from "../izzy-element";
import { trackDrag, type DropTarget } from "../sortable";

const HANDLE = ".ib-sortable-handle";
// The button whose menu holds the moves: the handle, or the item's ⋯ button when it has no handle.
const TRIGGER = ".ib-sortable-trigger";
const ITEM = ".ib-sortable-item";
// With data-ib-whole, a press on these stays theirs; the item's stretched link or button drags.
// Overlays an item opens (its menus, dialogs, a combobox's list) sit in the item too. Not
// [tabindex]: an item that Load more focuses has tabindex=-1 itself.
const CONTROL = "a[href]:not(.ib-listview-action), button:not(.ib-listview-action), input, select, textarea, label, summary, video, audio, [contenteditable]:not([contenteditable=false]), [popover], dialog, [role=dialog], [role=menu], [data-ib-role=menu], [role=listbox]";
// A touch on the item (not its handle) is a long press this long, so the page still scrolls.
const LONG_PRESS = 250;

type Move = "up" | "down" | "top" | "bottom";
// The moves within the list; "to" (a zone's entry in the menu) moves to another list.
const IN_LIST = new Set(["up", "down", "top", "bottom"]);

/**
 * <ib-sortable> moves a ListView's items. Every way of moving goes through the items of the menu
 * of moves (data-ib-move; the handle's, or the ⋯ menu's), so each announces and focuses the same way:
 * - A menu item: on a static page it moves the item here instead of posting (the hidden key
 *   inputs move with it, so the form posts the new order); data-ib-submit posts after each move.
 *   On an interactive page C# moves it, and focus goes back to the menu's button after the render.
 * - Alt+arrows and Alt+Home/End on a focused handle (or ⋯ button holding the moves) click the
 *   matching menu item.
 * - Dragging a handle. On a static page the drop moves the item here. On an interactive page it
 *   raises `ibmove` (key, index, seq) and C# moves it; the dragged item stays where it was dropped
 *   until the render that has handled that drop (data-ib-seq) arrives, so nothing jumps back first.
 * - With data-ib-whole, dragging the item itself, from anywhere but the controls in it: a mouse
 *   drags after a few pixels, a touch or pen after a long press.
 * In a DropZoneGroup each zone is its own <ib-sortable>, sharing data-ib-group: an item drags to
 * another zone whose data-ib-zone is in its data-ib-to and that has room (data-ib-max), and the
 * menu's "Move to" items (data-ib-move="to", data-ib-zone) move it to the end of one. On a static
 * page the node moves there and its hidden Zones input changes with it.
 * With JavaScript off, the menu items post the form and the server moves the item.
 */
class Sortable extends IzzyElement {
  // The last drop reported to C#, and whether one is waiting for its render.
  private seq = 0;
  private settling = false;
  // A drag just ended: the touchend that follows mustn't become a tap on the item.
  private dropped = false;

  get list(): HTMLElement | null {
    return this.querySelector(":scope > .ib-sortable-list");
  }

  private get live(): boolean {
    return this.hasAttribute("data-ib-interactive");
  }

  private get whole(): boolean {
    return this.hasAttribute("data-ib-whole");
  }

  private get sorting(): boolean {
    return this.list?.classList.contains("ib-sortable-sorting") ?? false;
  }

  protected setup(signal: AbortSignal): void {
    this.addEventListener("click", (event) => this.onClick(event), { signal });
    this.addEventListener("keydown", (event) => this.onKey(event), { signal });
    this.addEventListener("pointerdown", (event) => this.onPointerDown(event), { signal });
    // A drag from the item: a link or image would start the browser's own drag, a held touch
    // would open its context menu, and a moving one would scroll the page.
    this.addEventListener("dragstart", (event) => this.fromItem(event.target as Element) && event.preventDefault(), { signal });
    this.addEventListener("contextmenu", (event) => this.sorting && event.preventDefault(), { signal });
    // Only lists that drag from the item: a listener that can cancel touchmove makes every scroll
    // starting over the list wait for the script (handles don't need it: touch-action: none).
    // ListView re-creates the element when DragWholeItem changes, so this runs again.
    if (this.whole) {
      this.addEventListener("touchmove", (event) => this.sorting && event.preventDefault(), { signal, passive: false });
    }
    // A touch lifted and let go in place is still a tap to the browser, whose click comes later
    // than the drag's own swallowing: cancelling the touchend stops it.
    this.addEventListener("touchend", (event) => {
      if (!this.dropped) return;
      this.dropped = false;
      if (event.cancelable) event.preventDefault();
    }, { signal, passive: false });
  }

  /** With data-ib-whole, the movable item a press on this element drags, if not a control's. */
  private fromItem(target: Element): HTMLElement | null {
    const item = this.whole ? this.itemOf(target) : null;
    const control = target.closest(CONTROL);
    return item?.hasAttribute("data-ib-movable") && !(control && item.contains(control)) ? item : null;
  }

  /** The item's own move buttons (a nested list's items have theirs). */
  movesOf(item: Element): HTMLButtonElement[] {
    return [...item.querySelectorAll<HTMLButtonElement>("button[data-ib-move]")].filter((el) => this.itemOf(el) === item);
  }

  /** The item's own handle or ⋯ button with the moves (not a nested list's). */
  triggerOf(item: Element | undefined): HTMLElement | undefined {
    return item ? [...item.querySelectorAll<HTMLElement>(TRIGGER)].find((el) => this.itemOf(el) === item) : undefined;
  }

  private itemOf(el: Element | null): HTMLElement | null {
    const item = el?.closest<HTMLElement>(ITEM);
    return item && item.parentElement === this.list ? item : null;
  }

  /** Every zone of this one's DropZoneGroup, this one included; just this one outside a group. */
  private group(): Sortable[] {
    const id = this.dataset.ibGroup;
    if (!id) return [this];
    return [...document.querySelectorAll<Sortable>("ib-sortable")].filter((el) => el.dataset.ibGroup === id);
  }

  /** The group's zone named `zone`. */
  private zone(zone: string | undefined): Sortable | undefined {
    return zone === undefined ? undefined : this.group().find((el) => el.dataset.ibZone === zone);
  }

  /** Whether a zone has room for one more. */
  hasRoom(): boolean {
    const max = this.dataset.ibMax;
    return max === undefined || this.items().length < Number(max);
  }

  /** The other zones an item may be dragged to: they accept it and have room. */
  private targetsFor(item: HTMLElement): DropTarget[] {
    const accepts = (item.dataset.ibTo ?? "").split(" ");
    return this.group()
      .filter((el) => el !== this && accepts.includes(el.dataset.ibZone ?? "") && el.hasRoom() && el.list)
      .map((el) => ({ area: el.dropArea(), list: el.list! }));
  }

  /**
   * Where a drop lands in this zone: the largest box around it that holds no other zone of the
   * group, such as its Card with the title, so a drop on a column's heading counts.
   */
  dropArea(): HTMLElement {
    const zones = this.group();
    const root = this.closest(".ib-dropzone-group");
    let area: HTMLElement = this;
    for (let node = this.parentElement; node && node !== root; node = node.parentElement) {
      if (zones.some((zone) => zone !== this && node.contains(zone))) break;
      area = node;
    }
    return area;
  }

  private onClick(event: MouseEvent): void {
    const button = (event.target as Element).closest<HTMLButtonElement>("button[data-ib-move]");
    const item = this.itemOf(button);
    if (!button || !item || button.disabled) return;
    if (this.live) {
      // C# reorders the items; the menu button's node moves, which drops focus.
      this.refocusAfterRender(item.dataset.ibKey);
      return;
    }

    event.preventDefault();
    if (button.dataset.ibMove === "to") {
      const zone = this.zone(button.dataset.ibZone);
      if (zone && zone !== this) this.moveToZone(item, zone, zone.items().length, true);
      return;
    }
    const items = this.items();
    const from = items.indexOf(item);
    const last = items.length - 1;
    const to = ({ up: from - 1, down: from + 1, top: 0, bottom: last } as Record<Move, number>)[button.dataset.ibMove as Move];
    this.move(item, to, true);
  }

  private onKey(event: KeyboardEvent): void {
    if (!event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) return;
    // On the handle (or ⋯ button holding the moves), or on the item's own link or button, the first
    // Tab stop of a card that drags whole.
    const target = event.target as Element;
    const item = this.itemOf(target);
    const handle = item && (target.matches(TRIGGER) || target.matches(".ib-listview-action")) ? this.triggerOf(item) : undefined;
    if (!handle || !item) return;
    // Both axes in every layout: rows that wrap become one column on a phone, and the other
    // arrows would scroll the page.
    const rtl = getComputedStyle(this).direction === "rtl";
    const back = rtl ? "ArrowRight" : "ArrowLeft";
    const forward = rtl ? "ArrowLeft" : "ArrowRight";
    const kind: Move | null =
      event.key === "Home" ? "top"
      : event.key === "End" ? "bottom"
      : event.key === "ArrowUp" || event.key === back ? "up"
      : event.key === "ArrowDown" || event.key === forward ? "down"
      : null;
    if (!kind) return;
    // Alt+Left is the browser's Back: always ours on a menu button, even at the end of the list.
    event.preventDefault();
    this.movesOf(item).find((button) => button.dataset.ibMove === kind)?.click();
  }

  private onPointerDown(event: PointerEvent): void {
    // While prerendered for an interactive page, nothing would answer a drop yet: C# renders
    // data-ib-seq once it's running.
    // One drag at a time: a second finger doesn't start another.
    if (event.button !== 0 || !event.isPrimary || this.sorting || (this.live && !this.hasAttribute("data-ib-seq"))) return;
    this.dropped = false;
    const target = event.target as Element;
    const handle = target.closest(HANDLE);
    // In a group, one drop at a time across its zones: they share C#'s count of drops.
    if (this.group().some((zone) => zone.settling)) {
      // The last drop is still on its way: no new drag yet, and the press doesn't open the menu.
      if (handle) {
        handle.addEventListener("click", swallow, { capture: true, once: true });
        // The click, if any, comes right after the pointer is up; then the next one is the user's.
        const release = () => setTimeout(() => handle.removeEventListener("click", swallow, { capture: true }), 0);
        handle.addEventListener("pointerup", release, { once: true });
        handle.addEventListener("pointercancel", release, { once: true });
      }
      return;
    }
    // From the handle, or from the item itself: not a control in it (or one that took the press),
    // and only a movable item (data-ib-movable) moves.
    const item = handle ? this.itemOf(handle) : event.defaultPrevented ? null : this.fromItem(target);
    const fromItem = !handle;
    const list = this.list;
    if (!item || !list) return;
    trackDrag({
      list,
      item,
      event,
      delay: fromItem && event.pointerType !== "mouse" ? LONG_PRESS : 0,
      wrap: this.classList.contains("ib-sortable-horizontal"),
      targets: this.dataset.ibGroup ? () => this.targetsFor(item) : undefined,
      refusing: this.dataset.ibGroup ? () => this.refusingFor(item) : undefined,
      home: this.dataset.ibGroup ? this.dropArea() : undefined,
      onEnd: () => (this.dropped = event.pointerType !== "mouse"),
      onDrop: (_, to, settle, target) => {
        const zone = target?.list.parentElement as Sortable | undefined;
        // Dragged by its body, the press left focus on the list: the item's menu button takes it,
        // so the shortcuts work next (a field in the item keeps it).
        if (fromItem && !item.contains(document.activeElement)) this.triggerOf(item)?.focus({ preventScroll: true });
        if (this.live) {
          this.report(item, to, settle, zone?.dataset.ibZone);
          return;
        }

        settle();
        if (zone) this.moveToZone(item, zone, to, false);
        else this.move(item, to, false);
      },
    });
  }

  /** The other zones that won't take an item: they refuse it, or are full. */
  private refusingFor(item: HTMLElement): HTMLElement[] {
    const targets = new Set(this.targetsFor(item).map((t) => t.area));
    return this.group().filter((el) => el !== this).map((el) => el.dropArea()).filter((area) => !targets.has(area));
  }

  /** An interactive page: asks C# to move the item, and settles once its render has. */
  private report(item: HTMLElement, to: number, settle: () => void, zone?: string): void {
    const key = item.dataset.ibKey;
    if (key === undefined) {
      settle();
      return;
    }

    // Counted on from what C# rendered too, in case this element is new, and from every zone of a
    // group, which shares one count.
    const seq = (this.seq = Math.max(...this.group().map((zone) => Math.max(zone.seq, Number(zone.dataset.ibSeq ?? 0)))) + 1);
    const focused = document.activeElement;
    const hadFocus = item.contains(focused);
    this.settling = true;
    let timer = 0;
    const done = (answered: boolean) => {
      observer.disconnect();
      clearTimeout(timer);
      this.settling = false;
      settle();
      if (!answered) this.say((this.dataset.ibCancelled ?? "").replace("{0}", item.dataset.ibLabel ?? ""));
      // Blazor may have moved the item's node (or drawn it in another zone), which drops focus
      // from inside it, or the focus rescue (focus.ts) has passed it to a neighbor as the node
      // went; if the user has gone elsewhere meanwhile, focus stays there.
      const now = this.findItem(key);
      const lost = !document.activeElement || document.activeElement === document.body || !focused?.isConnected;
      if (hadFocus && lost && now) now.closest<Sortable>("ib-sortable")?.triggerOf(now)?.focus();
    };
    // The render applies its moves and data-ib-seq together, before this runs.
    const observer = new MutationObserver(() => {
      if (Number(this.dataset.ibSeq ?? 0) >= seq) done(true);
    });
    observer.observe(this, { attributes: true, attributeFilter: ["data-ib-seq"] });
    // No answer (the circuit is down): put the items back.
    timer = window.setTimeout(() => done(false), 3000);
    this.dispatchEvent(new CustomEvent("ibmove", { bubbles: true, detail: { key, to, zone, seq } }));
  }

  items(): HTMLElement[] {
    return [...(this.list?.children ?? [])] as HTMLElement[];
  }

  /** An item by key, in this list or another zone of its group. */
  private findItem(key: string): HTMLElement | undefined {
    for (const zone of this.group()) {
      const found = zone.items().find((el) => el.dataset.ibKey === key);
      if (found) return found;
    }
    return undefined;
  }

  /**
   * Moves an item's node to a place in another zone (static pages only): its Zones input and its
   * menu's "Move to" items change with it, then the zone announces it.
   */
  private moveToZone(item: HTMLElement, zone: Sortable, to: number, focus: boolean): void {
    const list = zone.list;
    const name = zone.dataset.ibZone;
    if (!list || name === undefined) return;
    const hadFocus = item.contains(document.activeElement);
    const handle = this.triggerOf(item);
    const rest = zone.items();
    list.insertBefore(item, rest[to] ?? null);
    for (const input of item.querySelectorAll<HTMLInputElement>(":scope > input[type=hidden]")) {
      if (input.name.endsWith(".Zones")) input.value = name;
    }
    for (const button of zone.movesOf(item)) {
      if (button.dataset.ibMove === "to") button.hidden = button.dataset.ibZone === name;
    }
    if (focus || hadFocus) handle?.focus();
    this.refreshEnds();
    zone.refreshEnds();
    this.refreshRoom();
    const index = zone.items().indexOf(item);
    zone.say((zone.dataset.ibMovedZone ?? "")
      .replace("{0}", item.dataset.ibLabel ?? "")
      .replace("{1}", zone.dataset.ibZoneLabel ?? name)
      .replace("{2}", String(index + 1))
      .replace("{3}", String(zone.items().length)));
    if (this.hasAttribute("data-ib-submit")) list.closest("form")?.requestSubmit();
  }

  /** Every item's "Move to" entries in the group: none to a zone that's full. */
  private refreshRoom(): void {
    const zones = this.group();
    for (const zone of zones) {
      for (const item of zone.items()) {
        for (const button of zone.movesOf(item)) {
          if (button.dataset.ibMove !== "to" || button.hidden) continue;
          button.disabled = !(zones.find((z) => z.dataset.ibZone === button.dataset.ibZone)?.hasRoom() ?? false);
        }
      }
    }
  }

  /** Moves an item's node to a place in the list (static pages only), then announces it. */
  private move(item: HTMLElement, to: number, focus: boolean): void {
    const list = this.list;
    const items = this.items();
    const from = items.indexOf(item);
    if (!list || to < 0 || to >= items.length || to === from) return;
    const rest = items.filter((el) => el !== item);
    const handle = this.triggerOf(item);
    // Moving a node drops focus from inside it.
    const hadFocus = item.contains(document.activeElement);
    list.insertBefore(item, rest[to] ?? null);
    if (focus || hadFocus) handle?.focus();
    this.refreshEnds();
    this.announce(item, to, items.length);
    if (this.hasAttribute("data-ib-submit")) list.closest("form")?.requestSubmit();
  }

  /** Each item's menu: no moving up from the top or down from the bottom. */
  refreshEnds(): void {
    const items = this.items();
    items.forEach((el, i) => {
      for (const button of this.movesOf(el)) {
        const kind = button.dataset.ibMove ?? "";
        if (!IN_LIST.has(kind)) continue;
        button.disabled = kind === "up" || kind === "top" ? i === 0 : i === items.length - 1;
      }
    });
  }

  private announce(item: HTMLElement, index: number, count: number): void {
    this.say((this.dataset.ibMoved ?? "")
      .replace("{0}", item.dataset.ibLabel ?? "")
      .replace("{1}", String(index + 1))
      .replace("{2}", String(count)));
  }

  say(text: string): void {
    // On an interactive page C# owns the first region; the script has its own.
    const region = this.querySelector(this.live ? ":scope > [data-ib-say]" : ":scope > [role=status]");
    if (!region) return;
    // Cleared first, so moving the same item twice in a row is announced again.
    region.textContent = "";
    requestAnimationFrame(() => (region.textContent = text));
  }

  /** On an interactive page, focuses the moved item's menu button once Blazor has moved its node. */
  private refocusAfterRender(key: string | undefined): void {
    const list = this.list;
    if (!list || key === undefined) return;
    const place = (item: HTMLElement | undefined) => (item ? [item.parentElement, [...(item.parentElement?.children ?? [])].indexOf(item)] as const : null);
    const was = place(this.findItem(key));
    // Every zone of the group: the item may land in another one.
    const observer = new MutationObserver(() => {
      const item = this.findItem(key);
      const now = place(item);
      if (!item || !now || (now[0] === was?.[0] && now[1] === was?.[1])) return;
      observer.disconnect();
      item.closest<Sortable>("ib-sortable")?.triggerOf(item)?.focus();
    });
    for (const zone of this.group()) if (zone.list) observer.observe(zone.list, { childList: true });
    setTimeout(() => observer.disconnect(), 2000);
  }
}

function swallow(event: Event): void {
  event.preventDefault();
  event.stopImmediatePropagation();
}

defineElement("ib-sortable", Sortable);
