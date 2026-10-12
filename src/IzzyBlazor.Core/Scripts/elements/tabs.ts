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

    const observer = new MutationObserver(() => this.sync());
    observer.observe(this, { attributes: true, attributeFilter: ["data-selected"] });
    signal.addEventListener("abort", () => observer.disconnect());
  }

  /** Applies tab roles and state from the radios. Safe to call any time. */
  sync(): void {
    const list = this.list;
    if (!list) return;
    list.setAttribute("role", "tablist");

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

    switch (event.key) {
      case "ArrowRight":
        next = enabled[(index + (rtl ? -1 : 1) + enabled.length) % enabled.length];
        break;
      case "ArrowLeft":
        next = enabled[(index + (rtl ? 1 : -1) + enabled.length) % enabled.length];
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
