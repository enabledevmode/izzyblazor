/**
 * Copies text and shows it on the button: the "Copied" check for a moment, then back, with
 * "Copied" announced (elements/announce.ts). Shared by <ib-code> and <ib-copy>.
 */
import { announce } from "./elements/announce";

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
  announce(copied, false, button);
  timers.set(
    button,
    window.setTimeout(() => button.removeAttribute("data-copied"), 1600)
  );
}
