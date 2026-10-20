/**
 * Copies text and shows it on the button: the "Copied" check for a moment, and "Copied" as its
 * name, then back. Shared by <ib-code> and <ib-copy>.
 */
const timers = new WeakMap<HTMLElement, number>();

export async function copyWithFeedback(button: HTMLElement, text: string): Promise<void> {
  try {
    await navigator.clipboard.writeText(text);
  } catch {
    return; // No clipboard access (insecure page, or denied): leave the button as it was.
  }
  // The name to restore, saved once: a second click within the delay would otherwise save "Copied".
  // A button with visible text and no aria-label gets its name back by removing the attribute.
  button.dataset.label ??= button.getAttribute("aria-label") ?? "";
  const label = button.dataset.label;
  button.toggleAttribute("data-copied", true);
  button.setAttribute("aria-label", button.dataset.copiedLabel ?? "Copied");
  clearTimeout(timers.get(button));
  timers.set(
    button,
    window.setTimeout(() => {
      button.removeAttribute("data-copied");
      if (label) button.setAttribute("aria-label", label);
      else button.removeAttribute("aria-label");
    }, 1600)
  );
}
