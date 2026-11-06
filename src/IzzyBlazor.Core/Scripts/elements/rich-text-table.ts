/**
 * The rich text editor's tables, loaded (as elements/parts/rich-text-table.js) by editors with the
 * table tool or a table in their text: the insert dialog (rows, columns, header row), adding and
 * deleting rows and columns, the header row, Tab and Shift+Tab between cells (Tab in the last cell
 * leaves the editor, as everywhere), and the table menu's items disabled outside a table. A header
 * row's cells are column headers (th scope="col").
 */

/** What tables need from their editor. */
export interface TableEditor {
  host: HTMLElement;
  surface: HTMLElement;
  /** The nearest element around the selection matching the selector, inside the text. */
  around(selector: string): Element | null;
  changed(): void;
}

let saved: Range | null = null;

function cell(tag: string): HTMLTableCellElement {
  const made = document.createElement(tag) as HTMLTableCellElement;
  made.append(document.createElement("br"));
  if (tag === "th") made.scope = "col";
  return made;
}

/** A cell made the other kind, its content moved over. */
function swap(old: HTMLTableCellElement, tag: string): HTMLTableCellElement {
  const made = document.createElement(tag) as HTMLTableCellElement;
  made.append(...old.childNodes);
  if (tag === "th") made.scope = "col";
  old.replaceWith(made);
  return made;
}

/** Puts the caret in a cell: at its end, or around its content. */
function caret(target: Element, select = false): void {
  const range = document.createRange();
  range.selectNodeContents(target);
  if (!select) range.collapse(false);
  getSelection()?.removeAllRanges();
  getSelection()?.addRange(range);
}

function row(count: number, tag: string): HTMLTableRowElement {
  const made = document.createElement("tr");
  for (let i = 0; i < count; i++) made.append(cell(tag));
  return made;
}

function dialog(editor: TableEditor): HTMLDialogElement | null {
  return editor.host.querySelector<HTMLDialogElement>(":scope > .ib-rich-text-table");
}

function input(editor: TableEditor, name: string): HTMLInputElement {
  return dialog(editor)!.querySelector<HTMLInputElement>(`[data-ib-table="${name}"]`)!;
}

function restore(editor: TableEditor): void {
  editor.surface.focus({ preventScroll: true });
  if (saved && editor.surface.contains(saved.commonAncestorContainer)) {
    getSelection()?.removeAllRanges();
    getSelection()?.addRange(saved);
  }
}

function insert(editor: TableEditor): void {
  const box = dialog(editor)!;
  const columns = Math.min(12, Math.max(1, Math.round(Number(input(editor, "columns").value)) || 3));
  const rows = Math.min(50, Math.max(1, Math.round(Number(input(editor, "rows").value)) || 3));
  const header = input(editor, "header").checked;
  box.close("ok");
  restore(editor);
  const head = header ? `<thead><tr>${'<th scope="col"><br></th>'.repeat(columns)}</tr></thead>` : "";
  const body = `<tr>${"<td><br></td>".repeat(columns)}</tr>`.repeat(Math.max(1, rows - (header ? 1 : 0)));
  // A paragraph after it, so there's somewhere to type below the table.
  document.execCommand("insertHTML", false, `<table data-ib-new>${head}<tbody>${body}</tbody></table><p><br></p>`);
  const table = editor.surface.querySelector("table[data-ib-new]");
  table?.removeAttribute("data-ib-new");
  const first = table?.querySelector("th,td");
  if (first) caret(first);
  editor.changed();
}

/** Runs a table menu item: insert (the dialog), or a change to the table at the caret. */
export function run(editor: TableEditor, action: string): void {
  if (action === "insert") {
    const box = dialog(editor);
    const selection = getSelection();
    if (!box || !selection?.rangeCount) return;
    saved = selection.getRangeAt(0).cloneRange();
    if (box.dataset.ibReady === undefined) {
      box.dataset.ibReady = "";
      box.addEventListener("click", (event) => {
        const what = (event.target as Element).closest<HTMLElement>("[data-ib-table]")?.dataset.ibTable;
        if (what === "apply") insert(editor);
        else if (what === "cancel") box.close();
      });
      box.addEventListener("keydown", (event) => {
        if (event.key === "Enter" && (event.target as Element).matches("input[type=number]")) {
          event.preventDefault();
          insert(editor);
        }
      });
      box.addEventListener("close", () => box.returnValue !== "ok" && restore(editor));
    }
    box.returnValue = "";
    box.showModal();
    input(editor, "rows").focus();
    return;
  }

  const at = editor.around("td,th") as HTMLTableCellElement | null;
  const tr = at?.parentElement as HTMLTableRowElement | null;
  const table = at?.closest("table");
  if (!at || !tr || !table) return;
  const index = at.cellIndex;
  const head = tr.parentElement?.localName === "thead";
  let next: Element | null = null;
  switch (action) {
    case "row-above":
    case "row-below": {
      const made = row(tr.cells.length, "td");
      // Rows go in the body: one beside the header row goes at the body's top.
      if (head) (table.tBodies[0] ?? table.appendChild(document.createElement("tbody"))).prepend(made);
      else if (action === "row-above") tr.before(made);
      else tr.after(made);
      next = made.cells[index] ?? made.cells[0];
      break;
    }
    case "column-before":
    case "column-after":
      for (const each of table.rows) {
        const ref = each.cells[Math.min(index, each.cells.length - 1)];
        const made = cell(each.parentElement?.localName === "thead" ? "th" : "td");
        if (!ref) each.append(made);
        else if (action === "column-before") ref.before(made);
        else ref.after(made);
        if (each === tr) next = made;
      }
      break;
    case "header":
      if (table.tHead) {
        // The header row goes back to being the body's first row.
        const rows = [...table.tHead.rows];
        const body = table.tBodies[0] ?? table.appendChild(document.createElement("tbody"));
        body.prepend(...rows);
        for (const each of rows) for (const c of [...each.cells]) swap(c, "td");
        table.tHead.remove();
      } else {
        const first = table.tBodies[0]?.rows[0];
        if (first) {
          const thead = table.createTHead();
          thead.append(first);
          for (const c of [...first.cells]) swap(c, "th");
        }
      }
      next = table.rows[tr.rowIndex]?.cells[index] ?? null;
      break;
    case "delete-row": {
      const near = table.rows[tr.rowIndex + 1] ?? table.rows[tr.rowIndex - 1];
      tr.remove();
      for (const section of [table.tHead, ...table.tBodies]) if (section && !section.rows.length) section.remove();
      next = near?.cells[Math.min(index, near.cells.length - 1)] ?? null;
      break;
    }
    case "delete-column":
      for (const each of [...table.rows]) each.cells[Math.min(index, each.cells.length - 1)]?.remove();
      next = tr.cells[Math.max(0, index - 1)] ?? null;
      break;
  }
  if (action === "delete-table" || !table.querySelector("td,th")) {
    next = table.nextElementSibling ?? table.previousElementSibling;
    table.remove();
  }
  editor.surface.focus({ preventScroll: true });
  if (next && editor.surface.contains(next)) caret(next);
  editor.changed();
}

/** Starts the part on an editor: Tab between cells, and the menu's table items off outside a table. */
export function attach(editor: TableEditor): void {
  if (editor.host.dataset.ibTables !== undefined) return;
  editor.host.dataset.ibTables = "";
  editor.host.addEventListener("keydown", (event) => {
    if (event.key !== "Tab" || event.ctrlKey || event.altKey || event.metaKey || event.target !== editor.surface) return;
    const at = editor.around("td,th");
    const table = at?.closest("table");
    if (!at || !table) return;
    const cells = [...table.querySelectorAll("th,td")].filter((c) => c.closest("table") === table);
    const next = cells[cells.indexOf(at) + (event.shiftKey ? -1 : 1)];
    // From the first or last cell, Tab goes on as anywhere else: out of the editor.
    if (!next) return;
    event.preventDefault();
    caret(next, true);
  });
  document.addEventListener("selectionchange", () => {
    if (!editor.host.isConnected) return;
    const off = !editor.around("td,th");
    for (const item of editor.host.querySelectorAll<HTMLElement>("[data-ib-in-table]")) {
      item.toggleAttribute("disabled", off);
      item.setAttribute("aria-disabled", String(off));
    }
  });
}
