/**
 * Copies text and shows it on the button: the "Copied" check for a moment, then back, with
 * "Copied" announced through a polite live region. Shared by <ib-code> and <ib-copy>.
 */
const timers = new WeakMap<HTMLElement, number>();

export async function copyWithFeedback(button: HTMLElement, text: string): Promise<void> {
  try {
    await navigator.clipboard.writeText(text);
  } catch {
    return; // No clipboard access (insecure page, or denied): leave the button as it was.
  }
  const copied = button.dataset.copiedLabel ?? "Copied";
  button.toggleAttribute("data-copied", true);
  clearTimeout(timers.get(button));
  // Announced, not put in the button's name: many screen readers don't read a change to the name
  // of the focused button, and the name stays what the button does.
  announce(copied, button);
  timers.set(
    button,
    window.setTimeout(() => button.removeAttribute("data-copied"), 1600)
  );
}

/**
 * Says something to screen readers through a hidden polite live region: in the open dialog the
 * button is in (outside a modal dialog everything is inert, so it wouldn't be read), else at the
 * end of the body.
 */
function announce(text: string, from: Element): void {
  const host = from.closest("dialog[open]") ?? document.body;
  let target = host.querySelector<HTMLElement>(":scope > .ib-copy-live");
  if (!target) {
    target = document.createElement("div");
    target.className = "ib-visually-hidden ib-copy-live";
    target.setAttribute("aria-live", "polite");
    host.append(target);
  }
  target.textContent = "";
  // A beat later, so a repeat of the same words is announced again.
  setTimeout(() => (target!.textContent = text), 50);
}
