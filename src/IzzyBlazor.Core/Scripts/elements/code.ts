import { IzzyElement, defineElement } from "../izzy-element";
import { copyWithFeedback } from "../copy-feedback";

/**
 * <ib-code> adds a working copy button to a Code block. The button is hidden by CSS until the
 * element is defined, so pages with no script never show a button that does nothing.
 */
class CodeBlock extends IzzyElement {
  protected setup(signal: AbortSignal): void {
    this.addEventListener(
      "click",
      (event) => {
        const button = (event.target as Element).closest<HTMLButtonElement>(".ib-code-copy");
        if (!button || button.closest("ib-code") !== this) return;
        void copyWithFeedback(button, this.querySelector("pre code")?.textContent ?? "");
      },
      { signal }
    );
  }
}

defineElement("ib-code", CodeBlock);
