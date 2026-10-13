import { IzzyElement, defineElement } from "../izzy-element";

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

/** Page-number keys: "page", or a table's prefixed one ("orders-page", "p_page"). Mirrors FilterForm.IsPage. */
export function isPageKey(key: string): boolean {
  return /^(.*[-_.])?page$/i.test(key);
}

/**
 * <ib-filter-form> makes a FilterForm live: the search box applies after a pause in typing,
 * selects and checkboxes apply when they change, and Enter applies at once. It builds the URL
 * from the current one plus the form's fields (as a normal submit would), drops page numbers,
 * and navigates through Blazor, which keeps the page in place.
 *
 * While someone keeps typing, a response for an earlier value can arrive and reset the box;
 * the element puts back what they typed. Text being composed with an input method (Chinese,
 * Japanese, Korean) is left alone until it's committed.
 */
class FilterForm extends IzzyElement {
  #timer = 0;
  #typing = new TypingGuard();

  private get form(): HTMLFormElement | null {
    return this.querySelector("form");
  }

  private get live(): boolean {
    return this.hasAttribute("data-live");
  }

  protected setup(signal: AbortSignal): void {
    const form = this.form;
    if (!form) return;

    form.addEventListener(
      "submit",
      (event) => {
        event.preventDefault();
        clearTimeout(this.#timer);
        this.apply(false, (event as SubmitEvent).submitter);
      },
      { signal }
    );

    form.addEventListener("compositionstart", () => (this.#typing.composing = true), { signal });
    form.addEventListener(
      "compositionend",
      (event) => {
        this.#typing.composing = false;
        this.typed(event.target as HTMLInputElement);
      },
      { signal }
    );

    form.addEventListener(
      "input",
      (event) => {
        if ((event as InputEvent).isComposing || this.#typing.composing) return;
        this.typed(event.target as HTMLInputElement);
      },
      { signal }
    );

    form.addEventListener(
      "change",
      (event) => {
        const target = event.target as HTMLElement;
        if (this.live && target.matches("select, input[type=checkbox], input[type=radio]")) this.apply(false);
      },
      { signal }
    );

    form.addEventListener("focusout", (event) => this.#typing.forget(event.target as HTMLInputElement), { signal });
    signal.addEventListener("abort", () => clearTimeout(this.#timer));
  }

  private typed(input: HTMLInputElement): void {
    if (!this.live || !input.matches?.("input[type=search], input[type=text]")) return;
    this.#typing.typed(input);
    clearTimeout(this.#timer);
    this.#timer = window.setTimeout(() => this.apply(true), 350);
  }

  /** Navigates to the current URL with this form's values, without page numbers. */
  private apply(replace: boolean, submitter?: HTMLElement | null): void {
    const form = this.form;
    if (!form || this.#typing.composing) return;
    this.#typing.restore();

    // The same values a native submit would send (disabled fields out, the submitter's
    // name=value in), minus the hidden fields that only carry the rest of the URL over.
    let data: FormData;
    try {
      data = submitter ? new FormData(form, submitter) : new FormData(form);
    } catch {
      data = new FormData(form);
    }
    const carried = new Set(
      [...form.querySelectorAll<HTMLInputElement>("input[type=hidden][name]")].map((input) => input.name)
    );
    const names = new Set(
      [...form.elements]
        .filter((el): el is HTMLInputElement => "name" in el && !!(el as HTMLInputElement).name && (el as HTMLInputElement).type !== "hidden")
        .map((el) => el.name)
    );

    const url = new URL(location.href);
    for (const name of names) url.searchParams.delete(name);
    for (const [key, value] of data) {
      if (carried.has(key) && !names.has(key)) continue;
      if (typeof value === "string" && value !== "") url.searchParams.append(key, value);
    }
    for (const key of [...url.searchParams.keys()]) {
      if (isPageKey(key)) url.searchParams.delete(key);
    }

    if (navigateTo(url, replace)) this.#typing.guard();
  }
}

// Equal parameters in any order, ignoring how they're encoded ("%20" vs "+").
function sameQuery(a: URLSearchParams, b: URLSearchParams): boolean {
  const list = (p: URLSearchParams) => [...p].map(([k, v]) => `${k}=${v}`).sort().join("&");
  return list(a) === list(b);
}

defineElement("ib-filter-form", FilterForm);
