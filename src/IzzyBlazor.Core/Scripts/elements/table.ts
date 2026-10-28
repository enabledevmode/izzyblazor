import { IzzyElement, defineElement } from "../izzy-element";
import { TypingGuard, changedFromServer, navigateTo } from "../filtering";

/**
 * <ib-table> adds two things to a Table:
 *
 * - The header "select all" checkbox of a selectable table. Rows are plain checkboxes that post
 *   with the form (and report their own changes on interactive pages), so select-all only checks
 *   or clears the rows on this page and fires `change` on each one it flips, then shows the
 *   header as checked, cleared or partly selected (indeterminate).
 * - Live filters. Without script the filter row is a GET form you submit with Enter or Apply.
 *   Here the boxes apply after a pause in typing, selects when they change and Enter at once:
 *   the element puts the values in the URL, drops this table's page number and navigates through
 *   Blazor, which keeps the page in place. (Filters kept in the component on interactive pages
 *   don't carry data-ib-filter, so this leaves them alone.) A header filter's panel stays open
 *   while you type; Enter applies and closes it, Escape closes it as it is, and either puts focus
 *   back on its button.
 * - Loading on scroll (Paging LoadOnScroll, interactive pages): when the last row comes into view,
 *   it presses the table's hidden "load next" button, as Load more does without moving focus.
 *   The visible button stays for the keyboard.
 */
class Table extends IzzyElement {
  #timer = 0;
  #typing = new TypingGuard();

  protected setup(signal: AbortSignal): void {
    this.#setupFilters(signal);
    this.#setupSelectAll(signal);
    this.#setupLoadOnScroll(signal);
  }

  #setupLoadOnScroll(signal: AbortSignal): void {
    if (!this.hasAttribute("data-ib-load-on-scroll")) return;
    let last: Element | null = null;
    let inView = false;
    // The trigger is drawn only on interactive pages, and not while a page loads: one at a time.
    const loadNext = () => this.querySelector<HTMLButtonElement>(":scope > .ib-table-footer [data-ib-load-next]")?.click();
    // Viewport-rooted, so the table's own scroll box (MaxHeight) clips it too.
    const visibility = new IntersectionObserver((entries) => {
      // Only the row watched now: a report queued for the one before may still arrive.
      const entry = entries.filter((e) => e.target === last).pop();
      if (!entry) return;
      inView = entry.isIntersecting;
      if (inView) loadNext();
    });
    // New rows move the last one; a trigger that comes back (a page loaded) while it's still in
    // view loads the next, until the rows fill the view.
    const watch = () => {
      const row = this.querySelector(":scope > .ib-table-scroll > table > tbody > tr:last-child");
      if (row !== last) {
        if (last) visibility.unobserve(last);
        last = row;
        inView = false;
        if (row) visibility.observe(row);
      } else if (inView) {
        loadNext();
      }
    };
    const changes = new MutationObserver(watch);
    changes.observe(this, { childList: true, subtree: true });
    signal.addEventListener("abort", () => {
      changes.disconnect();
      visibility.disconnect();
    });
    watch();
  }

  #setupSelectAll(signal: AbortSignal): void {
    if (!this.querySelector(".ib-table-select-all")) return;
    this.dataset.ready = "";

    this.addEventListener(
      "change",
      (event) => {
        const target = event.target as HTMLInputElement;
        if (target.matches(".ib-table-select-all")) this.selectAll(target.checked);
        else if (target.matches(".ib-table-select")) this.sync();
      },
      { signal }
    );

    // Rows replaced by paging, sorting, filtering or an interactive render.
    const observer = new MutationObserver(() => this.sync());
    observer.observe(this, { childList: true, subtree: true, attributes: true, attributeFilter: ["checked"] });
    this.closest("form")?.addEventListener("reset", () => setTimeout(() => this.sync(), 0), { signal });
    signal.addEventListener("abort", () => {
      observer.disconnect();
      delete this.dataset.ready;
    });

    this.sync();
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
   * this table's own panels: a row filter, or a table inside a popover, closes nothing.
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

  private get rows(): HTMLInputElement[] {
    return [...this.querySelectorAll<HTMLInputElement>("tbody .ib-table-select")].filter((box) => !box.disabled);
  }

  private selectAll(on: boolean): void {
    for (const box of this.rows) {
      if (box.checked === on) continue;
      box.checked = on;
      box.dispatchEvent(new Event("change", { bubbles: true }));
    }
    this.sync();
  }

  private sync(): void {
    const all = this.querySelector<HTMLInputElement>(".ib-table-select-all");
    if (!all) return;
    const rows = this.rows;
    const checked = rows.filter((box) => box.checked).length;
    all.checked = rows.length > 0 && checked === rows.length;
    all.indeterminate = checked > 0 && checked < rows.length;
    all.disabled = rows.length === 0;
  }
}

defineElement("ib-table", Table);
