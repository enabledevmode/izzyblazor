import { IzzyElement, defineElement } from "../izzy-element";

// How long the track must be still before a move counts as settled, where scrollend isn't fired.
const SETTLE_MS = 150;
// A button's or dot's move. The script animates it rather than asking for a smooth scroll, which
// Chrome on Windows turns into a jump when the system's animation effects are off.
const SLIDE_MS = 400;

/**
 * <ib-carousel> is a thin layer over a Carousel's track, which the server renders as a scroller
 * that snaps to each slide (so swiping, a trackpad and the arrow keys work without it). This
 * follows the scroll to mark the current dot, hide the dots past the last page, turn off the
 * buttons at the ends (unless it loops), and make the slides out of view inert. The buttons and
 * dots scroll the track; a new data-ib-index from the server scrolls to that slide. When a move
 * settles on another slide it reports the index as `iblayout` for @bind-Index.
 */
class Carousel extends IzzyElement {
  #track: HTMLElement | null = null;
  #index = 0;
  #reported = 0;
  #frame = 0;
  #timer = 0;
  #slide = 0;

  protected setup(signal: AbortSignal): void {
    const track = this.querySelector<HTMLElement>(":scope > .ib-carousel-track");
    if (!track) return;
    this.#track = track;

    this.addEventListener("click", (e) => this.#onClick(e), { signal });
    track.addEventListener("scroll", () => {
      cancelAnimationFrame(this.#frame);
      this.#frame = requestAnimationFrame(() => this.#update());
      clearTimeout(this.#timer);
      this.#timer = window.setTimeout(() => this.#settle(), SETTLE_MS);
    }, { signal, passive: true });
    track.addEventListener("scrollend", () => this.#settle(), { signal });
    // A touch, wheel or key during a move hands the track back to the person.
    for (const type of ["pointerdown", "wheel", "keydown"]) track.addEventListener(type, () => this.#stop(), { signal, passive: true });

    const sizes = new ResizeObserver(() => this.#update());
    sizes.observe(track);
    const slides = new MutationObserver(() => this.#update());
    slides.observe(track, { childList: true });
    const index = new MutationObserver(() => {
      const wanted = this.#wanted();
      this.#reported = wanted;
      if (wanted !== this.#index) this.#go(wanted);
    });
    index.observe(this, { attributes: true, attributeFilter: ["data-ib-index"] });
    signal.addEventListener("abort", () => {
      sizes.disconnect();
      slides.disconnect();
      index.disconnect();
      cancelAnimationFrame(this.#frame);
      clearTimeout(this.#timer);
      this.#stop();
    });

    const start = this.#wanted();
    this.#reported = start;
    if (start > 0) this.#go(start, true);
    this.#update();
  }

  #wanted(): number {
    return Math.max(0, parseInt(this.dataset.ibIndex ?? "0", 10) || 0);
  }

  get #loop(): boolean {
    return this.hasAttribute("data-ib-loop");
  }

  #slides(): HTMLElement[] {
    return this.#track ? [...this.#track.querySelectorAll<HTMLElement>(":scope > .ib-carousel-item")] : [];
  }

  #dots(): HTMLElement | null {
    return this.querySelector<HTMLElement>(":scope > .ib-carousel-dots");
  }

  // Slides in view at once, from the CSS (ItemsPerView per breakpoint).
  #per(count: number): number {
    const per = parseInt(getComputedStyle(this).getPropertyValue("--ib-per"), 10) || 1;
    return Math.max(1, Math.min(per, count));
  }

  #rtl(): boolean {
    return getComputedStyle(this.#track!).direction === "rtl";
  }

  // Where the track is: at its start, at its end (both when nothing scrolls).
  #ends(): [boolean, boolean] {
    const t = this.#track!;
    const position = Math.abs(t.scrollLeft);
    return [position <= 1, position >= t.scrollWidth - t.clientWidth - 1];
  }

  // The slide whose start edge is nearest the track's.
  #current(slides: HTMLElement[]): number {
    const t = this.#track!.getBoundingClientRect();
    const rtl = this.#rtl();
    let best = 0;
    let distance = Infinity;
    slides.forEach((slide, i) => {
      const r = slide.getBoundingClientRect();
      const d = Math.abs(rtl ? t.right - r.right : r.left - t.left);
      if (d < distance) {
        distance = d;
        best = i;
      }
    });
    return best;
  }

  #onClick(e: MouseEvent): void {
    const target = e.target as Element;
    const button = target.closest<HTMLElement>(".ib-carousel-prev, .ib-carousel-next");
    if (button?.parentElement === this) {
      if (button.getAttribute("aria-disabled") !== "true") this.#step(button.classList.contains("ib-carousel-next") ? 1 : -1);
      return;
    }
    const dot = target.closest<HTMLElement>(".ib-carousel-dot");
    const dots = this.#dots();
    if (dot && dot.parentElement === dots) {
      const count = this.#slides().length;
      this.#go([...dots.children].indexOf(dot) * this.#per(count));
    }
  }

  // A page on or back; with Loop, from the end to the start and back.
  #step(direction: 1 | -1): void {
    const count = this.#slides().length;
    const per = this.#per(count);
    const [atStart, atEnd] = this.#ends();
    if (this.#loop && direction > 0 && atEnd) this.#go(0);
    else if (this.#loop && direction < 0 && atStart) this.#go(count - per);
    else this.#go(this.#index + direction * per);
  }

  #go(index: number, instant?: boolean): void {
    const slides = this.#slides();
    if (!slides.length || !this.#track) return;
    const i = Math.max(0, Math.min(index, slides.length - this.#per(slides.length)));
    const t = this.#track.getBoundingClientRect();
    const r = slides[i].getBoundingClientRect();
    const delta = this.#rtl() ? r.right - t.right : r.left - t.left;
    this.#stop();
    if (instant || matchMedia("(prefers-reduced-motion: reduce)").matches) {
      this.#track.scrollBy({ left: delta, behavior: "instant" });
      // It has landed already: draw it now rather than on the next scroll event.
      this.#settle();
      return;
    }
    this.#animate(this.#track, delta);
  }

  // Eases the track by delta, with snapping off until it lands (snapping would pull each frame back).
  #animate(track: HTMLElement, delta: number): void {
    const from = track.scrollLeft;
    const max = track.scrollWidth - track.clientWidth;
    const to = this.#rtl() ? Math.max(-max, Math.min(0, from + delta)) : Math.max(0, Math.min(max, from + delta));
    if (Math.abs(to - from) < 1) return;
    track.setAttribute("data-ib-sliding", "");
    const start = performance.now();
    const frame = (now: number): void => {
      const p = Math.min(1, (now - start) / SLIDE_MS);
      track.scrollLeft = from + (to - from) * (1 - (1 - p) ** 3);
      if (p < 1) this.#slide = requestAnimationFrame(frame);
      else {
        this.#stop();
        this.#settle();
      }
    };
    this.#slide = requestAnimationFrame(frame);
  }

  #stop(): void {
    cancelAnimationFrame(this.#slide);
    this.#slide = 0;
    this.#track?.removeAttribute("data-ib-sliding");
  }

  // Marks the current dot, hides the dots past the last page, turns off the buttons at the ends,
  // and makes the slides out of view inert.
  #update(): void {
    const slides = this.#slides();
    if (!this.#track) return;
    const count = slides.length;
    const per = this.#per(count);
    const index = count ? this.#current(slides) : 0;
    this.#index = index;
    const [atStart, atEnd] = this.#ends();
    const scrolls = !(atStart && atEnd);

    const prev = this.querySelector(":scope > .ib-carousel-prev");
    const next = this.querySelector(":scope > .ib-carousel-next");
    prev?.setAttribute("aria-disabled", String(!scrolls || (atStart && !this.#loop)));
    next?.setAttribute("aria-disabled", String(!scrolls || (atEnd && !this.#loop)));

    const dots = this.#dots();
    if (dots) {
      const pages = Math.ceil(count / per);
      const page = atEnd ? pages - 1 : Math.floor(index / per);
      dots.hidden = pages < 2;
      [...dots.children].forEach((dot, i) => {
        const el = dot as HTMLElement;
        el.hidden = i >= pages;
        el.dataset.ibLabel ??= el.getAttribute("aria-label") ?? "";
        const first = Math.min(i * per, count - per);
        el.setAttribute("aria-label", per > 1
          ? (dots.dataset.ibPageLabel ?? "{0}-{1}").replace("{0}", String(first + 1)).replace("{1}", String(first + per))
          : el.dataset.ibLabel);
        if (i === page) el.setAttribute("aria-current", "true");
        else el.removeAttribute("aria-current");
      });
    }

    // A slide counts as in view when more than half of it shows.
    const t = this.#track.getBoundingClientRect();
    for (const slide of slides) {
      const r = slide.getBoundingClientRect();
      const shown = Math.min(r.right, t.right) - Math.max(r.left, t.left);
      slide.inert = shown < r.width / 2;
    }
  }

  #settle(): void {
    clearTimeout(this.#timer);
    // Mid-move, each frame's scroll ends; report once it lands.
    if (this.#slide) return;
    this.#update();
    if (this.#index !== this.#reported) {
      this.#reported = this.#index;
      this.emit("iblayout", { layout: String(this.#index) });
    }
  }
}

defineElement("ib-carousel", Carousel);
