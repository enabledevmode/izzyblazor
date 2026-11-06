/**
 * The rich text editor's link dialog, loaded (as elements/parts/rich-text-link.js) the first time
 * someone opens it: the link tool or Ctrl+K. A modal, as the library's Dialog: it adds, changes or
 * removes the link at the selection, its Text the selected text (changed, it replaces it).
 * Addresses other than web, mail, phone or relative are refused, as the sanitizer would drop them.
 */

/** What the dialog needs from its editor. */
export interface LinkEditor {
  surface: HTMLElement;
  dialog: HTMLDialogElement;
  /** The nearest element around the selection matching the selector, inside the text. */
  around(selector: string): Element | null;
  /** Writes the value after a change, and shows the selection's formats on the tools. */
  changed(): void;
}

// The dialog's state while it's open; one dialog is open at a time.
let link: HTMLAnchorElement | null = null;
let saved: Range | null = null;
let selected = "";

function part<T extends HTMLElement>(editor: LinkEditor, name: string): T {
  return editor.dialog.querySelector<T>(`[data-ib-link="${name}"]`)!;
}

function escape(text: string, attribute = false): string {
  const out = text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
  return attribute ? out.replace(/"/g, "&quot;") : out;
}

function safeUrl(value: string): string | null {
  // eslint-disable-next-line no-control-regex
  const url = value.replace(/[\u0000-\u001F\u007F]/g, "").trim();
  const scheme = /^([a-z][a-z0-9+.-]*):/i.exec(url)?.[1].toLowerCase();
  return !url ? null : !scheme || ["http", "https", "mailto", "tel"].includes(scheme) ? url : null;
}

/** Puts back the selection the dialog was opened on. */
function restore(editor: LinkEditor): void {
  editor.surface.focus({ preventScroll: true });
  if (saved && editor.surface.contains(saved.commonAncestorContainer)) {
    getSelection()?.removeAllRanges();
    getSelection()?.addRange(saved);
  }
}

function apply(editor: LinkEditor): void {
  const href = part<HTMLInputElement>(editor, "href");
  const url = safeUrl(href.value);
  if (!url) {
    href.setAttribute("aria-invalid", "true");
    href.focus();
    return;
  }
  const text = part<HTMLInputElement>(editor, "text").value.trim();
  const blank = part<HTMLInputElement>(editor, "tab").checked;
  editor.dialog.close("ok");
  restore(editor);
  const target = blank ? ' target="_blank" rel="noopener noreferrer"' : "";
  if (link && editor.surface.contains(link)) {
    link.setAttribute("href", url);
    if (blank) {
      link.target = "_blank";
      link.rel = "noopener noreferrer";
    } else {
      link.removeAttribute("target");
      link.removeAttribute("rel");
    }
    if (text && text !== link.textContent) link.textContent = text;
  } else if (selected && (!text || text === selected.trim())) {
    // The selected text becomes the link, keeping its formats.
    document.execCommand("createLink", false, url);
    const made = editor.around("a") as HTMLAnchorElement | null;
    if (made && blank) {
      made.target = "_blank";
      made.rel = "noopener noreferrer";
    }
  } else {
    // New text, or none selected: the link's text goes in (in place of the selection).
    document.execCommand("insertHTML", false, `<a href="${escape(url, true)}"${target}>${escape(text || url)}</a>`);
  }
  editor.changed();
}

function setup(editor: LinkEditor): void {
  const { dialog } = editor;
  if (dialog.dataset.ibReady !== undefined) return;
  dialog.dataset.ibReady = "";
  dialog.addEventListener("click", (event) => {
    const action = (event.target as Element).closest<HTMLElement>("[data-ib-link]")?.dataset.ibLink;
    if (action === "apply") apply(editor);
    else if (action === "cancel") dialog.close();
    else if (action === "remove") {
      dialog.close("ok");
      restore(editor);
      if (link && editor.surface.contains(link)) {
        const range = document.createRange();
        range.selectNodeContents(link);
        getSelection()?.removeAllRanges();
        getSelection()?.addRange(range);
        document.execCommand("unlink");
      }
      editor.changed();
    }
  });
  dialog.addEventListener("keydown", (event) => {
    if (event.key === "Enter" && (event.target as Element).matches("input[type=url],input[type=text]")) {
      event.preventDefault();
      apply(editor);
    }
  });
  // Escape or Cancel: back to the text, with its selection.
  dialog.addEventListener("close", () => {
    if (dialog.returnValue !== "ok") restore(editor);
  });
}

/** Opens the dialog, filled in from the link at the selection, or for the selected text. */
export function openLink(editor: LinkEditor): void {
  const selection = getSelection();
  if (!selection?.rangeCount) return;
  setup(editor);
  link = editor.around("a") as HTMLAnchorElement | null;
  saved = selection.getRangeAt(0).cloneRange();
  selected = link ? "" : selection.toString();
  // Text shows trimmed; left as it is, the selection (spaces and formats too) becomes the link.
  const title = part(editor, "title");
  title.textContent = (link ? title.dataset.ibEdit : title.dataset.ibAdd) ?? "";
  const href = part<HTMLInputElement>(editor, "href");
  href.value = link?.getAttribute("href") ?? "";
  href.removeAttribute("aria-invalid");
  part<HTMLInputElement>(editor, "text").value = link?.textContent ?? selected.trim();
  part<HTMLInputElement>(editor, "tab").checked = link?.target === "_blank";
  part(editor, "remove").hidden = !link;
  editor.dialog.returnValue = "";
  editor.dialog.showModal();
  href.focus();
}
