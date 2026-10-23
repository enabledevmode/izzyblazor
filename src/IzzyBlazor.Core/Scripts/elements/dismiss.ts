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
