import { IzzyElement, defineElement } from "../izzy-element";
import { emitToggle } from "../toggle";

/**
 * <ib-dialog> wraps a Dialog's <dialog>. Opening by DialogTrigger, closing, Escape, focus
 * trapping and focus return are native. This element adds:
 *   - data-open: the server (?dialog=id, Open="true") or C# (@bind-Open) can open it as a
 *     real modal; the server's non-modal `open` is upgraded to showModal(),
 *   - closing on a backdrop click where the native closedby attribute isn't supported,
 *   - removing ?dialog=id (or the dialog's own QueryKey) from the address after closing, so a
 *     reload doesn't reopen it,
 *   - `ibtoggle` events, so @bind-Open follows the browser.
 */
class DialogRoot extends IzzyElement {
  static observedAttributes = ["data-open"];

  #observer: MutationObserver | null = null;
  #reported: boolean | null = null;
  #syncTarget: boolean | null = null;
  #userState: boolean | null = null;
  #userAt = 0;
  #pressedOnBackdrop = false;

  private get dialog(): HTMLDialogElement | null {
    return this.querySelector<HTMLDialogElement>(":scope > dialog");
  }

  protected setup(signal: AbortSignal): void {
    const dialog = this.dialog;
    if (!dialog) return;

    this.#reported = dialog.open;
    // Set up again after enhanced navigation (reconnect): drop the old observer first, and forget
    // the user's last open or close: the new page's markup is the current state.
    this.#observer?.disconnect();
    this.#userState = null;
    this.#observer = new MutationObserver(() => this.report());
    this.#observer.observe(dialog, { attributes: true, attributeFilter: ["open"] });

    dialog.addEventListener("close", () => this.clearQuery(dialog), { signal });

    if (this.hasAttribute("data-close-on-backdrop") && !("closedBy" in HTMLDialogElement.prototype)) {
      // Only a press that starts and ends on the backdrop closes it: dragging a text selection
      // out of a field must not.
      dialog.addEventListener("pointerdown", (event) => (this.#pressedOnBackdrop = event.target === dialog && this.outside(dialog, event)), { signal });
      dialog.addEventListener(
        "click",
        (event) => {
          const pressed = this.#pressedOnBackdrop;
          this.#pressedOnBackdrop = false;
          if (event.target !== dialog || !pressed) return;
          const r = dialog.getBoundingClientRect();
          const inside = event.clientX >= r.left && event.clientX <= r.right && event.clientY >= r.top && event.clientY <= r.bottom;
          if (!inside) dialog.close();
        },
        { signal }
      );
    }

    if (this.hasAttribute("data-open")) this.sync();
  }

  protected teardown(): void {
    this.#observer?.disconnect();
    this.#observer = null;
  }

  attributeChangedCallback(): void {
    if (this.isConnected && this.#observer) this.sync();
  }

  private outside(dialog: HTMLDialogElement, event: MouseEvent): boolean {
    const r = dialog.getBoundingClientRect();
    return !(event.clientX >= r.left && event.clientX <= r.right && event.clientY >= r.top && event.clientY <= r.bottom);
  }

  private sync(): void {
    const dialog = this.dialog;
    if (!dialog) return;
    const want = this.hasAttribute("data-open");
    // Until the server has caught up with the user's last open or close, a render that left
    // before it mustn't undo it. Once data-open matches what the user did, C# is in charge again.
    if (this.#userState !== null) {
      if (want === this.#userState || Date.now() - this.#userAt >= 2000) this.#userState = null;
      else return;
    }
    this.#syncTarget = want;
    if (want && !dialog.matches(":modal")) {
      // Removing the attribute (rather than close()) fires no close event, so ?dialog=id stays.
      if (dialog.open) dialog.removeAttribute("open");
      try {
        dialog.showModal();
      } catch {
        dialog.setAttribute("open", "");
      }
    } else if (!want && dialog.open) {
      dialog.close();
    }
  }

  private report(): void {
    const dialog = this.dialog;
    if (!dialog || dialog.open === this.#reported) return;
    if (this.#syncTarget === dialog.open) {
      this.#syncTarget = null;
    } else {
      this.#userState = dialog.open;
      this.#userAt = Date.now();
    }
    this.#reported = dialog.open;
    emitToggle(dialog, dialog.open);
  }

  private clearQuery(dialog: HTMLDialogElement): void {
    // data-query-key is the dialog's QueryKey; without one, no link opens it.
    const key = this.dataset.queryKey;
    const url = new URL(location.href);
    if (key && dialog.id && url.searchParams.get(key) === dialog.id) {
      url.searchParams.delete(key);
      history.replaceState(history.state, "", url);
    }
  }
}

defineElement("ib-dialog", DialogRoot);

/**
 * A small fallback for the native commandfor/command attributes (DialogTrigger, DialogClose
 * and the dialog's close button), for browsers that don't support them yet.
 */
export function installCommandFallback(): void {
  if (typeof HTMLButtonElement === "undefined" || "commandForElement" in HTMLButtonElement.prototype) return;

  document.addEventListener("click", (event) => {
    const button = (event.target as Element | null)?.closest?.<HTMLButtonElement>("button[commandfor]");
    if (!button || button.disabled) return;
    const target = document.getElementById(button.getAttribute("commandfor") ?? "");
    const command = button.getAttribute("command");
    if (!target || !command) return;

    if (target instanceof HTMLDialogElement) {
      if (command === "show-modal" && !target.open) target.showModal();
      else if (command === "close" && target.open) target.close();
      else if (command === "request-close" && target.open) {
        const requestClose = (target as HTMLDialogElement & { requestClose?: () => void }).requestClose;
        requestClose ? requestClose.call(target) : target.close();
      }
    } else if (typeof target.showPopover === "function") {
      if (command === "show-popover") target.showPopover();
      else if (command === "hide-popover") target.hidePopover();
      else if (command === "toggle-popover") target.togglePopover();
    }
  });
}

/** The dialog whose own form was just posted, to reopen if the response shows errors. */
let postedFrom: string | null = null;
const ERRORS = ".ib-field-invalid, .ib-form-errors, [aria-invalid='true']";

/**
 * Closes a dialog when a button in it submits a form other than its own close form (a
 * ConfirmButton's confirm, say), or a link in it navigates. With enhanced navigation or an
 * enhanced form post the page isn't reloaded, so the open dialog would otherwise stay on top
 * of the response. Without script the browser loads a new page, which closes it anyway.
 */
export function installDialogAutoClose(): void {
  document.addEventListener("submit", (event) => {
    const form = event.target;
    const submitter = (event as SubmitEvent).submitter;
    if (!(form instanceof HTMLFormElement) || form.method === "dialog" || !submitter) return;
    const dialog = submitter.closest("dialog");
    if (!dialog?.open) return;
    // Interactive dialogs: the submit is handled in C#, which decides whether to close.
    if (dialog.closest("ib-dialog[data-ib-interactive]")) return;
    // A form inside the dialog (not one around it, as for a ConfirmButton) may come back with
    // validation errors; reopenAfterPost shows them.
    postedFrom = form.closest("dialog") === dialog && dialog.id ? dialog.id : null;
    queueMicrotask(() => dialog.close());
  });

  document.addEventListener("click", (event) => {
    if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
    const link = (event.target as Element | null)?.closest?.<HTMLAnchorElement>("dialog[open] a[href]");
    if (!link || link.hasAttribute("download") || (link.target && link.target !== "_self")) return;
    const dialog = link.closest("dialog");
    if (dialog) queueMicrotask(() => dialog.close());
  });
}

/**
 * After an enhanced form post: if the form came from a dialog and the response shows validation
 * errors in it, reopen the dialog so they're seen. Without script, a dialog opened by ?dialog=id
 * posts to the same address, so the server renders it open again anyway.
 */
export function reopenAfterPost(): void {
  const id = postedFrom;
  postedFrom = null;
  if (!id) return;
  const dialog = document.getElementById(id);
  if (!(dialog instanceof HTMLDialogElement) || !dialog.querySelector(ERRORS)) return;
  // The server may have rendered it open (a ?dialog=id address): that's non-modal, so make it modal.
  if (!dialog.matches(":modal")) {
    if (dialog.open) dialog.removeAttribute("open");
    try {
      dialog.showModal();
    } catch {
      dialog.setAttribute("open", "");
    }
  }
  // Start at the first field with an error (a FormErrors summary in the dialog takes focus next).
  dialog.querySelector<HTMLElement>("[aria-invalid='true']")?.focus();
}
