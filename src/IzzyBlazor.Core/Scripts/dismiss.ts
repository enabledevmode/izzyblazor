/**
 * Keeps dismissed messages closed through enhanced loads, on every page. The close button itself
 * (<ib-dismiss>) loads on demand (elements/dismiss.ts).
 */

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
