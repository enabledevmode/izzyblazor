import { IzzyElement, defineElement } from "../izzy-element";

/**
 * <ib-treeselect> is a TreeSelect's field: a button, and a popover panel (<ib-popover> opens,
 * places and closes it, and puts focus back on the button) holding a TreeView (<ib-tree> gives it
 * the tree's roles and keys). This adds:
 * - Down or Up on the button opens the panel; a click on the label focuses the button without
 *   opening it, as a select's would.
 * - Opening focuses the search box, else the chosen item, else the first.
 * - Choosing one item (a click, or Enter or Space in the tree) closes the panel, also on the item
 *   already chosen. Tab out of the panel closes it.
 * - On a static page the button shows the chosen item's text at once (Blazor draws it on an
 *   interactive page), and the clear button follows.
 * - Closing ends a search, so the tree opens whole again.
 * - The clear button checks the field's empty radio, which posts nothing and, on an interactive
 *   page, clears the value.
 */
class TreeSelect extends IzzyElement {
  protected setup(signal: AbortSignal): void {
    const button = this.querySelector<HTMLButtonElement>(".ib-treeselect-button");
    const panel = this.querySelector<HTMLElement>(".ib-treeselect-panel");
    if (!button || !panel || typeof panel.showPopover !== "function") return;
    const multiple = this.hasAttribute("data-ib-multiple");
    const interactive = this.hasAttribute("data-ib-interactive");
    const isOpen = () => panel.matches(":popover-open");
    const close = () => {
      if (isOpen()) panel.hidePopover();
    };
    this.dataset.ibReady = "";
    signal.addEventListener("abort", () => delete this.dataset.ibReady);

    button.addEventListener(
      "keydown",
      (event) => {
        if ((event.key !== "ArrowDown" && event.key !== "ArrowUp") || event.ctrlKey || event.metaKey) return;
        event.preventDefault();
        if (!isOpen() && !button.disabled) panel.showPopover();
      },
      { signal }
    );
    document.querySelector(`label[for="${CSS.escape(button.id)}"]`)?.addEventListener(
      "click",
      (event) => {
        event.preventDefault();
        button.focus();
      },
      { signal }
    );

    // The popover focuses its [autofocus] element as it opens.
    panel.addEventListener(
      "beforetoggle",
      (event) => {
        const search = panel.querySelector<HTMLInputElement>("[data-ib-tree-search]");
        if ((event as Event & { newState?: string }).newState !== "open") {
          // A search ends with the panel: it opens again on the whole tree.
          if (search?.value) {
            search.value = "";
            search.dispatchEvent(new Event("input", { bubbles: true }));
          }
          // Closing with focus inside (a choice, Escape) puts it back on the field. WebKit hands it
          // to the nearest focusable ancestor as the panel hides, before <ib-popover> can.
          if (panel.contains(document.activeElement)) queueMicrotask(() => button.focus());
          return;
        }
        for (const el of panel.querySelectorAll("[autofocus]")) el.removeAttribute("autofocus");
        const target =
          panel.querySelector<HTMLElement>("[data-ib-tree-search]:not([hidden] *)") ??
          panel.querySelector<HTMLElement>("[role=treeitem][tabindex='0']") ??
          panel.querySelector<HTMLElement>(".ib-tree-select:checked") ??
          panel.querySelector<HTMLElement>(".ib-tree-select");
        target?.setAttribute("autofocus", "");
      },
      { signal }
    );

    // Down in the search box moves into the tree: to the first match, not the parents kept around
    // it, or with no search to the tree's Tab stop.
    panel.querySelector<HTMLInputElement>("[data-ib-tree-search]")?.addEventListener(
      "keydown",
      (event) => {
        if (event.key !== "ArrowDown") return;
        const query = (event.target as HTMLInputElement).value.trim().toLowerCase();
        const shown = [...panel.querySelectorAll<HTMLElement>("li[role=treeitem]")].filter((li) => li.checkVisibility());
        const stop =
          (query ? shown.find((li) => (li.getAttribute("aria-label") ?? "").toLowerCase().includes(query)) : undefined) ??
          panel.querySelector<HTMLElement>("[role=treeitem][tabindex='0']:not([hidden])");
        if (!stop) return;
        event.preventDefault();
        stop.focus();
      },
      { signal }
    );

    if (!multiple) {
      // A radio's click comes from a click on its line, or from the tree's Enter and Space.
      panel.addEventListener(
        "click",
        (event) => {
          const box = event.target;
          if (box instanceof HTMLInputElement && box.matches(".ib-tree-select") && box.checked) close();
        },
        { signal }
      );
      // The tree doesn't click the item already chosen; choosing it again closes too.
      panel.addEventListener(
        "keydown",
        (event) => {
          if (event.key !== "Enter" && event.key !== " ") return;
          const box = (event.target as Element).closest("[role=treeitem]")?.querySelector<HTMLInputElement>(":scope > .ib-tree-item > .ib-tree-select");
          if (box?.checked && !box.disabled) close();
        },
        { signal }
      );
    }

    panel.addEventListener(
      "focusout",
      (event) => {
        const to = (event as FocusEvent).relatedTarget as Node | null;
        if (to && !panel.contains(to)) close();
      },
      { signal }
    );

    const text = this.querySelector<HTMLElement>(".ib-treeselect-text");
    const clear = this.querySelector<HTMLButtonElement>(":scope > .ib-treeselect-clear");
    const none = this.querySelector<HTMLInputElement>(":scope > .ib-treeselect-none");
    const show = (box: HTMLInputElement | null) => {
      if (interactive || !text) return;
      const label = box?.closest("li")?.querySelector<HTMLElement>(":scope > .ib-tree-item [data-ib-label]")?.dataset.ibLabel;
      text.textContent = label ?? this.dataset.ibPlaceholder ?? "";
      text.toggleAttribute("data-ib-empty", label === undefined);
      if (clear) clear.hidden = label === undefined;
    };
    panel.addEventListener(
      "change",
      (event) => {
        const box = event.target;
        if (!multiple && box instanceof HTMLInputElement && box.matches(".ib-tree-select") && box.checked) show(box);
      },
      { signal }
    );

    clear?.addEventListener(
      "click",
      () => {
        if (!none) return;
        const was = panel.querySelector<HTMLInputElement>(".ib-tree-select:checked");
        none.checked = true;
        none.dispatchEvent(new Event("input", { bubbles: true }));
        none.dispatchEvent(new Event("change", { bubbles: true }));
        if (!interactive) {
          show(null);
          // The tree hears its box was unchecked and redraws the item's state.
          was?.dispatchEvent(new Event("change", { bubbles: true }));
        }
        button.focus();
      },
      { signal }
    );
  }
}

defineElement("ib-treeselect", TreeSelect);
