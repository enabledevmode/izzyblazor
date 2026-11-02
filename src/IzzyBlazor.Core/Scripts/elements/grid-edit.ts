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
  // The row being edited in place, whose first field gets focus as it arrives.
  #editing: HTMLElement | null = null;

  protected setup(signal: AbortSignal): void {
    const root = this.closest("ib-table");
    if (!root) return;
    root.addEventListener("submit", (event) => {
      const form = event.target as Element;
      this.#saving = (form.closest("dialog") ?? form).getAttribute("data-ib-edit-for");
    }, { signal });
    const check = () => this.#check(root);
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
    const editing = root.querySelector<HTMLElement>(":scope > .ib-table-scroll > table > tbody > tr[data-ib-editing]");
    if (editing !== this.#editing) {
      this.#editing = editing;
      (this.#field(editing, "[autofocus]") ?? this.#field(editing, "input, select, textarea"))?.focus();
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
    // The row's first action, or for a row deleted or added, the first there is.
    (root.querySelector<HTMLElement>(`tr[data-ib-key="${CSS.escape(this.#saving)}"] [data-ib-edit]`) ?? root.querySelector<HTMLElement>("[data-ib-edit]"))?.focus();
    this.#saving = null;
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
