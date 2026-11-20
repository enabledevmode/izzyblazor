import { IzzyElement, defineElement } from "../izzy-element";

type Slides = HTMLElement & { goTo?: (index: number, instant?: boolean) => void };

/**
 * <ib-lightbox> opens a Lightbox's viewer, a <dialog> the server renders closed with a Carousel
 * of the full images inside. A click on an item's link (which, without this, opens the image
 * itself) shows the dialog as a modal on that image; the carousel, its slides stacked, moves between them and reports
 * each move. This keeps the counter and the current thumbnail in step, gives the whole viewer
 * the arrow keys, Home and End, closes on the close button and on a click outside the image and
 * controls (data-ib-backdrop), and fades the controls (data-ib-idle on the dialog) when nothing
 * has moved for data-ib-idle-after milliseconds. The open item is reported as `iblayout`, "" when
 * the viewer closes, for @bind-Index; a new data-ib-index from the server opens, moves or closes it.
 */
// Whether a click on an image landed on the picture itself. The image element fills its stage
// and draws the picture inside (object-fit: scale-down), so the bands beside it are the element too.
function onPicture(e: MouseEvent): boolean {
  const img = e.target as HTMLImageElement;
  if (img.localName !== "img") return false;
  if (!img.naturalWidth) return true;
  const box = img.getBoundingClientRect();
  const scale = Math.min(1, box.width / img.naturalWidth, box.height / img.naturalHeight);
  const [w, h] = [img.naturalWidth * scale, img.naturalHeight * scale];
  return Math.abs(e.clientX - (box.left + box.width / 2)) <= w / 2 && Math.abs(e.clientY - (box.top + box.height / 2)) <= h / 2;
}

class Lightbox extends IzzyElement {
  #dialog: HTMLDialogElement | null = null;
  #index = -1;
  #idle = 0;

  protected setup(signal: AbortSignal): void {
    const dialog = this.querySelector<HTMLDialogElement>(":scope > dialog.ib-lightbox-panel");
    if (!dialog) return;
    this.#dialog = dialog;

    this.addEventListener("click", (e) => this.#onClick(e), { signal });
    dialog.addEventListener("close", () => this.#closed(), { signal });
    dialog.addEventListener("iblayout", (e) => {
      if (e.target !== this.#slides() || !dialog.open) return;
      this.#at(parseInt((e as CustomEvent<{ layout: string }>).detail.layout, 10) || 0);
    }, { signal });
    dialog.addEventListener("keydown", (e) => this.#keys(e), { signal });
    for (const type of ["pointermove", "pointerdown", "keydown", "focusin"]) dialog.addEventListener(type, () => this.#wake(), { signal, passive: true });

    const index = new MutationObserver(() => this.#sync());
    index.observe(this, { attributes: true, attributeFilter: ["data-ib-index"] });
    signal.addEventListener("abort", () => {
      index.disconnect();
      clearTimeout(this.#idle);
    });
    this.#sync();
  }

  #links(): HTMLElement[] {
    return [...this.querySelectorAll<HTMLElement>("a.ib-lightbox-link")].filter((link) => link.closest("ib-lightbox") === this);
  }

  #slides(): Slides | null {
    return this.#dialog?.querySelector<Slides>(":scope > ib-carousel") ?? null;
  }

  // What the server asks for: an item open, or the viewer closed.
  #sync(): void {
    const wanted = this.dataset.ibIndex;
    if (wanted === undefined || wanted === "") {
      if (this.#dialog?.open) this.#dialog.close();
    } else if (parseInt(wanted, 10) !== this.#index) this.#open(parseInt(wanted, 10) || 0);
  }

  #onClick(e: MouseEvent): void {
    const dialog = this.#dialog!;
    const target = e.target as Element;
    const link = target.closest<HTMLElement>("a.ib-lightbox-link");
    if (link && !dialog.contains(link)) {
      // A modified click keeps the link's own meaning: a new tab, a download.
      const at = this.#links().indexOf(link);
      if (at < 0 || e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
      e.preventDefault();
      this.#open(at);
      return;
    }
    if (!dialog.contains(target)) return;
    const thumb = target.closest<HTMLElement>(".ib-lightbox-thumb");
    if (thumb) this.#go([...thumb.parentElement!.children].indexOf(thumb));
    else if (target.closest(".ib-lightbox-close")) dialog.close();
    else if (this.hasAttribute("data-ib-backdrop") && !target.closest("figcaption, button, .ib-lightbox-bar, .ib-lightbox-thumbs, .ib-carousel-dots") && !onPicture(e)) dialog.close();
  }

  #open(index: number): void {
    const dialog = this.#dialog!;
    const opening = !dialog.open;
    if (opening) dialog.showModal();
    this.#go(index, opening);
    this.#wake();
  }

  // Moves the carousel, whose code may still be on its way.
  #go(index: number, instant = false): void {
    const slides = this.#slides();
    const count = this.#links().length;
    if (!slides || !count) return;
    const i = Math.max(0, Math.min(index, count - 1));
    this.#at(i);
    if (slides.goTo) slides.goTo(i, instant);
    else void customElements.whenDefined("ib-carousel").then(() => requestAnimationFrame(() => this.#index === i && slides.goTo?.(i, true)));
  }

  // The item in view: the counter, the current thumbnail, and the report.
  #at(index: number): void {
    const dialog = this.#dialog!;
    const changed = index !== this.#index;
    this.#index = index;
    const counter = dialog.querySelector<HTMLElement>(".ib-lightbox-counter");
    if (counter) counter.dataset.ibText = (counter.dataset.ibFormat ?? "{0} / {1}").replace("{0}", String(index + 1)).replace("{1}", String(this.#links().length));
    dialog.querySelectorAll<HTMLElement>(".ib-lightbox-thumb").forEach((thumb, i) => {
      if (i === index) {
        thumb.setAttribute("aria-current", "true");
        thumb.scrollIntoView({ block: "nearest", inline: "nearest" });
      } else thumb.removeAttribute("aria-current");
    });
    if (changed) this.emit("iblayout", { layout: String(index) });
  }

  #closed(): void {
    clearTimeout(this.#idle);
    this.#dialog?.removeAttribute("data-ib-idle");
    if (this.#index < 0) return;
    this.#index = -1;
    this.emit("iblayout", { layout: "" });
  }

  #keys(e: KeyboardEvent): void {
    // The carousel's track has the same keys when focus is on it: it has moved already.
    if (e.defaultPrevented || e.ctrlKey || e.metaKey || e.altKey) return;
    const rtl = getComputedStyle(this.#dialog!).direction === "rtl";
    const count = this.#links().length;
    const loop = this.hasAttribute("data-ib-loop");
    const step = (by: number): number => {
      const to = this.#index + by;
      return loop ? (to + count) % count : to;
    };
    if (e.key === (rtl ? "ArrowLeft" : "ArrowRight")) this.#go(step(1));
    else if (e.key === (rtl ? "ArrowRight" : "ArrowLeft")) this.#go(step(-1));
    else if (e.key === "Home") this.#go(0);
    else if (e.key === "End") this.#go(count - 1);
    else return;
    e.preventDefault();
  }

  // Shows the controls, and fades them once nothing has moved for a while, unless focus shows on one.
  #wake(): void {
    const dialog = this.#dialog!;
    dialog.removeAttribute("data-ib-idle");
    clearTimeout(this.#idle);
    const after = parseInt(this.dataset.ibIdleAfter ?? "0", 10);
    if (!after || !dialog.open) return;
    this.#idle = window.setTimeout(() => {
      if (dialog.open && !dialog.querySelector(":focus-visible")) dialog.setAttribute("data-ib-idle", "");
    }, after);
  }
}

defineElement("ib-lightbox", Lightbox);
