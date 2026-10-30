import { IzzyElement, defineElement } from "../izzy-element";
import { setupLoadOnScroll, setupSelectAll } from "../data-view";

/**
 * <ib-listview> adds a ListView's select-all checkbox (SelectionMode.Multiple) and loading on scroll (Paging
 * LoadOnScroll, interactive pages), both shared with <ib-table>. Everything else works without it.
 */
class ListView extends IzzyElement {
  protected setup(signal: AbortSignal): void {
    setupSelectAll(this, ".ib-listview-select-all", ".ib-listview-list > li > .ib-listview-select", signal);
    setupLoadOnScroll(this, ":scope > .ib-listview-body > .ib-listview-list > li:last-child", ":scope > .ib-listview-body > .ib-data-more [data-ib-load-next]", signal);
  }
}

defineElement("ib-listview", ListView);
