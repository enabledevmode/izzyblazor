import { IzzyElement, defineElement } from "../izzy-element";

/**
 * <ib-filter-list> adds what a list filter can't do in HTML: Select all, which ticks or unticks the
 * rows shown and fires `change` on each it flips; the search, which narrows the rows and applies
 * nothing; and the rows as one Tab stop, with arrows and Home and End. Applying is <ib-table>'s on
 * static pages and C#'s on bound ones. Without script both Select all and the search are hidden by
 * CSS, the boxes are Tab stops of their own, and they post with Apply.
 */
class FilterList extends IzzyElement {
  // Kept when setup runs again as a response arrives, which puts the box back to what the server drew.
  #search = "";

  protected setup(signal: AbortSignal): void {
    const all = this.querySelector<HTMLInputElement>("[data-ib-list-all]")!;
    const search = this.querySelector<HTMLInputElement>("[data-ib-list-search]");
    const results = this.querySelector<HTMLElement>("[data-ib-list-results]")!;
    const rows = [...this.querySelectorAll<HTMLLabelElement>(".ib-filter-list-items > label")];
    const box = (row: HTMLLabelElement) => row.querySelector("input")!;
    const shown = () => rows.filter((row) => !row.hidden).map(box);

    // One Tab stop among the rows: the one with focus, else the one that was, else the first shown.
    const rove = () => {
      const boxes = shown();
      const stop = boxes.includes(document.activeElement as HTMLInputElement) ? document.activeElement : boxes.find((b) => b.tabIndex === 0);
      for (const row of rows) box(row).tabIndex = box(row) === stop ? 0 : -1;
    };
    const sync = () => {
      const boxes = shown();
      const ticked = boxes.filter((b) => b.checked).length;
      all.checked = ticked > 0 && ticked === boxes.length;
      all.indeterminate = ticked > 0 && ticked < boxes.length;
      all.disabled = boxes.length === 0;
    };
    const narrow = () => {
      const text = this.#search.trim().toLocaleLowerCase();
      for (const row of rows) row.hidden = !!text && !row.textContent!.toLocaleLowerCase().includes(text);
      (results.previousElementSibling as HTMLElement).hidden = !!text;
      results.hidden = !text;
      results.textContent = text ? this.dataset.ibResults!.replace("{0}", String(shown().length)) : "";
      rove();
      sync();
    };

    this.addEventListener(
      "change",
      (event) => {
        if (event.target === all) {
          // Read once: each row's change comes back here, and syncs Select all.
          const on = all.checked;
          for (const b of shown()) {
            if (b.checked === on) continue;
            b.checked = on;
            b.dispatchEvent(new Event("change", { bubbles: true }));
          }
        }
        sync();
      },
      { signal }
    );
    search?.addEventListener(
      "input",
      () => {
        this.#search = search.value;
        narrow();
      },
      { signal }
    );
    this.addEventListener(
      "keydown",
      (event) => {
        const stops = [search, all, ...shown()].filter((s): s is HTMLInputElement => !!s && !s.disabled);
        const at = stops.indexOf(event.target as HTMLInputElement);
        const boxes = shown();
        // In the search, Home and End are the caret's.
        const moves: Record<string, HTMLInputElement | undefined> = { ArrowDown: stops[at + 1], ArrowUp: stops[at - 1] };
        if (event.target !== search) Object.assign(moves, { Home: boxes[0], End: boxes[boxes.length - 1] });
        if (at < 0 || !(event.key in moves)) return;
        event.preventDefault();
        event.stopPropagation();
        moves[event.key]?.focus();
      },
      { signal }
    );
    this.addEventListener("focusin", rove, { signal });
    // A search lasts as long as the panel is open.
    this.closest("[popover]")?.addEventListener(
      "toggle",
      (event) => {
        if ((event as ToggleEvent).newState !== "closed" || !search) return;
        search.value = this.#search = "";
        narrow();
      },
      { signal }
    );

    // A static page's response changes the checked attributes, and a bound page's render the count.
    const observer = new MutationObserver(sync);
    observer.observe(this, { attributes: true, subtree: true, attributeFilter: ["checked", "data-ib-ticked"] });
    signal.addEventListener("abort", () => observer.disconnect());

    if (search && search.value !== this.#search) search.value = this.#search;
    narrow();
  }
}

defineElement("ib-filter-list", FilterList);
