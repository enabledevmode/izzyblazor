import { IzzyElement, defineElement } from "../izzy-element";
import { copyWithFeedback } from "../copy-feedback";

/**
 * <ib-code> adds a working copy button to a Code block. CSS shows the button where the browser
 * runs script, from the first paint, so the block doesn't change when this arrives; with
 * JavaScript off there's no button that does nothing.
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
