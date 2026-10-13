import { IzzyElement, defineElement } from "../izzy-element";
import { readCookie, writeCookie } from "../cookies";

/**
 * <ib-banner> adds two things to Banner, both optional:
 *
 * - Remembering a dismissal. With no script the close button still works (a checkbox and CSS);
 *   this writes the cookie that keeps a banner with a RememberKey closed on later pages. The server
 *   reads it and leaves the banner out. Where it can't (WebAssembly, a cached page), the element
 *   finds the cookie when it connects and closes the banner itself.
 * - Measuring a sticky banner in an AppShell. CSS keeps one line of room for it above the sticky
 *   header and sidebar; when the message wraps, this sets the real height (--ib-banner-h).
 */
class Banner extends IzzyElement {
  #shell: HTMLElement | null = null;

  protected setup(signal: AbortSignal): void {
    this.#remember(signal);
    this.#measure(signal);
  }

  protected teardown(): void {
    this.#shell?.style.removeProperty("--ib-banner-h");
    this.#shell = null;
  }

  #remember(signal: AbortSignal): void {
    const input = this.querySelector<HTMLInputElement>(":scope > .ib-banner-dismiss[data-ib-remember]");
    if (!input) return;
    const name = input.dataset.ibRemember!;

    if (!input.checked && readCookie(name) === "1") {
      input.checked = true;
      // Lets @bind-Dismissed on an interactive page hear about it.
      input.dispatchEvent(new Event("change", { bubbles: true }));
    }

    input.addEventListener(
      "change",
      () => {
        if (input.checked) writeCookie(name, "1");
      },
      { signal }
    );
  }

  #measure(signal: AbortSignal): void {
    const shell = this.parentElement;
    if (!this.classList.contains("ib-banner-sticky") || !shell?.matches("ib-shell")) return;
    this.#shell = shell;
    const observer = new ResizeObserver(([entry]) => {
      // Dismissed with no script: display:none measures 0, which is also the room it needs.
      const height = Math.round(entry.borderBoxSize?.[0]?.blockSize ?? this.offsetHeight);
      shell.style.setProperty("--ib-banner-h", `${height}px`);
    });
    observer.observe(this);
    signal.addEventListener("abort", () => observer.disconnect());
  }
}

defineElement("ib-banner", Banner);
