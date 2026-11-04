import { readCookie, writeCookie } from "./cookies";

/**
 * The color scheme a user chose with ColorSchemeSwitch, which every page applies: on load, after
 * enhanced navigation, and when another tab or frame switches. The switch itself loads on demand
 * (elements/scheme-switch.ts, which carries its own copy of this module).
 */
export type Scheme = "system" | "light" | "dark";

export const COOKIE = "ib-scheme";
export const channel = typeof BroadcastChannel === "function" ? new BroadcastChannel("ib-scheme") : null;

export function parse(value: string | null | undefined): Scheme | null {
  return value === "system" || value === "light" || value === "dark" ? value : null;
}

// Segments mark the choice with aria-pressed, and menu items with aria-checked. The toggle has
// nothing to mark: CSS shows the right button.
export function mark(root: ParentNode, scheme: Scheme): void {
  for (const option of root.querySelectorAll<HTMLElement>("[data-ib-scheme]")) {
    const on = String(option.dataset.ibScheme === scheme);
    if (option.classList.contains("ib-segment")) option.setAttribute("aria-pressed", on);
    else if (option.classList.contains("ib-menu-item")) option.setAttribute("aria-checked", on);
  }
}

/** Puts a scheme on the page and on every switch in it. */
export function apply(scheme: Scheme): void {
  const root = document.documentElement;
  if (scheme === "system") root.removeAttribute("data-ib-color-scheme");
  else root.setAttribute("data-ib-color-scheme", scheme);

  // IzzyTheme's meta carries the server's idea of the scheme; keep it from disagreeing.
  const meta = document.head.querySelector<HTMLMetaElement>('meta[name="ib-color-scheme"]');
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
export function choose(scheme: Scheme): void {
  writeCookie(COOKIE, scheme);
  apply(scheme);
  channel?.postMessage(scheme);
}

/** Follows another tab or a demo frame that switched, and puts the saved choice on this page. */
export function installSchemeSync(): void {
  channel?.addEventListener("message", (event: MessageEvent) => {
    const scheme = parse(event.data);
    if (scheme) apply(scheme);
  });

  applySavedScheme();
}
