import { IzzyElement, defineElement } from "../izzy-element";
import { load } from "../lazy";

/**
 * <ib-field-control> wraps a field's native input. It only adds conveniences that need
 * script; the field works without it:
 * - a show/hide toggle for passwords (rendered hidden, revealed here)
 * - a live character count for text areas
 * - increase and decrease buttons for number inputs (hold to repeat)
 * - a slider's track fill and value tooltip, kept current while dragging
 * - a range slider's thumbs kept apart, its fill and tooltips, and clicks on its track
 * - a text field's Mask, loaded with the first field that has one
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
          // A toggle button keeps its name; aria-pressed says whether the password is shown.
          reveal.setAttribute("aria-pressed", String(show));
          input.focus();
        },
        { signal }
      );
    }

    // A password's Caps Lock warning and requirements load only where there's a password.
    if (input instanceof HTMLInputElement && (input.type === "password" || reveal)) {
      void load<typeof import("./password")>("ib-password").then((module) => {
        if (!signal.aborted) module.setupPassword(this, input, signal);
      }, () => {});
    }

    // A mask loads only where there's one.
    if (input instanceof HTMLInputElement && input.dataset.ibMask) {
      void load<typeof import("./mask")>("ib-mask").then((module) => {
        if (!signal.aborted) module.setupMask(input, signal);
      }, () => {});
    }

    const end = this.querySelector<HTMLInputElement>("input[type=range][data-ib-end]");
    if (input instanceof HTMLInputElement && input.type === "range" && end) {
      setupRangeSlider(this, input, end, signal);
      return;
    }

    if (input instanceof HTMLInputElement && input.type === "range") {
      setupSlider(this, input, signal);
      return;
    }

    const steppers = this.querySelector<HTMLElement>("[data-ib-steppers]");
    if (steppers && input instanceof HTMLInputElement && input.type === "number") {
      setupSteppers(input, steppers, signal);
    }

    // With script the count is live, so it becomes the field's description (with JavaScript off it
    // would read a stale number). The number's text node belongs to Blazor: change its data, never
    // replace it, so Blazor's next render of the count finds its own node (and writes the same
    // number). Blazor also writes aria-describedby (the hint and errors), so put the count back
    // whenever it does.
    const count = this.querySelector<HTMLElement>(".ib-field-count[id]");
    const current = count?.querySelector<HTMLElement>(".ib-field-count-current");
    if (count && current) {
      const update = () => {
        const text = String(input.value.length);
        const node = current.firstChild;
        if (node instanceof Text && node.data !== text) node.data = text;
      };
      const link = () => {
        const ids = (input.getAttribute("aria-describedby") ?? "").split(/\s+/).filter(Boolean);
        if (!ids.includes(count.id)) input.setAttribute("aria-describedby", [...ids, count.id].join(" "));
      };
      input.addEventListener("input", update, { signal });
      // A reset changes the value after its event, with no input event.
      input.form?.addEventListener("reset", () => setTimeout(update), { signal });
      const observer = new MutationObserver(link);
      observer.observe(input, { attributes: true, attributeFilter: ["aria-describedby"] });
      signal.addEventListener("abort", () => observer.disconnect());
      link();
      // The browser may restore typed text on reload, which the server's count didn't see.
      update();
    }
  }
}

/**
 * Keeps a slider's fill (--ib-slider-fill on the control) and its value tooltip current as it
 * moves. The tooltip's text belongs to Blazor, so the live one goes in data-current, which CSS
 * shows; aria-valuetext gets the same formatted text.
 */
function setupSlider(control: HTMLElement, input: HTMLInputElement, signal: AbortSignal): void {
  const tip = control.querySelector<HTMLElement>(".ib-slider-tip");
  const format = input.dataset.ibFormat ?? "{0}";
  const update = () => {
    const min = input.min === "" ? 0 : Number(input.min);
    const max = input.max === "" ? 100 : Number(input.max);
    const percent = max > min ? ((input.valueAsNumber - min) / (max - min)) * 100 : 0;
    control.style.setProperty("--ib-slider-fill", String(Math.min(100, Math.max(0, percent))));
    const text = format.replace("{0}", input.value);
    input.setAttribute("aria-valuetext", text);
    if (tip) tip.dataset.current = text;
  };
  input.addEventListener("input", update, { signal });
  input.addEventListener("change", update, { signal });
  // The browser may hold a different value than the server rendered: one it snapped to the step,
  // or one it restored on reload.
  update();

  // When Blazor re-renders the value, the server's text and fill are current again.
  if (tip) followBlazor([tip], signal);
}

/** Drops a tooltip's live text when Blazor writes its own, which is current again. */
function followBlazor(tips: HTMLElement[], signal: AbortSignal): void {
  for (const tip of tips) {
    const observer = new MutationObserver(() => delete tip.dataset.current);
    observer.observe(tip, { childList: true, characterData: true, subtree: true });
    signal.addEventListener("abort", () => observer.disconnect());
  }
}

/**
 * A range slider: two range inputs on one track. Each thumb stops at the other, the track fills
 * between them (--ib-slider-from and --ib-slider-fill), the tooltips and aria-valuetext follow,
 * and a press on the track moves the nearer thumb there.
 */
function setupRangeSlider(control: HTMLElement, start: HTMLInputElement, end: HTMLInputElement, signal: AbortSignal): void {
  const startTip = control.querySelector<HTMLElement>(".ib-slider-tip:not([data-ib-end])");
  const endTip = control.querySelector<HTMLElement>(".ib-slider-tip[data-ib-end]");
  const track = start.parentElement!;
  const format = (input: HTMLInputElement) => (input.dataset.ibFormat ?? "{0}").replace("{0}", input.value);
  const scale = () => {
    const min = start.min === "" ? 0 : Number(start.min);
    const max = start.max === "" ? 100 : Number(start.max);
    return { min, max };
  };
  const percent = (input: HTMLInputElement) => {
    const { min, max } = scale();
    return max > min ? Math.min(100, Math.max(0, ((input.valueAsNumber - min) / (max - min)) * 100)) : 0;
  };
  const update = () => {
    control.style.setProperty("--ib-slider-from", String(percent(start)));
    control.style.setProperty("--ib-slider-fill", String(percent(end)));
    start.setAttribute("aria-valuetext", format(start));
    end.setAttribute("aria-valuetext", format(end));
    if (startTip) startTip.dataset.current = format(start);
    if (endTip) endTip.dataset.current = format(end);
    // Where the thumbs meet, the one with room to move is on top: the lower thumb near the top of
    // the scale, the upper one elsewhere.
    const { min, max } = scale();
    start.toggleAttribute("data-ib-top", start.valueAsNumber > (min + max) / 2);
  };

  // Runs before Blazor's handlers (they listen at the document), so they read the stopped value.
  const keepApart = (moved: HTMLInputElement) => () => {
    if (start.valueAsNumber > end.valueAsNumber) {
      moved.value = moved === start ? end.value : start.value;
    }
    update();
  };
  for (const input of [start, end]) {
    input.addEventListener("input", keepApart(input), { signal });
    input.addEventListener("change", keepApart(input), { signal });
  }

  // A press on the track (the inputs let it through, all but their thumbs) moves the nearer thumb.
  track.addEventListener(
    "pointerdown",
    (event) => {
      if (event.target !== track || start.disabled || event.button !== 0) return;
      const box = track.getBoundingClientRect();
      const vertical = control.classList.contains("ib-slider-vertical");
      const rtl = getComputedStyle(track).direction === "rtl";
      const thumb = vertical ? box.width : box.height;
      const length = (vertical ? box.height : box.width) - thumb;
      let along = vertical ? box.bottom - event.clientY : rtl ? box.right - event.clientX : event.clientX - box.left;
      along = Math.min(length, Math.max(0, along - thumb / 2));
      const { min, max } = scale();
      const wanted = min + (length > 0 ? along / length : 0) * (max - min);
      const [low, high] = [start.valueAsNumber, end.valueAsNumber];
      const nearer = wanted <= low ? start : wanted >= high ? end : wanted - low <= high - wanted ? start : end;
      const before = nearer.value;
      nearer.value = String(wanted); // the browser snaps it to the step and the scale
      nearer.focus();
      event.preventDefault();
      if (nearer.value !== before) {
        nearer.dispatchEvent(new Event("input", { bubbles: true }));
        nearer.dispatchEvent(new Event("change", { bubbles: true }));
      }
    },
    { signal }
  );

  // The browser may hold different values than the server rendered: snapped, or restored on reload.
  update();

  // When Blazor re-renders the values, the server's text and fill are current again.
  followBlazor([startTip, endTip].filter((tip): tip is HTMLElement => tip !== null), signal);
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
