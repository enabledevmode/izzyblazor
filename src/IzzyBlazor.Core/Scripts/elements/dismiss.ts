import { IzzyElement, defineElement } from "../izzy-element";

/**
 * <ib-dismiss> is the close button of a dismissible Alert, Banner or toast once the script runs.
 *
 * With no script the close button is a visually hidden checkbox and its label: checking it hides
 * the message with CSS, but screen readers call it a checkbox, and focus falls to the page when
 * the message disappears. The server also renders this element around a real <button>; CSS shows
 * it, and hides the checkbox and label, where the browser runs script (@media (scripting:
 * enabled)), from the first paint, so the message doesn't change when this arrives; the button
 * works once it has. The markup Blazor renders never changes.
 *
 * A click checks the hidden checkbox and raises its change event, so everything that listens for
 * a dismissal still hears one (the CSS that hides the message, Banner's cookie, @bind-Dismissed on
 * interactive pages, the toast region). Then, unless something else already moved focus (the
 * toast region does), focus goes to the next control after the message, or the one before it.
 */
const FOCUSABLE =
  'a[href], button:not([disabled]), input:not([disabled]):not([type="hidden"]), select:not([disabled]), ' +
  'textarea:not([disabled]), summary, [tabindex]:not([tabindex="-1"])';

class Dismiss extends IzzyElement {
  protected setup(signal: AbortSignal): void {
    this.addEventListener(
      "click",
      (event) => {
        if (!(event.target as Element | null)?.closest("button")) return;
        const message = this.parentElement;
        const input = message?.querySelector<HTMLInputElement>(":scope > input.ib-alert-dismiss, :scope > input.ib-banner-dismiss");
        if (!message || !input || input.checked) return;

        // Found before the message hides: hidden elements don't take focus.
        const target = neighbor(message);
        input.checked = true;
        input.dispatchEvent(new Event("change", { bubbles: true }));
        if (message.contains(document.activeElement)) {
          if (target) target.focus();
          else (document.activeElement as HTMLElement | null)?.blur();
        }
      },
      { signal }
    );
  }
}

/** The first visible control after the message in page order, else the last one before it. */
function neighbor(message: HTMLElement): HTMLElement | null {
  const candidates = [...document.querySelectorAll<HTMLElement>(FOCUSABLE)].filter(
    (el) => !message.contains(el) && !el.closest("template, [inert]") && el.checkVisibility()
  );
  const after = candidates.find((el) => message.compareDocumentPosition(el) & Node.DOCUMENT_POSITION_FOLLOWING);
  return after ?? candidates.filter((el) => message.compareDocumentPosition(el) & Node.DOCUMENT_POSITION_PRECEDING).pop() ?? null;
}

defineElement("ib-dismiss", Dismiss);

// A remembered Banner (data-ib-remember) isn't kept here: its cookie decides, and the server
// renders it again only under a new key. Nor is a toast (an Alert in ToastRegion): the same
// words again are a new toast.
const DISMISS_INPUT = ":not(.ib-toast) > input.ib-alert-dismiss, input.ib-banner-dismiss:not([data-ib-remember])";

/** Messages dismissed on this page: the checkbox, its page and the message's text then. */
const dismissed = new Map<HTMLInputElement, { path: string; text: string }>();

/**
 * Keeps a dismissed Alert (or a Banner that isn't remembered) closed through an enhanced post or
 * navigation that stays on the page, as an interactive page does. Blazor patches the page in
 * place and sets each checkbox back to what the server rendered, unchecked, so without this the
 * message comes back with any post on the page, another form's included. A message in the form
 * that was posted is that post's answer and shows again ("Saved", after a second save), as is
 * one whose text changed; any other stays closed, as an unbound one does on an interactive page.
 * With JavaScript off the whole page loads again, so it comes back.
 */
export function installDismissMemory(): void {
  document.addEventListener("change", (event) => {
    const input = event.target;
    if (!(input instanceof HTMLInputElement) || !input.matches(DISMISS_INPUT)) return;
    // Interactive pages have no enhanced loads to clear it: drop what has left the page.
    for (const old of dismissed.keys()) if (!old.isConnected) dismissed.delete(old);
    if (input.checked && input.parentElement) dismissed.set(input, { path: location.pathname, text: input.parentElement.textContent ?? "" });
    else dismissed.delete(input);
  });

  document.addEventListener(
    "submit",
    (event) => {
      if (!(event.target instanceof HTMLFormElement)) return;
      for (const input of dismissed.keys()) if (event.target.contains(input)) dismissed.delete(input);
    },
    true
  );
}

/** After an enhanced load: closes again what was dismissed on this page and is still the same message. */
export function keepDismissed(): void {
  for (const [input, { path, text }] of dismissed) {
    const message = input.parentElement;
    if (!input.isConnected || !message || path !== location.pathname || message.textContent !== text) {
      dismissed.delete(input);
    } else if (!input.checked) {
      // No change event: the dismissal was already reported.
      input.checked = true;
    }
  }
}
