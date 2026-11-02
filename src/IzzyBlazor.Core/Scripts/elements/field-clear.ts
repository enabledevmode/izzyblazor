import { IzzyElement, defineElement } from "../izzy-element";

/**
 * <ib-field-clear> is a field's clear button, there while it holds something. Clearing writes the
 * control that holds the value and fires its events, so posting, @bind and validation see what they'd
 * see from a person emptying it: a select where there's one (a Combobox's and MultiSelect's are under
 * their text), a DurationField's text input rather than its boxes. With no script it stays hidden.
 */
class FieldClear extends IzzyElement {
  protected setup(signal: AbortSignal): void {
    const control = this.closest(".ib-field-control, .ib-duration");
    if (!control) return;
    // Not a panel's own controls, nor the hidden input that takes both ends of a range at once.
    const parts = [...control.querySelectorAll<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>("input:not([type=hidden]), textarea, select")].filter((part) => !part.closest("[popover]"));
    const both = control.querySelector<HTMLInputElement>("[data-ib-range-value]");
    const select = parts.find((part): part is HTMLSelectElement => part instanceof HTMLSelectElement);
    const text = control.querySelector<HTMLInputElement>(".ib-duration-text input");
    const holders = select ? [select] : text ? [text] : parts;

    const observer = new MutationObserver(() => draw());
    observer.observe(this, { attributes: true, attributeFilter: ["data-ib-has", "data-ready", "hidden"] });
    signal.addEventListener("abort", () => observer.disconnect());
    const draw = (): void => {
      // A date part way through has no value yet, but it's something to clear.
      const empty = select ? ![...select.selectedOptions].some((option) => option.value) : holders.every((part) => !part.value && !part.validity.badInput);
      if (this.hidden !== empty) this.hidden = empty;
      if (this.dataset.ready === undefined) this.dataset.ready = "";
      observer.takeRecords();
    };

    // After the field's own listeners, as a DurationField writes its text from its boxes.
    const redraw = (event: Event): void => {
      if (parts.includes(event.target as HTMLInputElement)) setTimeout(draw);
    };
    control.addEventListener("input", redraw, { signal });
    control.addEventListener("change", redraw, { signal });
    this.addEventListener(
      "click",
      () => {
        for (const part of parts) {
          if (part instanceof HTMLSelectElement && part.multiple) for (const option of part.options) option.selected = false;
          else part.value = "";
        }
        if (both) both.value = "";
        control.querySelector(".ib-duration-sign")?.setAttribute("aria-pressed", "false");
        for (const part of both ? [both] : holders) {
          part.dispatchEvent(new Event("input", { bubbles: true }));
          part.dispatchEvent(new Event("change", { bubbles: true }));
        }
        draw();
        // The button's gone, so focus goes where the next thing would be typed.
        parts[0]?.focus();
      },
      { signal }
    );
    draw();
  }
}

defineElement("ib-field-clear", FieldClear);
