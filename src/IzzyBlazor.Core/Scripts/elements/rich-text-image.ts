/**
 * The rich text editor's images, loaded (as elements/parts/rich-text-image.js) the first time an
 * image is added: the image dialog, a modal as the library's Dialog, with two sources as tabs (a
 * file from the device, uploaded or embedded; an address from the web), its size (proportions
 * kept), and a description or the decorative mark; and images pasted or dropped. An upload posts
 * to the editor's ImageUploadUrl with the form's antiforgery token and takes the URL it answers
 * with; without one, AllowDataImages embeds the image as a data: URL. Every new image opens the
 * dialog, so it gets a description.
 */

/** What the image dialog needs from its editor. */
export interface ImageEditor {
  host: HTMLElement;
  surface: HTMLElement;
  dialog: HTMLDialogElement;
  changed(): void;
}

const types = ["image/png", "image/jpeg", "image/gif", "image/webp", "image/avif"];

// The dialog's state while it's open; one dialog is open at a time.
let image: HTMLImageElement | null = null;
let saved: Range | null = null;
let chosen = "";
let ratio = 0;

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

/** Shows one source tab and its panel. */
function show(editor: ImageEditor, panel: string, focus = false): void {
  for (const tab of editor.dialog.querySelectorAll<HTMLElement>("[data-ib-image=tab]")) {
    const on = tab.dataset.ibPanel === panel;
    tab.setAttribute("aria-selected", String(on));
    tab.tabIndex = on ? 0 : -1;
    if (on && focus) tab.focus();
  }
  const device = editor.dialog.querySelector<HTMLElement>("[data-ib-image=device]");
  if (device) {
    device.hidden = panel !== "device";
    part(editor, "web").hidden = panel !== "web";
  }
}

/** A chosen or dropped file: uploaded or read, then shown in the drop zone. */
async function take(editor: ImageEditor, file: File): Promise<void> {
  error(editor, null);
  const result = await source(editor, file);
  if (result.url) {
    chosen = result.url;
    const preview = part<HTMLImageElement>(editor, "preview");
    preview.src = result.url;
    preview.hidden = false;
  }
  error(editor, result.error ?? null);
}

/** Notes an image's proportions, and shows its own size in empty size fields. */
function measure(editor: ImageEditor, url: string): void {
  const probe = new Image();
  probe.onload = () => {
    ratio = probe.naturalWidth / probe.naturalHeight;
    part<HTMLInputElement>(editor, "width").placeholder = String(probe.naturalWidth);
    part<HTMLInputElement>(editor, "height").placeholder = String(probe.naturalHeight);
  };
  probe.src = url;
}

function setup(editor: ImageEditor): void {
  const { dialog } = editor;
  if (dialog.dataset.ibReady !== undefined) return;
  dialog.dataset.ibReady = "";
  dialog.addEventListener("click", (event) => {
    const target = (event.target as Element).closest<HTMLElement>("[data-ib-image]");
    const action = target?.dataset.ibImage;
    if (action === "apply") apply(editor);
    else if (action === "cancel") dialog.close();
    else if (action === "remove") {
      dialog.close("ok");
      restore(editor);
      image?.remove();
      editor.changed();
    } else if (action === "tab") show(editor, target!.dataset.ibPanel!);
    else if (action === "ratio") target!.setAttribute("aria-pressed", String(target!.getAttribute("aria-pressed") !== "true"));
  });
  dialog.addEventListener("keydown", (event) => {
    const target = event.target as HTMLElement;
    if (target.dataset.ibImage === "tab" && (event.key === "ArrowLeft" || event.key === "ArrowRight")) {
      event.preventDefault();
      show(editor, target.dataset.ibPanel === "device" ? "web" : "device", true);
    } else if (event.key === "Enter" && target.matches("input[type=url],input[type=text],input[type=number]")) {
      event.preventDefault();
      apply(editor);
    }
  });
  dialog.addEventListener("input", (event) => {
    // With proportions kept, one side follows the other.
    const input = event.target as HTMLInputElement;
    const side = input.dataset.ibImage;
    if ((side !== "width" && side !== "height") || !ratio || part(editor, "ratio").getAttribute("aria-pressed") !== "true") return;
    const value = Number(input.value);
    const other = part<HTMLInputElement>(editor, side === "width" ? "height" : "width");
    other.value = value > 0 ? String(Math.round(side === "width" ? value / ratio : value * ratio)) : "";
  });
  dialog.addEventListener("change", (event) => {
    const input = event.target as HTMLInputElement;
    if (input.dataset.ibImage === "decorative") part<HTMLInputElement>(editor, "alt").disabled = input.checked;
    else if (input.dataset.ibImage === "src" && safeSrc(editor, input.value)) measure(editor, input.value);
    else if (input.dataset.ibImage === "file" && input.files?.[0]) {
      void take(editor, input.files[0]).then(() => chosen && measure(editor, chosen));
      input.value = "";
    }
  });
  const drop = dialog.querySelector<HTMLElement>("[data-ib-image=device]");
  drop?.addEventListener("dragover", (event) => {
    event.preventDefault();
    drop.toggleAttribute("data-ib-over", true);
  });
  drop?.addEventListener("dragleave", () => drop.removeAttribute("data-ib-over"));
  drop?.addEventListener("drop", (event) => {
    event.preventDefault();
    drop.removeAttribute("data-ib-over");
    const file = event.dataTransfer?.files[0];
    if (file) void take(editor, file).then(() => chosen && measure(editor, chosen));
  });
  // Escape or Cancel: back to the text, with its selection.
  dialog.addEventListener("close", () => {
    if (dialog.returnValue !== "ok") restore(editor);
  });
}

/** Puts back the selection the dialog was opened on. */
function restore(editor: ImageEditor): void {
  editor.surface.focus({ preventScroll: true });
  if (saved && editor.surface.contains(saved.commonAncestorContainer)) {
    getSelection()?.removeAllRanges();
    getSelection()?.addRange(saved);
  }
}

function size(editor: ImageEditor, name: string): string | null {
  const value = Math.round(Number(part<HTMLInputElement>(editor, name).value));
  return value >= 1 && value <= 10000 ? String(value) : null;
}

function apply(editor: ImageEditor): void {
  const device = editor.dialog.querySelector<HTMLElement>("[data-ib-image=device]");
  const fromDevice = !!device && !device.hidden;
  const src = part<HTMLInputElement>(editor, "src");
  const url = fromDevice ? chosen || null : safeSrc(editor, src.value);
  if (!url) {
    if (!fromDevice) {
      src.setAttribute("aria-invalid", "true");
      src.focus();
    }
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
  const width = size(editor, "width");
  const height = size(editor, "height");
  editor.dialog.close("ok");
  restore(editor);
  if (image && editor.surface.contains(image)) {
    image.setAttribute("src", url);
    image.setAttribute("alt", alt);
    for (const [name, value] of [["width", width], ["height", height]] as const) {
      if (value) image.setAttribute(name, value);
      else image.removeAttribute(name);
    }
  } else {
    const sized = (width ? ` width="${width}"` : "") + (height ? ` height="${height}"` : "");
    document.execCommand("insertHTML", false, `<img src="${escape(url)}" alt="${escape(alt)}"${sized}>`);
  }
  editor.changed();
}

/** Opens the dialog for a new image at the selection, or for the image given (a new one too). */
export function openImage(editor: ImageEditor, target: HTMLImageElement | null = null, isNew = false): void {
  const selection = getSelection();
  if (!selection?.rangeCount) return;
  setup(editor);
  saved = selection.getRangeAt(0).cloneRange();
  image = target;
  chosen = "";
  ratio = 0;
  const title = part(editor, "title");
  title.textContent = (image && !isNew ? title.dataset.ibEdit : title.dataset.ibAdd) ?? "";
  const src = part<HTMLInputElement>(editor, "src");
  src.value = image?.getAttribute("src") ?? "";
  src.removeAttribute("aria-invalid");
  const preview = editor.dialog.querySelector<HTMLImageElement>("[data-ib-image=preview]");
  if (preview) preview.hidden = true;
  for (const side of ["width", "height"]) {
    const input = part<HTMLInputElement>(editor, side);
    input.value = image?.getAttribute(side) ?? "";
    input.placeholder = "";
  }
  if (image) measure(editor, image.src);
  const alt = part<HTMLInputElement>(editor, "alt");
  alt.value = image?.getAttribute("alt") ?? "";
  alt.removeAttribute("aria-invalid");
  const decorative = part<HTMLInputElement>(editor, "decorative");
  decorative.checked = !!image && !isNew && image.getAttribute("alt") === "";
  alt.disabled = decorative.checked;
  part(editor, "remove").hidden = !image || isNew;
  error(editor, null);
  // An image already there is edited by its address; a new one starts from the device.
  show(editor, image ? "web" : "device");
  editor.dialog.returnValue = "";
  editor.dialog.showModal();
  (image ? alt : editor.dialog.querySelector<HTMLElement>("[data-ib-image=tab]") ?? src).focus();
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
  if (last) openImage(editor, last, true);
}
