import { IzzyElement, defineElement } from "../izzy-element";

/**
 * <ib-code> adds a working copy button to a Code block. The button is hidden by CSS until the
 * element is defined, so pages with no script never show a button that does nothing.
 */
class CodeBlock extends IzzyElement {
  #timer = 0;

  protected setup(signal: AbortSignal): void {
    this.addEventListener(
      "click",
      async (event) => {
        const button = (event.target as Element).closest<HTMLButtonElement>(".ib-code-copy");
        if (!button || button.closest("ib-code") !== this) return;
        const code = this.querySelector("pre code")?.textContent ?? "";
        try {
          await navigator.clipboard.writeText(code);
        } catch {
          return; // No clipboard access (insecure page, or denied): leave the button as it was.
        }
        // The label to restore, saved once: a second click within the delay would otherwise save "Copied".
        button.dataset.label ??= button.getAttribute("aria-label") ?? "";
        const label = button.dataset.label;
        button.toggleAttribute("data-copied", true);
        button.setAttribute("aria-label", button.dataset.copiedLabel ?? "Copied");
        clearTimeout(this.#timer);
        this.#timer = window.setTimeout(() => {
          button.removeAttribute("data-copied");
          if (label) button.setAttribute("aria-label", label);
        }, 1600);
      },
      { signal }
    );
  }
}

defineElement("ib-code", CodeBlock);
