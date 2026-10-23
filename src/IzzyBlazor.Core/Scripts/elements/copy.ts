import { IzzyElement, defineElement } from "../izzy-element";
import { copyWithFeedback } from "../copy-feedback";

/**
 * <ib-copy> makes a CopyButton work: it copies data-copy-text, or the value or text of the
 * element whose id is data-copy-for. CSS shows the button where the browser runs script, from the
 * first paint, so the page doesn't change when this arrives; with JavaScript off there's no
 * button that does nothing.
 */
class CopyButton extends IzzyElement {
  protected setup(signal: AbortSignal): void {
    this.addEventListener(
      "click",
      (event) => {
        const button = (event.target as Element).closest<HTMLButtonElement>("button");
        if (!button || button.closest("ib-copy") !== this || button.disabled) return;
        void copyWithFeedback(button, this.#text());
      },
      { signal }
    );
  }

  #text(): string {
    if (this.dataset.copyText !== undefined) return this.dataset.copyText;
    const target = this.dataset.copyFor ? document.getElementById(this.dataset.copyFor) : null;
    if (!target) return "";
    if (target instanceof HTMLInputElement || target instanceof HTMLTextAreaElement) return target.value;
    return target.textContent ?? "";
  }
}

defineElement("ib-copy", CopyButton);
