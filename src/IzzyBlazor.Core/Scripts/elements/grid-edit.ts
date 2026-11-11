import { IzzyElement, defineElement } from "../izzy-element";

// The first that's this grid's own, not a grid's in one of its details.
const own = <T extends Element = HTMLElement>(root: Element, selector: string): T | null =>
  [...root.querySelectorAll<T>(selector)].find((el) => el.closest("ib-table") === root) ?? null;

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
  // A value set from here, which no change event will tell C# of, and the address a post was made from.
  #set = false;
  #address = "";

  protected setup(signal: AbortSignal): void {
    const root = this.closest("ib-table");
    if (!root) return;
    root.addEventListener("submit", (event) => {
      const form = event.target as Element;
      if (form.closest("ib-table") !== root) return;
      this.#saving = (form.closest("dialog") ?? form).getAttribute("data-ib-edit-for");
      this.#address = location.href;
      // A column hidden to fit has its field twice, in its cell and in the row's detail: only the one that shows posts.
      for (const field of form.id ? root.querySelectorAll<HTMLInputElement>(`:is(td[class*=ib-h], .ib-grid-hidden > div) [form="${CSS.escape(form.id)}"]`) : []) {
        if (field.disabled || field.checkVisibility({ visibilityProperty: true })) continue;
        field.disabled = true;
        setTimeout(() => (field.disabled = false));
      }
      // A second post from a row's Save, which is outside the form, so that the form's own guard misses it.
      // Blazor's merge takes this away again. Enter waits on it too, as it's the form's default button.
      const save = root.hasAttribute("data-ib-stays-static") && form.id ? root.querySelector<HTMLButtonElement>(`button[form="${CSS.escape(form.id)}"]`) : null;
      if (save) setTimeout(() => (save.disabled = true));
    }, { signal });
    // A closed dialog gives focus back by itself, but a circuit starting may have redrawn the button.
    root.addEventListener("close", (event) => {
      if ((event.target as Element).closest("ib-table") !== root) return;
      const key = (event.target as Element).getAttribute("data-ib-edit-for");
      const row = key && own(root, `tr[data-ib-key="${CSS.escape(key)}"]`);
      if (row && !row.contains(document.activeElement)) this.#focusAction(root, key!);
    }, { signal, capture: true });
    const check = () => {
      this.#check(root);
      this.#checkCell(root);
    };
    // Set up again after an enhanced load: a post that left the editor open has Blazor go back to the
    // address it last knew, which isn't one this element pushed.
    if (this.#address && (own(root, "[data-ib-edit-for]") || own(root, "tr[data-ib-editing]")) && location.href !== this.#address) {
      history.replaceState(history.state, "", this.#address);
    }
    this.#address = "";
    this.#setupCells(root, signal);
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
    // Not gone just for the moment an enhanced load is merging the page.
    const open = own(root, "[data-ib-edit-for]") ?? editing;
    if (this.#saving === null) return;
    if (open) {
      // A failed save in a row: to its first invalid field, whose message is beside it.
      const invalid = this.#field(editing, '[aria-invalid="true"]');
      if (invalid) {
        this.#saving = null;
        invalid.focus();
      } else if (own(root, "[data-ib-edit-for] .ib-form-errors")) this.#saving = null;
      return;
    }
    this.#focusAction(root, this.#saving);
    this.#saving = null;
  }

  // The row's first action, not one in a closed menu but its ⋮, or for a row deleted or added, the first there is.
  #focusAction(root: Element, key: string): void {
    const row = `tr[data-ib-key="${CSS.escape(key)}"]`;
    const action = "[data-ib-edit]:not([popover] *)";
    // Not a button that's folded into the menu (DataGridSmallScreen).
    const shown = (selector: string) => [...root.querySelectorAll<HTMLElement>(selector)].find((found) => found.closest("ib-table") === root && found.checkVisibility());
    (shown(`${row} ${action}`) ?? shown(`${row} [data-ib-row-menu]`) ?? shown(action))?.focus();
  }

  // A row or cell has its field: the cursor goes after its text, and what started a cell by typing goes
  // there too. A number has no cursor to set, but setting a value leaves one there.
  #began(editing: HTMLElement | null, field: HTMLInputElement | null | undefined): void {
    if (editing?.matches("td")) {
      this.#cell = this.#addressOf(editing);
      this.#after = "";
    }
    if (field?.matches("textarea, input:is([type=text], [type=number], [type=email], [type=tel], [type=url], [type=search])")) {
      const was = field.value;
      field.value = "";
      field.value = was + this.#typed;
      // A letter in a number: the browser empties it, and the key is ignored.
      if (!field.value) field.value = was;
      if (field.value !== was) {
        this.#set = true;
        field.dispatchEvent(new Event("input", { bubbles: true }));
      }
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
    return own(root, `tr[data-ib-key="${CSS.escape(key)}"] > td[data-ib-edit-col="${CSS.escape(column)}"]`);
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
    if (this.#set) field.dispatchEvent(new Event("change", { bubbles: true }));
    this.#set = false;
    field.blur();
    this.#committing = false;
    form.requestSubmit();
  }

  #committing = false;
  #committed = false;

  #setupCells(root: Element, signal: AbortSignal): void {
    const live = () => root.hasAttribute("data-ib-interactive");
    const on = (type: string, handler: (event: Event) => void, capture = false) => root.addEventListener(type, handler, { signal, capture });
    // This grid's own cells: not a grid's in one of its details.
    const mine = (cell: HTMLElement | null | undefined) => (cell?.closest("ib-table") === root ? cell : null);
    const editable = (target: EventTarget | null) => mine((target as Element | null)?.closest<HTMLElement>("td[data-ib-edit-col]:not([data-ib-editing])"));

    on("click", (event) => {
      const target = event.target as Element;
      const cell = editable(target);
      if (cell && live() && !target.closest("a, button, input, select, textarea, label") && !getSelection()?.toString()) this.#start(this.#addressOf(cell));
    });

    on("keydown", (event) => {
      const key = event as KeyboardEvent;
      const target = key.target as HTMLElement;
      if (key.isComposing) return;
      const open = mine(target.closest<HTMLElement>("td[data-ib-editing]"));
      if (open && !target.closest("[popover]") && !target.matches('[aria-expanded="true"]') && !this.#listOpen(target)) {
        const field = this.#field(open, "input:not([type=hidden]), select, textarea") as HTMLInputElement;
        if (key.key === "Escape") {
          key.preventDefault();
          key.stopPropagation();
          this.#after = "cell";
          this.dispatchEvent(new CustomEvent("iblayout", { bubbles: true, detail: { layout: "" } }));
        } else if (key.key === "Tab") {
          // Its own controls first, such as a date's picker button.
          const own = [...open.querySelectorAll<HTMLElement>("input:not([type=hidden]), select, textarea, button, a[href]")].filter((c) => !c.closest("[popover]") && !c.matches(":disabled") && c.checkVisibility());
          if (own[own.indexOf(target) + (key.shiftKey ? -1 : 1)]) return;
          key.preventDefault();
          key.stopPropagation();
          const cells = [...root.querySelectorAll<HTMLElement>("tbody td[data-ib-edit-col]")].filter((c) => c.closest("ib-table") === root);
          const next = cells[cells.indexOf(open) + (key.shiftKey ? -1 : 1)];
          this.#next = next ? this.#addressOf(next) : "";
          // Told to C# first, to wait for how the save ends: an invalid cell stays open.
          if (this.#next) this.#commit(field, "next");
          else this.#commit(field, "cell");
        } else if (key.key === "Enter" && target.matches("input, select")) {
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
        // All of them, if they come quicker than the field does.
        if (key.key.length === 1) this.#typed += key.key;
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

  #listOpen(target: Element): boolean {
    try {
      return !!target.closest("select")?.matches(":open");
    } catch {
      return false;
    }
  }

  // Not in a closed picker, which has controls of its own and can't take focus.
  // The first that shows, in the row, then in its detail, where a column hidden to fit has its field.
  #field(row: HTMLElement | null, selector: string): HTMLElement | null | undefined {
    const detail = row?.nextElementSibling;
    const scope = detail?.matches(".ib-grid-detail") ? [row!, detail] : row ? [row] : [];
    return scope.flatMap((part) => [...part.querySelectorAll<HTMLElement>(`:is(${selector}):not([popover] *)`)]).find((field) => field.checkVisibility());
  }

  async #open(root: Element, link: HTMLAnchorElement): Promise<void> {
    if (this.#opening) return;
    this.#opening = true;
    try {
      const page = new DOMParser().parseFromString(await (await fetch(link.href)).text(), "text/html");
      if (link.hasAttribute("data-ib-edit-row")) {
        this.#takeRow(root, page, link);
        history.pushState(null, "", link.href);
        return;
      }
      const named = `ib-dialog[data-query-key="${CSS.escape(link.dataset.ibEdit!)}"]`;
      const editor = page.querySelector(named);
      if (!editor) throw new Error();
      own(root, named)?.remove();
      history.pushState(null, "", link.href);
      // Where the server draws it, so that a later merge leaves it be.
      this.after(document.adoptNode(editor));
      // After its own close handler, which takes the key out of the address.
      editor.querySelector("dialog")?.addEventListener("close", () => setTimeout(() => editor.remove()));
    } catch {
      location.href = link.href;
    } finally {
      this.#opening = false;
    }
  }

  // A row starts or stops being edited: only it, and its form, come from the page the link leads to.
  #takeRow(root: Element, page: Document, link: Element): void {
    const fresh = page.querySelectorAll("ib-table")[[...document.querySelectorAll("ib-table")].indexOf(root)];
    const body = ":scope > .ib-table-scroll > table > tbody";
    const from = fresh?.querySelector(body);
    const into = root.querySelector(body);
    // Rows still streaming, or a row that's gone, whose message is only in the whole page.
    if (!from || !into || page.querySelector("blazor-ssr") || (!link.hasAttribute("data-ib-edit-cancel") && !from.querySelector(":scope > tr[data-ib-editing]"))) throw new Error();
    const rowIn = (rows: Element, key?: string) => key && rows.querySelector(`:scope > tr[data-ib-key="${CSS.escape(key)}"]`);
    // A row goes with its detail, which holds the fields of columns hidden to fit.
    const withDetail = (row: Element | null | undefined | "") => (row ? [row, ...(row.nextElementSibling?.matches(".ib-grid-detail") ? [row.nextElementSibling] : [])] : []);
    const swap = (old: Element, rows: Element[]) => {
      const [, detail] = withDetail(old);
      detail?.remove();
      old.replaceWith(...rows);
    };
    for (const row of into.querySelectorAll<HTMLElement>(":scope > tr[data-ib-editing]")) swap(row, withDetail(rowIn(from, row.dataset.ibKey)));
    for (const row of [...from.querySelectorAll<HTMLElement>(":scope > tr[data-ib-editing]")]) {
      const old = rowIn(into, row.dataset.ibKey);
      const rows = withDetail(row);
      // A new row goes where the page has it: a TreeList's under its parent, a grid's on top.
      // The nearest keyed row before it: a parent's open detail row has no key.
      let prior = row.previousElementSibling as HTMLElement | null;
      while (prior && !prior.dataset.ibKey) prior = prior.previousElementSibling as HTMLElement | null;
      const before = prior?.dataset.ibKey;
      const after = withDetail(rowIn(into, before)).at(-1);
      if (old) swap(old, rows);
      else if (after) after.after(...rows);
      else into.prepend(...rows);
    }
    // The rest of the rows' actions and the toolbar's Add are drawn for the old address.
    for (const cell of from.querySelectorAll<HTMLElement>(":scope > tr[data-ib-key] > .ib-grid-menu-cell")) {
      const old = rowIn(into, (cell.parentElement as HTMLElement).dataset.ibKey);
      if (old) old.querySelector(":scope > .ib-grid-menu-cell")?.replaceWith(cell);
    }
    for (const tool of [...root.querySelectorAll<HTMLAnchorElement>(".ib-grid-toolbar a[data-ib-edit]")].filter((t) => t.closest("ib-table") === root)) {
      const href = own(fresh, `.ib-grid-toolbar a[data-ib-edit="${CSS.escape(tool.dataset.ibEdit!)}"]`)?.getAttribute("href");
      if (href) tool.setAttribute("href", href);
    }
    const form = ":scope > form[data-ib-edit-for]";
    root.querySelector(form)?.remove();
    const next = fresh.querySelector(form);
    if (next) this.after(next);
    // The height held for the page starts again with the edit, which can open a row's detail.
    const hold = fresh.getAttribute("data-ib-hold");
    if (hold === null) root.removeAttribute("data-ib-hold");
    else root.setAttribute("data-ib-hold", hold);
  }
}

defineElement("ib-grid-edit", GridEdit);
