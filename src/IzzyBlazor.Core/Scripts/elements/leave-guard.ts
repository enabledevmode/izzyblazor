import { IzzyElement, defineElement } from "../izzy-element";

/**
 * <ib-leave-guard> is Form's ConfirmLeave (FormLeaveGuard.razor): it asks before the user leaves
 * the page while the form it's in has unsaved changes.
 *
 * - A link (any but those opening elsewhere, downloading, changing only the #fragment, or not
 *   http) and, on static pages, another form's post: caught in the capture phase, before Blazor's
 *   enhanced navigation or interactive router (both skip a cancelled event). The guard's dialog
 *   asks; Leave clicks the link or submits the form again, and lets it through. On interactive
 *   pages Leave tells C# instead (a change event on the guard's hidden input, with the address),
 *   which navigates past its own location-changing handler.
 * - Reload, closing the tab, a typed address: beforeunload, the browser's own prompt.
 * - Back and forward, and NavigateTo, on interactive pages: C# asks with the same dialog, and
 *   Leave reports an empty address.
 *
 * Unsaved: the server marks it (data-ib-unsaved), or the form's values differ from those it had
 * when the user first focused, pressed or dropped anything in it. Taking them then, not on load, leaves out
 * values that fields' own scripts set as they start (a mask formatting its value).
 */
let leaving = false;
const guards = new Set<LeaveGuard>();

class LeaveGuard extends IzzyElement {
  #baseline: string | null = null;
  #epoch: string | undefined;
  #pending: (() => void) | null = null;

  protected setup(signal: AbortSignal): void {
    guards.add(this);
    signal.addEventListener("abort", () => guards.delete(this));
    leaving = false;
    this.#baseline = null;
    const form = this.closest("form");
    const take = () => {
      if (form && (this.#baseline === null || this.#epoch !== this.dataset.ibEpoch)) {
        this.#baseline = values(form);
        this.#epoch = this.dataset.ibEpoch;
      }
    };
    for (const type of ["focusin", "pointerdown", "keydown", "dragenter"]) form?.addEventListener(type, take, { capture: true, signal });

    this.addEventListener(
      "click",
      (event) => {
        if (!(event.target as Element).closest("[data-ib-leave]")) return;
        const go = this.#pending;
        this.#pending = null;
        this.querySelector("dialog")?.close();
        leaving = true;
        if (go) go();
        else this.tell("");
      },
      { signal }
    );
    this.querySelector("dialog")?.addEventListener("close", () => queueMicrotask(() => (this.#pending = null)), { signal });
  }

  /** Forgets the values to compare with: the page was posted and came back. */
  reset(): void {
    this.#baseline = null;
  }

  get interactive(): boolean {
    return this.hasAttribute("data-ib-interactive");
  }

  get unsaved(): boolean {
    if (this.hasAttribute("data-ib-unsaved")) return true;
    const form = this.closest("form");
    return !!form && this.#baseline !== null && this.#epoch === this.dataset.ibEpoch && values(form) !== this.#baseline;
  }

  /** Opens the dialog; Leave then runs go (static) or tells C# the address (interactive). */
  ask(go: () => void, href?: string): void {
    this.#pending = this.interactive && href !== undefined ? () => this.tell(href) : go;
    this.querySelector("dialog")?.showModal();
  }

  /** Reports Leave to C#, which navigates: to href, or where its own handler was going. */
  tell(href: string): void {
    const input = this.querySelector<HTMLInputElement>(":scope > input");
    if (!input) return;
    input.value = href;
    input.dispatchEvent(new Event("change", { bubbles: true }));
  }
}

/** The form's values as one string; files by name and size. Blazor's own fields are left out. */
function values(form: HTMLFormElement): string {
  const parts: string[] = [];
  for (const [name, value] of new FormData(form)) {
    if (name === "_handler" || name === "__RequestVerificationToken") continue;
    parts.push(`${name}=${typeof value === "string" ? value : `${value.name}:${value.size}`}`);
  }
  return parts.join("&");
}

/** The first guard on the page with unsaved changes. */
function unsaved(): LeaveGuard | undefined {
  if (leaving) return undefined;
  for (const guard of guards) if (guard.isConnected && guard.unsaved) return guard;
  return undefined;
}

document.addEventListener(
  "click",
  (event) => {
    if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
    const link = (event.target as Element | null)?.closest?.<HTMLAnchorElement>("a[href]");
    if (!link || link.hasAttribute("download") || (link.target && link.target !== "_self")) return;
    const url = new URL(link.href, location.href);
    if (!url.protocol.startsWith("http")) return;
    if (url.hash && url.href.split("#")[0] === location.href.split("#")[0]) return;
    const guard = unsaved();
    if (!guard) return;
    event.preventDefault();
    event.stopImmediatePropagation();
    guard.ask(() => link.click(), link.href);
  },
  true
);

document.addEventListener(
  "submit",
  (event) => {
    const form = event.target as HTMLFormElement;
    if (event.defaultPrevented || form.method === "dialog") return;
    const submitter = (event as SubmitEvent).submitter;
    const target = (submitter?.getAttribute("formtarget") ?? form.target) || "_self";
    if (target !== "_self") return;
    // The guarded form's own post never asks, and the reload or close it causes doesn't either.
    if (form.querySelector(":scope ib-leave-guard")) {
      leaving = true;
      return;
    }
    const guard = unsaved();
    // On interactive pages other forms are handled in C# and don't leave the page.
    if (!guard || guard.interactive) return;
    event.preventDefault();
    event.stopImmediatePropagation();
    guard.ask(() => form.requestSubmit(submitter as HTMLElement | null));
  },
  true
);

window.addEventListener("beforeunload", (event) => {
  if (!unsaved()) return;
  event.preventDefault();
  event.returnValue = true;
});

window.addEventListener("pageshow", () => (leaving = false));

// After an enhanced post or navigation the guard stays, but the form's values are the server's.
(window as { Blazor?: { addEventListener?(name: string, callback: () => void): void } }).Blazor?.addEventListener?.("enhancedload", () => {
  leaving = false;
  guards.forEach((guard) => guard.reset());
});

defineElement("ib-leave-guard", LeaveGuard);
