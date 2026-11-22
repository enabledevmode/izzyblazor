import { IzzyElement, defineElement } from "../izzy-element";

/**
 * <ib-kanban> keeps a static board's column headers right between posts: it counts a column's
 * cards again when one arrives or leaves (the count, its text for screen readers and the color
 * past a limit), and collapses and expands columns. On an interactive page C# does both, and this
 * tells it the order the page draws the columns and lanes in when that changes (`iborder`): the
 * page may reorder or add to a keyed list, and Blazor tells a parent nothing of its children's order.
 */
class Kanban extends IzzyElement {
  #observer: MutationObserver | null = null;

  protected setup(signal: AbortSignal): void {
    if (this.hasAttribute("data-ib-interactive")) {
      this.follow();
      return;
    }
    this.addEventListener(
      "click",
      (event) => {
        const button = (event.target as Element).closest<HTMLElement>(".ib-kanban-toggle");
        // A column's button, or on a board with lanes a lane's.
        const column = button?.closest<HTMLElement>(".ib-kanban-column, .ib-kanban-lane");
        if (!button || !column || column.closest("ib-kanban") !== this) return;
        const collapsed = column.toggleAttribute("data-ib-collapsed");
        button.setAttribute("aria-expanded", String(!collapsed));
        const posted = column.querySelector<HTMLInputElement>(".ib-kanban-collapsed");
        if (posted) posted.disabled = !collapsed;
      },
      { signal }
    );
    this.#observer = new MutationObserver(() => this.count());
    for (const list of this.querySelectorAll(".ib-kanban-cards .ib-listview-list")) {
      this.#observer.observe(list, { childList: true });
    }
  }

  protected teardown(): void {
    this.#observer?.disconnect();
    this.#observer = null;
  }

  /** The columns' zones and the lanes' values, in the order they're on the page. */
  private order(): { columns: string[]; lanes: string[] } {
    const mine = (el: Element) => el.closest("ib-kanban") === this;
    return {
      columns: [...this.querySelectorAll<HTMLElement>(".ib-kanban-column[data-ib-column]")].filter(mine).map((el) => el.dataset.ibColumn ?? ""),
      lanes: [...this.querySelectorAll<HTMLElement>("[data-ib-lane]")].filter(mine).map((el) => el.dataset.ibLane ?? ""),
    };
  }

  private follow(): void {
    let last = JSON.stringify(this.order());
    // A render's changes arrive together, so this runs once for each.
    this.#observer = new MutationObserver(() => {
      const now = this.order();
      const text = JSON.stringify(now);
      if (text === last) return;
      last = text;
      this.dispatchEvent(new CustomEvent("iborder", { bubbles: true, detail: now }));
    });
    this.#observer.observe(this, { childList: true, subtree: true });
  }

  private count(): void {
    // Not a board with lanes, whose headers count every lane's cells: the post that follows a move redraws them.
    for (const column of this.querySelectorAll<HTMLElement>(".ib-kanban-column:not(.ib-kanban-head)")) {
      const n = column.querySelectorAll(".ib-kanban-cards .ib-listview-list > .ib-sortable-item").length;
      const shown = column.querySelector<HTMLElement>(".ib-kanban-n");
      const text = n.toLocaleString(document.documentElement.lang || undefined);
      if (!shown || shown.textContent === text) continue;
      shown.textContent = text;
      const limit = column.getAttribute("data-ib-limit");
      const min = column.getAttribute("data-ib-min");
      column.toggleAttribute("data-ib-off", (limit !== null && n > Number(limit)) || (min !== null && n < Number(min)));
      const spoken = column.querySelector<HTMLElement>(".ib-kanban-count-text");
      const format = this.getAttribute(limit === null ? "data-ib-count" : "data-ib-count-limit");
      if (spoken && format) spoken.textContent = format.replace("{0}", text).replace("{1}", limit ?? "");
    }
  }
}

defineElement("ib-kanban", Kanban);
