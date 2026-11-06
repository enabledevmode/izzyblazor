/**
 * The rich text editor's Markdown shortcuts, loaded (as elements/parts/rich-text-markdown.js) by
 * editors that have them on, as they set up: typed Markdown at a line's start (## , - , 1. , > ,
 * three backticks, and --- then Enter) or in a line (**bold**, *italic*, `code`) becomes formats.
 */

/** What the shortcuts need from their editor. */
export interface MarkdownEditor {
  /** The highest heading level (TopHeading). */
  top: number;
  /** Whether a heading level is one the editor offers. */
  offers(level: number): boolean;
  /** The nearest element around the selection matching the selector, inside the text. */
  around(selector: string): Element | null;
  changed(): void;
}

/** Typed Markdown becomes formats: a line's start (## , - , 1. , > , ```) or **bold**, *italic*, `code`. */
export function markdown(editor: MarkdownEditor, data: string | null): void {
  const selection = getSelection();
  const node = selection?.anchorNode;
  if (!selection?.isCollapsed || !node || node.nodeType !== Node.TEXT_NODE || editor.around("pre")) return;
  const offset = selection.anchorOffset;
  const before = (node.textContent ?? "").slice(0, offset);

  if (data === " ") {
    const block = editor.around("p,div");
    const marker = /^(#{1,6}|[-*]|1[.)]|>|```)[ \u00A0]$/.exec(before)?.[1];
    if (!block || !marker || editor.around("li,blockquote")) return;
    // # is the top heading level, ## the one below it, and so on.
    if (marker.startsWith("#") && !editor.offers(editor.top + marker.length - 1)) return;
    // Only at the very start of the line.
    const lead = document.createRange();
    lead.setStart(block, 0);
    lead.setEnd(node, 0);
    if (lead.toString() !== "") return;
    const typed = document.createRange();
    typed.setStart(node, 0);
    typed.setEnd(node, offset);
    selection.removeAllRanges();
    selection.addRange(typed);
    document.execCommand("delete");
    if (marker.startsWith("#")) document.execCommand("formatBlock", false, `<h${editor.top + marker.length - 1}>`);
    else if (marker === "-" || marker === "*") document.execCommand("insertUnorderedList");
    else if (marker === ">") document.execCommand("formatBlock", false, "<blockquote>");
    else if (marker === "```") document.execCommand("formatBlock", false, "<pre>");
    else document.execCommand("insertOrderedList");
    return;
  }

  if (data !== "*" && data !== "`" || editor.around("code")) return;
  const forms: [RegExp, string][] = [
    [/\*\*([^*\s](?:[^*]*[^*\s])?)\*\*$/, "strong"],
    [/(?:^|[^*\w])\*([^*\s](?:[^*]*[^*\s])?)\*$/, "em"],
    [/`([^`]+)`$/, "code"],
  ];
  for (const [pattern, name] of forms) {
    const match = pattern.exec(before);
    if (!match) continue;
    const marks = name === "strong" ? 2 : 1;
    const start = offset - match[1].length - 2 * marks;
    const range = document.createRange();
    range.setStart(node, start);
    range.setEnd(node, offset);
    range.deleteContents();
    const element = document.createElement(name);
    element.textContent = match[1];
    const after = document.createTextNode("\u200B");
    range.insertNode(after);
    range.insertNode(element);
    selection.collapse(after, 1);
    return;
  }
}

/** "---" alone on a line, then Enter, becomes a horizontal line. */
export function rule(editor: MarkdownEditor): boolean {
  const block = editor.around("p,div");
  if (!block || (block.textContent ?? "").trim() !== "---" || editor.around("li,blockquote,pre")) return false;
  const line = document.createElement("hr");
  const next = document.createElement("p");
  next.append(document.createElement("br"));
  block.replaceWith(line, next);
  getSelection()?.collapse(next, 0);
  editor.changed();
  return true;
}

