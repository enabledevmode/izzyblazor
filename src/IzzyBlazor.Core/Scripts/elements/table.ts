import { IzzyElement, defineElement } from "../izzy-element";
import { TypingGuard, changedFromServer, navigateTo } from "../filtering";
import { setupLoadOnScroll, setupSelectAll, setupSelection } from "../data-view";

/**
 * <ib-table> adds two things to a Table:
 *
 * - The header "select all" checkbox of a selectable table. Rows are plain checkboxes that post
 *   with the form (and report their own changes on interactive pages), so select-all for the page
 *   only checks or clears its rows and fires `change` on each one it flips, then shows the header as
 *   checked, cleared or partly selected (indeterminate). For every match, it sets the selection.
 * - Live filters. Without script the header filters' fields are a GET form you submit with Enter
 *   or Apply. Here the boxes apply after a pause in typing, dates after the same pause once they're
 *   whole (no segment empty, and a year of four digits, so not 0002 on the way to 2026), and Enter at once:
 *   the element puts the values in the URL, drops this table's page number and navigates through
 *   Blazor, which keeps the page in place. An operator's select applies at once if there's a value;
 *   with none, the address stays as it is, and the choice only shapes the panel until the next
 *   navigation. (Filters kept in the component on interactive pages don't carry data-ib-filter, so
 *   this leaves them alone.) A list's ticks apply after the same pause, and only those it needs: all
 *   ticked says nothing, and none ticked says so under the list's own key. A header filter's panel
 *   stays open while you type; Enter applies and closes it, Escape closes it as it is, and either
 *   puts focus back on its button.
 * - Loading on scroll (Paging LoadOnScroll, interactive pages): when the last row comes into view,
 *   it presses the table's hidden "load next" button, as Load more does without moving focus.
 *   The visible button stays for the keyboard.
 * - Shift+click on a DataGrid's sort link adds the column to the sort (its data-ib-sort-add), where
 *   the browser would open a window.
 * - The scroll box's Tab stop: the server renders it focusable so the keyboard can scroll it with
 *   no script; here it's a Tab stop only while there's something to scroll. Focused, it stays.
 *   It also gives the box its visible width (--ib-table-view), which keeps the "no items" message
 *   centered in view when the columns are wider than the box.
 * - A filter row's operator shows at once on a static page, on its button and in its field's
 *   placeholder, ahead of the response to its link.
 * - A page of rows holds its height (data-ib-hold, the page size): the tallest it's been, so filters
 *   that leave fewer rows don't pull up what's below. Changing the page size or the page starts again.
 */
const NO_VALUE = ["isempty", "isnotempty"];
const PAUSE = 350;

// A date input someone is part way through: a segment is empty, or the year has yet to reach four digits.
function unfinished(control: Element): boolean {
  return control instanceof HTMLInputElement && control.type === "date" && (control.validity.badInput || control.value.startsWith("0"));
}

function isTick(control: Element): control is HTMLInputElement {
  return control instanceof HTMLInputElement && control.type === "checkbox";
}

/**
 * What a filter's control puts in the address, given the operator chosen in its panel: nothing for a
 * value the operator doesn't take, and the operator only with something for it to apply to, as an
 * operator alone is no filter, and not when it's the column's default.
 */
function written(control: HTMLInputElement | HTMLSelectElement): string {
  if (isTick(control)) return control.checked && control.closest("ib-filter-list")?.querySelector("input[data-ib-filter]:not(:checked)") ? control.value : "";
  const value = control.value.trim();
  const panel = control.closest(".ib-table-filter-panel");
  const op = panel?.querySelector<HTMLSelectElement>("select[data-ib-filter]");
  if (!panel || !op) return value;
  const noValue = NO_VALUE.includes(op.value);
  const used = (c: Element) => !noValue && (op.value === "between" || !c.hasAttribute("data-ib-to"));
  if (control !== op) return used(control) ? value : "";
  const hasValue = [...panel.querySelectorAll<HTMLInputElement>("input[data-ib-filter]")].some((c) => (c.value.trim() || unfinished(c)) && used(c));
  return (noValue || hasValue) && value !== op.dataset.ibDefault ? value : "";
}

class Table extends IzzyElement {
  // Kept when setup runs again as a response arrives: the key pressed after that request still counts.
  #timer = 0;
  #boundDate = 0;
  #releasing = false;
  #popped = false;
  // The names in panels changed since the last apply. Only these go in the address: the rest may not yet
  // show what a link has just changed, and the address is what's right about them.
  #changed = new Set<string>();
  #typing = new TypingGuard(this);
  // A filter row's fields edited since the last apply: each hands its value to its panel's field, which
  // is what's read, so one filter has one place that says it.
  #rowEdits = new Set<HTMLInputElement | HTMLSelectElement>();
  // A panel the row opened, whose button in the row gets focus back from it. An id: choosing Between
  // draws that button only as the panel opens.
  #rowPanel: string | null = null;
  // Kept for the same reason: setup runs again after the response that made the table shorter.
  #tallest = 0;
  #heldFor = "";

  protected setup(signal: AbortSignal): void {
    this.#setupHold(signal);
    this.#setupFilters(signal);
    setupSelectAll(this, ".ib-table-select-all", "tbody .ib-table-select", signal);
    setupSelection(this, "tbody .ib-table-select", signal);
    setupLoadOnScroll(this, ":scope > .ib-table-scroll > table > tbody > tr:last-child", ":scope > .ib-table-scroll > .ib-data-more [data-ib-load-next]", signal);
    this.#setupScrollStop(signal);
    this.#setupBoundPanels(signal);
    // A FilterBuilder in a panel is done with the panel.
    this.addEventListener("ibfilterapplied", (event) => (event.target as Element).closest<HTMLElement>("[popover]:popover-open")?.hidePopover(), { signal });
    this.addEventListener(
      "toggle",
      (event) => {
        const panel = event.target as HTMLElement;
        if ((event as ToggleEvent).newState !== "closed" || panel.id !== this.#rowPanel) return;
        this.#rowPanel = null;
        // Past the panel's own trigger, which it hands focus to first. Not if it's gone somewhere of its own.
        const active = document.activeElement;
        if (!active || active === document.body || panel.contains(active) || active.closest("ib-popover")?.contains(panel)) {
          this.querySelector<HTMLElement>(`.ib-filter-cell-summary[popovertarget="${CSS.escape(panel.id)}"]`)?.focus();
        }
      },
      { capture: true, signal }
    );
    this.addEventListener(
      "click",
      (event) => {
        // A panel's Clear is done with the panel.
        const opens = (event.target as Element).closest<HTMLElement>("[data-ib-opens-panel]")?.dataset.ibOpensPanel;
        if (opens) document.getElementById(opens)?.showPopover();
        this.#rowPanel = opens ?? (event.target as Element).closest<HTMLElement>(".ib-filter-cell-summary")?.getAttribute("popovertarget") ?? this.#rowPanel;
        const clear = (event.target as Element).closest("[data-ib-filter-clear]");
        if (clear && !(event as MouseEvent).ctrlKey && !(event as MouseEvent).metaKey && !(event as MouseEvent).shiftKey) this.#closePanel(clear);
        // An operator is a link on a static page, so what it changes would wait for the response.
        // Reset would show the column's default, which only the server knows.
        const operator = (event.target as Element).closest("a.ib-menu-item:not([data-ib-reset])");
        const cell = operator?.closest(".ib-filter-cell");
        const icon = operator?.querySelector(":scope > .ib-icon");
        if (cell && icon && !event.ctrlKey && !event.metaKey && !event.shiftKey) {
          cell.querySelector(".ib-filter-cell-op")?.replaceChildren(icon.cloneNode(true));
          const field = cell.querySelector<HTMLInputElement>("input[placeholder]");
          if (field) field.placeholder = operator!.querySelector(".ib-menu-label")?.textContent ?? "";
        }
        const add = (event.target as Element).closest<HTMLElement>("[data-ib-sort-add]")?.dataset.ibSortAdd;
        if (!add || !event.shiftKey || event.ctrlKey || event.metaKey) return;
        event.preventDefault();
        navigateTo(new URL(add, location.href), false);
      },
      { signal }
    );
  }

  /**
   * Filters kept in the component (bound, interactive pages) handle their own typing; Enter or Escape in one of their
   * panels closes it and puts focus back on its button, as for the URL filters below (Escape before a search box
   * clears itself, which would clear the filter).
   */
  #setupBoundPanels(signal: AbortSignal): void {
    // A bound search box: C# hears nothing until a composition is committed, and Enter isn't the
    // surrounding form's.
    const isBoundSearch = (target: EventTarget | null): target is HTMLInputElement => target instanceof HTMLInputElement && target.hasAttribute("data-ib-bound-search");
    this.addEventListener("input", (event) => {
      if (isBoundSearch(event.target) && (event as InputEvent).isComposing) event.stopPropagation();
    }, { signal });
    this.addEventListener("compositionend", (event) => {
      if (isBoundSearch(event.target)) event.target.dispatchEvent(new Event("input", { bubbles: true }));
    }, { signal });
    // A bound date: C# hears it once it's whole and the keys have paused. Heard on each change, its
    // answer writes back an older value over the keys pressed since. On the window, in the capture
    // phase, as Blazor's own listener is on the document in that phase.
    window.addEventListener(
      "change",
      (event) => {
        const target = event.target;
        if (this.#releasing || !(target instanceof HTMLInputElement) || !this.contains(target) || target.type !== "date" || target.hasAttribute("data-ib-filter") || !target.closest(".ib-table-filter-panel, .ib-filter-cell")) return;
        event.stopPropagation();
        clearTimeout(this.#boundDate);
        if (unfinished(target)) return;
        this.#boundDate = window.setTimeout(() => {
          this.#releasing = true;
          target.dispatchEvent(new Event("change", { bubbles: true }));
          this.#releasing = false;
        }, PAUSE);
      },
      { capture: true, signal }
    );
    this.addEventListener(
      "keydown",
      (event) => {
        const target = event.target;
        if (event.key === "Enter" && isBoundSearch(target)) event.preventDefault();
        if ((event.key !== "Enter" && event.key !== "Escape") || event.isComposing || !(target instanceof HTMLInputElement || target instanceof HTMLSelectElement)) return;
        if (target.hasAttribute("data-ib-filter") || !target.closest(".ib-table-filter-panel")) return;
        // Enter on the operator is for its own list.
        if (event.key === "Enter" && target instanceof HTMLSelectElement) return;
        if (this.#closePanel(target)) event.preventDefault();
      },
      { signal }
    );
  }

  #setupHold(signal: AbortSignal): void {
    const hold = () => {
      const size = this.dataset.ibHold;
      const key = size ? location.pathname + size : "";
      if (key !== this.#heldFor) {
        this.#heldFor = key;
        this.#tallest = 0;
        this.style.removeProperty("min-height");
      }
      if (!key) return;
      this.#tallest = Math.max(this.#tallest, this.offsetHeight);
      const height = this.#tallest ? `${this.#tallest}px` : "";
      if (this.style.minHeight !== height) this.style.minHeight = height;
    };
    // Enhanced navigation's merge takes the style off, and a microtask puts it back before a paint.
    const sizes = new ResizeObserver(hold);
    sizes.observe(this);
    const attributes = new MutationObserver(hold);
    attributes.observe(this, { attributes: true, attributeFilter: ["style", "data-ib-hold"] });
    signal.addEventListener("abort", () => {
      sizes.disconnect();
      attributes.disconnect();
    });
    hold();
  }

  #setupScrollStop(signal: AbortSignal): void {
    const box = this.querySelector<HTMLElement>(":scope > .ib-table-scroll");
    if (!box) return;
    const update = () => {
      // Hidden (a closed tab or dialog), it measures 0: leave the message its own width until shown.
      const view = box.clientWidth > 0 ? `${box.clientWidth}px` : "";
      if (box.style.getPropertyValue("--ib-table-view") !== view) {
        if (view) box.style.setProperty("--ib-table-view", view);
        else box.style.removeProperty("--ib-table-view");
      }
      // A DataGrid's <ib-grid> is its one Tab stop.
      if (this.querySelector(":scope > ib-grid")) return;
      const scrolls = box.scrollWidth > box.clientWidth || box.scrollHeight > box.clientHeight;
      if (scrolls) {
        if (box.getAttribute("tabindex") !== "0") box.setAttribute("tabindex", "0");
      } else if (box.hasAttribute("tabindex") && document.activeElement !== box) {
        box.removeAttribute("tabindex");
      }
    };
    // The box resizes with the window, the table with its rows; enhanced navigation's merge puts
    // the server's tabindex and style back without changing either.
    const sizes = new ResizeObserver(update);
    sizes.observe(box);
    const grid = box.querySelector(":scope > table");
    if (grid) sizes.observe(grid);
    const attributes = new MutationObserver(update);
    attributes.observe(box, { attributes: true, attributeFilter: ["tabindex", "style"] });
    box.addEventListener("focusout", update, { signal });
    signal.addEventListener("abort", () => {
      sizes.disconnect();
      attributes.disconnect();
    });
    update();
  }

  #setupFilters(signal: AbortSignal): void {
    if (!this.querySelector("[data-ib-filter]")) return;
    const isFilter = (target: EventTarget | null): target is HTMLInputElement | HTMLSelectElement =>
      target instanceof HTMLElement && target.matches("[data-ib-filter]") && target.closest("ib-table") === this;

    this.addEventListener("compositionstart", () => (this.#typing.composing = true), { signal });
    this.addEventListener(
      "compositionend",
      (event) => {
        this.#typing.composing = false;
        if (event.target instanceof HTMLInputElement && isFilter(event.target)) this.#typed(event.target);
        // Composing in a list's search held back a tick's apply: it's due now.
        else if (this.#typing.size) this.#timer = window.setTimeout(() => this.#apply(false), PAUSE);
      },
      { signal }
    );
    this.addEventListener(
      "input",
      (event) => {
        if ((event as InputEvent).isComposing || this.#typing.composing) return;
        // Date inputs apply from their change, below.
        if (event.target instanceof HTMLInputElement && event.target.type !== "date" && isFilter(event.target)) this.#typed(event.target);
      },
      { signal }
    );
    this.addEventListener(
      "change",
      (event) => {
        const target = event.target;
        if (!isFilter(target)) return;
        this.#touch(target);
        if (target instanceof HTMLSelectElement) {
          clearTimeout(this.#timer);
          this.#typing.forget(target);
          // With nothing to apply, it only shapes the panel: a response on its way mustn't undo it.
          if (!this.#apply(false)) this.#typing.chosen(target);
        } else if (isTick(target)) {
          // A response for earlier ticks mustn't undo this one.
          this.#typing.typed(target);
          clearTimeout(this.#timer);
          this.#timer = window.setTimeout(() => this.#apply(false), PAUSE);
        } else if (target.type === "date") {
          clearTimeout(this.#timer);
          if (!unfinished(target)) this.#timer = window.setTimeout(() => this.#apply(false), PAUSE);
        }
      },
      { signal }
    );
    this.addEventListener(
      "keydown",
      (event) => {
        // Escape closes the panel and leaves the filter as it is (a search box would clear itself first); a value
        // typed before it still applies after the usual pause. Not while an IME is composing: Escape cancels that.
        const target = event.target;
        // A list's search box isn't a filter, but its keys are the panel's.
        const inPanel = isFilter(target) || (target instanceof HTMLElement && !!target.closest("ib-filter-list"));
        if (event.key === "Escape" && inPanel && !event.isComposing && !this.#typing.composing && this.#closePanel(target)) {
          event.preventDefault();
          return;
        }
        // Enter on a list's Load more or Retry is that button's.
        if (event.key !== "Enter" || !inPanel || target instanceof HTMLSelectElement || target instanceof HTMLButtonElement || event.isComposing || this.#typing.composing) return;
        event.preventDefault(); // Not the GET form's full reload.
        clearTimeout(this.#timer);
        this.#apply(false);
        this.#closePanel(target);
      },
      { signal }
    );
    this.addEventListener(
      "focusout",
      (event) => {
        // A tick waits for its pause wherever focus goes, and so does what's typed, which a response
        // for a sort or a page clicked in the pause would otherwise blank.
        if (event.target instanceof HTMLInputElement && !isTick(event.target) && !this.#changed.has(event.target.name)) this.#typing.forget(event.target);
      },
      { signal }
    );
    // Back or Forward: the address decides, not what was being typed. Setup runs again as the page
    // arrives, which puts the boxes back to what the server drew.
    window.addEventListener("popstate", () => {
      clearTimeout(this.#timer);
      this.#typing.undoTicks();
      this.#typing.clear();
      this.#changed.clear(); this.#rowEdits.clear();
      this.#popped = true;
    }, { signal });
    // A link in a table carries on with what's waiting to apply, as sorting and paging do, except a
    // tag's or a Clear, which removes it. A link out of the tables leaves it behind.
    document.addEventListener(
      "click",
      (event) => {
        const link = event.target instanceof Element ? event.target.closest("a[href]") : null;
        if (!link || event.ctrlKey || event.metaKey || event.shiftKey) return;
        const table = link.closest("ib-table");
        const tag = table === this && !!link.closest(".ib-table-filter-tags, [data-ib-filter-clear]");
        if (table && !tag) return;
        clearTimeout(this.#timer);
        this.#typing.undoTicks();
        this.#typing.clear();
        this.#changed.clear(); this.#rowEdits.clear();
      },
      { signal }
    );
    if (this.#popped) {
      this.#popped = false;
      // An interactive page's render sets its values as properties, so the defaults here are old.
      if (!this.hasAttribute("data-ib-interactive")) for (const control of this.querySelectorAll<HTMLInputElement | HTMLSelectElement>("[data-ib-filter]")) {
        if (control.closest("ib-table") !== this) continue;
        if (isTick(control)) control.checked = control.defaultChecked;
        else if (control instanceof HTMLInputElement) control.value = control.defaultValue;
        else if (control instanceof HTMLSelectElement) for (const option of control.options) option.selected = option.defaultSelected;
      }
    }

    // A response for an earlier value has just arrived: what's been typed since goes back, and below,
    // being a change from what the server drew, it applies after the usual pause.
    this.#typing.restore();

    // A filter changed before this script arrived (the table shows no Apply button where script
    // runs): apply it now rather than drop it; one still being typed in, after the usual pause.
    const filters = [...this.querySelectorAll("[data-ib-filter]")].filter((c) => c.closest("ib-table") === this);
    // Not on an interactive page, whose render sets values as properties (see filter-form.ts).
    if (!this.hasAttribute("data-ib-interactive") && changedFromServer(filters)) {
      for (const control of filters) if (changedFromServer([control])) this.#touch(control);
      const active = document.activeElement;
      // A tick keeps its own place in the history, where typing replaces its own.
      if (active instanceof HTMLInputElement && filters.includes(active) && active.type !== "date" && !isTick(active)) this.#typed(active);
      else this.#apply(false);
    }
  }

  /**
   * Closes the open header filter panel holding a control, and puts focus back on the button that opened it. Only
   * this table's own panels: a table inside a popover doesn't close the popover around it.
   */
  #closePanel(control: EventTarget | null): boolean {
    const panel = control instanceof Element ? control.closest(".ib-table-filter-panel")?.closest<HTMLElement>("[popover]") : null;
    if (!panel || !this.contains(panel) || !panel.matches(":popover-open")) return false;
    const trigger = this.querySelector<HTMLElement>(`[popovertarget="${CSS.escape(panel.id)}"]`);
    if (!trigger) return false;
    panel.hidePopover();
    trigger.focus();
    return true;
  }

  #touch(control: Element): void {
    // A panel's controls go together; the toolbar's search is on its own.
    this.#changed.add((control as HTMLInputElement).name);
    if (control.hasAttribute("data-ib-row")) this.#rowEdits.add(control as HTMLInputElement);
    for (const c of control.closest(".ib-table-filter-panel")?.querySelectorAll<HTMLInputElement | HTMLSelectElement>("[data-ib-filter]") ?? []) this.#changed.add(c.name);
  }

  #typed(input: HTMLInputElement): void {
    this.#touch(input);
    this.#typing.typed(input);
    clearTimeout(this.#timer);
    this.#timer = window.setTimeout(() => this.#apply(true), PAUSE);
  }

  /**
   * Navigates to the current URL with the values of this table's changed panels, without its page
   * number, and says whether that changed it. A date part way through stays as the address has it.
   */
  #apply(replace: boolean): boolean {
    if (this.#typing.composing || !this.isConnected) return false;
    this.#typing.restore();
    const url = new URL(location.href);
    const own = [...this.querySelectorAll<HTMLInputElement | HTMLSelectElement>("[data-ib-filter]")].filter((c) => c.closest("ib-table") === this && c.name && !c.hasAttribute("data-ib-row"));
    // A date part way through stays as the address has it, as in a panel.
    for (const edit of this.#rowEdits) {
      const twin = own.find((c) => c.name === edit.name);
      if (twin && edit.isConnected && !unfinished(edit)) twin.value = edit.value;
    }
    this.#rowEdits.clear();
    const controls = own.filter((c) => this.#changed.has(c.name) && !unfinished(c));
    if (controls.length === 0) return false;
    // A name can repeat: all of them go, then each control with something to say comes back.
    for (const control of controls) url.searchParams.delete(control.name);
    for (const control of controls) {
      const value = written(control);
      if (value) url.searchParams.append(control.name, value);
    }
    for (const list of this.querySelectorAll<HTMLElement>("ib-filter-list[data-ib-none]")) {
      if (list.closest("ib-table") !== this || !this.#changed.has(list.querySelector<HTMLInputElement>("input[data-ib-filter]")?.name ?? "")) continue;
      url.searchParams.delete(list.dataset.ibNone!);
      if (!list.querySelector("input[data-ib-filter]:checked")) url.searchParams.append(list.dataset.ibNone!, "1");
    }
    this.#typing.forgetTicks();
    this.#changed.clear();
    const pageKey = this.dataset.ibPageKey;
    if (pageKey) url.searchParams.delete(pageKey);
    const cursorKey = this.dataset.ibCursorKey;
    if (cursorKey) url.searchParams.delete(cursorKey);
    if (!navigateTo(url, replace)) return false;
    this.#typing.forgetChosen();
    this.#typing.forgetLeft();
    // An operator with no value doesn't outlast a new filter. A static page's response redraws it, and an interactive page's render doesn't.
    for (const control of own) if (control instanceof HTMLSelectElement && control.dataset.ibDefault && !written(control)) control.value = control.dataset.ibDefault;
    this.#typing.guard();
    return true;
  }
}

defineElement("ib-table", Table);
