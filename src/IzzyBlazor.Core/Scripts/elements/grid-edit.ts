import { IzzyElement, defineElement } from "../izzy-element";

/**
 * <ib-grid-edit> is an empty element in a DataGrid that can edit or delete rows. It moves focus as an
 * editor comes and goes, and on a static page opens one with no round trip for the whole page. With no
 * script the links and forms work on their own.
 */
class GridEdit extends IzzyElement {
  // The row being saved, whose edit button gets focus back. Kept here, as a navigation sets this element up again.
  #saving: string | null = null;
  #opening = false;
  // The row or cell being edited in place, whose first field gets focus as it arrives.
  #editing: HTMLElement | null = null;
  // With a cell: where it is, where focus goes as it ends, and what was typed to start it.
  #cell = "";
  #after: "cell" | "next" | "" = "";
  #next = "";
  #typed = "";

  protected setup(signal: AbortSignal): void {
    const root = this.closest("ib-table");
    if (!root) return;
    root.addEventListener("submit", (event) => {
      const form = event.target as Element;
      this.#saving = (form.closest("dialog") ?? form).getAttribute("data-ib-edit-for");
    }, { signal });
    // A closed dialog gives focus back by itself, but a circuit starting may have redrawn the button.
    root.addEventListener("close", (event) => {
      const key = (event.target as Element).getAttribute("data-ib-edit-for");
      const row = key && root.querySelector(`tr[data-ib-key="${CSS.escape(key)}"]`);
      if (row && !row.contains(document.activeElement)) this.#focusAction(root, key!);
    }, { signal, capture: true });
    const check = () => {
      this.#check(root);
      this.#checkCell(root);
    };
    if (root.querySelector("td[data-ib-edit-col]")) this.#setupCells(root, signal);
    const gone = new MutationObserver(check);
    gone.observe(root, { childList: true, subtree: true });
    // Set up again by an enhanced navigation that has already taken the editor away.
    check();

    // On a static page, only the editor is taken from the page the link leads to: merging a whole page
    // back in, to add one dialog, takes as long as the page is big. Not on one prerendered for a circuit:
    // the circuit knows nothing of an editor added this way, and takes it away as it starts.
    root.addEventListener("click", (event) => {
      const click = event as MouseEvent;
      const target = click.target as Element;
      const cancel = target.closest("[data-ib-editing] [data-ib-edit-cancel]");
      if (cancel) this.#saving = cancel.closest("tr")!.dataset.ibKey ?? "";
      const link = target.closest<HTMLAnchorElement>("a[data-ib-edit], a[data-ib-edit-row]");
      if (!link || !root.hasAttribute("data-ib-stays-static") || click.ctrlKey || click.metaKey || click.shiftKey) return;
      click.preventDefault();
      click.stopPropagation();
      void this.#open(root, link);
    }, { signal });
    signal.addEventListener("abort", () => gone.disconnect());
  }

  // A save takes the editor away with the next render, so it can't hand focus back as closing it does:
  // focus goes to the row's edit button when it's gone. A row being edited takes focus as it arrives.
  #check(root: Element): void {
    const editing = root.querySelector<HTMLElement>(":scope > .ib-table-scroll > table > tbody > tr[data-ib-editing], :scope > .ib-table-scroll > table > tbody > tr > td[data-ib-editing]");
    if (editing !== this.#editing) {
      this.#editing = editing;
      const field = this.#field(editing, "[autofocus]") ?? this.#field(editing, "input, select, textarea");
      field?.focus();
      this.#began(editing, field as HTMLInputElement);
    }
    if (this.#saving === null) return;
    if (root.querySelector("[data-ib-edit-for]")) {
      // A failed save in a row: to its first invalid field, whose message is beside it.
      const invalid = this.#field(editing, '[aria-invalid="true"]');
      if (invalid) {
        this.#saving = null;
        invalid.focus();
      }
      return;
    }
    this.#focusAction(root, this.#saving);
    this.#saving = null;
  }

  // The row's first action, or for a row deleted or added, the first there is.
  #focusAction(root: Element, key: string): void {
    (root.querySelector<HTMLElement>(`tr[data-ib-key="${CSS.escape(key)}"] [data-ib-edit]`) ?? root.querySelector<HTMLElement>("[data-ib-edit]"))?.focus();
  }

  // A row or cell has its field: the cursor goes after its text, and what started a cell by typing goes
  // there too. A number has no cursor to set, but setting a value leaves one there.
  #began(editing: HTMLElement | null, field: HTMLInputElement | null | undefined): void {
    if (editing?.matches("td")) {
      this.#cell = this.#addressOf(editing);
      this.#after = "";
    }
    if (field?.matches("textarea, input:is([type=text], [type=number], [type=email], [type=tel], [type=url], [type=search])")) {
      const value = field.value + this.#typed;
      field.value = "";
      field.value = value;
      if (this.#typed) field.dispatchEvent(new Event("input", { bubbles: true }));
    }
    this.#typed = "";
  }

  // A cell has ended: on to the next, or back to where it was.
  #checkCell(root: Element): void {
    if (!this.#cell) return;
    if (this.#editing) {
      // A failed save: it stays open, and has focus back, wherever the next was going.
      const invalid = this.#committed && this.#field(this.#editing, '[aria-invalid="true"]');
      if (invalid) {
        this.#committed = false;
        this.#after = this.#next = "";
        invalid.focus();
      }
      return;
    }
    this.#committed = false;
    const after = this.#after;
    const cell = this.#cellAt(root, this.#cell);
    this.#cell = this.#after = "";
    if (after === "next" && this.#next) this.#start(this.#next);
    else if (after) cell?.focus();
    this.#next = "";
  }

  #addressOf(cell: HTMLElement): string {
    return `${cell.dataset.ibEditCol}\n${cell.closest("tr")!.dataset.ibKey}`;
  }

  #cellAt(root: Element, address: string): HTMLElement | null {
    const [column, key] = address.split(/\n(.*)/s);
    return root.querySelector(`tr[data-ib-key="${CSS.escape(key)}"] > td[data-ib-edit-col="${CSS.escape(column)}"]`);
  }

  #start(address: string): void {
    this.dispatchEvent(new CustomEvent("iblayout", { bubbles: true, detail: { layout: address } }));
  }

  // Saves the cell. Its field keeps it while the browser's own checks fail, and what it's left with
  // goes to C# with the blur, ahead of the submit.
  #commit(field: HTMLInputElement, after: "cell" | "next" | ""): void {
    const form = field.form;
    if (!form?.reportValidity()) return;
    this.#after = after;
    this.#committed = this.#committing = true;
    field.blur();
    this.#committing = false;
    form.requestSubmit();
  }

  #committing = false;
  #committed = false;

  #setupCells(root: Element, signal: AbortSignal): void {
    const live = () => root.hasAttribute("data-ib-interactive");
    const on = (type: string, handler: (event: Event) => void, capture = false) => root.addEventListener(type, handler, { signal, capture });
    const editable = (target: EventTarget | null) => (target as Element | null)?.closest<HTMLElement>("td[data-ib-edit-col]:not([data-ib-editing])");

    on("click", (event) => {
      const target = event.target as Element;
      const cell = editable(target);
      if (cell && live() && !target.closest("a, button, input, select, textarea, label") && !getSelection()?.toString()) this.#start(this.#addressOf(cell));
    });

    on("keydown", (event) => {
      const key = event as KeyboardEvent;
      const target = key.target as HTMLElement;
      const open = target.closest<HTMLElement>("td[data-ib-editing]");
      if (open && !target.closest("[popover]") && !target.matches('[aria-expanded="true"]')) {
        const field = target as HTMLInputElement;
        if (key.key === "Escape") {
          key.preventDefault();
          key.stopPropagation();
          this.#after = "cell";
          this.dispatchEvent(new CustomEvent("iblayout", { bubbles: true, detail: { layout: "" } }));
        } else if (key.key === "Tab") {
          key.preventDefault();
          key.stopPropagation();
          const cells = [...root.querySelectorAll<HTMLElement>("tbody td[data-ib-edit-col]")];
          const next = cells[cells.indexOf(open) + (key.shiftKey ? -1 : 1)];
          this.#next = next ? this.#addressOf(next) : "";
          // Told to C# first, to wait for how the save ends: an invalid cell stays open.
          if (this.#next) this.#commit(field, "next");
          else this.#commit(field, "cell");
        } else if (key.key === "Enter" && !target.matches("textarea")) {
          key.preventDefault();
          key.stopPropagation();
          this.#commit(field, "cell");
        }
        return;
      }
      const cell = target === editable(target) ? target : null;
      if (!cell || !live() || key.ctrlKey || key.metaKey || key.altKey) return;
      if (key.key === "Enter" || key.key === "F2" || (key.key.length === 1 && key.key !== " ")) {
        key.preventDefault();
        key.stopPropagation();
        if (key.key.length === 1) this.#typed = key.key;
        this.#start(this.#addressOf(cell));
      }
    }, true);

    // A click elsewhere saves. Into its own picker isn't elsewhere, nor is leaving the window.
    on("focusout", (event) => {
      const focus = event as FocusEvent;
      const cell = (focus.target as Element).closest<HTMLElement>("td[data-ib-editing]");
      if (!cell || this.#committing || !document.hasFocus() || cell.contains(focus.relatedTarget as Node | null)) return;
      const field = cell.querySelector<HTMLInputElement>("input, select, textarea");
      if (field) this.#commit(field, "");
    }, true);
  }

  // Not in a closed picker, which has controls of its own and can't take focus.
  #field(row: HTMLElement | null, selector: string): HTMLElement | null | undefined {
    return row?.querySelector<HTMLElement>(`:is(${selector}):not([popover] *)`);
  }

  async #open(root: Element, link: HTMLAnchorElement): Promise<void> {
    if (this.#opening) return;
    this.#opening = true;
    try {
      const page = new DOMParser().parseFromString(await (await fetch(link.href)).text(), "text/html");
      if (link.hasAttribute("data-ib-edit-row")) {
        this.#takeRow(root, page);
        history.pushState(null, "", link.href);
        return;
      }
      const named = `ib-dialog[data-query-key="${CSS.escape(link.dataset.ibEdit!)}"]`;
      const editor = page.querySelector(named);
      if (!editor) throw new Error();
      root.querySelector(named)?.remove();
      history.pushState(null, "", link.href);
      // Where the server draws it, so that a later merge leaves it be.
      root.querySelector(":scope > ib-grid")?.after(document.adoptNode(editor));
      // After its own close handler, which takes the key out of the address.
      editor.querySelector("dialog")?.addEventListener("close", () => setTimeout(() => editor.remove()));
    } catch {
      location.href = link.href;
    } finally {
      this.#opening = false;
    }
  }

  // A row starts or stops being edited: only it, and its form, come from the page the link leads to.
  #takeRow(root: Element, page: Document): void {
    const fresh = page.querySelectorAll("ib-table")[[...document.querySelectorAll("ib-table")].indexOf(root)];
    const body = ":scope > .ib-table-scroll > table > tbody";
    const from = fresh?.querySelector(body);
    const into = root.querySelector(body);
    if (!from || !into) throw new Error();
    const rowIn = (rows: Element, key?: string) => key && rows.querySelector(`tr[data-ib-key="${CSS.escape(key)}"]`);
    for (const row of into.querySelectorAll<HTMLElement>("tr[data-ib-editing]")) row.replaceWith(rowIn(from, row.dataset.ibKey) || "");
    for (const row of [...from.querySelectorAll<HTMLElement>("tr[data-ib-editing]")]) {
      const old = rowIn(into, row.dataset.ibKey);
      if (old) old.replaceWith(row);
      else into.prepend(row);
    }
    const form = ":scope > form[data-ib-edit-for]";
    root.querySelector(form)?.remove();
    const next = fresh.querySelector(form);
    if (next) root.querySelector(":scope > ib-grid")?.after(next);
  }
}

defineElement("ib-grid-edit", GridEdit);
