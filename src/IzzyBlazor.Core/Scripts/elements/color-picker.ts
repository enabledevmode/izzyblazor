import { IzzyElement, defineElement } from "../izzy-element";

/**
 * <ib-color-picker> adds the hex box and preset swatches to a ColorPicker. The native color input
 * holds the value (it posts with the form and Blazor binds it); the hex box and the presets write
 * into it and raise its input and change events, so binding sees them as a pick. CSS shows the
 * extras only once this element is defined.
 */
const HEX = /^#?([0-9a-f]{3}|[0-9a-f]{6})$/i;

/** "#ABC", "abc" and "#aabbcc" all become "#aabbcc"; anything else is null. */
function normalize(value: string): string | null {
  const match = HEX.exec(value.trim());
  if (!match) return null;
  const digits = (match[1] ?? "").toLowerCase();
  return "#" + (digits.length === 3 ? [...digits].map((d) => d + d).join("") : digits);
}

class ColorPicker extends IzzyElement {
  protected setup(signal: AbortSignal): void {
    const color = this.querySelector<HTMLInputElement>('input[type="color"]');
    if (!color) return;
    const hex = this.querySelector<HTMLInputElement>(".ib-color-hex");

    // The presets' datalist is for pages with no script, where Chrome and Edge offer it in a menu
    // before their picker. Here the swatches under the field offer them, so the swatch opens the
    // picker directly.
    color.removeAttribute("list");

    const mark = (value: string) => {
      for (const swatch of this.querySelectorAll<HTMLElement>("[data-ib-color]")) {
        swatch.setAttribute("aria-pressed", String(swatch.dataset.ibColor === value));
      }
    };

    // Writes a value into the native input as if it had been picked there.
    const commit = (value: string) => {
      if (color.value === value) return;
      color.value = value;
      color.dispatchEvent(new Event("input", { bubbles: true }));
      color.dispatchEvent(new Event("change", { bubbles: true }));
    };

    this.addEventListener(
      "input",
      (event) => {
        if (event.target === color) {
          if (hex && document.activeElement !== hex) hex.value = color.value;
          mark(color.value);
        } else if (event.target === hex && hex) {
          const value = normalize(hex.value);
          if (value) commit(value);
        }
      },
      { signal }
    );

    // Leaving the hex box shows the value in full ("#abc" becomes "#aabbcc"), or puts back the
    // last good one if what was typed isn't a color.
    hex?.addEventListener("blur", () => (hex.value = color.value), { signal });

    this.addEventListener(
      "click",
      (event) => {
        const swatch = (event.target as Element).closest<HTMLElement>("[data-ib-color]");
        if (!swatch || !this.contains(swatch) || !swatch.dataset.ibColor) return;
        commit(swatch.dataset.ibColor);
        if (hex) hex.value = color.value;
        mark(color.value);
      },
      { signal }
    );

    mark(color.value);
  }
}

defineElement("ib-color-picker", ColorPicker);
