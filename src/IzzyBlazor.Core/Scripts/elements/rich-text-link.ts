/**
 * The rich text editor's link dialog, loaded (as elements/parts/rich-text-link.js) the first time
 * someone opens it: the link tool or Ctrl+K. It adds, changes or removes the link at the
 * selection; addresses other than web, mail, phone or relative are refused, as the sanitizer
 * would drop them.
 */

/** What the dialog needs from its editor. */
export interface LinkEditor {
  surface: HTMLElement;
  dialog: HTMLElement;
  /** The nearest element around the selection matching the selector, inside the text. */
  around(selector: string): Element | null;
  /** Writes the value after a change, and shows the selection's formats on the tools. */
  changed(): void;
}

// The dialog's state while it's open; one dialog is open at a time.
let link: HTMLAnchorElement | null = null;
let collapsed = true;
let saved: Range | null = null;

function part<T extends HTMLElement>(editor: LinkEditor, name: string): T {
  return editor.dialog.querySelector<T>(`[data-ib-link="${name}"]`)!;
}

function escape(text: string, attribute = false): string {
  let out = text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
  return attribute ? out.replace(/"/g, "&quot;") : out;
}

function safeUrl(value: string): string | null {
  // eslint-disable-next-line no-control-regex
  const url = value.replace(/[\u0000-\u001F\u007F]/g, "").trim();
  const scheme = /^([a-z][a-z0-9+.-]*):/i.exec(url)?.[1].toLowerCase();
  return !url ? null : !scheme || ["http", "https", "mailto", "tel"].includes(scheme) ? url : null;
}

/** Puts back the selection the dialog was opened on: focus in the dialog collapsed it. */
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
  editor.dialog.hidePopover();
  restore(editor);
  const current = link && editor.surface.contains(link) ? link : null;
  if (current) {
    current.setAttribute("href", url);
    if (blank) {
      current.target = "_blank";
      current.rel = "noopener noreferrer";
    } else {
      current.removeAttribute("target");
      current.removeAttribute("rel");
    }
    if (text && text !== current.textContent) current.textContent = text;
  } else if (collapsed) {
    const target = blank ? ' target="_blank" rel="noopener noreferrer"' : "";
    document.execCommand("insertHTML", false, `<a href="${escape(url, true)}"${target}>${escape(text || url)}</a>`);
  } else {
    document.execCommand("createLink", false, url);
    const made = editor.around("a") as HTMLAnchorElement | null;
    if (made && blank) {
      made.target = "_blank";
      made.rel = "noopener noreferrer";
    }
  }
  editor.changed();
}

function remove(editor: LinkEditor): void {
  editor.dialog.hidePopover();
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

/** Opens the dialog under the selection, filled in from the link there, if any. */
export function openLink(editor: LinkEditor): void {
  const { dialog, surface } = editor;
  const selection = getSelection();
  if (!selection?.rangeCount) return;
  if (!dialog.dataset.ibReady) {
    dialog.dataset.ibReady = "";
    dialog.addEventListener("click", (event) => {
      const action = (event.target as Element).closest<HTMLElement>("[data-ib-link=apply],[data-ib-link=remove]")?.dataset.ibLink;
      if (action === "apply") apply(editor);
      else if (action === "remove") remove(editor);
    });
    dialog.addEventListener("keydown", (event) => {
      if (event.key === "Enter" && (event.target as Element).matches("input[type=url],input[type=text]")) {
        event.preventDefault();
        apply(editor);
      } else if (event.key === "Escape") {
        // The popover closes itself; the text takes focus back after, with its selection.
        setTimeout(() => restore(editor), 0);
      }
    });
  }

  link = editor.around("a") as HTMLAnchorElement | null;
  collapsed = selection.isCollapsed;
  saved = selection.getRangeAt(0).cloneRange();
  const href = part<HTMLInputElement>(editor, "href");
  href.value = link?.getAttribute("href") ?? "";
  href.removeAttribute("aria-invalid");
  part<HTMLInputElement>(editor, "text").value = link?.textContent ?? "";
  part<HTMLInputElement>(editor, "tab").checked = link?.target === "_blank";
  // Text only for a new link with nothing selected, or to change a link's own text.
  part(editor, "text-field").hidden = !link && !collapsed;
  part(editor, "remove").hidden = !link;

  const rect = saved.getBoundingClientRect();
  const anchor = rect.height ? rect : surface.getBoundingClientRect();
  dialog.style.position = "fixed";
  dialog.style.margin = "0";
  dialog.style.top = `${Math.round(anchor.bottom + 6)}px`;
  dialog.showPopover();
  dialog.style.left = `${Math.round(Math.max(8, Math.min(anchor.left, innerWidth - dialog.offsetWidth - 8)))}px`;
  href.focus();
}
