import { IzzyElement, defineElement } from "../izzy-element";

/**
 * <ib-file-upload> is the drop zone around a FileUpload's file input. The input works on its own
 * (its button chooses files, which post with the form); this adds:
 * - dropping files on the zone, highlighted while dragging over it; files that don't match the
 *   input's accept attribute are refused (the picker filters by type, a drop doesn't)
 * - flagging files over data-ib-max-size before the form posts (setCustomValidity)
 * - a console warning when the form would post only file names (no multipart enctype)
 */
class FileUpload extends IzzyElement {
  protected setup(signal: AbortSignal): void {
    const input = this.querySelector<HTMLInputElement>("input[type=file]");
    if (!input) return;

    // Interactive pages stream files over Blazor, so only a form that posts them needs multipart.
    const form = input.form;
    if (form && !this.hasAttribute("data-ib-interactive") && form.enctype !== "multipart/form-data" && !form.dataset.ibFileWarned) {
      form.dataset.ibFileWarned = "";
      console.warn(`IzzyBlazor: the form around FileUpload "${input.name}" isn't multipart, so it posts only file names. Add Multipart to the Form.`);
    }

    this.addEventListener(
      "dragover",
      (event) => {
        if (input.disabled || !event.dataTransfer?.types.includes("Files")) return;
        event.preventDefault();
        event.dataTransfer.dropEffect = "copy";
        this.toggleAttribute("data-dragging", true);
      },
      { signal }
    );

    const leave = (event: DragEvent) => {
      if (event.type === "drop" || !this.contains(event.relatedTarget as Node | null)) {
        this.removeAttribute("data-dragging");
      }
    };
    this.addEventListener("dragleave", leave, { signal });

    this.addEventListener(
      "drop",
      (event) => {
        leave(event);
        if (input.disabled || !event.dataTransfer?.files.length) return;
        // Handle drops on the input too: the browser would take any type there.
        event.preventDefault();
        const dropped = Array.from(event.dataTransfer.files);
        const accepted = dropped.filter((file) => accepts(input.accept, file));
        const refused = dropped.find((file) => !accepted.includes(file));
        if (!accepted.length) {
          if (refused) refuse(refused);
          return;
        }
        const files = new DataTransfer();
        for (const file of input.multiple ? accepted : accepted.slice(0, 1)) files.items.add(file);
        input.files = files.files;
        if (refused) refuse(refused);
        input.dispatchEvent(new Event("input", { bubbles: true }));
        input.dispatchEvent(new Event("change", { bubbles: true }));
      },
      { signal }
    );

    // Files over the size limit keep the input invalid until a smaller choice replaces them.
    const check = () => {
      const max = Number(this.dataset.ibMaxSize);
      const big = max > 0 ? Array.from(input.files ?? []).find((file) => file.size > max) : undefined;
      const message = (this.dataset.ibTooLarge ?? "{0} is larger than {1}.").replace("{0}", big?.name ?? "").replace("{1}", formatSize(max));
      input.setCustomValidity(big ? message : "");
      return big;
    };
    input.addEventListener("change", () => check() && input.reportValidity(), { signal });
    // A reset empties the input without a change event.
    form?.addEventListener("reset", () => setTimeout(check, 0), { signal });
    check();

    // A refused drop says why next to the input, briefly, without clearing an earlier choice;
    // then the size check decides validity again.
    let refusal = 0;
    const refuse = (file: File) => {
      const message = (this.dataset.ibWrongType ?? "{0} isn't an accepted file type.").replace("{0}", file.name);
      input.setCustomValidity(message);
      input.reportValidity();
      clearTimeout(refusal);
      refusal = window.setTimeout(check, 3000);
    };
    signal.addEventListener("abort", () => clearTimeout(refusal));
  }
}

/** Whether a file matches an accept attribute: ".pdf", "image/*", "application/pdf", comma-separated. */
export function accepts(accept: string, file: File): boolean {
  const rules = accept.split(",").map((rule) => rule.trim().toLowerCase()).filter(Boolean);
  if (!rules.length) return true;
  const name = file.name.toLowerCase();
  const type = file.type.toLowerCase();
  return rules.some((rule) =>
    rule.startsWith(".") ? name.endsWith(rule)
    : rule.endsWith("/*") ? type.startsWith(rule.slice(0, -1))
    : type === rule
  );
}

/** 2000000 becomes "2 MB"; 1536 becomes "1.5 KB". */
export function formatSize(bytes: number): string {
  const units = ["bytes", "KB", "MB", "GB"];
  let size = bytes;
  let unit = 0;
  while (size >= 1000 && unit < units.length - 1) {
    size /= 1000;
    unit++;
  }
  return `${Number(size.toFixed(1))} ${units[unit]}`;
}

defineElement("ib-file-upload", FileUpload);
