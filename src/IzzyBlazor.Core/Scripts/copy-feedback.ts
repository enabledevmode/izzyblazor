/**
 * Copies text and shows it on the button: the "Copied" check for a moment, and "Copied" as an
 * icon button's name (or announced, for a button with text), then back. Shared by <ib-code> and
 * <ib-copy>.
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
  // An icon button is named by its aria-label, which says "Copied" for a moment. A button with
  // visible text keeps its name (it must match the text) and the word is announced instead.
  const iconOnly = button.hasAttribute("aria-label") || button.dataset.label !== undefined;
  if (iconOnly) {
    // The name to restore, saved once: a second click within the delay would otherwise save "Copied".
    button.dataset.label ??= button.getAttribute("aria-label") ?? "";
    button.setAttribute("aria-label", copied);
  } else {
    announce(copied);
  }
  timers.set(
    button,
    window.setTimeout(() => {
      button.removeAttribute("data-copied");
      if (iconOnly) button.setAttribute("aria-label", button.dataset.label ?? "");
    }, 1600)
  );
}

let region: HTMLElement | null = null;

/** Says something to screen readers through a hidden polite live region at the end of the body. */
function announce(text: string): void {
  if (!region?.isConnected) {
    region = document.createElement("div");
    region.className = "ib-visually-hidden";
    region.setAttribute("aria-live", "polite");
    document.body.append(region);
  }
  const target = region;
  target.textContent = "";
  // A beat later, so a repeat of the same words is announced again.
  setTimeout(() => (target.textContent = text), 50);
}
