import { IzzyElement, defineElement } from "../izzy-element";
import { trackDrag } from "../sortable";

/**
 * <ib-pick-list> turns a PickList's `select multiple` into two lists, Available and Chosen, drawn
 * by the server from the same items. A move works out the chosen values in order, then:
 * - on a static page, moves the items between the lists and puts the select's options in that
 *   order, the chosen ones selected, so the form posts them as they are;
 * - on an interactive page, reports them in the hidden order field (a change event) and leaves
 *   the moving to Blazor's render, so the markup stays Blazor's.
 *
 * Each list is a multi-select listbox with one Tab stop (roving tabindex on its options): arrows,
 * Home and End move, Space selects, Shift extends, Ctrl+A selects all, Enter moves the selection
 * across, Alt+Up and Alt+Down reorder the chosen list. Click selects one, Ctrl or Cmd adds, Shift
 * extends; double-click moves one. Items drag between the lists, and within Chosen when it can be
 * reordered (sortable.ts). Buttons say there's nothing to move with aria-disabled, so focus can
 * stay on them; after a move focus stays, or goes to the other list when its own has emptied.
 * Each move is announced.
 */

const LONG_PRESS = 250;

type Pane = "available" | "chosen";

class PickList extends IzzyElement {
  #anchor: HTMLElement | null = null;

  protected setup(signal: AbortSignal): void {
    const select = this.querySelector<HTMLSelectElement>(":scope > .ib-pick-list-select");
    if (!select || !this.#list("available") || !this.#list("chosen")) return;
    this.dataset.ready = "";
    signal.addEventListener("abort", () => delete this.dataset.ready);
    select.tabIndex = -1;
    const on = <K extends keyof HTMLElementEventMap>(type: K, handler: (event: HTMLElementEventMap[K]) => void) => this.addEventListener(type, handler, { signal });

    on("click", (event) => {
      const target = event.target as Element;
      const button = target.closest<HTMLElement>("[data-ib-move]");
      if (button && button.closest("ib-pick-list") === this) {
        if (button.getAttribute("aria-disabled") !== "true") this.#press(button.dataset.ibMove!, button);
        return;
      }
      const option = this.#optionAt(target);
      if (option) this.#choose(option, event);
    });
    on("dblclick", (event) => {
      const option = this.#optionAt(event.target as Element);
      if (!option || this.#locked(option)) return;
      for (const o of this.#options(this.#paneOf(option))) this.#mark(o, o === option);
      this.#press(this.#paneOf(option) === "available" ? "chosen" : "available");
    });
    on("keydown", (event) => this.#onKey(event));
    on("focusin", (event) => {
      const option = this.#optionAt(event.target as Element);
      if (option) this.#rove(option);
    });
    on("pointerdown", (event) => this.#drag(event));
    on("input", (event) => {
      const box = event.target as HTMLInputElement;
      if (box.matches("[data-ib-pick-search]")) this.#filter(box);
    });

    // Blazor redraws the lists on interactive pages.
    const observer = new MutationObserver(() => {
      this.#prepare();
      const then = this.#pending;
      this.#pending = null;
      then?.();
    });
    for (const pane of ["available", "chosen"] as Pane[]) observer.observe(this.#list(pane)!, { childList: true });
    signal.addEventListener("abort", () => observer.disconnect());
    this.#prepare();
  }

  #list(pane: Pane): HTMLElement | null {
    return this.querySelector<HTMLElement>(`:scope > .ib-pick-list-panes > [data-ib-pane=${pane}] > .ib-pick-list-list`);
  }

  #options(pane: Pane, shownOnly = false): HTMLElement[] {
    const all = [...(this.#list(pane)?.children ?? [])] as HTMLElement[];
    return shownOnly ? all.filter((o) => !o.hidden) : all;
  }

  #optionAt(target: Element): HTMLElement | null {
    const option = target.closest<HTMLElement>("[role=option]");
    return option && option.closest("ib-pick-list") === this ? option : null;
  }

  #paneOf(option: Element): Pane {
    return (option.closest("[data-ib-pane]") as HTMLElement).dataset.ibPane as Pane;
  }

  #locked(option: HTMLElement): boolean {
    return option.getAttribute("aria-disabled") === "true" || this.querySelector<HTMLSelectElement>(":scope > select")!.disabled;
  }

  #selected(pane: Pane): HTMLElement[] {
    return this.#options(pane, true).filter((o) => o.getAttribute("aria-selected") === "true" && !this.#locked(o));
  }

  #mark(option: HTMLElement, on: boolean): void {
    option.setAttribute("aria-selected", String(on));
  }

  /** Each option's state and the one Tab stop per list, after a render or a move. */
  #prepare(): void {
    for (const pane of ["available", "chosen"] as Pane[]) {
      const options = this.#options(pane);
      for (const o of options) {
        if (!o.hasAttribute("aria-selected")) this.#mark(o, false);
        if (!o.hasAttribute("tabindex")) o.tabIndex = -1;
      }
      const shown = options.filter((o) => !o.hidden);
      if (!shown.some((o) => o.tabIndex === 0)) {
        for (const o of options) o.tabIndex = -1;
        if (shown[0]) shown[0].tabIndex = 0;
      }
      // An empty list takes focus itself, so a move that empties it has somewhere to land.
      this.#list(pane)!.tabIndex = shown.length ? -1 : 0;
    }
    this.#buttons();
  }

  #rove(option: HTMLElement): void {
    for (const o of this.#options(this.#paneOf(option))) if (o !== option) o.tabIndex = -1;
    option.tabIndex = 0;
  }

  #focus(option: HTMLElement): void {
    this.#rove(option);
    option.focus();
    option.scrollIntoView({ block: "nearest" });
  }

  /** Each button says whether it has anything to move. */
  #buttons(): void {
    const has = (pane: Pane, selected: boolean) => (selected ? this.#selected(pane) : this.#options(pane, true).filter((o) => !this.#locked(o))).length > 0;
    const chosen = this.#selected("chosen");
    const all = this.#options("chosen", true);
    const can: Record<string, boolean> = {
      chosen: has("available", true),
      "all-chosen": has("available", false),
      available: has("chosen", true),
      "all-available": has("chosen", false),
      up: chosen.length > 0 && all.indexOf(chosen[0]) > 0,
      down: chosen.length > 0 && all.indexOf(chosen[chosen.length - 1]) < all.length - 1,
    };
    for (const button of this.querySelectorAll<HTMLElement>("[data-ib-move]")) {
      if (can[button.dataset.ibMove!]) button.removeAttribute("aria-disabled");
      else button.setAttribute("aria-disabled", "true");
    }
  }

  /** A click: one item, or Ctrl/Cmd to add it, or Shift for the run from the last one clicked. */
  #choose(option: HTMLElement, event: MouseEvent): void {
    const options = this.#options(this.#paneOf(option), true);
    if (event.shiftKey && this.#anchor && options.includes(this.#anchor)) {
      const [a, b] = [options.indexOf(this.#anchor), options.indexOf(option)].sort((x, y) => x - y);
      options.forEach((o, i) => this.#mark(o, i >= a && i <= b));
    } else if (event.ctrlKey || event.metaKey) {
      this.#mark(option, option.getAttribute("aria-selected") !== "true");
      this.#anchor = option;
    } else {
      for (const o of options) this.#mark(o, o === option);
      this.#anchor = option;
    }
    this.#focus(option);
    this.#buttons();
  }

  #onKey(event: KeyboardEvent): void {
    const option = this.#optionAt(event.target as Element);
    if (!option) return;
    const pane = this.#paneOf(option);
    const options = this.#options(pane, true);
    const at = options.indexOf(option);
    let to: HTMLElement | undefined;
    if (event.altKey && (event.key === "ArrowUp" || event.key === "ArrowDown")) {
      if (pane === "chosen" && this.hasAttribute("data-ib-reorder")) {
        if (option.getAttribute("aria-selected") !== "true") this.#mark(option, true);
        this.#press(event.key === "ArrowUp" ? "up" : "down", option);
        event.preventDefault();
      }
      return;
    }
    switch (event.key) {
      case "ArrowDown":
        to = options[at + 1];
        break;
      case "ArrowUp":
        to = options[at - 1];
        break;
      case "Home":
        to = options[0];
        break;
      case "End":
        to = options[options.length - 1];
        break;
      case " ":
        this.#mark(option, option.getAttribute("aria-selected") !== "true");
        this.#anchor = option;
        break;
      case "Enter":
        if (!this.#selected(pane).length && !this.#locked(option)) this.#mark(option, true);
        this.#press(pane === "available" ? "chosen" : "available");
        break;
      case "a":
      case "A":
        if (!(event.ctrlKey || event.metaKey)) return;
        for (const o of options) this.#mark(o, true);
        break;
      default:
        return;
    }
    event.preventDefault();
    if (to) {
      // Shift extends the selection from the last item chosen, as a click does.
      if (event.shiftKey) {
        this.#anchor ??= option;
        const [a, b] = [options.indexOf(this.#anchor), options.indexOf(to)].sort((x, y) => x - y);
        options.forEach((o, i) => this.#mark(o, i >= a && i <= b));
      }
      this.#focus(to);
    }
    this.#buttons();
  }

  /** The chosen values in order, now. */
  #values(): string[] {
    return this.#options("chosen").map((o) => o.dataset.value!);
  }

  #press(move: string, from?: HTMLElement): void {
    const values = this.#values();
    let moved: HTMLElement[] = [];
    let message: string | undefined;
    if (move === "chosen" || move === "all-chosen") {
      moved = move === "chosen" ? this.#selected("available") : this.#options("available", true).filter((o) => !this.#locked(o));
      values.push(...moved.map((o) => o.dataset.value!));
      message = this.dataset.ibToChosen;
    } else if (move === "available" || move === "all-available") {
      moved = move === "available" ? this.#selected("chosen") : this.#options("chosen", true).filter((o) => !this.#locked(o));
      const out = new Set(moved.map((o) => o.dataset.value));
      values.splice(0, values.length, ...values.filter((v) => !out.has(v)));
      message = this.dataset.ibToAvailable;
    } else {
      moved = this.#selected("chosen");
      const up = move === "up";
      const order = up ? moved : [...moved].reverse();
      for (const o of order) {
        const i = values.indexOf(o.dataset.value!);
        const j = up ? i - 1 : i + 1;
        if (j < 0 || j >= values.length || order.some((m) => m.dataset.value === values[j])) continue;
        [values[i], values[j]] = [values[j], values[i]];
      }
      // "Moved Status and Owner to position 2 of 5": where the first of them is now.
      const i = Math.min(...moved.map((o) => values.indexOf(o.dataset.value!)));
      message = (this.dataset.ibMoved ?? "").replace("{1}", String(i + 1)).replace("{2}", String(values.length));
    }
    if (!moved.length) return;
    const keep = move === "up" || move === "down";
    this.#commit(values, keep ? moved.map((o) => o.dataset.value!) : []);
    if (message) this.#say(message.replace("{0}", this.#names(moved)));
    // Focus: where it was, unless that list has emptied. On an interactive page, once Blazor has
    // drawn the move.
    const source: Pane = move.endsWith("available") || keep ? "chosen" : "available";
    const other: Pane = source === "chosen" ? "available" : "chosen";
    this.#after(() => {
      if (keep) {
        const first = this.#options("chosen").find((o) => moved.some((m) => m.dataset.value === o.dataset.value));
        if (first && from?.matches("[role=option]")) this.#focus(first);
        return;
      }
      if (from?.matches("[role=option]") || !this.#options(source, true).length) {
        const next = this.#options(source, true)[0] ?? this.#options(other, true)[0];
        if (next) this.#focus(next);
        else this.#list(other)?.focus();
      }
    });
  }

  #pending: (() => void) | null = null;

  #after(then: () => void): void {
    if (this.hasAttribute("data-ib-interactive")) this.#pending = then;
    else queueMicrotask(then);
  }

  #names(options: HTMLElement[]): string {
    const names = options.map((o) => o.dataset.text ?? "");
    try {
      return new Intl.ListFormat(document.documentElement.lang || undefined, { type: "conjunction" }).format(names);
    } catch {
      return names.join(", ");
    }
  }

  #say(text: string): void {
    const say = this.querySelector<HTMLElement>(":scope > [data-ib-say]");
    if (say) say.textContent = text;
  }

  /** The new chosen order: to Blazor on an interactive page, into the lists and select on a static one. */
  #commit(values: string[], stillSelected: string[]): void {
    if (this.hasAttribute("data-ib-interactive")) {
      const order = this.querySelector<HTMLInputElement>(":scope > .ib-pick-list-order");
      if (!order) return;
      order.value = JSON.stringify(values);
      order.dispatchEvent(new Event("change", { bubbles: true }));
      return;
    }
    const chosen = this.#list("chosen")!;
    const available = this.#list("available")!;
    const all = [...this.#options("available"), ...this.#options("chosen")];
    const byValue = new Map(all.map((o) => [o.dataset.value!, o]));
    const set = new Set(values);
    for (const v of values) chosen.append(byValue.get(v)!);
    const rest = all.filter((o) => !set.has(o.dataset.value!)).sort((a, b) => Number(a.dataset.index) - Number(b.dataset.index));
    for (const o of rest) available.append(o);
    for (const o of all) this.#mark(o, stillSelected.includes(o.dataset.value!));
    // The select: the chosen first, in order, selected; the rest after, in the items' order.
    const select = this.querySelector<HTMLSelectElement>(":scope > select")!;
    const options = new Map([...select.options].map((o) => [o.value, o]));
    for (const v of values) {
      const option = options.get(v);
      if (option) {
        option.selected = true;
        select.append(option);
      }
    }
    for (const o of rest) {
      const option = options.get(o.dataset.value!);
      if (option) {
        option.selected = false;
        select.append(option);
      }
    }
    select.dispatchEvent(new Event("input", { bubbles: true }));
    select.dispatchEvent(new Event("change", { bubbles: true }));
    for (const box of this.querySelectorAll<HTMLInputElement>("[data-ib-pick-search]")) this.#filter(box);
    this.#prepare();
  }

  /** A search box narrows its list to the items whose text holds what's typed. */
  #filter(box: HTMLInputElement): void {
    const pane = (box.closest("[data-ib-pane]") as HTMLElement).dataset.ibPane as Pane;
    const query = box.value.trim().toLowerCase();
    for (const o of this.#options(pane)) {
      o.hidden = !!query && !(o.dataset.text ?? o.textContent ?? "").toLowerCase().includes(query);
      if (o.hidden) this.#mark(o, false);
    }
    this.#prepare();
  }

  /** Dragging an item to the other list, or within Chosen when it can be reordered. */
  #drag(event: PointerEvent): void {
    if (event.button !== 0) return;
    const item = this.#optionAt(event.target as Element);
    if (!item || this.#locked(item)) return;
    const pane = this.#paneOf(item);
    const other: Pane = pane === "available" ? "chosen" : "available";
    const list = this.#list(pane)!;
    const target = { area: this.#list(other)!.parentElement as HTMLElement, list: this.#list(other)! };
    trackDrag({
      list,
      item,
      event,
      delay: event.pointerType === "mouse" ? 0 : LONG_PRESS,
      wrap: false,
      targets: () => [target],
      home: list.parentElement as HTMLElement,
      onDrop: (from, to, settle, into) => {
        settle();
        const value = item.dataset.value!;
        const values = this.#values().filter((v) => v !== value);
        if (into) {
          if (pane === "chosen") {
            this.#commit(values, []);
            this.#say((this.dataset.ibToAvailable ?? "").replace("{0}", item.dataset.text ?? ""));
            return;
          }
          // Where it was dropped among Chosen's items, counted over the ones shown.
          const shown = this.#options("chosen", true);
          const before = shown[to]?.dataset.value;
          values.splice(before ? values.indexOf(before) : values.length, 0, value);
          this.#commit(values, []);
          this.#say((this.dataset.ibToChosen ?? "").replace("{0}", item.dataset.text ?? ""));
          return;
        }
        if (pane !== "chosen" || !this.hasAttribute("data-ib-reorder") || from === to) return;
        const shown = this.#options("chosen", true).filter((o) => o !== item);
        const before = shown[to]?.dataset.value;
        values.splice(before ? values.indexOf(before) : values.length, 0, value);
        this.#commit(values, []);
        this.#say((this.dataset.ibMoved ?? "").replace("{0}", item.dataset.text ?? "").replace("{1}", String(values.indexOf(value) + 1)).replace("{2}", String(values.length)));
      },
    });
  }
}

defineElement("ib-pick-list", PickList);
