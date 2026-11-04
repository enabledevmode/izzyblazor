import { IzzyElement, defineElement } from "../izzy-element";

/**
 * <ib-pin-field> draws a PinField's code in its boxes. The input is the field: it holds the code,
 * takes paste and autofill, and posts; it sits over the boxes with its text and caret transparent.
 * This drops what the code can't hold as you type or paste (spaces, dashes, letters in a digits
 * code, past Length), writes each character into its box's data-c (dots for a masked code), and
 * marks the box the caret is at. Without script, or if this fails to load, CSS shows the input
 * as a plain field.
 */
class PinField extends IzzyElement {
  protected setup(signal: AbortSignal): void {
    const input = this.querySelector<HTMLInputElement>(":scope > .ib-pin-input");
    const boxes = [...this.querySelectorAll<HTMLElement>(":scope > .ib-pin-boxes > .ib-pin-box")];
    if (!input || boxes.length === 0) return;
    const letters = input.inputMode === "text";

    const paint = () => {
      const value = input.value;
      const at = document.activeElement === input ? Math.min(input.selectionStart ?? value.length, boxes.length - 1) : -1;
      boxes.forEach((box, i) => {
        const c = value[i] ? (input.type === "password" ? "•" : value[i]) : null;
        if (box.getAttribute("data-c") !== c) {
          if (c === null) box.removeAttribute("data-c");
          else box.setAttribute("data-c", c);
        }
        box.toggleAttribute("data-active", i === at);
      });
    };

    const clean = () => {
      let text = input.value.replace(/[\s-]/g, "");
      text = (letters ? text.toUpperCase().replace(/[^0-9A-Z]/g, "") : text.replace(/\D/g, "")).slice(0, boxes.length);
      if (text !== input.value) {
        input.value = text;
        input.setSelectionRange(text.length, text.length);
      }
      paint();
    };

    input.addEventListener("input", (event) => {
      if (!(event as InputEvent).isComposing) clean();
    }, { signal });
    input.addEventListener("compositionend", clean, { signal });
    // The caret goes after the last character, so the box with the ring is where typing goes.
    const toEnd = () => requestAnimationFrame(() => {
      if (document.activeElement === input) input.setSelectionRange(input.value.length, input.value.length);
      paint();
    });
    input.addEventListener("focus", toEnd, { signal });
    input.addEventListener("pointerup", toEnd, { signal });
    input.addEventListener("blur", paint, { signal });
    document.addEventListener("selectionchange", () => {
      if (document.activeElement === input) paint();
    }, { signal });
    input.form?.addEventListener("reset", () => setTimeout(paint, 0), { signal });
    clean();
  }
}

defineElement("ib-pin-field", PinField);
