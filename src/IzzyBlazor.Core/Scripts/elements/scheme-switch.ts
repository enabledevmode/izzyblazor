import { IzzyElement, defineElement } from "../izzy-element";
import { readCookie, writeCookie } from "../cookies";

/**
 * <ib-scheme-switch> makes ColorSchemeSwitch instant. With no script each option is a link to the
 * endpoint that sets the ib-scheme cookie and comes back; with it, a click sets the cookie here,
 * puts the scheme on the page at once (data-theme on <html>, and IzzyTheme's meta if there is
 * one), moves the switch's mark, closes its menu, tells other tabs and frames, and raises
 * `ibscheme` so @bind-Value follows on interactive pages. The other way round, a Value set from
 * C# arrives as data-ib-value, and the switch applies it as if it had been clicked.
 */
type Scheme = "system" | "light" | "dark";

const COOKIE = "ib-scheme";
const channel = typeof BroadcastChannel === "function" ? new BroadcastChannel("ib-scheme") : null;

function parse(value: string | null | undefined): Scheme | null {
  return value === "system" || value === "light" || value === "dark" ? value : null;
}

// Segments mark the choice with aria-current; menu items with aria-checked (data-ib-checked before
// the menu's script runs). The toggle has nothing to mark: CSS shows the right button.
function mark(root: ParentNode, scheme: Scheme): void {
  for (const option of root.querySelectorAll<HTMLElement>("a[data-ib-scheme]")) {
    const on = option.dataset.ibScheme === scheme;
    if (option.classList.contains("ib-segment")) {
      if (on) option.setAttribute("aria-current", "true");
      else option.removeAttribute("aria-current");
      continue;
    }

    if (!option.classList.contains("ib-menu-item")) continue;
    if (option.hasAttribute("data-ib-checked")) option.setAttribute("data-ib-checked", String(on));
    if (option.hasAttribute("aria-checked")) option.setAttribute("aria-checked", String(on));
    else if (on) option.setAttribute("aria-current", "true");
    else option.removeAttribute("aria-current");
  }
}

/** Puts a scheme on the page and on every switch in it. */
function apply(scheme: Scheme): void {
  const root = document.documentElement;
  if (scheme === "system") root.removeAttribute("data-theme");
  else root.setAttribute("data-theme", scheme);

  // IzzyTheme's meta carries the server's idea of the scheme; keep it from disagreeing.
  const meta = document.head.querySelector<HTMLMetaElement>('meta[name="ib-scheme"]');
  if (meta && scheme === "system") meta.remove();
  else if (meta) meta.content = scheme;

  mark(document, scheme);
}

/**
 * Re-applies the user's saved choice, if they made one: on load (pages without IzzyTheme, or
 * served from a cache) and after enhanced navigation, which can put back the server's <html>.
 * With no saved choice the page keeps what the server sent (IzzyTheme's default, or the system).
 */
export function applySavedScheme(): void {
  const saved = parse(readCookie(COOKIE));
  if (saved) apply(saved);
}

/** Saves a choice and puts it on this page and the others. */
function choose(scheme: Scheme): void {
  writeCookie(COOKIE, scheme);
  apply(scheme);
  channel?.postMessage(scheme);
}

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
        const option = (event.target as Element | null)?.closest<HTMLAnchorElement>("a[data-ib-scheme]");
        if (!option || !this.contains(option)) return;
        if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
        const scheme = parse(option.dataset.ibScheme);
        if (!scheme) return;

        event.preventDefault();
        writeCookie(COOKIE, scheme);
        apply(scheme);
        // A menu closes as it would after following the link.
        option.closest<HTMLElement>("[popover]")?.hidePopover();
        // Keep keyboard focus on the switch: the toggle's clicked button is now hidden (focus
        // moves to the one shown in its place), and a closed menu hands it back to its trigger.
        if (document.activeElement === option && !option.checkVisibility()) {
          const visible = [...this.querySelectorAll<HTMLElement>("a[data-ib-scheme], button")].find(
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

// Another tab or a demo frame switched: follow it.
channel?.addEventListener("message", (event: MessageEvent) => {
  const scheme = parse(event.data);
  if (scheme) apply(scheme);
});

applySavedScheme();
