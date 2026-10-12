import { IzzyElement, defineElement } from "../izzy-element";

/**
 * <ib-field-control> wraps a field's native input. It only adds conveniences that need
 * script; the field works without it:
 * - a show/hide toggle for passwords (rendered hidden, revealed here)
 * - a live character count for text areas
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

defineElement("ib-field-control", FieldControl);
