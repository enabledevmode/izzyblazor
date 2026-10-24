import { IzzyElement, defineElement } from "../izzy-element";
import { TypingGuard, changedFromServer, isPageKey, navigateTo } from "../filtering";

/**
 * <ib-filter-form> makes a FilterForm live: the search box applies after a pause in typing,
 * selects and checkboxes apply when they change, other inputs of your own (a date) after a pause,
 * and Enter applies at once. It builds the URL
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
        // Text boxes apply as they're typed in. A select, checkbox or radio applies when it
        // changes; any other input of your own (a date, a number) after a pause, since typing a
        // date with the keyboard changes it once per part.
        const target = event.target as HTMLElement;
        if (!this.live || typedIn(target)) return;
        if (target.matches("select, input[type=checkbox], input[type=radio]")) this.apply(false);
        else if (target instanceof HTMLInputElement && target.type !== "hidden") {
          clearTimeout(this.#timer);
          this.#timer = window.setTimeout(() => this.apply(false), 350);
        }
      },
      { signal }
    );

    // Clear: what's waiting to apply, or to be put back while typing, is for the old filter.
    // Clear goes once nothing is filtered, so focus would fall to the next control on the page:
    // send it to the first filter instead, before the link navigates (not after a tap, which
    // would open the on-screen keyboard).
    form.addEventListener(
      "click",
      (event) => {
        if (event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
        if (!(event.target as Element).closest?.(".ib-filter-clear")) return;
        clearTimeout(this.#timer);
        this.#typing.clear();
        if ((event as PointerEvent).pointerType === "touch") return;
        const first =
          [...form.querySelectorAll<HTMLElement>("input:not([type=hidden], :disabled), select:not(:disabled), textarea:not(:disabled)")].find(
            (el) => el.checkVisibility?.() ?? true
          ) ?? null;
        first?.focus({ preventScroll: true });
        // A search box shows the old filter until the page updates: empty it, so typing starts a
        // new one, and keep what's typed from being reset by the cleared page arriving.
        if (typedIn(first)) {
          first.value = "";
          this.#typing.typed(first);
          // A live form records typing itself; without Live, record it until the page arrives.
          if (!this.live) first.addEventListener("input", () => this.#typing.typed(first), { signal: AbortSignal.timeout(3000) });
          this.#typing.guard();
        }
      },
      { signal }
    );

    form.addEventListener("focusout", (event) => this.#typing.forget(event.target as HTMLInputElement), { signal });
    signal.addEventListener("abort", () => clearTimeout(this.#timer));

    // Back and Forward show another filter: what was being typed no longer applies.
    window.addEventListener(
      "popstate",
      () => {
        clearTimeout(this.#timer);
        this.#typing.clear();
      },
      { signal }
    );

    // A filter changed before this script arrived (the page shows no Apply button where script
    // runs): apply it now rather than drop it. Not while it's still being typed in. An
    // interactive render sets values as properties, not attributes, so the defaults it leaves
    // can't tell a change; and it replaced whatever was changed before it.
    if (this.live && !this.hasAttribute("data-ib-interactive") && changedFromServer(form.elements)) {
      const active = document.activeElement;
      if (active instanceof HTMLInputElement && form.contains(active) && typedIn(active)) this.typed(active);
      else this.apply(false);
    }
  }

  private typed(input: HTMLInputElement): void {
    if (!this.live || !typedIn(input)) return;
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

/** Inputs that apply as they're typed in: text-like types (an input with no type is text). */
function typedIn(target: EventTarget | null): target is HTMLInputElement {
  return target instanceof HTMLInputElement && /^(text|search|email|tel|url)$/.test(target.type);
}

defineElement("ib-filter-form", FilterForm);
