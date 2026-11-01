import { IzzyElement, defineElement } from "../izzy-element";

/**
 * <ib-time-slots> is a TimeField's list of times, in a Popover beside its input. Choosing one writes
 * that input, the one that posts and that @bind listens to, and closes the list. The server draws the
 * slots, with those that can't be chosen marked; this keeps the chosen one, the Tab stop and the
 * button's name in step with what the input holds.
 *
 * One Tab stop, the slot that holds it, with arrow keys between them; Tab leaves the list and closes it.
 */
class TimeSlots extends IzzyElement {
  protected setup(signal: AbortSignal): void {
    const panel = this.closest<HTMLElement>("[popover]");
    const toggle = panel?.previousElementSibling as HTMLButtonElement | null | undefined;
    const control = panel?.closest(".ib-field-control");
    const text = control?.querySelector<HTMLInputElement>("input");
    const slots = [...this.querySelectorAll<HTMLButtonElement>("[role=option]")];
    if (!panel || !toggle || !control || !text) return;

    let stop: HTMLButtonElement | undefined;
    const data = this.dataset;
    const put = (element: Element, name: string, value: string | null): void => {
      if (element.getAttribute(name) === value) return;
      if (value === null) element.removeAttribute(name);
      else element.setAttribute(name, value);
    };
    const held = (): HTMLButtonElement | undefined => slots.find((slot) => slot.dataset.value === text.value.slice(0, 5));
    // The chosen one, or the next from the time now, as the list may be long.
    const startingPoint = (): HTMLButtonElement | undefined => {
      const now = new Date();
      const clock = `${String(now.getHours()).padStart(2, "0")}:${String(now.getMinutes()).padStart(2, "0")}`;
      const free = slots.filter((slot) => !slot.ariaDisabled);
      return held() ?? free.find((slot) => slot.dataset.value! >= clock) ?? free[0] ?? slots[0];
    };

    const observer = new MutationObserver(() => draw());
    observer.observe(this, { attributes: true, subtree: true, attributeFilter: ["data-ib-value", "data-ready", "tabindex", "aria-selected", "autofocus"] });
    signal.addEventListener("abort", () => observer.disconnect());
    const draw = (): void => {
      try {
        const chosen = held();
        stop = slots.includes(stop!) ? stop : startingPoint();
        for (const slot of slots) {
          put(slot, "aria-selected", String(slot === chosen));
          put(slot, "tabindex", slot === stop ? "0" : "-1");
          // Where the popover puts focus as it opens.
          put(slot, "autofocus", slot === stop ? "" : null);
        }
        put(toggle, "aria-label", text.value && data.ibChange ? data.ibChange.replace("{0}", chosen?.textContent?.trim() ?? text.value) : data.ibChoose ?? null);
        // A phone's own picker would open on a tap, and could choose what this field turns off.
        if (control.classList.contains("ib-date-own") && matchMedia("(pointer: coarse)").matches && !text.readOnly) text.readOnly = true;
        if (data.ready === undefined) data.ready = "";
      } finally {
        observer.takeRecords();
      }
    };

    let opener: HTMLElement = toggle;
    const close = (to: HTMLElement = opener): void => {
      to.focus();
      panel.hidePopover();
    };

    panel.addEventListener(
      "beforetoggle",
      (event) => {
        if ((event as ToggleEvent).newState !== "open") return;
        // Back to the input if the keys opened it from there.
        opener = document.activeElement === text ? text : toggle;
        stop = startingPoint();
        draw();
      },
      { signal }
    );
    this.addEventListener(
      "click",
      (event) => {
        const slot = (event.target as Element).closest<HTMLButtonElement>("[role=option]");
        if (!slot || slot.ariaDisabled) return;
        text.value = slot.dataset.value!;
        text.dispatchEvent(new Event("input", { bubbles: true }));
        text.dispatchEvent(new Event("change", { bubbles: true }));
        draw();
        close();
      },
      { signal }
    );
    this.addEventListener(
      "keydown",
      (event) => {
        const at = slots.indexOf(event.target as HTMLButtonElement);
        const moves: Record<string, number> = { ArrowUp: at - 1, ArrowDown: at + 1, Home: 0, End: slots.length - 1 };
        if (event.key === "Tab") {
          if (event.shiftKey) event.preventDefault();
          close(toggle);
        } else if (event.key in moves && at >= 0) {
          event.preventDefault();
          stop = slots[Math.min(Math.max(moves[event.key], 0), slots.length - 1)];
          draw();
          stop.focus();
        }
      },
      { signal }
    );
    text.addEventListener("change", draw, { signal });
    // In place of the browser's own picker, once this one's button is showing.
    text.addEventListener(
      "keydown",
      (event) => {
        if (event.altKey && event.key === "ArrowDown" && toggle.checkVisibility()) {
          event.preventDefault();
          toggle.click();
        }
      },
      { signal }
    );
    draw();
  }
}

defineElement("ib-time-slots", TimeSlots);
