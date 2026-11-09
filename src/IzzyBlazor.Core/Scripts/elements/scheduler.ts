import { IzzyElement, defineElement } from "../izzy-element";

// A drag moves and resizes by quarter hours.
const SNAP = 15;
// A press that moves this far is a drag, not a click.
const DRAG_PX = 4;

const two = (n: number) => String(n).padStart(2, "0");
const iso = (day: string, minutes: number) => {
  const d = new Date(`${day}T00:00`);
  d.setMinutes(minutes);
  return `${d.getFullYear()}-${two(d.getMonth() + 1)}-${two(d.getDate())}T${two(d.getHours())}:${two(d.getMinutes())}`;
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
  protected setup(signal: AbortSignal): void {
    this.setAttribute("data-ib-ready", "");
    signal.addEventListener("abort", () => this.removeAttribute("data-ib-ready"));

    // Open on the working day, with room above for its hour's label.
    const scroll = this.querySelector<HTMLElement>(".ib-sched-scroll");
    const grid = this.querySelector<HTMLElement>(".ib-sched-grid");
    if (scroll && grid) scroll.scrollTop = (grid.offsetHeight * (Number(this.dataset.ibDayStart) || 0)) / 24 - 12;

    this.addEventListener("keydown", (e) => this.#key(e), { signal });

    const newHref = this.dataset.ibNew;
    if (!newHref) return;
    this.addEventListener("pointerdown", (e) => this.#press(e, signal), { signal });
  }

  // The toolbar's shortcuts, while focus is in the Scheduler and not in a field or the form.
  #key(e: KeyboardEvent): void {
    if (e.ctrlKey || e.metaKey || e.altKey || e.key.length !== 1) return;
    const t = e.target as Element;
    if (t.closest("input, textarea, select, [contenteditable], dialog")) return;
    const link = this.querySelector<HTMLAnchorElement>(`a[data-ib-key="${e.key.toLowerCase()}"]`);
    if (!link) return;
    e.preventDefault();
    link.click();
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
    const box = target.closest<HTMLElement>(".ib-sched-col .ib-sched-event[data-ib-key]");
    const col = target.closest<HTMLElement>(".ib-sched-col");
    const cell = target.closest<HTMLElement>(".ib-sched-cell");

    // Empty time: a click opens a new appointment there (to the half hour), a month's day at the working day.
    if (!box) {
      if (target.closest("a, button")) return;
      const up = (u: PointerEvent) => {
        if (Math.hypot(u.clientX - e.clientX, u.clientY - e.clientY) > DRAG_PX) return;
        if (col?.dataset.ibDay) {
          const r = col.getBoundingClientRect();
          this.#open(col.dataset.ibDay, Math.floor((((u.clientY - r.top) / r.height) * 1440) / 30) * 30);
        } else if (cell?.dataset.ibDay) {
          this.#open(cell.dataset.ibDay, (Number(this.dataset.ibDayStart) || 0) * 60);
        }
      };
      this.addEventListener("pointerup", up, { once: true, signal });
      return;
    }

    // An appointment: a drag moves it, from its bottom edge resizes it; a click is its link.
    if (box.hasAttribute("data-ib-cut")) return;
    const start = box.dataset.ibStart!;
    const end = box.dataset.ibEnd!;
    const day0 = col!.dataset.ibDay!;
    const length = dayDiff(start, end) * 1440 + minutesOf(end) - minutesOf(start);
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
      if (column && box.parentElement !== column) column.append(box);
      box.style.top = `${(minutes / 1440) * 100}%`;
    };
    const done = () => {
      box.removeEventListener("pointermove", move);
      if (!dragging || !moved) return;
      box.removeAttribute("data-ib-dragging");
      // The click that ends a drag isn't the link's.
      box.addEventListener("click", (c) => c.preventDefault(), { once: true, capture: true });
      const startAt = iso(moved.day, moved.minutes);
      const endAt = iso(moved.day, moved.minutes + moved.length);
      if (startAt === start && endAt === end) {
        home.append(box);
        [box.style.top, box.style.height] = [top0, height0];
        return;
      }
      this.#post(box.dataset.ibKey!, startAt, endAt);
    };
    box.addEventListener("pointermove", move, { signal });
    box.addEventListener("pointerup", done, { once: true, signal });
    box.addEventListener("pointercancel", () => {
      box.removeEventListener("pointermove", move);
      box.removeAttribute("data-ib-dragging");
      home.append(box);
      [box.style.top, box.style.height] = [top0, height0];
    }, { once: true, signal });
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
