import { IzzyElement, defineElement } from "../izzy-element";

interface BlazorNavigator {
  navigateTo(uri: string, options?: { replaceHistoryEntry?: boolean }): void;
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
  #typed = new Map<HTMLInputElement, string>();
  #composing = false;

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

    form.addEventListener("compositionstart", () => (this.#composing = true), { signal });
    form.addEventListener(
      "compositionend",
      (event) => {
        this.#composing = false;
        this.typed(event.target as HTMLInputElement);
      },
      { signal }
    );

    form.addEventListener(
      "input",
      (event) => {
        if ((event as InputEvent).isComposing || this.#composing) return;
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

    form.addEventListener("focusout", (event) => this.#typed.delete(event.target as HTMLInputElement), { signal });
    signal.addEventListener("abort", () => clearTimeout(this.#timer));
  }

  private typed(input: HTMLInputElement): void {
    if (!this.live || !input.matches?.("input[type=search], input[type=text]")) return;
    this.#typed.set(input, input.value);
    clearTimeout(this.#timer);
    this.#timer = window.setTimeout(() => this.apply(true), 350);
  }

  /** Navigates to the current URL with this form's values, without page numbers. */
  private apply(replace: boolean, submitter?: HTMLElement | null): void {
    const form = this.form;
    if (!form || this.#composing) return;
    this.restoreTyping();

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

    if (sameQuery(url.searchParams, new URLSearchParams(location.search))) return;
    const blazor = (window as unknown as { Blazor?: BlazorNavigator }).Blazor;
    if (blazor?.navigateTo) blazor.navigateTo(url.pathname + url.search + url.hash, { replaceHistoryEntry: replace });
    else location.assign(url);
    this.guardTyping();
  }

  // Puts back typing that a response for an earlier value overwrote.
  private restoreTyping(): void {
    if (this.#composing) return;
    for (const [input, typed] of this.#typed) {
      if (input.isConnected && input.value !== typed) {
        const focused = document.activeElement === input;
        input.value = typed;
        if (focused) input.setSelectionRange(typed.length, typed.length);
      }
    }
  }

  private guardTyping(): void {
    if (this.#typed.size === 0) return;
    for (const delay of [50, 150, 400, 900, 1600, 3000]) {
      setTimeout(() => this.restoreTyping(), delay);
    }
  }
}

// Equal parameters in any order, ignoring how they're encoded ("%20" vs "+").
function sameQuery(a: URLSearchParams, b: URLSearchParams): boolean {
  const list = (p: URLSearchParams) => [...p].map(([k, v]) => `${k}=${v}`).sort().join("&");
  return list(a) === list(b);
}

defineElement("ib-filter-form", FilterForm);
