/**
 * The rich text editor's task lists, loaded (as elements/parts/rich-text-task.js) by editors with
 * the task list tool or a task in their text. A task is a list item whose first child is a
 * checkbox, as GitHub-flavored Markdown renders one. This part: the tool (tasks on, or off again),
 * ticking a box with a click or Ctrl+Enter, a new task after Enter in one (Enter in an empty one
 * ends the list), the caret kept after a box, and each box named by its task's text.
 */

/** What task lists need from their editor. */
export interface TaskEditor {
  host: HTMLElement;
  /** The text (Blazor may replace it). */
  surface(): HTMLElement | null;
  /** The nearest element around the selection matching the selector, inside the text. */
  around(selector: string): Element | null;
  changed(): void;
}

const attached = new WeakSet<HTMLElement>();

/** A task's checkbox; null when the item isn't a task. */
function boxOf(item: Element | null | undefined): HTMLInputElement | null {
  return item?.querySelector<HTMLInputElement>(":scope > input[type=checkbox]:first-child") ?? null;
}

/** Makes an item a task, not done. An empty item's line break (a browser's placeholder) goes, or it stays after what's typed. */
function addBox(item: Element): void {
  const box = document.createElement("input");
  box.type = "checkbox";
  box.tabIndex = -1;
  if (!item.textContent?.trim()) item.querySelector(":scope > br")?.remove();
  item.prepend(box);
}

/** A task's own text (not its sublists'), on one line. */
function text(item: Element): string {
  let out = "";
  for (const node of item.childNodes) {
    if (node instanceof Element && /^(UL|OL)$/.test(node.tagName)) continue;
    out += " " + (node.textContent ?? "");
  }
  return out.replace(/[\s\u200B]+/g, " ").trim();
}

/** Puts a collapsed caret that's before an item's box just after it, where the text starts. */
function afterBox(editor: TaskEditor): void {
  const selection = getSelection();
  const box = boxOf(editor.around("li"));
  if (!selection?.isCollapsed || !selection.rangeCount || !box) return;
  const after = document.createRange();
  after.setStartAfter(box);
  if (selection.getRangeAt(0).compareBoundaryPoints(Range.START_TO_START, after) < 0) selection.collapse(after.startContainer, after.startOffset);
}

/** The list items the selection is in: in their own text, not only in a sublist of theirs. */
function selected(editor: TaskEditor): Element[] {
  const selection = getSelection();
  const surface = editor.surface();
  if (!selection?.rangeCount || !surface) return [];
  const range = selection.getRangeAt(0);
  return [...surface.querySelectorAll("li")].filter((item) =>
    [...item.childNodes].some((node) => !(node instanceof Element && /^(UL|OL)$/.test(node.tagName)) && range.intersectsNode(node))
  );
}

/** Boxes work while the text is editable; each is named by its task's text. */
function tidy(editor: TaskEditor): void {
  const surface = editor.surface();
  if (!surface) return;
  for (const item of surface.querySelectorAll("li")) {
    const box = boxOf(item);
    if (!box) continue;
    box.disabled = !surface.isContentEditable;
    box.tabIndex = -1;
    const name = text(item);
    if (name) box.setAttribute("aria-label", name);
    else box.removeAttribute("aria-label");
  }
}

function toggleBox(editor: TaskEditor, box: HTMLInputElement): void {
  box.toggleAttribute("checked");
  box.checked = box.hasAttribute("checked");
  editor.changed();
}

/** The task list tool: the selected items become tasks, or plain items again when they all are. */
export function toggle(editor: TaskEditor): void {
  let items = selected(editor);
  if (items.length && items.every(boxOf)) {
    for (const item of items) boxOf(item)!.remove();
  } else {
    if (!items.length || items[0].parentElement?.localName !== "ul") {
      document.execCommand("insertUnorderedList");
      items = selected(editor);
    }
    for (const item of items) if (!boxOf(item)) addBox(item);
    afterBox(editor);
  }
  tidy(editor);
  editor.changed();
}

/** Starts task lists in an editor: clicks, keys and names. Once per editor. */
export function attach(editor: TaskEditor): void {
  const host = editor.host;
  if (attached.has(host)) return;
  attached.add(host);
  tidy(editor);

  let queued = false;
  new MutationObserver(() => {
    if (queued) return;
    queued = true;
    requestAnimationFrame(() => {
      queued = false;
      tidy(editor);
    });
  }).observe(host, { childList: true, characterData: true, subtree: true });

  // A press on a box keeps the caret in the text; the click ticks it (the editor's own change, so
  // undo knows it).
  host.addEventListener("mousedown", (event) => {
    if ((event.target as Element).matches?.("li > input[type=checkbox]")) event.preventDefault();
  });
  host.addEventListener("click", (event) => {
    const box = event.target as HTMLInputElement;
    if (!box.matches?.("li > input[type=checkbox]") || !editor.surface()?.contains(box)) return;
    // Cancelled, so every browser leaves it to the editor (Firefox ignores clicks in editable
    // text); ticked after the click, since cancelling one puts the box back as it was.
    event.preventDefault();
    if (editor.surface()!.isContentEditable && boxOf(box.parentElement) === box) setTimeout(() => toggleBox(editor, box));
  });

  host.addEventListener("keydown", (event) => {
    const surface = editor.surface();
    if (event.target !== surface || !surface.isContentEditable || event.key !== "Enter" || event.shiftKey || event.altKey) return;
    const item = editor.around("li");
    const box = boxOf(item);
    if (!item || !box) return;
    if (event.ctrlKey || event.metaKey) {
      event.preventDefault();
      toggleBox(editor, box);
    } else if (!text(item) && !item.querySelector("img")) {
      // Enter in an empty task ends the list there, as in an empty bullet.
      event.preventDefault();
      box.remove();
      const list = item.parentElement!;
      if (list.parentElement?.closest("li")) {
        // In a sublist it moves out a level, as a plain item.
        document.execCommand("outdent");
      } else {
        // Done by hand: a browser's insertUnorderedList leaves a bare line break, and the next
        // list typed there ends up inside a paragraph.
        const line = document.createElement("p");
        line.append(document.createElement("br"));
        if (item.nextElementSibling) {
          const rest = list.cloneNode(false) as Element;
          while (item.nextSibling) rest.append(item.nextSibling);
          list.after(rest);
        }
        list.after(line);
        item.remove();
        if (!list.querySelector("li")) list.remove();
        getSelection()?.collapse(line, 0);
      }
      editor.changed();
    }
  });

  // Enter in a task: the new item is a task too, not done.
  host.addEventListener("input", (event) => {
    if ((event as InputEvent).inputType !== "insertParagraph") return;
    const item = editor.around("li");
    if (!item || boxOf(item) || !boxOf(item.previousElementSibling)) return;
    addBox(item);
    afterBox(editor);
    editor.changed();
  });

  document.addEventListener("selectionchange", () => {
    if (editor.surface()?.contains(getSelection()?.anchorNode ?? null)) afterBox(editor);
  });
}
