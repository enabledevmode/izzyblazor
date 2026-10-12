import { IzzyElement, defineElement } from "../izzy-element";

/**
 * <ib-field-control> wraps a field's native input. It only adds conveniences that need
 * script; the field works without it:
 * - a show/hide toggle for passwords (rendered hidden, revealed here)
 * - a live character count for text areas
 * - increase and decrease buttons for number inputs (hold to repeat)
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

/** Wires a number input's stepper buttons. Stepping fires input and change, so binding sees it. */
function setupSteppers(input: HTMLInputElement, steppers: HTMLElement, signal: AbortSignal): void {
  const up = steppers.querySelector<HTMLButtonElement>('[data-ib-step="up"]');
  const down = steppers.querySelector<HTMLButtonElement>('[data-ib-step="down"]');
  if (!up || !down) return;
  steppers.hidden = false;

  // Disabled and read-only come from the server; the min and max ends are tracked here.
  const locked = up.disabled;
  const sync = () => {
    if (locked) return;
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
      // step="any" has no step size: move by 1.
      const next = (Number.isNaN(input.valueAsNumber) ? 0 : input.valueAsNumber) + (direction === "up" ? 1 : -1);
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
  }
  signal.addEventListener("abort", stop);

  input.addEventListener("input", sync, { signal });
  input.addEventListener("change", sync, { signal });
  sync();
}

defineElement("ib-field-control", FieldControl);
