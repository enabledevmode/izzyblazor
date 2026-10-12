import { IzzyElement, defineElement } from "../izzy-element";

/**
 * <ib-table> adds the header "select all" checkbox to a selectable Table. Rows are plain
 * checkboxes that post with the form (and report their own changes on interactive pages), so
 * select-all only checks or clears the rows on this page and fires `change` on each one it flips,
 * then shows the header as checked, cleared or partly selected (indeterminate).
 */
class Table extends IzzyElement {
  protected setup(signal: AbortSignal): void {
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
    this.querySelector("form")?.addEventListener("reset", () => setTimeout(() => this.sync(), 0), { signal });
    signal.addEventListener("abort", () => {
      observer.disconnect();
      delete this.dataset.ready;
    });

    this.sync();
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
