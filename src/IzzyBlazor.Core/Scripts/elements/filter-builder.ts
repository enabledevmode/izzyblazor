import { IzzyElement, defineElement } from "../izzy-element";
import { navigateTo } from "../filtering";

/**
 * <ib-filter-builder> draws FilterBuilder's rows, in every mode: the server draws one copy of each
 * group, rule and field's controls in a hidden block, and this clones them, so no markup lives here.
 * The rows are the state, and Blazor's tree has none of them: the container is empty there and
 * data-permanent to a navigation. The server says the filter as JSON in data-ib-value, which is drawn
 * when it changes. Apply writes the filter's text to the address, or to the hidden field C# hears.
 */
interface Rule {
  field?: string;
  op?: string;
  values?: string[];
  to?: string | null;
  raw?: string;
}

interface Group {
  logic: string;
  items: (Group | Rule)[];
}

const NO_VALUE = ["isempty", "isnotempty"];
const LIST = ["in", "notin"];

function isGroup(item: Group | Rule): item is Group {
  return "logic" in item;
}

const quoted = (text: string) => `'${text.replace(/'/g, "''")}'`;

class FilterBuilder extends IzzyElement {
  #drawn: string | null = null;
  #ids = 0;

  get #root(): HTMLElement {
    return this.querySelector<HTMLElement>(":scope > [data-ib-fb-root]")!;
  }

  #clone(selector: string): HTMLElement {
    return this.querySelector<HTMLElement>(`:scope > [data-ib-fb-lib] > ${selector}`)!.cloneNode(true) as HTMLElement;
  }

  protected setup(signal: AbortSignal): void {
    this.addEventListener("click", (event) => this.#onClick(event), { signal });
    this.addEventListener("change", (event) => this.#onChange(event.target as HTMLElement), { signal });
    // Enter in a value applies, and isn't a surrounding form's.
    this.addEventListener(
      "keydown",
      (event) => {
        if (event.key !== "Enter" || event.isComposing || !(event.target instanceof HTMLInputElement) || event.target.type === "checkbox") return;
        event.preventDefault();
        this.#apply();
      },
      { signal }
    );
    this.addEventListener("input", (event) => this.#named((event.target as Element).closest<HTMLElement>("[data-ib-fb-rule]")), { signal });

    // A Value set from C#, or a navigation's new address. Not the same one over, which would lose edits.
    const observer = new MutationObserver(() => this.#draw());
    observer.observe(this, { attributes: true, attributeFilter: ["data-ib-value", "data-ib-fields"] });
    signal.addEventListener("abort", () => observer.disconnect());
    this.#draw();
  }

  #draw(): void {
    const json = this.dataset.ibValue ?? "";
    // With the fields: a navigation to another page's builder keeps these rows, and those are its fields' no longer.
    const drawn = `${this.dataset.ibFields}|${json}`;
    if (drawn === this.#drawn) return;
    this.#drawn = drawn;
    const group: Group = json ? (JSON.parse(json) as Group) : { logic: "all", items: [] };
    if (group.items.length === 0) group.items.push({});
    // Focus stays at its place: Apply draws again, as the address has changed.
    const controls = () => [...this.#root.querySelectorAll<HTMLElement>("select, input, button")];
    const at = controls().indexOf(document.activeElement as HTMLElement);
    this.#root.replaceChildren(this.#group(group, 1, true));
    if (at >= 0) {
      const now = controls();
      now[Math.min(at, now.length - 1)]?.focus();
    }
  }

  #group(group: Group, depth: number, top = false): HTMLElement {
    const box = this.#clone("[data-ib-fb-group]");
    box.dataset.ibDepth = String(depth);
    this.#setLogic(box, group.logic);
    const head = box.querySelector(":scope > .ib-fb-head")!;
    if (top) head.querySelector("[data-ib-fb-remove]")!.remove();
    const items = box.querySelector(":scope > [data-ib-fb-items]")!;

    // At the depth limit, only rules can be added, so there's no menu. Otherwise its menu needs ids of its own.
    const atLimit = depth >= Number(this.dataset.ibMaxDepth);
    items.querySelector(atLimit ? ":scope > [data-ib-fb-add]" : ":scope > [data-ib-fb-add-plain]")!.remove();
    if (!atLimit) {
      const add = items.querySelector<HTMLElement>(":scope > [data-ib-fb-add]")!;
      let html = add.outerHTML;
      for (const el of add.querySelectorAll("[id]")) html = html.replaceAll(el.id, `${el.id}-${++this.#ids}`);
      add.outerHTML = html;
    }
    const last = items.lastElementChild;
    for (const item of group.items) items.insertBefore(isGroup(item) ? this.#group(item, depth + 1) : item.raw !== undefined ? this.#raw(item.raw) : this.#rule(item), last);
    return box;
  }

  #setLogic(group: HTMLElement, logic: string): void {
    for (const button of group.querySelectorAll<HTMLElement>(":scope > .ib-fb-head [data-ib-logic]")) button.setAttribute("aria-pressed", String(button.dataset.ibLogic === logic));
  }

  #raw(text: string): HTMLElement {
    const row = this.#clone("[data-ib-fb-raw]");
    row.querySelector("[data-ib-fb-raw-text]")!.textContent = text;
    row.dataset.ibText = text;
    this.#nameRemove(row, text);
    return row;
  }

  #rule(rule: Rule): HTMLElement {
    const row = this.#clone("[data-ib-fb-rule]");
    const select = row.querySelector<HTMLSelectElement>("[data-ib-fb-field-select]")!;
    if (rule.field) select.value = rule.field;
    this.#fill(row, rule.op, rule.values ?? [], rule.to ?? "");
    return row;
  }

  // The operator and value for the field chosen, keeping what still fits.
  #fill(row: HTMLElement, op: string | undefined, values: string[], to: string): void {
    const key = row.querySelector<HTMLSelectElement>("[data-ib-fb-field-select]")!.value;
    const source = [...this.querySelectorAll<HTMLElement>(":scope > [data-ib-fb-lib] > [data-ib-fb-field]")].find((f) => f.dataset.ibFbField === key);
    if (!source) return;
    row.dataset.ibKind = source.dataset.ibKind;
    const ops = source.querySelector("[data-ib-fb-op]")!.closest(".ib-field")!.cloneNode(true) as HTMLElement;
    const editor = source.querySelector("[data-ib-fb-editor]")!.cloneNode(true) as HTMLElement;
    row.querySelector("[data-ib-fb-op-slot]")!.replaceChildren(ops);
    row.querySelector("[data-ib-fb-value-slot]")!.replaceChildren(editor);
    const opSelect = ops.querySelector("select")!;
    if (op && [...opSelect.options].some((o) => o.value === op)) opSelect.value = op;
    const boxes = [...editor.querySelectorAll<HTMLInputElement>("input[type=checkbox]")];
    for (const box of boxes) box.checked = values.includes(box.value);
    const from = editor.querySelector<HTMLInputElement>("[data-ib-fb-from]");
    if (from) from.value = values[0] ?? "";
    const upper = editor.querySelector<HTMLInputElement>("[data-ib-fb-to]");
    if (upper) upper.value = to;
    this.#shape(row);
  }

  // What the operator takes: nothing, one value, two, or the list.
  #shape(row: HTMLElement): void {
    const op = row.querySelector<HTMLSelectElement>("[data-ib-fb-op]")?.value ?? "";
    const slot = row.querySelector<HTMLElement>("[data-ib-fb-value-slot]")!;
    slot.hidden = NO_VALUE.includes(op);
    const toBox = slot.querySelector<HTMLElement>("[data-ib-fb-to-box]");
    if (toBox) toBox.hidden = op !== "between";
    this.#named(row);
  }

  #named(row: HTMLElement | null): void {
    if (!row) return;
    const words = (selector: string) => {
      const select = row.querySelector<HTMLSelectElement>(selector);
      return select?.selectedOptions[0]?.text ?? "";
    };
    const value = row.querySelector<HTMLElement>("[data-ib-fb-value-slot]");
    const said = !value || value.hidden ? "" : [...value.querySelectorAll<HTMLInputElement>("input")].filter((i) => (i.type === "checkbox" ? i.checked : i.value)).map((i) => (i.type === "checkbox" ? i.closest("label")!.textContent!.trim() : i.value)).join(", ");
    this.#nameRemove(row, [words("[data-ib-fb-field-select]"), words("[data-ib-fb-op]"), said].filter(Boolean).join(" "));
  }

  #nameRemove(row: HTMLElement, what: string): void {
    row.querySelector(":scope > [data-ib-fb-remove]")?.setAttribute("aria-label", this.dataset.ibRemove!.replace("{0}", what));
  }

  #onChange(target: HTMLElement): void {
    const row = target.closest<HTMLElement>("[data-ib-fb-rule]");
    if (!row) return;
    if (target.matches("[data-ib-fb-field-select]")) {
      // A value carries over to a field of the same kind.
      const kind = row.dataset.ibKind;
      const key = (target as HTMLSelectElement).value;
      const same = [...this.querySelectorAll<HTMLElement>(":scope > [data-ib-fb-lib] > [data-ib-fb-field]")].find((f) => f.dataset.ibFbField === key)?.dataset.ibKind === kind && kind !== "choice";
      const from = row.querySelector<HTMLInputElement>("[data-ib-fb-from]")?.value ?? "";
      const to = row.querySelector<HTMLInputElement>("[data-ib-fb-to]")?.value ?? "";
      const op = row.querySelector<HTMLSelectElement>("[data-ib-fb-op]")?.value;
      this.#fill(row, same ? op : undefined, same && from ? [from] : [], same ? to : "");
    } else if (target.matches("[data-ib-fb-op]")) {
      this.#shape(row);
    } else {
      this.#named(row);
    }
  }

  #onClick(event: Event): void {
    const target = event.target as Element;
    const button = target.closest<HTMLElement>("button");
    if (!button) return;
    if (button.matches("[data-ib-fb-apply]")) this.#apply();
    else if (button.matches("[data-ib-fb-clear]")) this.#clear();
    else if (button.matches("[data-ib-logic]")) this.#setLogic(button.closest<HTMLElement>("[data-ib-fb-group]")!, button.dataset.ibLogic!);
    else if (button.matches("[data-ib-fb-add-rule], [data-ib-fb-add-group]")) this.#add(button);
    else if (button.matches("[data-ib-fb-remove]")) this.#remove(button);
  }

  #add(button: HTMLElement): void {
    const group = button.closest<HTMLElement>("[data-ib-fb-group]")!;
    const items = group.querySelector(":scope > [data-ib-fb-items]")!;
    const added = button.matches("[data-ib-fb-add-rule]") ? this.#rule({}) : this.#group({ logic: "all", items: [{}] }, Number(group.dataset.ibDepth) + 1);
    items.insertBefore(added, items.lastElementChild);
    added.querySelector<HTMLElement>("[data-ib-fb-field-select]")?.focus();
  }

  // Focus goes to the next row, or the one before, or the group's add button.
  #remove(button: HTMLElement): void {
    const row = button.closest<HTMLElement>("[data-ib-fb-rule], [data-ib-fb-raw], [data-ib-fb-group]")!;
    const parent = row.parentElement!.closest<HTMLElement>("[data-ib-fb-group]")!;
    const next = (row.nextElementSibling ?? row.previousElementSibling) as HTMLElement | null;
    row.remove();
    (next?.querySelector<HTMLElement>("select, button") ?? parent.querySelector<HTMLElement>("[data-ib-fb-add-rule]"))?.focus();
  }

  #clear(): void {
    this.#root.replaceChildren(this.#group({ logic: "all", items: [{}] }, 1, true));
    this.#apply();
    this.querySelector<HTMLElement>("[data-ib-fb-apply]")?.focus();
  }

  // The filter's text, in the form Filter.Parse reads. A rule with nothing to compare is left out, as is
  // an empty group: Any() of nothing would hide every row.
  #groupText(group: HTMLElement, top: boolean): string {
    const parts: string[] = [];
    for (const item of group.querySelector(":scope > [data-ib-fb-items]")!.children) {
      const text = item.matches("[data-ib-fb-group]") ? this.#groupText(item as HTMLElement, false)
        : item.matches("[data-ib-fb-raw]") ? (item as HTMLElement).dataset.ibText!
        : item.matches("[data-ib-fb-rule]") ? this.#ruleText(item as HTMLElement)
        : "";
      if (text) parts.push(text);
    }
    if (parts.length === 0) return "";
    const logic = group.querySelector<HTMLElement>(':scope > .ib-fb-head [data-ib-logic][aria-pressed="true"]')!.dataset.ibLogic;
    const joined = parts.join(logic === "all" ? " and " : " or ");
    return top || parts.length === 1 ? joined : `(${joined})`;
  }

  #ruleText(row: HTMLElement): string {
    const field = row.querySelector<HTMLSelectElement>("[data-ib-fb-field-select]")!.value;
    const op = row.querySelector<HTMLSelectElement>("[data-ib-fb-op]")?.value ?? "";
    const kind = row.dataset.ibKind;
    if (NO_VALUE.includes(op)) {
      if (kind === "text") return `${op}(${field})`;
      return `${field} ${op === "isempty" ? "eq" : "ne"} null`;
    }
    if (LIST.includes(op)) {
      const ticked = [...row.querySelectorAll<HTMLInputElement>("input[type=checkbox]:checked")].map((b) => (b.value === "true" || b.value === "false" ? b.value : quoted(b.value)));
      if (ticked.length === 0) return "";
      const list = `${field} in (${ticked.join(",")})`;
      return op === "in" ? list : `not (${list})`;
    }
    const literal = (value: string) => (kind === "text" ? quoted(value) : value);
    const from = row.querySelector<HTMLInputElement>("[data-ib-fb-from]")?.value ?? "";
    const trimmed = kind === "text" ? from : from.trim();
    if (op === "between") {
      const to = row.querySelector<HTMLInputElement>("[data-ib-fb-to]")?.value.trim() ?? "";
      if (!trimmed && !to) return "";
      return `between(${field},${trimmed ? literal(trimmed) : "null"},${to ? literal(to) : "null"})`;
    }
    if (!trimmed) return "";
    if (op === "contains" || op === "startswith" || op === "endswith") return `${op}(${field},${literal(trimmed)})`;
    if (op === "notcontains") return `not contains(${field},${literal(trimmed)})`;
    return `${field} ${op} ${literal(trimmed)}`;
  }

  #apply(): void {
    const text = this.#groupText(this.#root.firstElementChild as HTMLElement, true);
    this.dispatchEvent(new CustomEvent("ibfilterapplied", { bubbles: true }));
    const out = this.querySelector<HTMLInputElement>(":scope > [data-ib-fb-out]");
    if (out) {
      out.value = text;
      out.dispatchEvent(new Event("change", { bubbles: true }));
      return;
    }
    const url = new URL(location.href);
    if (text) url.searchParams.set(this.dataset.ibName!, text);
    else url.searchParams.delete(this.dataset.ibName!);
    if (this.dataset.ibPageKey) url.searchParams.delete(this.dataset.ibPageKey);
    if (this.dataset.ibCursorKey) url.searchParams.delete(this.dataset.ibCursorKey);
    navigateTo(url, false);
  }
}

defineElement("ib-filter-builder", FilterBuilder);
