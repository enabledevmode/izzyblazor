import { IzzyElement, defineElement } from "../izzy-element";
import { readCookie, writeCookie } from "../cookies";

/**
 * <ib-scheme-switch> makes ColorSchemeSwitch instant. With no script each option is a link to the
 * endpoint that sets the ib-scheme cookie and comes back; with it, a click sets the cookie here,
 * puts the scheme on the page at once (data-theme on <html>, and IzzyTheme's meta if there is
 * one), moves the switch's aria-current, tells other tabs and frames, and raises `ibscheme` so
 * @bind-Value follows on interactive pages.
 */
type Scheme = "system" | "light" | "dark";

const COOKIE = "ib-scheme";
const channel = typeof BroadcastChannel === "function" ? new BroadcastChannel("ib-scheme") : null;

function parse(value: string | null | undefined): Scheme | null {
  return value === "system" || value === "light" || value === "dark" ? value : null;
}

function mark(root: ParentNode, scheme: Scheme): void {
  for (const option of root.querySelectorAll<HTMLElement>("a[data-ib-scheme]")) {
    if (option.dataset.ibScheme === scheme) option.setAttribute("aria-current", "true");
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

class SchemeSwitch extends IzzyElement {
  protected setup(signal: AbortSignal): void {
    const saved = parse(readCookie(COOKIE));
    if (saved) mark(this, saved);

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
        channel?.postMessage(scheme);
        this.dispatchEvent(new CustomEvent("ibscheme", { bubbles: true, detail: { scheme } }));
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
