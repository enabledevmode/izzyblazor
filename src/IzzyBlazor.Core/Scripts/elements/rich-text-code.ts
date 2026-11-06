/**
 * The rich text editor's code block language, loaded (as elements/parts/rich-text-code.js) by
 * editors with the code block tool: while the caret is in a code block, a button at its top end
 * corner shows its language and opens a menu of them. The language is the code's class
 * (language-csharp), as Markdown, Prism and highlight.js write it; RichText highlights by it.
 */

/** What the language button needs from its editor. */
export interface CodeEditor {
  host: HTMLElement;
  /** The text, looked up each time: Blazor can replace it. */
  surface(): HTMLElement | null;
  /** The nearest element around the selection matching the selector, inside the text. */
  around(selector: string): Element | null;
  changed(): void;
}

export function attach(editor: CodeEditor): void {
  const { host } = editor;
  const box = host.querySelector<HTMLElement>(":scope > [data-ib-code-language]");
  if (!box || box.dataset.ibReady !== undefined) return;
  box.dataset.ibReady = "";
  const button = box.querySelector<HTMLElement>("[data-ib-code-button]")!;
  let block: HTMLElement | null = null;

  const languageOf = (pre: HTMLElement) =>
    [...(pre.querySelector(":scope > code")?.classList ?? [])].find((c) => c.startsWith("language-"))?.slice(9) ?? "";

  /** Shows the button at the caret's code block, with its language checked; hides it elsewhere. */
  const place = () => {
    const pre = editor.around("pre") as HTMLElement | null;
    if (pre) block = pre;
    if (!pre) {
      if (!box.contains(document.activeElement)) box.hidden = true;
      return;
    }
    box.hidden = false;
    const at = pre.getBoundingClientRect();
    const frame = host.getBoundingClientRect();
    box.style.top = `${Math.round(at.top - frame.top + 4)}px`;
    box.style.insetInlineEnd = `${Math.round(frame.right - at.right + 4)}px`;
    const language = languageOf(pre);
    for (const item of box.querySelectorAll<HTMLElement>("[data-ib-code-lang]")) {
      const on = item.dataset.ibCodeLang === language;
      if (item.hasAttribute("aria-checked")) item.setAttribute("aria-checked", String(on));
      if (on) {
        button.setAttribute("aria-label", item.dataset.ibLabel ?? "");
        button.querySelector(".ib-rich-text-menu-name")!.textContent = item.textContent!.trim();
      }
    }
  };

  document.addEventListener("selectionchange", () => host.isConnected && place());
  addEventListener("resize", () => !box.hidden && place());

  // The button keeps the caret where it is: a press doesn't take focus from the text.
  box.addEventListener("mousedown", (event) => {
    if ((event.target as Element).closest("[data-ib-code-button]")) event.preventDefault();
  });

  box.addEventListener("click", (event) => {
    const item = (event.target as Element).closest<HTMLElement>("[data-ib-code-lang]");
    if (!item || !block || !editor.surface()?.contains(block)) return;
    // The language goes on the code inside the block; one with no code element gets one.
    let code = block.querySelector<HTMLElement>(":scope > code");
    if (!code) {
      code = document.createElement("code");
      code.append(...block.childNodes);
      block.append(code);
    }
    for (const c of [...code.classList]) if (c.startsWith("language-")) code.classList.remove(c);
    if (item.dataset.ibCodeLang) code.classList.add(`language-${item.dataset.ibCodeLang}`);
    if (!code.classList.length) code.removeAttribute("class");
    editor.changed();
    // Back in the block, after the menu closes.
    const target = block;
    setTimeout(() => {
      editor.surface()?.focus({ preventScroll: true });
      const range = document.createRange();
      range.selectNodeContents(target.querySelector(":scope > code") ?? target);
      range.collapse(false);
      getSelection()?.removeAllRanges();
      getSelection()?.addRange(range);
    }, 0);
  });

  host.addEventListener("focusout", (event) => {
    if (!host.contains(event.relatedTarget as Node)) box.hidden = true;
  });
}
