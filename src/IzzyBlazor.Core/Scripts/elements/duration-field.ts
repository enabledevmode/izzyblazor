import { IzzyElement, defineElement } from "../izzy-element";

/**
 * <ib-duration-field> writes a DurationField's boxes into its text input, the one that posts and
 * that @bind listens to. CSS shows the boxes in place of that input, which stays focusable so its
 * label and error links still lead here, and passes focus on to the first box. It's read-only under
 * script, so the browser doesn't check a value it can't show; the first box takes `required`.
 */
const pad = (n: number): string => String(n).padStart(2, "0");

class DurationField extends IzzyElement {
  protected setup(signal: AbortSignal): void {
    const text = this.querySelector<HTMLInputElement>(".ib-duration-text input");
    const boxes = [...this.querySelectorAll<HTMLInputElement>("[data-ib-seconds]")];
    const sign = this.querySelector("select");
    const first = boxes[0];
    if (!text || !first) return;
    const size = (box: HTMLInputElement): number => Number(box.dataset.ibSeconds);

    // Enhanced navigation and a parent's ReadOnly reset these: guarded, as setting one again would observe itself.
    const claim = (): void => {
      if (!text.readOnly) text.readOnly = true;
      if (text.tabIndex !== -1) text.tabIndex = -1;
      if (text.getAttribute("aria-hidden") !== "true") text.setAttribute("aria-hidden", "true");
      if (text.dataset.ibRequired) first.dataset.ibRequired = text.dataset.ibRequired;
      first.required = text.required && boxes.every((box) => !box.value);
    };
    claim();
    const observer = new MutationObserver(claim);
    observer.observe(text, { attributes: true, attributeFilter: ["readonly", "tabindex", "aria-hidden"] });
    signal.addEventListener("abort", () => observer.disconnect());

    const write = (edited: EventTarget | null, settle: boolean): void => {
      // Clearing a box with nothing in the others empties the field. Not "all blank": on an
      // interactive page C# draws a 0 in a box whose part changed, even one that was just cleared.
      const cleared = edited !== sign && !(edited as HTMLInputElement).value && boxes.every((box) => !Number(box.value));
      first.required = text.required && cleared;
      if (cleared) {
        text.value = "";
        if (settle) for (const box of boxes) box.value = "";
        return;
      }
      const smallest = size(boxes[boxes.length - 1]);
      let left = Math.round(boxes.reduce((sum, box) => sum + Number(box.value) * size(box), 0));
      left -= left % smallest;
      const [days, hours] = [Math.floor(left / 86400), Math.floor(left / 3600) % 24];
      text.value = `${sign?.value === "-" && left > 0 ? "-" : ""}${days ? `${days}.` : ""}${pad(hours)}:${pad(Math.floor(left / 60) % 60)}:${pad(left % 60)}`;
      if (!settle) return;
      // What C# would draw, which leaves a box alone if its part didn't change: 60 minutes on 0 stays
      // 60. A blank box stays blank unless a value carries into it.
      for (const box of boxes) {
        const part = Math.floor(left / size(box));
        if (box.value || part) box.value = String(part);
        left %= size(box);
      }
    };

    text.addEventListener("focus", () => first.focus(), { signal });
    this.addEventListener("input", (event) => { if (event.target !== text) write(event.target, false); }, { signal });
    this.addEventListener(
      "change",
      (event) => {
        if (event.target === text) return;
        write(event.target, true);
        text.dispatchEvent(new Event("change", { bubbles: true }));
      },
      { signal }
    );
  }
}

defineElement("ib-duration-field", DurationField);
