import { IzzyElement, defineElement } from "../izzy-element";
import { setupLoadOnScroll } from "../data-view";

/**
 * <ib-filter-list> adds what a list filter can't do in HTML: Select all, which ticks or unticks the
 * rows shown and fires `change` on each it flips; the search, which narrows the rows and applies
 * nothing; and the rows as one Tab stop, with arrows and Home and End. Applying is <ib-table>'s on
 * static pages and C#'s on bound ones. Without script both Select all and the search are hidden by
 * CSS, the boxes are Tab stops of their own, and they post with Apply.
 *
 * A list C# looks up (data-ib-list-remote) may have more rows than are here, so C# does Select all
 * and the search, and says what Select all shows in data-ib-all. Here it keeps its keys and its one
 * Tab stop as rows arrive, loads more as the last row comes into view, and takes focus from Load
 * more to the first new row.
 */
class FilterList extends IzzyElement {
  // Kept when setup runs again as a response arrives, which puts the box back to what the server drew.
  #search = "";

  protected setup(signal: AbortSignal): void {
    const remote = this.hasAttribute("data-ib-list-remote");
    const all = this.querySelector<HTMLInputElement>("[data-ib-list-all]")!;
    const search = this.querySelector<HTMLInputElement>("[data-ib-list-search], [data-ib-bound-search]");
    const results = this.querySelector<HTMLElement>("[data-ib-list-results]")!;
    const rows = () => [...this.querySelectorAll<HTMLLabelElement>(".ib-filter-list-items > label")];
    const box = (row: HTMLLabelElement) => row.querySelector("input")!;
    const shown = () => rows().filter((row) => !row.hidden).map(box);

    // One Tab stop among the rows: the one with focus, else the one that was, else the first shown.
    const rove = () => {
      const boxes = shown().filter((b) => !b.disabled);
      const stop = boxes.includes(document.activeElement as HTMLInputElement) ? document.activeElement : boxes.find((b) => b.tabIndex === 0);
      for (const row of rows()) box(row).tabIndex = box(row) === stop ? 0 : -1;
    };
    const sync = () => {
      if (remote) {
        all.checked = this.dataset.ibAll === "on";
        all.indeterminate = this.dataset.ibAll === "mixed";
        return;
      }
      const boxes = shown();
      const ticked = boxes.filter((b) => b.checked).length;
      all.checked = ticked > 0 && ticked === boxes.length;
      all.indeterminate = ticked > 0 && ticked < boxes.length;
      all.disabled = boxes.length === 0;
    };
    const narrow = () => {
      if (!remote) {
        const text = this.#search.trim().toLocaleLowerCase();
        for (const row of rows()) row.hidden = !!text && !row.textContent!.toLocaleLowerCase().includes(text);
        (results.previousElementSibling as HTMLElement).hidden = !!text;
        results.hidden = !text;
        results.textContent = text ? this.dataset.ibResults!.replace("{0}", String(shown().length)) : "";
      }
      rove();
      sync();
    };

    this.addEventListener(
      "change",
      (event) => {
        if (!remote && event.target === all) {
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
    if (!remote) {
      search?.addEventListener(
        "input",
        () => {
          this.#search = search.value;
          narrow();
        },
        { signal }
      );
    }
    this.addEventListener(
      "keydown",
      (event) => {
        const stops = [search, all, ...shown()].filter((s): s is HTMLInputElement => !!s && !s.disabled);
        const at = stops.indexOf(event.target as HTMLInputElement);
        const boxes = shown().filter((b) => !b.disabled);
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
        // A list opens at its top, where what's unticked now comes first.
        if ((event as ToggleEvent).newState !== "closed") {
          if (remote) this.querySelector(".ib-filter-list-items")!.scrollTop = 0;
          return;
        }
        if (!search) return;
        search.value = this.#search = "";
        narrow();
      },
      { signal }
    );

    // A static page's response changes the checked attributes, and a bound page's render the count, or what C# says.
    const observer = new MutationObserver(sync);
    observer.observe(this, { attributes: true, subtree: true, attributeFilter: ["checked", "data-ib-ticked", "data-ib-all"] });
    signal.addEventListener("abort", () => observer.disconnect());

    if (remote) {
      setupLoadOnScroll(this, ".ib-filter-list-items > label:last-of-type", "[data-ib-load-next]", signal);
      // Load more goes when there's no more, and new rows are above it: focus goes to the first of them.
      let from = -1;
      this.addEventListener(
        "click",
        (event) => {
          const pressed = event.target as Element;
          if (pressed.closest("[data-ib-list-more]")) from = rows().length;
          // Retry goes as it answers.
          else if (pressed.closest("[data-ib-list-retry]")) search?.focus();
        },
        { signal }
      );
      const arrivals = new MutationObserver(() => {
        narrow();
        const first = from < 0 ? undefined : rows()[from];
        if (first) {
          from = -1;
          box(first).focus();
        }
      });
      arrivals.observe(this.querySelector(".ib-filter-list-items")!, { childList: true });
      signal.addEventListener("abort", () => arrivals.disconnect());
    }

    if (search && !remote && search.value !== this.#search) search.value = this.#search;
    narrow();
  }
}

defineElement("ib-filter-list", FilterList);
