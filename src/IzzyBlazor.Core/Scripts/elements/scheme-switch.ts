import { IzzyElement, defineElement } from "../izzy-element";
import { readCookie, writeCookie } from "../cookies";
import { COOKIE, apply, channel, choose, mark, parse } from "../scheme";

/**
 * <ib-scheme-switch> runs ColorSchemeSwitch, which CSS hides with no script. A click sets the
 * ib-scheme cookie, puts the scheme on the page at once (data-ib-color-scheme on <html>, and IzzyTheme's meta if there is
 * one), moves the switch's mark, closes its menu, tells other tabs and frames, and raises
 * `ibscheme` so @bind-Value follows on interactive pages. The other way round, a Value set from
 * C# arrives as data-ib-value, and the switch applies it as if it had been clicked.
 */
class SchemeSwitch extends IzzyElement {
  #observer: MutationObserver | null = null;

  protected setup(signal: AbortSignal): void {
    const saved = parse(readCookie(COOKIE));
    if (saved) mark(this, saved);

    // A bound Value changed from C# (interactive pages render it as data-ib-value). The first
    // value is what the page started with, so only changes apply; one that already matches the
    // saved choice (a click coming back through the binding) does nothing.
    this.#observer?.disconnect();
    this.#observer = new MutationObserver(() => {
      const scheme = parse(this.dataset.ibValue);
      if (scheme && scheme !== (parse(readCookie(COOKIE)) ?? "system")) choose(scheme);
    });
    this.#observer.observe(this, { attributes: true, attributeFilter: ["data-ib-value"] });
    signal.addEventListener("abort", () => this.#observer?.disconnect(), { once: true });

    this.addEventListener(
      "click",
      (event) => {
        const option = (event.target as Element | null)?.closest<HTMLElement>("[data-ib-scheme]");
        if (!option || !this.contains(option)) return;
        const scheme = parse(option.dataset.ibScheme);
        if (!scheme) return;

        writeCookie(COOKIE, scheme);
        apply(scheme);
        // A menu closes once it's chosen.
        option.closest<HTMLElement>("[popover]")?.hidePopover();
        // Keep keyboard focus on the switch: the toggle's clicked button is now hidden (focus
        // moves to the one shown in its place), and a closed menu hands it back to its trigger.
        if (document.activeElement === option && !option.checkVisibility()) {
          const visible = [...this.querySelectorAll<HTMLElement>("button")].find(
            (el) => el !== option && el.checkVisibility()
          );
          visible?.focus();
        }
        channel?.postMessage(scheme);
        this.emit("ibscheme", { scheme });
      },
      { signal }
    );
  }
}

defineElement("ib-scheme-switch", SchemeSwitch);
