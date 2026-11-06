/**
 * The rich text editor's images, loaded (as elements/parts/rich-text-image.js) the first time an
 * image is added: the image dialog (address, description or decorative, upload), and images
 * pasted or dropped. An upload posts to the editor's ImageUploadUrl with the form's antiforgery
 * token and takes the URL it answers with; without one, AllowDataImages embeds the image as a
 * data: URL. Every new image opens the dialog, so it gets a description.
 */

import { place } from "./rich-text-place";

/** What the image dialog needs from its editor. */
export interface ImageEditor {
  host: HTMLElement;
  surface: HTMLElement;
  dialog: HTMLElement;
  changed(): void;
}

const types = ["image/png", "image/jpeg", "image/gif", "image/webp", "image/avif"];

// The dialog's state while it's open; one dialog is open at a time.
let image: HTMLImageElement | null = null;
let saved: Range | null = null;

function part<T extends HTMLElement>(editor: ImageEditor, name: string): T {
  return editor.dialog.querySelector<T>(`[data-ib-image="${name}"]`)!;
}

function escape(text: string): string {
  return text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}

function safeSrc(editor: ImageEditor, value: string): string | null {
  // eslint-disable-next-line no-control-regex
  const url = value.replace(/[\u0000-\u001F\u007F]/g, "").trim();
  const scheme = /^([a-z][a-z0-9+.-]*):/i.exec(url)?.[1].toLowerCase();
  if (!url) return null;
  if (!scheme || scheme === "http" || scheme === "https") return url;
  const data = /^data:([a-z/]+)[;,]/i.exec(url)?.[1].toLowerCase();
  return data && types.includes(data) && editor.host.hasAttribute("data-ib-data-images") ? url : null;
}

function error(editor: ImageEditor, message: string | null): void {
  const box = part(editor, "error");
  box.textContent = message ?? "";
  box.hidden = !message;
}

/** Uploads an image, or reads it as a data: URL; the URL to use, or an error message. */
async function source(editor: ImageEditor, file: File): Promise<{ url?: string; error?: string }> {
  const { ibImageUpload: endpoint, ibImageMax: max, ibImageTooLarge: tooLarge, ibImageFailed: failed } = editor.host.dataset;
  if (!types.includes(file.type)) return {};
  if (max && file.size > Number(max)) return { error: tooLarge };
  if (!endpoint) {
    if (!editor.host.hasAttribute("data-ib-data-images")) return {};
    return new Promise((resolve) => {
      const reader = new FileReader();
      reader.onload = () => resolve({ url: reader.result as string });
      reader.onerror = () => resolve({ error: failed });
      reader.readAsDataURL(file);
    });
  }
  try {
    const body = new FormData();
    body.append("file", file);
    // The form's antiforgery token, as a field and a header, so a protected endpoint takes it.
    const token = editor.host.closest("form")?.querySelector<HTMLInputElement>("input[name=__RequestVerificationToken]")?.value;
    if (token) body.append("__RequestVerificationToken", token);
    const response = await fetch(endpoint, { method: "POST", body, credentials: "same-origin", headers: token ? { RequestVerificationToken: token } : {} });
    if (!response.ok) return { error: failed };
    const url = (response.headers.get("content-type") ?? "").includes("json")
      ? ((await response.json()) as { url?: string }).url
      : (await response.text()).trim();
    return url ? { url } : { error: failed };
  } catch {
    return { error: failed };
  }
}

function setup(editor: ImageEditor): void {
  const { dialog } = editor;
  if (dialog.dataset.ibReady !== undefined) return;
  dialog.dataset.ibReady = "";
  dialog.addEventListener("click", (event) => {
    const action = (event.target as Element).closest<HTMLElement>("[data-ib-image=apply],[data-ib-image=remove]")?.dataset.ibImage;
    if (action === "apply") apply(editor);
    else if (action === "remove") remove(editor);
  });
  dialog.addEventListener("keydown", (event) => {
    if (event.key === "Enter" && (event.target as Element).matches("input[type=url],input[type=text]")) {
      event.preventDefault();
      apply(editor);
    } else if (event.key === "Escape") {
      setTimeout(() => restore(editor), 0);
    }
  });
  dialog.addEventListener("change", async (event) => {
    const input = event.target as HTMLInputElement;
    if (input.dataset.ibImage === "decorative") {
      part<HTMLInputElement>(editor, "alt").disabled = input.checked;
      return;
    }
    if (input.dataset.ibImage !== "file" || !input.files?.[0]) return;
    error(editor, null);
    const result = await source(editor, input.files[0]);
    input.value = "";
    if (result.url) part<HTMLInputElement>(editor, "src").value = result.url;
    error(editor, result.error ?? null);
  });
}

/** Puts back the selection the dialog was opened on: focus in the dialog moved it. */
function restore(editor: ImageEditor): void {
  editor.surface.focus({ preventScroll: true });
  if (saved && editor.surface.contains(saved.commonAncestorContainer)) {
    getSelection()?.removeAllRanges();
    getSelection()?.addRange(saved);
  }
}

function apply(editor: ImageEditor): void {
  const src = part<HTMLInputElement>(editor, "src");
  const url = safeSrc(editor, src.value);
  if (!url) {
    src.setAttribute("aria-invalid", "true");
    src.focus();
    return;
  }
  const decorative = part<HTMLInputElement>(editor, "decorative").checked;
  const altInput = part<HTMLInputElement>(editor, "alt");
  const alt = decorative ? "" : altInput.value.trim();
  if (!decorative && !alt) {
    altInput.setAttribute("aria-invalid", "true");
    error(editor, editor.host.dataset.ibImageNeedsAlt ?? null);
    altInput.focus();
    return;
  }
  editor.dialog.hidePopover();
  restore(editor);
  if (image && editor.surface.contains(image)) {
    image.setAttribute("src", url);
    image.setAttribute("alt", alt);
  } else {
    document.execCommand("insertHTML", false, `<img src="${escape(url)}" alt="${escape(alt)}">`);
  }
  editor.changed();
}

function remove(editor: ImageEditor): void {
  editor.dialog.hidePopover();
  restore(editor);
  image?.remove();
  editor.changed();
}

/** Opens the dialog under the selection, filled in from the image there (a new one too). */
export function openImage(editor: ImageEditor, target: HTMLImageElement | null = null): void {
  const selection = getSelection();
  if (!selection?.rangeCount) return;
  setup(editor);
  saved = selection.getRangeAt(0).cloneRange();
  const contents = saved.cloneContents();
  image = target ?? (contents.childNodes.length === 1 && contents.firstChild?.nodeName === "IMG"
    ? [...editor.surface.querySelectorAll("img")].find((img) => saved!.intersectsNode(img)) ?? null
    : null);
  const src = part<HTMLInputElement>(editor, "src");
  src.value = image?.getAttribute("src") ?? "";
  src.removeAttribute("aria-invalid");
  const alt = part<HTMLInputElement>(editor, "alt");
  alt.value = image?.getAttribute("alt") ?? "";
  alt.removeAttribute("aria-invalid");
  const decorative = part<HTMLInputElement>(editor, "decorative");
  decorative.checked = !!image && image.getAttribute("alt") === "" && !image.hasAttribute("data-ib-new");
  alt.disabled = decorative.checked;
  part(editor, "remove").hidden = !image;
  error(editor, null);

  const { dialog, surface } = editor;
  const anchorOf = image ?? saved;
  place(dialog, () => {
    const rect = anchorOf.getBoundingClientRect();
    return rect.height ? rect : surface.getBoundingClientRect();
  });
  (image && !image.hasAttribute("data-ib-new") ? alt : src).focus();
  if (image?.hasAttribute("data-ib-new")) {
    image.removeAttribute("data-ib-new");
    alt.focus();
  }
}

/** Adds pasted or dropped image files at the selection, then asks for the last one's description. */
export async function insertFiles(editor: ImageEditor, files: File[]): Promise<void> {
  let last: HTMLImageElement | null = null;
  for (const file of files) {
    const result = await source(editor, file);
    if (!result.url) continue;
    editor.surface.focus({ preventScroll: true });
    document.execCommand("insertHTML", false, `<img src="${escape(result.url)}" alt="" data-ib-new>`);
    last = editor.surface.querySelector<HTMLImageElement>("img[data-ib-new]");
    last?.removeAttribute("data-ib-new");
  }
  editor.changed();
  if (last) {
    last.setAttribute("data-ib-new", "");
    const range = document.createRange();
    range.selectNode(last);
    getSelection()?.removeAllRanges();
    getSelection()?.addRange(range);
    openImage(editor, last);
  }
}
