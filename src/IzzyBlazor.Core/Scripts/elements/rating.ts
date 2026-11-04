import { IzzyElement, defineElement } from "../izzy-element";

/**
 * <ib-rating> lets a click on the chosen star clear a Rating that can be empty: it checks the
 * "No rating" radio and fires its events, so posting, @bind and validation see what a person
 * choosing it with the arrow keys would. Without script the arrow keys still reach that radio.
 */
class Rating extends IzzyElement {
  protected setup(signal: AbortSignal): void {
    // The click on an already checked radio changes nothing, so note which one was checked first.
    let wasChecked: HTMLInputElement | null = null;
    this.addEventListener(
      "pointerdown",
      (event) => {
        const input = (event.target as Element).closest(".ib-rating-star")?.querySelector<HTMLInputElement>("input");
        wasChecked = input?.checked ? input : null;
      },
      { signal }
    );
    this.addEventListener(
      "click",
      (event) => {
        const none = this.querySelector<HTMLInputElement>(".ib-rating-none");
        const input = event.target as HTMLInputElement;
        if (!none || !wasChecked || input !== wasChecked || none.disabled) return;
        wasChecked = null;
        none.checked = true;
        none.dispatchEvent(new Event("input", { bubbles: true }));
        none.dispatchEvent(new Event("change", { bubbles: true }));
      },
      { signal }
    );
  }
}

defineElement("ib-rating", Rating);
