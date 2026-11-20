import { IzzyElement, defineElement } from "../izzy-element";

/**
 * <ib-confirm data-for="…"> wraps a ConfirmDialog's dialog. A click on what it's for (an id, or a
 * selector when it starts with "#", "." or "[") opens the dialog instead of acting; the dialog's
 * confirm button then clicks it again, and that click goes through: it posts its form, follows
 * its link or runs its C#, whichever it is. "{0}" in the title and message is the clicked one's
 * data-confirm.
 */
class Confirm extends IzzyElement {
  protected setup(signal: AbortSignal): void {
    const dialog = this.querySelector<HTMLDialogElement>("dialog");
    const target = this.dataset.for ?? "";
    if (!dialog || !target) return;
    const selector = /^[#.[]/.test(target) ? target : `#${CSS.escape(target)}`;
    let asked: HTMLElement | null = null;
    let passing = false;

    // On the window, before the page's own handlers (a menu's, Blazor's) see the click.
    window.addEventListener(
      "click",
      (event) => {
        const hit = passing || !(event.target instanceof Element) ? null : event.target.closest<HTMLElement>(selector);
        if (!hit || hit.matches(":disabled, [aria-disabled=true]")) return;
        event.preventDefault();
        event.stopImmediatePropagation();
        asked = hit;
        const name = hit.dataset.confirm ?? "";
        // The text node Blazor rendered, changed in place: a new one would be lost to its next render.
        const fill = (element: Element | null, text: string | undefined) => {
          const node = element?.firstChild;
          if (text?.includes("{0}") && node instanceof Text) node.data = text.replace("{0}", name);
        };
        fill(dialog.querySelector(".ib-dialog-title"), this.dataset.title);
        fill(dialog.querySelector(".ib-confirm-message > p"), this.dataset.message);
        // Opening a modal dialog closes the menu the item was in.
        dialog.showModal();
      },
      { capture: true, signal }
    );

    dialog.querySelector("[data-ib-confirm]")?.addEventListener(
      "click",
      () => {
        const go = asked;
        asked = null;
        dialog.close();
        if (!go?.isConnected) return;
        passing = true;
        try {
          go.click();
        } finally {
          passing = false;
        }
      },
      { signal }
    );

    // Cancelled from a menu's item: the item is hidden again, so focus goes to the menu's button.
    dialog.addEventListener(
      "close",
      () => {
        const from = asked;
        asked = null;
        if (!from || from.checkVisibility()) return;
        const menu = from.closest("[popover]");
        if (menu?.id) document.querySelector<HTMLElement>(`[popovertarget="${CSS.escape(menu.id)}"]`)?.focus();
      },
      { signal }
    );
  }
}

defineElement("ib-confirm", Confirm);
