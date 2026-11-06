import { IzzyElement, defineElement } from "../izzy-element";

/**
 * <ib-color-picker> makes a ColorPicker's hex box work, with one of two pickers:
 *   - The panel (the default): the swatch opens a ColorPanel in a popover. The panel's hidden
 *     field holds the value and posts (the native input, the no-script picker, stops posting);
 *     each change shows on the swatch and in the hex box, and a hex typed there goes to the panel.
 *   - NativePicker: the native color input holds the value (it posts and Blazor binds it); the
 *     hex box and the swatches under the field write into it and raise its input and change
 *     events, so binding sees them as a pick.
 * CSS shows the script's parts from the first paint, so the field doesn't change when this
 * arrives; they work once it has.
 */
const HEX = /^#?([0-9a-f]{3}|[0-9a-f]{6})$/i;

/** "#ABC", "abc" and "#aabbcc" all become "#aabbcc"; anything else is null. */
function normalize(value: string): string | null {
  const match = HEX.exec(value.trim());
  if (!match) return null;
  const digits = (match[1] ?? "").toLowerCase();
  return "#" + (digits.length === 3 ? [...digits].map((d) => d + d).join("") : digits);
}

type Panel = HTMLElement & { select?(value: string): void };

class ColorPicker extends IzzyElement {
  protected setup(signal: AbortSignal): void {
    const color = this.querySelector<HTMLInputElement>('input[type="color"]');
    if (!color) return;
    const hex = this.querySelector<HTMLInputElement>(".ib-color-hex");
    const panel = this.querySelector<Panel>("ib-color-panel");
    if (panel) this.withPanel(panel, color, hex, signal);
    else this.native(color, hex, signal);
  }

  private withPanel(panel: Panel, color: HTMLInputElement, hex: HTMLInputElement | null, signal: AbortSignal): void {
    // The panel's field posts now (it has the name), so the native input mustn't as well.
    if (panel.dataset.ibName) color.removeAttribute("name");
    const value = panel.querySelector<HTMLInputElement>(":scope > .ib-cp-value");
    const trigger = this.querySelector<HTMLElement>(".ib-color-trigger");
    if (!value) return;

    const show = () => {
      const match = /^#([0-9a-f]{6})([0-9a-f]{2})?$/i.exec(value.value);
      trigger?.style.setProperty("--ib-color-current", match ? `#${match[1]}` : "transparent");
      trigger?.style.setProperty("--ib-color-alpha", match?.[2] ? String(parseInt(match[2], 16) / 255) : "1");
      if (hex && document.activeElement !== hex) hex.value = value.value;
    };
    for (const type of ["input", "change"]) {
      panel.addEventListener(type, (event) => {
        if (event.target === value) show();
      }, { signal });
    }

    // A whole hex value typed goes to the panel as it's typed (six or eight digits; three on
    // leaving, since it may be the start of six).
    if (hex) {
      const send = (short: boolean) => {
        const digits = hex.value.trim().replace(/^#/, "");
        if (/^([0-9a-f]{6}|[0-9a-f]{8})$/i.test(digits) || (short && /^[0-9a-f]{3}$/i.test(digits))) panel.select?.(`#${digits}`);
      };
      hex.addEventListener("input", () => send(false), { signal });
      hex.addEventListener("change", () => send(true), { signal });
      // Leaving shows the value in full, or puts back the last good one.
      hex.addEventListener("blur", () => (hex.value = value.value), { signal });
    }
  }

  private native(color: HTMLInputElement, hex: HTMLInputElement | null, signal: AbortSignal): void {
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
