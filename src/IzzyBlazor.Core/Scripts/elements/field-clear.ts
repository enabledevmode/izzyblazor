import { IzzyElement, defineElement } from "../izzy-element";

/**
 * <ib-field-clear> is a field's clear button, there while its input holds something. Clearing writes
 * the input and fires its events, so posting, @bind and validation see what they'd see from a person
 * emptying it. With no script it stays hidden, and the input can be emptied by hand.
 */
class FieldClear extends IzzyElement {
  protected setup(signal: AbortSignal): void {
    const control = this.closest(".ib-field-control");
    if (!control) return;
    // Not a panel's own inputs, nor the hidden one that takes both ends of a range at once.
    const inputs = [...control.querySelectorAll<HTMLInputElement>("input:not([type=hidden])")].filter((input) => !input.closest("[popover]"));
    const both = control.querySelector<HTMLInputElement>("[data-ib-range-value]");

    const observer = new MutationObserver(() => draw());
    observer.observe(this, { attributes: true, attributeFilter: ["data-ib-has", "data-ready", "hidden"] });
    signal.addEventListener("abort", () => observer.disconnect());
    const draw = (): void => {
      // A date part way through has no value yet, but it's something to clear.
      const empty = inputs.every((input) => !input.value && !input.validity.badInput);
      if (this.hidden !== empty) this.hidden = empty;
      if (this.dataset.ready === undefined) this.dataset.ready = "";
      observer.takeRecords();
    };

    const redraw = (event: Event): void => {
      if (inputs.includes(event.target as HTMLInputElement)) draw();
    };
    control.addEventListener("input", redraw, { signal });
    control.addEventListener("change", redraw, { signal });
    this.addEventListener(
      "click",
      () => {
        for (const input of inputs) input.value = "";
        if (both) both.value = "";
        for (const input of both ? [both] : inputs) {
          input.dispatchEvent(new Event("input", { bubbles: true }));
          input.dispatchEvent(new Event("change", { bubbles: true }));
        }
        draw();
        // The button's gone, so focus goes where the next thing would be typed.
        inputs[0]?.focus();
      },
      { signal }
    );
    draw();
  }
}

defineElement("ib-field-clear", FieldClear);
