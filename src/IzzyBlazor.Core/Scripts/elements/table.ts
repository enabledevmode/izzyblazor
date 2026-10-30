import { IzzyElement, defineElement } from "../izzy-element";
import { TypingGuard, changedFromServer, navigateTo } from "../filtering";
import { setupLoadOnScroll, setupSelectAll, setupSelection } from "../data-view";

/**
 * <ib-table> adds two things to a Table:
 *
 * - The header "select all" checkbox of a selectable table. Rows are plain checkboxes that post
 *   with the form (and report their own changes on interactive pages), so select-all only checks
 *   or clears the rows on this page and fires `change` on each one it flips, then shows the
 *   header as checked, cleared or partly selected (indeterminate).
 * - Live filters. Without script the header filters' fields are a GET form you submit with Enter
 *   or Apply. Here the boxes apply after a pause in typing, dates when they change and Enter at once:
 *   the element puts the values in the URL, drops this table's page number and navigates through
 *   Blazor, which keeps the page in place. (Filters kept in the component on interactive pages
 *   don't carry data-ib-filter, so this leaves them alone.) A header filter's panel stays open
 *   while you type; Enter applies and closes it, Escape closes it as it is, and either puts focus
 *   back on its button.
 * - Loading on scroll (Paging LoadOnScroll, interactive pages): when the last row comes into view,
 *   it presses the table's hidden "load next" button, as Load more does without moving focus.
 *   The visible button stays for the keyboard.
 * - Shift+click on a DataGrid's sort link adds the column to the sort (its data-ib-sort-add), where
 *   the browser would open a window.
 * - The scroll box's Tab stop: the server renders it focusable so the keyboard can scroll it with
 *   no script; here it's a Tab stop only while there's something to scroll. Focused, it stays.
 *   It also gives the box its visible width (--ib-table-view), which keeps the "no items" message
 *   centered in view when the columns are wider than the box.
 */
class Table extends IzzyElement {
  #timer = 0;
  #typing = new TypingGuard();

  protected setup(signal: AbortSignal): void {
    this.#setupFilters(signal);
    setupSelectAll(this, ".ib-table-select-all", "tbody .ib-table-select", signal);
    setupSelection(this, "tbody .ib-table-select", signal);
    setupLoadOnScroll(this, ":scope > .ib-table-scroll > table > tbody > tr:last-child", ":scope > .ib-table-scroll > .ib-data-more [data-ib-load-next]", signal);
    this.#setupScrollStop(signal);
    this.#setupBoundPanels(signal);
    this.addEventListener(
      "click",
      (event) => {
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
    this.addEventListener(
      "keydown",
      (event) => {
        const target = event.target;
        if ((event.key !== "Enter" && event.key !== "Escape") || event.isComposing || !(target instanceof HTMLInputElement)) return;
        if (target.hasAttribute("data-ib-filter") || !target.closest(".ib-table-filter-panel")) return;
        if (this.#closePanel(target)) event.preventDefault();
      },
      { signal }
    );
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
      },
      { signal }
    );
    this.addEventListener(
      "input",
      (event) => {
        if ((event as InputEvent).isComposing || this.#typing.composing) return;
        // Date inputs apply on change, once a whole date is chosen.
        if (event.target instanceof HTMLInputElement && event.target.type !== "date" && isFilter(event.target)) this.#typed(event.target);
      },
      { signal }
    );
    this.addEventListener(
      "change",
      (event) => {
        const target = event.target;
        if ((target instanceof HTMLSelectElement || (target instanceof HTMLInputElement && target.type === "date")) && isFilter(target)) {
          clearTimeout(this.#timer);
          this.#apply(false);
        }
      },
      { signal }
    );
    this.addEventListener(
      "keydown",
      (event) => {
        // Escape closes the panel and leaves the filter as it is (a search box would clear itself first); a value
        // typed before it still applies after the usual pause. Not while an IME is composing: Escape cancels that.
        if (event.key === "Escape" && isFilter(event.target) && !event.isComposing && !this.#typing.composing && this.#closePanel(event.target)) {
          event.preventDefault();
          return;
        }
        if (event.key !== "Enter" || !isFilter(event.target) || this.#typing.composing) return;
        event.preventDefault(); // Not the GET form's full reload.
        clearTimeout(this.#timer);
        this.#apply(false);
        this.#closePanel(event.target);
      },
      { signal }
    );
    this.addEventListener(
      "focusout",
      (event) => {
        if (event.target instanceof HTMLInputElement) this.#typing.forget(event.target);
      },
      { signal }
    );
    signal.addEventListener("abort", () => clearTimeout(this.#timer));

    // A filter changed before this script arrived (the table shows no Apply button where script
    // runs): apply it now rather than drop it; one still being typed in, after the usual pause.
    const filters = [...this.querySelectorAll("[data-ib-filter]")].filter((c) => c.closest("ib-table") === this);
    // Not on an interactive page, whose render sets values as properties (see filter-form.ts).
    if (!this.hasAttribute("data-ib-interactive") && changedFromServer(filters)) {
      const active = document.activeElement;
      if (active instanceof HTMLInputElement && filters.includes(active) && active.type !== "date") this.#typed(active);
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

  #typed(input: HTMLInputElement): void {
    this.#typing.typed(input);
    clearTimeout(this.#timer);
    this.#timer = window.setTimeout(() => this.#apply(true), 350);
  }

  /** Navigates to the current URL with this table's filter values, without its page number. */
  #apply(replace: boolean): void {
    if (this.#typing.composing) return;
    this.#typing.restore();
    const url = new URL(location.href);
    for (const control of this.querySelectorAll<HTMLInputElement | HTMLSelectElement>("[data-ib-filter]")) {
      if (control.closest("ib-table") !== this || !control.name) continue;
      const value = control.value.trim();
      if (value) url.searchParams.set(control.name, value);
      else url.searchParams.delete(control.name);
    }
    const pageKey = this.dataset.ibPageKey;
    if (pageKey) url.searchParams.delete(pageKey);
    const cursorKey = this.dataset.ibCursorKey;
    if (cursorKey) url.searchParams.delete(cursorKey);
    if (navigateTo(url, replace)) this.#typing.guard();
  }
}

defineElement("ib-table", Table);
