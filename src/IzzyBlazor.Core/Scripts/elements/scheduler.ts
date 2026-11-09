import { IzzyElement, defineElement } from "../izzy-element";
import { navigateTo } from "../filtering";
import { track } from "../position";

// A drag moves and resizes by quarter hours.
const SNAP = 15;
// A press that moves this far is a drag, not a click.
const DRAG_PX = 4;

const two = (n: number) => String(n).padStart(2, "0");
// A day and minutes into it, counted in UTC so a clock change that day can't shift the hour.
const iso = (day: string, minutes: number) => {
  const [y, m, d] = day.split("-").map(Number);
  const t = new Date(Date.UTC(y, m - 1, d, 0, minutes));
  return `${t.getUTCFullYear()}-${two(t.getUTCMonth() + 1)}-${two(t.getUTCDate())}T${two(t.getUTCHours())}:${two(t.getUTCMinutes())}`;
};
const minutesOf = (at: string) => {
  const [h, m] = at.slice(11, 16).split(":").map(Number);
  return h * 60 + m;
};
const dayDiff = (a: string, b: string) => Math.round((Date.parse(`${b.slice(0, 10)}T00:00Z`) - Date.parse(`${a.slice(0, 10)}T00:00Z`)) / 86400000);

/**
 * <ib-scheduler> adds to a Scheduler the server drew: the day and week views open scrolled to the
 * working day (DayStart); the toolbar's shortcuts while focus is in it (t today, n and p next and
 * previous, d w x m a the views, c a new appointment), as Google Calendar's; and with OnSave, a
 * click on empty time opens a new appointment there, and in the day and week views an appointment
 * drags to another time or day, or its bottom edge to change its end, by quarter hours. A drop
 * posts the hidden move form (the server saves it through OnSave, on a static page too). Links,
 * the form and the toolbar work without this.
 */
class Scheduler extends IzzyElement {
  #scrolled: Element | null = null;

  protected setup(signal: AbortSignal): void {
    // Enhanced navigation keeps this element but resets its attributes: set up again then. A new
    // view's hours (interactive pages draw them in place) open on the working day too.
    const watch = new MutationObserver(() => {
      if (!this.hasAttribute("data-ib-ready")) this.connectedCallback();
      else this.#scroll();
    });
    signal.addEventListener("abort", () => {
      watch.disconnect();
      this.removeAttribute("data-ib-ready");
    });
    this.setAttribute("data-ib-ready", "");
    watch.observe(this, { attributes: true, attributeFilter: ["data-ib-ready"], childList: true });
    this.#scrolled = null;
    this.#scroll();

    this.addEventListener("keydown", (e) => this.#key(e), { signal });

    // The title's calendar: a day chosen goes there at once (Go is for no script), interactive
    // pages too, through Blazor's navigation.
    this.addEventListener("change", (e) => this.#jump(e), { signal });
    this.addEventListener("submit", (e) => this.#jump(e), { signal });

    // A read-only appointment's card: placed beside it where CSS anchor positioning isn't there.
    let untrack: (() => void) | undefined;
    this.addEventListener("toggle", (e) => {
      const card = e.target as HTMLElement;
      if (!card.classList?.contains("ib-sched-card")) return;
      untrack?.();
      untrack = (e as ToggleEvent).newState === "open" ? track(card, card.previousElementSibling!, "bottom-start") : undefined;
    }, { capture: true, signal });

    // The form's dialog closed in the browser (a static page): out of the address with it, so a
    // reload doesn't open it again. Interactive pages do this in C#.
    this.addEventListener("close", (e) => {
      // The form's own dialog, not Delete's question in it.
      const root = (e.target as Element).parentElement;
      if (root?.localName !== "ib-dialog" || root.dataset.queryKey !== this.dataset.ibEditKey || root.hasAttribute("data-ib-interactive")) return;
      const url = new URL(location.href);
      url.searchParams.delete(this.dataset.ibEditKey!);
      url.searchParams.delete(this.dataset.ibStartKey!);
      history.replaceState(history.state, "", url);
    }, { capture: true, signal });

    const newHref = this.dataset.ibNew;
    if (!newHref) return;
    this.addEventListener("pointerdown", (e) => this.#press(e, signal), { signal });
  }

  // Opens the day and week views on the working day, with room above for its hour's label.
  #scroll(): void {
    const scroll = this.querySelector<HTMLElement>(".ib-sched-scroll");
    const grid = this.querySelector<HTMLElement>(".ib-sched-grid");
    if (!scroll || !grid || scroll === this.#scrolled) return;
    this.#scrolled = scroll;
    scroll.scrollTop = (grid.offsetHeight * (Number(this.dataset.ibDayStart) || 0)) / 24 - 12;
  }

  // The toolbar's shortcuts, while focus is in the Scheduler and not in a field, a menu (its
  // type-ahead) or the form.
  #key(e: KeyboardEvent): void {
    if (e.defaultPrevented || e.ctrlKey || e.metaKey || e.altKey || !/^[a-z]$/i.test(e.key)) return;
    const t = e.target as Element;
    if (t.closest("input, textarea, select, [contenteditable], dialog, [popover], [role^=menu], [aria-haspopup]")) return;
    const link = this.querySelector<HTMLAnchorElement>(`a[data-ib-key="${e.key.toLowerCase()}"]`);
    if (!link) return;
    e.preventDefault();
    link.click();
  }

  // Goes to the date in the title's calendar, closing it.
  #jump(e: Event): void {
    const form = (e.target as Element).closest?.("form");
    if (!form?.classList.contains("ib-sched-jump-form")) return;
    if (e.type === "submit") e.preventDefault();
    const url = new URL(form.action, location.href);
    for (const [k, v] of new FormData(form)) if (typeof v === "string" && !k.startsWith("__")) url.searchParams.append(k, v);
    // Only a whole date: not a half-typed one with no script's input.
    if (!/^\d{4}-\d{2}-\d{2}$/.test(url.searchParams.get(form.dataset.ibDateKey ?? "") ?? "")) return;
    form.closest<HTMLElement>("[popover]")?.hidePopover();
    navigateTo(url, false);
  }

  // A new appointment at a day and minute: the form's link with the start set.
  #open(day: string, minutes: number): void {
    const url = new URL(this.dataset.ibNew!, location.href);
    url.searchParams.set(this.dataset.ibStartKey!, iso(day, minutes));
    const a = document.createElement("a");
    a.href = url.pathname + url.search;
    a.hidden = true;
    this.append(a);
    a.click();
    a.remove();
  }

  #press(e: PointerEvent, signal: AbortSignal): void {
    if (e.button !== 0) return;
    const target = e.target as HTMLElement;
    const box = target.closest<HTMLElement>(".ib-sched-col .ib-sched-event[data-ib-item]");
    const col = target.closest<HTMLElement>(".ib-sched-col");
    const cell = target.closest<HTMLElement>(".ib-sched-cell");

    // Each press's listeners end with it.
    const press = new AbortController();
    signal.addEventListener("abort", () => press.abort(), { signal: press.signal });
    const end = { signal: press.signal };

    // Empty time: a click opens a new appointment there (to the half hour), a month's day at the working day.
    if (!box) {
      if (target.closest("a, button")) return;
      const up = (u: PointerEvent) => {
        press.abort();
        if (Math.hypot(u.clientX - e.clientX, u.clientY - e.clientY) > DRAG_PX) return;
        if (col?.dataset.ibDay) {
          const r = col.getBoundingClientRect();
          this.#open(col.dataset.ibDay, Math.floor((((u.clientY - r.top) / r.height) * 1440) / 30) * 30);
        } else if (cell?.dataset.ibDay) {
          this.#open(cell.dataset.ibDay, (Number(this.dataset.ibDayStart) || 0) * 60);
        }
      };
      document.addEventListener("pointerup", up, end);
      document.addEventListener("pointercancel", () => press.abort(), end);
      return;
    }

    // An appointment: a drag moves it, from its bottom edge resizes it; a click is its link.
    if (box.hasAttribute("data-ib-cut")) return;
    const start = box.dataset.ibStart!;
    const finish = box.dataset.ibEnd!;
    const day0 = col!.dataset.ibDay!;
    const length = dayDiff(start, finish) * 1440 + minutesOf(finish) - minutesOf(start);
    const from = minutesOf(start);
    const rect = box.getBoundingClientRect();
    const resize = e.clientY > rect.bottom - 6;
    const pxPerMinute = col!.getBoundingClientRect().height / 1440;
    let dragging = false;
    let moved: { day: string; minutes: number; length: number } | null = null;
    const top0 = box.style.top;
    const height0 = box.style.height;
    const home = box.parentElement!;

    // Held from the press, so a drag from the edge that leaves the box at once still reaches it.
    box.setPointerCapture(e.pointerId);
    const move = (m: PointerEvent) => {
      if (!dragging) {
        if (Math.hypot(m.clientX - e.clientX, m.clientY - e.clientY) <= DRAG_PX) return;
        dragging = true;
        box.setAttribute("data-ib-dragging", "");
      }
      const dy = Math.round((m.clientY - e.clientY) / pxPerMinute / SNAP) * SNAP;
      if (resize) {
        const len = Math.max(SNAP, length + dy);
        moved = { day: day0, minutes: from, length: len };
        box.style.height = `${(Math.min(len, 1440 - from) / 1440) * 100}%`;
        return;
      }
      // The day under the pointer, and the time by how far it's gone up or down.
      const over = document.elementsFromPoint(m.clientX, m.clientY).find((el) => el.classList.contains("ib-sched-col")) as HTMLElement | undefined;
      const day = over?.dataset.ibDay ?? moved?.day ?? day0;
      const minutes = Math.max(0, Math.min(1440 - SNAP, from + dy));
      moved = { day, minutes, length };
      const column = this.querySelector<HTMLElement>(`.ib-sched-col[data-ib-day="${day}"]`);
      if (column && box.parentElement !== column) {
        column.append(box);
        // Moving it can drop the capture; take it again.
        if (!box.hasPointerCapture(m.pointerId)) box.setPointerCapture(m.pointerId);
      }
      box.style.top = `${(minutes / 1440) * 100}%`;
    };
    // Back where the server drew it: the server draws where it went (or, refused, it stays).
    const back = () => {
      press.abort();
      // Only after a drag: moving the box under a plain click would cancel the click.
      if (!dragging) return;
      box.removeAttribute("data-ib-dragging");
      if (box.parentElement !== home) home.append(box);
      [box.style.top, box.style.height] = [top0, height0];
    };
    const done = () => {
      back();
      if (!dragging || !moved) return;
      // The click that ends a drag isn't the link's.
      box.addEventListener("click", (c) => c.preventDefault(), { once: true, capture: true });
      const startAt = iso(moved.day, moved.minutes);
      const endAt = iso(moved.day, moved.minutes + moved.length);
      if (startAt !== start || endAt !== finish) this.#post(box.dataset.ibItem!, startAt, endAt);
    };
    box.addEventListener("pointermove", move, end);
    box.addEventListener("pointerup", done, end);
    box.addEventListener("pointercancel", back, end);
  }

  // Posts a move through the hidden form: its fields changed as a reader would, so an interactive
  // page's bindings take them, then submitted.
  #post(key: string, start: string, end: string): void {
    const form = this.querySelector<HTMLFormElement>("form.ib-sched-move");
    if (!form) return;
    for (const [name, value] of [["Key", key], ["Start", start], ["End", end]] as const) {
      const input = form.querySelector<HTMLInputElement>(`input[name$=".${name}"]`);
      if (!input) continue;
      input.value = value;
      input.dispatchEvent(new Event("change", { bubbles: true }));
    }
    form.requestSubmit();
  }
}

defineElement("ib-scheduler", Scheduler);
export {};
