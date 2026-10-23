/**
 * Helpers shared by FilterForm and Table's filters. A module of its own, with no side effects, so
 * the on-demand Table bundle doesn't also carry (and define) the FilterForm element.
 */

interface BlazorNavigator {
  navigateTo(uri: string, options?: { replaceHistoryEntry?: boolean }): void;
}

/**
 * Puts back typing that a response for an earlier value overwrote. While someone keeps typing, a
 * live filter's navigation can come back with the value from a moment ago and reset the box.
 * Shared by FilterForm and Table's filter row.
 */
export class TypingGuard {
  #typed = new Map<HTMLInputElement, string>();
  composing = false;

  get size(): number {
    return this.#typed.size;
  }

  typed(input: HTMLInputElement): void {
    this.#typed.set(input, input.value);
  }

  forget(input: HTMLInputElement): void {
    this.#typed.delete(input);
  }

  restore(): void {
    if (this.composing) return;
    for (const [input, typed] of this.#typed) {
      if (input.isConnected && input.value !== typed) {
        const focused = document.activeElement === input;
        input.value = typed;
        // Only text-like inputs have a caret (a date or number input throws).
        if (focused && /^(text|search|url|tel|password)$/.test(input.type)) input.setSelectionRange(typed.length, typed.length);
      }
    }
  }

  /** Restores a few times while the navigation's response arrives. */
  guard(): void {
    if (this.#typed.size === 0) return;
    for (const delay of [50, 150, 400, 900, 1600, 3000]) {
      setTimeout(() => this.restore(), delay);
    }
  }
}

/** Navigates to a URL through Blazor (keeping the page in place), or loads it without Blazor. */
export function navigateTo(url: URL, replace: boolean): boolean {
  if (sameQuery(url.searchParams, new URLSearchParams(location.search)) && url.pathname === location.pathname) return false;
  const blazor = (window as unknown as { Blazor?: BlazorNavigator }).Blazor;
  if (blazor?.navigateTo) blazor.navigateTo(url.pathname + url.search + url.hash, { replaceHistoryEntry: replace });
  else location.assign(url);
  return true;
}

/**
 * Whether someone changed any of these controls from what the server rendered (their default
 * value, checked or selected state): before the script arrived, say, when nothing applied it.
 */
export function changedFromServer(controls: Iterable<Element>): boolean {
  for (const control of controls) {
    if (control instanceof HTMLSelectElement) {
      if ([...control.options].some((o) => o.selected !== o.defaultSelected)) return true;
    } else if (control instanceof HTMLInputElement) {
      if (control.type === "checkbox" || control.type === "radio" ? control.checked !== control.defaultChecked : control.value !== control.defaultValue) return true;
    } else if (control instanceof HTMLTextAreaElement && control.value !== control.defaultValue) {
      return true;
    }
  }
  return false;
}

/** Page-number keys: "page", or a table's prefixed one ("orders-page", "p_page"). Mirrors FilterForm.IsPage. */
export function isPageKey(key: string): boolean {
  return /^(.*[-_.])?page$/i.test(key);
}

// Equal parameters in any order, ignoring how they're encoded ("%20" vs "+").
function sameQuery(a: URLSearchParams, b: URLSearchParams): boolean {
  const list = (p: URLSearchParams) => [...p].map(([k, v]) => `${k}=${v}`).sort().join("&");
  return list(a) === list(b);
}
