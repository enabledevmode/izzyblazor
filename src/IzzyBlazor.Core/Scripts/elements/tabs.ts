import { IzzyElement, defineElement } from "../izzy-element";

/**
 * <ib-tabs> upgrades Tabs from a radio group to ARIA tabs. The radios stay the source of truth
 * (CSS shows the panel whose radio is checked); the element only adds roles, aria-selected and
 * roving focus to the labels, hides the radios from the tab order, and keeps the URL in step
 * when data-query-key is set.
 *
 * It re-syncs when a radio changes, when C# changes the selection (data-selected), and after
 * enhanced navigation, which resets attributes to the server's markup.
 */
class Tabs extends IzzyElement {
  private get list(): HTMLElement | null {
    return this.querySelector<HTMLElement>(":scope > .ib-tabs-list");
  }

  private get tabs(): HTMLLabelElement[] {
    return [...this.querySelectorAll<HTMLLabelElement>(":scope > .ib-tabs-list > .ib-tab")];
  }

  /** Whether the list is laid out as a column (vertical tabs, from 640px up). */
  private get isColumn(): boolean {
    const list = this.list;
    return !!list && getComputedStyle(list).flexDirection === "column";
  }

  private radio(tab: Element): HTMLInputElement | null {
    return tab.querySelector<HTMLInputElement>(":scope > .ib-tab-input");
  }

  protected setup(signal: AbortSignal): void {
    this.sync();

    this.addEventListener(
      "change",
      (event) => {
        const input = event.target as HTMLInputElement;
        if (!input.matches?.(".ib-tab-input") || input.name !== this.id) return;
        this.sync();
        this.writeUrl(input.value);
      },
      { signal }
    );

    this.addEventListener("keydown", (event) => this.onKey(event), { signal });

    // The browser can't focus an invalid field in a closed panel, and blocks the submit with no
    // message: open its tab first. "invalid" doesn't bubble, so listen while it travels down.
    // Only the first invalid field of a check counts (the browser focuses that one).
    let opened = false;
    this.addEventListener(
      "invalid",
      (event) => {
        const field = event.target as HTMLInputElement;
        // The browser points at the first invalid field in the form; a field before the tabs may be it.
        if (field.form && field.form.querySelector("input:invalid, select:invalid, textarea:invalid") !== field) return;
        let panel = field.closest(".ib-tab-panel");
        while (panel && panel.parentElement !== this) panel = panel.parentElement?.closest(".ib-tab-panel") ?? null;
        if (!panel || opened) return;
        opened = true;
        setTimeout(() => (opened = false));
        const radio = this.querySelector<HTMLInputElement>(`:scope > .ib-tabs-list .ib-tab-input[aria-controls="${panel.id}"]`);
        if (radio && !radio.checked) {
          radio.checked = true;
          // Announce it once the check is over: a change during it would clear the field's own
          // message (forms.ts) before the browser shows it.
          setTimeout(() => radio.dispatchEvent(new Event("change", { bubbles: true })));
        }
      },
      { capture: true, signal }
    );

    // Vertical tabs switch between a column and a row at 640px; keep aria-orientation in step
    // (checked on each change, since C# can switch the orientation on interactive pages).
    const wide = matchMedia("(min-width: 640px)");
    wide.addEventListener("change", () => this.sync(), { signal });

    // Re-apply roles when C# changes the selection or orientation (data-selected, class), adds or
    // removes tabs, or disables one. sync() sets only role, aria-* and tabindex, which aren't watched.
    const observer = new MutationObserver(() => this.sync());
    observer.observe(this, { attributes: true, attributeFilter: ["data-selected", "class"] });
    const list = this.list;
    if (list) observer.observe(list, { childList: true, subtree: true, attributes: true, attributeFilter: ["disabled"] });
    signal.addEventListener("abort", () => observer.disconnect());
  }

  /** Applies tab roles and state from the radios. Safe to call any time. */
  sync(): void {
    const list = this.list;
    if (!list) return;
    list.setAttribute("role", "tablist");
    // Vertical tabs are a row on phones, so the orientation follows the layout.
    if (this.isColumn) list.setAttribute("aria-orientation", "vertical");
    else list.removeAttribute("aria-orientation");

    const tabs = this.tabs;
    let focusable: HTMLElement | null = null;
    for (const tab of tabs) {
      const radio = this.radio(tab);
      if (!radio) continue;
      const selected = radio.checked;
      tab.setAttribute("role", "tab");
      tab.setAttribute("aria-selected", String(selected));
      tab.tabIndex = selected ? 0 : -1;
      const panelId = radio.getAttribute("aria-controls");
      if (panelId) {
        tab.setAttribute("aria-controls", panelId);
        const panel = document.getElementById(panelId);
        if (panel) {
          panel.setAttribute("role", "tabpanel");
          panel.setAttribute("aria-labelledby", tab.id);
          panel.tabIndex = 0;
        }
      }
      if (radio.disabled) tab.setAttribute("aria-disabled", "true");
      else tab.removeAttribute("aria-disabled");
      radio.hidden = true;
      if (selected) focusable = tab;
    }

    // Nothing selected (every tab disabled): keep the list reachable.
    if (!focusable && tabs[0]) tabs[0].tabIndex = 0;
  }

  private onKey(event: KeyboardEvent): void {
    const tab = (event.target as Element).closest<HTMLLabelElement>(".ib-tab");
    if (!tab || tab.parentElement !== this.list) return;
    // Alt+Arrow is the browser's Back and Forward; leave modified keys alone.
    if (event.altKey || event.ctrlKey || event.metaKey) return;

    const enabled = this.tabs.filter((t) => !this.radio(t)?.disabled);
    const index = enabled.indexOf(tab);
    const rtl = getComputedStyle(this).direction === "rtl";
    let next: HTMLLabelElement | undefined;

    // In a row, Left and Right move between tabs (mirrored in right-to-left pages); in a column
    // (vertical tabs), Up and Down.
    const column = this.isColumn;
    const step = (forward: boolean) => enabled[(index + (forward ? 1 : -1) + enabled.length) % enabled.length];

    switch (event.key) {
      case "ArrowRight":
      case "ArrowLeft":
        if (column) return;
        next = step((event.key === "ArrowRight") !== rtl);
        break;
      case "ArrowDown":
      case "ArrowUp":
        if (!column) return;
        next = step(event.key === "ArrowDown");
        break;
      case "Home":
        next = enabled[0];
        break;
      case "End":
        next = enabled[enabled.length - 1];
        break;
      case "Enter":
      case " ":
        next = tab;
        break;
      default:
        return;
    }

    event.preventDefault();
    if (next) this.select(next);
  }

  private select(tab: HTMLLabelElement): void {
    const radio = this.radio(tab);
    if (!radio || radio.disabled) return;
    if (!radio.checked) {
      radio.checked = true;
      radio.dispatchEvent(new Event("change", { bubbles: true }));
    }
    tab.focus();
  }

  private writeUrl(value: string): void {
    const key = this.dataset.queryKey;
    if (!key) return;
    const url = new URL(location.href);
    url.searchParams.set(key, value);
    history.replaceState(history.state, "", url);
  }
}

defineElement("ib-tabs", Tabs);

/** Re-applies tab roles after enhanced navigation reset the attributes. */
export function syncTabs(): void {
  document.querySelectorAll<HTMLElement & { sync?: () => void }>("ib-tabs").forEach((tabs) => tabs.sync?.());
}
