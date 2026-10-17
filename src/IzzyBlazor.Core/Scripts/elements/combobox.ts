import { IzzyElement, defineElement } from "../izzy-element";
import { track } from "../position";

/**
 * <ib-combobox> turns a Combobox's native select into a text input with a filtered listbox
 * (the WAI-ARIA combobox pattern: focus stays in the input, the active option is
 * aria-activedescendant).
 *
 * The select stays in the form under the input: choosing sets select.value and fires `change`
 * on it, so form posts, required validation and Blazor's @bind all work as without script.
 * With data-server, Blazor fills the list from the typed text and this element doesn't filter.
 *
 * MultiSelect uses the same element over a <select multiple>: choosing toggles an option and the
 * list stays open, the chosen options show as chips (drawn here from the select, so they're right
 * whatever changed it), and Backspace in the empty input removes the last one.
 */
const supported = typeof HTMLElement !== "undefined" && "showPopover" in HTMLElement.prototype;

const fold = (text: string) =>
  text
    .normalize("NFD")
    .replace(/\p{Diacritic}/gu, "")
    .toLocaleLowerCase()
    .trim();

class Combobox extends IzzyElement {
  #input!: HTMLInputElement;
  #select!: HTMLSelectElement;
  #list!: HTMLElement;
  #active: HTMLElement | null = null;
  #untrack: (() => void) | null = null;
  #typed = false;
  #multiple = false;
  #chips: HTMLElement | null = null;

  protected setup(signal: AbortSignal): void {
    const input = this.querySelector<HTMLInputElement>(":scope > .ib-combobox-input, :scope > .ib-multiselect-box > .ib-combobox-input");
    const select = this.querySelector<HTMLSelectElement>(":scope > select");
    const list = this.querySelector<HTMLElement>(":scope > [role=listbox]");
    if (!supported || !input || !select || !list) return;
    this.#input = input;
    this.#select = select;
    this.#list = list;
    this.#multiple = select.multiple;
    this.#chips = this.querySelector(".ib-multiselect-chips");
    this.dataset.ready = "";

    // The select is still focusable for the label, error links and validation; send focus on.
    select.tabIndex = -1;
    select.setAttribute("aria-hidden", "true");
    select.addEventListener("focus", () => input.focus(), { signal });
    select.addEventListener(
      "invalid",
      (event) => {
        event.preventDefault();
        input.setCustomValidity(select.validationMessage);
        input.reportValidity();
      },
      { signal }
    );

    input.addEventListener("input", () => this.onInput(), { signal });
    input.addEventListener("keydown", (event) => this.onKey(event), { signal });
    input.addEventListener("click", () => this.open(), { signal });
    input.addEventListener("blur", () => this.commit(), { signal });

    // Pressing the toggle or an option mustn't move focus out of the input (and commit it).
    const keepFocus = (event: Event) => event.preventDefault();
    for (const el of [list, this.querySelector(".ib-combobox-toggle")]) {
      el?.addEventListener("pointerdown", keepFocus, { signal });
      el?.addEventListener("mousedown", keepFocus, { signal });
    }

    this.querySelector(".ib-combobox-toggle")?.addEventListener(
      "click",
      () => {
        if (this.isOpen) this.close();
        else this.open(true);
        input.focus();
      },
      { signal }
    );

    list.addEventListener(
      "click",
      (event) => {
        const option = (event.target as Element).closest<HTMLElement>("[role=option]");
        if (option && !option.hidden && option.getAttribute("aria-disabled") !== "true") this.choose(option);
      },
      { signal }
    );
    list.addEventListener(
      "pointermove",
      (event) => {
        const option = (event.target as Element).closest<HTMLElement>("[role=option]");
        if (option && option !== this.#active) this.activate(option, false);
      },
      { signal }
    );

    // Blazor replaces the options (server search, or new Items).
    const observer = new MutationObserver(() => {
      if (this.isOpen) this.refresh();
    });
    observer.observe(list, { childList: true, subtree: true, characterData: true });

    let selectObserver: MutationObserver | null = null;
    if (this.#multiple) {
      // Chips follow the select, whoever changes it (this element, Blazor, a form reset).
      selectObserver = new MutationObserver((records) => {
        // Once an option's selected property has been set, the browser ignores changes to its
        // selected attribute; apply Blazor's (C# changing the value) to the property.
        for (const r of records) {
          if (r.type === "attributes" && r.target instanceof HTMLOptionElement) r.target.selected = r.target.hasAttribute("selected");
        }
        this.renderChips();
      });
      selectObserver.observe(select, { childList: true, subtree: true, attributes: true, attributeFilter: ["selected"] });
      select.addEventListener("change", () => this.renderChips(), { signal });
      this.#chips?.addEventListener(
        "click",
        (event) => {
          const chip = (event.target as Element).closest<HTMLElement>(".ib-chip-remove")?.closest<HTMLElement>(".ib-chip");
          if (chip) this.toggle(chip.dataset.value ?? "", false);
          input.focus();
        },
        { signal }
      );
      this.#chips?.addEventListener("pointerdown", keepFocus, { signal });
      // A click anywhere in the box (between chips) goes to the input.
      input.parentElement?.addEventListener(
        "click",
        (event) => {
          if (event.target === input.parentElement) {
            input.focus();
            this.open();
          }
        },
        { signal }
      );
      this.renderChips();
    }

    // A form reset puts the select and the input's default text back; drop any custom error.
    select.form?.addEventListener("reset", () => setTimeout(() => this.syncText(), 0), { signal });

    signal.addEventListener("abort", () => {
      observer.disconnect();
      selectObserver?.disconnect();
      this.close();
      delete this.dataset.ready;
    });
  }

  private get isOpen(): boolean {
    return this.#list?.matches(":popover-open") ?? false;
  }

  private get options(): HTMLElement[] {
    return [...this.#list.querySelectorAll<HTMLElement>(":scope > [role=option]")];
  }

  private get visible(): HTMLElement[] {
    return this.options.filter((o) => !o.hidden && o.getAttribute("aria-disabled") !== "true");
  }

  private open(showAll = !this.#typed): void {
    if (this.#input.disabled || this.#input.readOnly) return;
    if (!this.isOpen) {
      try {
        this.#list.showPopover();
      } catch {
        return;
      }
      this.#input.setAttribute("aria-expanded", "true");
      // Where anchor-size() isn't supported, match the field's width here.
      this.#list.style.minInlineSize = `${this.offsetWidth}px`;
      this.#untrack = track(this.#list, this, "bottom-start");
    }
    this.filter(showAll ? "" : this.#input.value);
    const selected = this.#multiple ? undefined : this.visible.find((o) => o.getAttribute("aria-selected") === "true");
    const active = selected ?? (showAll ? null : this.visible[0] ?? null);
    if (!active) this.#list.scrollTop = 0;
    this.activate(active);
  }

  private close(): void {
    this.#untrack?.();
    this.#untrack = null;
    if (this.isOpen) this.#list.hidePopover();
    this.#input?.setAttribute("aria-expanded", "false");
    this.activate(null);
  }

  /** After Blazor changed the options: filter again and keep a sensible active option. */
  private refresh(): void {
    this.filter(this.#typed ? this.#input.value : "");
    const keep = this.#active && this.#active.isConnected && !this.#active.hidden ? this.#active : null;
    this.activate(keep ?? (this.#typed ? this.visible[0] ?? null : null));
  }

  private filter(query: string): void {
    const server = this.hasAttribute("data-server");
    const q = fold(query);
    let shown = 0;
    for (const option of this.options) {
      const match = server || !q || fold(option.dataset.text ?? option.textContent ?? "").includes(q);
      option.hidden = !match;
      if (match) shown++;
    }
    this.#list.toggleAttribute("data-empty", shown === 0);
  }

  private onInput(): void {
    this.#typed = true;
    this.#input.setCustomValidity("");
    this.open(false);
  }

  private onKey(event: KeyboardEvent): void {
    if (event.isComposing || event.ctrlKey || event.metaKey) return;
    const options = this.visible;
    const index = this.#active ? options.indexOf(this.#active) : -1;

    switch (event.key) {
      case "ArrowDown":
        event.preventDefault();
        if (!this.isOpen) {
          this.open();
          if (!event.altKey && !this.#active) this.activate(this.visible[0] ?? null);
        } else if (!event.altKey) {
          this.activate(options[Math.min(index + 1, options.length - 1)] ?? null);
        }
        break;
      case "ArrowUp":
        event.preventDefault();
        if (!this.isOpen) {
          this.open();
          if (!this.#active) this.activate(this.visible.at(-1) ?? null);
        } else if (event.altKey) {
          if (this.#active) this.choose(this.#active);
          else this.close();
        } else {
          this.activate(options[Math.max(index - 1, 0)] ?? null);
        }
        break;
      case "PageDown":
      case "PageUp":
        if (!this.isOpen) return;
        event.preventDefault();
        this.activate(options[Math.max(0, Math.min(index + (event.key === "PageDown" ? 10 : -10), options.length - 1))] ?? null);
        break;
      case "Enter":
        if (this.isOpen && this.#active) {
          event.preventDefault();
          this.choose(this.#active);
        }
        break;
      case "Backspace":
        if (this.#multiple && this.#input.value === "") {
          const last = [...this.#select.selectedOptions].at(-1);
          if (last) {
            event.preventDefault();
            this.toggle(last.value, false);
          }
        }
        break;
      case "Escape":
        if (this.isOpen) {
          // Don't let a surrounding dialog close too.
          event.preventDefault();
          event.stopPropagation();
          this.close();
          this.syncText();
        }
        break;
      case "Tab":
        this.close();
        break;
    }
  }

  private activate(option: HTMLElement | null, scroll = true): void {
    this.#active?.removeAttribute("data-active");
    this.#active = option;
    if (option) {
      option.setAttribute("data-active", "");
      this.#input?.setAttribute("aria-activedescendant", option.id);
      if (scroll) option.scrollIntoView({ block: "nearest" });
    } else {
      this.#input?.removeAttribute("aria-activedescendant");
    }
  }

  private choose(option: HTMLElement): void {
    if (this.#multiple) {
      this.toggle(option.dataset.value ?? "");
      // Start the next search; the list stays open with every option.
      this.#input.value = "";
      this.#typed = false;
      this.filter("");
      this.activate(option.hidden ? null : option, false);
      return;
    }
    this.setValue(option.dataset.value ?? "", option.dataset.text ?? option.textContent?.trim() ?? "");
    this.close();
  }

  /** MultiSelect: selects or clears one option (flips it when `on` is left out). */
  private toggle(value: string, on?: boolean): void {
    // A read-only MultiSelect's select is disabled: its chips can't be removed.
    if (this.#select.disabled) return;
    const option = [...this.#select.options].find((o) => o.value === value);
    if (!option) return;
    const want = on ?? !option.selected;
    if (option.selected === want) return;
    option.selected = want;
    this.#list.querySelector(`[role=option][data-value="${CSS.escape(value)}"]`)?.setAttribute("aria-selected", String(want));
    this.#input.setCustomValidity("");
    this.#select.dispatchEvent(new Event("input", { bubbles: true }));
    this.#select.dispatchEvent(new Event("change", { bubbles: true }));
  }

  /** MultiSelect: one chip per chosen option, with a remove button. */
  private renderChips(): void {
    if (!this.#chips) return;
    const remove = this.dataset.removeLabel ?? "Remove";
    const chosen = [...this.#select.selectedOptions];
    // Already right (most observer calls): leave the DOM alone.
    const now = [...this.#chips.children].map((c) => (c as HTMLElement).dataset.value).join("\n");
    if (now === chosen.map((o) => o.value).join("\n")) return;
    this.#chips.replaceChildren(
      ...chosen.map((o) => {
        const chip = document.createElement("span");
        chip.className = "ib-chip";
        chip.dataset.value = o.value;
        const text = document.createElement("span");
        text.className = "ib-chip-text";
        text.textContent = o.text;
        const button = document.createElement("button");
        button.type = "button";
        button.className = "ib-chip-remove";
        button.tabIndex = -1;
        button.setAttribute("aria-label", `${remove} ${o.text}`);
        button.innerHTML = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" aria-hidden="true"><path d="M18 6 6 18M6 6l12 12"/></svg>';
        chip.append(text, button);
        return chip;
      })
    );
    for (const option of this.options) option.setAttribute("aria-selected", String(chosen.some((o) => o.value === option.dataset.value)));
  }

  private setValue(value: string, text: string): void {
    const select = this.#select;
    this.#typed = false;
    this.#input.value = text;
    this.#input.setCustomValidity("");
    if (select.value === value) return;
    select.value = value;
    for (const o of this.options) o.setAttribute("aria-selected", String(o.dataset.value === value));
    select.dispatchEvent(new Event("input", { bubbles: true }));
    select.dispatchEvent(new Event("change", { bubbles: true }));
  }

  /** On leaving the field: take an exact match, clear when emptied, otherwise put the chosen text back. */
  private commit(): void {
    this.close();
    if (this.#multiple) {
      // Typing only searches; leaving drops the search text.
      this.#input.value = "";
      this.#typed = false;
      return;
    }
    if (!this.#typed) return;
    const text = fold(this.#input.value);
    if (!text) {
      this.setValue("", "");
      return;
    }
    const match = this.options.find((o) => fold(o.dataset.text ?? "") === text);
    if (match) this.setValue(match.dataset.value ?? "", match.dataset.text ?? "");
    else this.syncText();
  }

  /** Shows the chosen option's text. */
  private syncText(): void {
    this.#typed = false;
    if (this.#multiple) {
      this.#input.value = "";
      this.renderChips();
      return;
    }
    const option = this.#select.selectedOptions[0];
    this.#input.value = option && option.value !== "" ? option.text : "";
    this.#input.setCustomValidity("");
  }
}

defineElement("ib-combobox", Combobox);
