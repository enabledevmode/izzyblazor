import { IzzyElement, defineElement } from "../izzy-element";

/**
 * <ib-show-more> shows its button when the content is taller than the lines CSS cuts it to, and
 * the button opens and closes it (data-open, aria-expanded). It measures again when the content's
 * box changes: a narrower column wraps to more lines. Without script CSS doesn't cut anything.
 */
class ShowMore extends IzzyElement {
  #observer: ResizeObserver | null = null;

  protected setup(signal: AbortSignal): void {
    const content = this.querySelector<HTMLElement>(":scope > .ib-show-more-content");
    const button = this.querySelector<HTMLElement>(":scope > .ib-show-more-button");
    if (!content || !button) return;
    const measure = (): void => {
      if (this.hasAttribute("data-open")) return;
      const over = content.scrollHeight > content.clientHeight + 1;
      this.toggleAttribute("data-ib-over", over);
      button.hidden = !over;
    };
    button.addEventListener(
      "click",
      () => {
        const open = this.toggleAttribute("data-open");
        button.setAttribute("aria-expanded", String(open));
        measure();
      },
      { signal }
    );
    this.#observer = new ResizeObserver(measure);
    this.#observer.observe(content);
    measure();
  }

  protected teardown(): void {
    this.#observer?.disconnect();
    this.#observer = null;
  }
}

defineElement("ib-show-more", ShowMore);
