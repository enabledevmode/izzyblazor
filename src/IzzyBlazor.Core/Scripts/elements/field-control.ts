import { IzzyElement, defineElement } from "../izzy-element";

/**
 * <ib-field-control> wraps a field's native input. It only adds conveniences that need
 * script; the field works without it:
 * - a show/hide toggle for passwords (rendered hidden, revealed here)
 * - a live character count for text areas
 * - increase and decrease buttons for number inputs (hold to repeat)
 * - a slider's track fill and shown value, kept current while dragging
 */
class FieldControl extends IzzyElement {
  protected setup(signal: AbortSignal): void {
    const input = this.querySelector<HTMLInputElement | HTMLTextAreaElement>("input, textarea");
    if (!input) return;

    const reveal = this.querySelector<HTMLButtonElement>("[data-ib-reveal]");
    if (reveal && input instanceof HTMLInputElement) {
      reveal.hidden = false;
      reveal.addEventListener(
        "click",
        () => {
          const show = input.type === "password";
          input.type = show ? "text" : "password";
          reveal.setAttribute("aria-pressed", String(show));
          reveal.setAttribute("aria-label", show ? "Hide password" : "Show password");
          input.focus();
        },
        { signal }
      );
    }

    if (input instanceof HTMLInputElement && input.type === "range") {
      setupSlider(this, input, signal);
      return;
    }

    const steppers = this.querySelector<HTMLElement>("[data-ib-steppers]");
    if (steppers && input instanceof HTMLInputElement && input.type === "number") {
      setupSteppers(input, steppers, signal);
    }

    // The count's text belongs to Blazor, so the live number goes in an attribute that CSS
    // shows instead. When Blazor re-renders the text, its number is current again.
    const count = this.querySelector<HTMLElement>("[data-ib-count]");
    if (count) {
      const update = () => (count.dataset.current = String(input.value.length));
      input.addEventListener("input", update, { signal });
      const observer = new MutationObserver(() => delete count.dataset.current);
      observer.observe(count, { childList: true, characterData: true, subtree: true });
      signal.addEventListener("abort", () => observer.disconnect());
    }
  }
}

/**
 * Keeps a slider's fill (--ib-slider-fill on the control) and its shown value current as it
 * moves. The value's text belongs to Blazor, so the live one goes in data-current, which CSS
 * shows; aria-valuetext gets the same formatted text.
 */
function setupSlider(control: HTMLElement, input: HTMLInputElement, signal: AbortSignal): void {
  const output = control.querySelector<HTMLElement>(".ib-slider-value");
  const format = input.dataset.ibFormat ?? "{0}";
  const update = () => {
    const min = input.min === "" ? 0 : Number(input.min);
    const max = input.max === "" ? 100 : Number(input.max);
    const percent = max > min ? ((input.valueAsNumber - min) / (max - min)) * 100 : 0;
    control.style.setProperty("--ib-slider-fill", String(Math.min(100, Math.max(0, percent))));
    const text = format.replace("{0}", input.value);
    input.setAttribute("aria-valuetext", text);
    if (output) output.dataset.current = text;
  };
  input.addEventListener("input", update, { signal });
  input.addEventListener("change", update, { signal });
  // The browser may hold a different value than the server rendered: one it snapped to the step,
  // or one it restored on reload.
  update();

  // When Blazor re-renders the value, the server's text and fill are current again.
  if (output) {
    const observer = new MutationObserver(() => delete output.dataset.current);
    observer.observe(output, { childList: true, characterData: true, subtree: true });
    signal.addEventListener("abort", () => observer.disconnect());
  }
}

/** Wires a number input's stepper buttons. Stepping fires input and change, so binding sees it. */
function setupSteppers(input: HTMLInputElement, steppers: HTMLElement, signal: AbortSignal): void {
  const up = steppers.querySelector<HTMLButtonElement>('[data-ib-step="up"]');
  const down = steppers.querySelector<HTMLButtonElement>('[data-ib-step="down"]');
  if (!up || !down) return;
  steppers.hidden = false;

  // Disabled and read-only come from the server (and can change on interactive pages); the min
  // and max ends are tracked here.
  const locked = () => input.disabled || input.readOnly;
  const sync = () => {
    if (locked()) {
      up.disabled = down.disabled = true;
      return;
    }
    const value = input.valueAsNumber;
    const min = input.min === "" ? NaN : Number(input.min);
    const max = input.max === "" ? NaN : Number(input.max);
    up.disabled = !Number.isNaN(value) && !Number.isNaN(max) && value >= max;
    down.disabled = !Number.isNaN(value) && !Number.isNaN(min) && value <= min;
  };

  const step = (direction: "up" | "down") => {
    const before = input.value;
    try {
      if (input.value === "") {
        // An empty field starts at min, or 0 within the range, rather than stepping from nothing.
        const min = input.min === "" ? -Infinity : Number(input.min);
        const max = input.max === "" ? Infinity : Number(input.max);
        input.value = String(Math.min(max, Math.max(min, 0)));
      } else if (direction === "up") {
        input.stepUp();
      } else {
        input.stepDown();
      }
    } catch {
      // step="any" has no step size: move by 1, keeping the value's decimals exact
      // (0.14 + 1 is 1.1400000000000001 in floating point).
      const current = Number.isNaN(input.valueAsNumber) ? 0 : input.valueAsNumber;
      const decimals = (input.value.split(".")[1] ?? "").replace(/e.*$/i, "").length;
      const next = Number((current + (direction === "up" ? 1 : -1)).toFixed(Math.min(decimals, 20)));
      const min = input.min === "" ? -Infinity : Number(input.min);
      const max = input.max === "" ? Infinity : Number(input.max);
      input.value = String(Math.min(max, Math.max(min, next)));
    }
    if (input.value !== before) input.dispatchEvent(new Event("input", { bubbles: true }));
    sync();
  };

  // Hold to repeat: one step on press, then steady steps after a pause.
  let delay = 0;
  let repeat = 0;
  let pending = false;
  const stop = () => {
    clearTimeout(delay);
    clearInterval(repeat);
    up.removeAttribute("data-pressed");
    down.removeAttribute("data-pressed");
    if (pending) {
      pending = false;
      input.dispatchEvent(new Event("change", { bubbles: true }));
    }
  };

  for (const button of [up, down]) {
    const direction = button === up ? "up" : "down";
    button.addEventListener(
      "pointerdown",
      (event) => {
        if (button.disabled || event.button !== 0) return;
        event.preventDefault(); // keep focus where it is
        if (document.activeElement !== input) input.focus({ preventScroll: true });
        stop();
        pending = true;
        button.setAttribute("data-pressed", "");
        step(direction);
        delay = window.setTimeout(() => {
          repeat = window.setInterval(() => (button.disabled ? stop() : step(direction)), 60);
        }, 400);
      },
      { signal }
    );
    for (const type of ["pointerup", "pointerleave", "pointercancel"]) {
      button.addEventListener(type, stop, { signal });
    }
    // Screen readers, voice control and switch access send a click with no pointer events.
    button.addEventListener(
      "click",
      (event) => {
        if (event.detail !== 0 || button.disabled) return;
        step(direction);
        input.dispatchEvent(new Event("change", { bubbles: true }));
      },
      { signal }
    );
  }
  signal.addEventListener("abort", stop);

  input.addEventListener("input", sync, { signal });
  input.addEventListener("change", sync, { signal });
  sync();
}

defineElement("ib-field-control", FieldControl);
