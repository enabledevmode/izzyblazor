import { IzzyElement, defineElement } from "../izzy-element";
import { navigateTo } from "../filtering";
import { track } from "../position";

// A drag moves and resizes by quarter hours.
const SNAP = 15;
// A press that moves this far is a drag, not a click.
const DRAG_PX = 4;
// The session key for the appointment focus goes back to after its form.
const FOCUS = "ib-sched-focus";

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
  // The days the hours were last scrolled for, and how far: the same days keep their place
  // through a render or a static page coming back after a save; other days open on DayStart.
  #scrollKey = "";
  #scrollTop = 0;

  protected setup(signal: AbortSignal): void {
    // Enhanced navigation keeps this element but resets its attributes: set up again then. A new
    // view's hours (interactive pages draw them in place) open on the working day too.
    const watch = new MutationObserver(() => {
      if (!this.hasAttribute("data-ib-ready")) this.connectedCallback();
      else {
        this.#scroll();
        this.#refocus();
      }
    });
    signal.addEventListener("abort", () => {
      watch.disconnect();
      this.removeAttribute("data-ib-ready");
    });
    this.setAttribute("data-ib-ready", "");
    watch.observe(this, { attributes: true, attributeFilter: ["data-ib-ready"], childList: true });
    // Another week drawn in place (interactive): its columns' days change, not this element's children.
    const days = new MutationObserver(() => this.#scroll());
    days.observe(this, { subtree: true, attributes: true, attributeFilter: ["data-ib-day"] });
    signal.addEventListener("abort", () => days.disconnect());
    this.addEventListener("scroll", (e) => {
      if ((e.target as Element).classList?.contains("ib-sched-scroll")) this.#scrollTop = (e.target as HTMLElement).scrollTop;
    }, { capture: true, signal });
    this.#scroll();

    this.addEventListener("keydown", (e) => this.#key(e), { signal });

    // The title's calendar: a day chosen goes there at once (Go is for no script), interactive
    // pages too, through Blazor's navigation.
    this.addEventListener("change", (e) => this.#jump(e), { signal });
    this.addEventListener("submit", (e) => this.#jump(e), { signal });

    // An appointment's card, placed beside it where CSS anchor positioning isn't there. Opened by
    // a press (read-only), focus goes to it (a dialog, read by its title; closing it puts focus
    // back); opened by hovering, focus stays where it is.
    let untrack: (() => void) | undefined;
    this.addEventListener("toggle", (e) => {
      const card = e.target as HTMLElement;
      if (!card.classList?.contains("ib-sched-card")) return;
      untrack?.();
      const open = (e as ToggleEvent).newState === "open";
      untrack = open ? track(card, card.previousElementSibling!, "bottom-start") : undefined;
      if (open && card !== this.#hovered) card.focus();
      if (!open && card === this.#hovered) this.#hovered = null;
    }, { capture: true, signal });

    // Hovering an appointment with a mouse shows its card after a moment (Outlook's peek); it
    // stays while the pointer is on the appointment or the card, and Escape closes it.
    this.addEventListener("pointerover", (e) => this.#hover(e), { signal });
    this.addEventListener("pointerout", (e) => this.#leave(e), { signal });
    // A press on a read-only appointment whose card hovering opened keeps it open, and focuses it.
    this.addEventListener("click", (e) => {
      // A press is the reader's choice: a hover's card still to come doesn't come (it would open
      // again after Escape closed the one the press opened).
      clearTimeout(this.#timer);
      const card = (e.target as Element).closest?.("button.ib-sched-event")?.nextElementSibling as HTMLElement | null;
      if (!card || card !== this.#hovered) return;
      e.preventDefault();
      this.#hovered = null;
      card.focus();
    }, { capture: true, signal });
    signal.addEventListener("abort", () => clearTimeout(this.#timer));

    // The form's dialog closed (or its form sent): focus goes back to the appointment, or New for
    // a new or deleted one, once the dialog is gone. Kept in the session, since a static page's
    // save comes back as a new page.
    this.addEventListener("submit", (e) => {
      if ((e.target as Element).closest?.("ib-dialog")?.getAttribute("data-query-key") === this.dataset.ibEditKey) this.#remember();
    }, { capture: true, signal });
    // On a static page, closing it in the browser also takes it out of the address, so a reload
    // doesn't open it again. Interactive pages do that in C#.
    this.addEventListener("close", (e) => {
      const root = (e.target as Element).parentElement;
      const form = root?.closest<HTMLElement>("ib-dialog");
      let edit: HTMLElement | null = form ?? null;
      while (edit && edit.dataset.queryKey !== this.dataset.ibEditKey) edit = edit.parentElement?.closest<HTMLElement>("ib-dialog") ?? null;
      if (!edit) return;
      // Delete's question in the form closing (an interactive delete then takes the form away with
      // no close of its own): remember the appointment, nothing more.
      this.#remember();
      if (root !== edit) return;
      requestAnimationFrame(() => this.#refocus());
      if (root.hasAttribute("data-ib-interactive")) return;
      const url = new URL(location.href);
      url.searchParams.delete(this.dataset.ibEditKey!);
      url.searchParams.delete(this.dataset.ibStartKey!);
      history.replaceState(history.state, "", url);
    }, { capture: true, signal });
    this.#refocus();

    const newHref = this.dataset.ibNew;
    if (!newHref) return;
    this.addEventListener("pointerdown", (e) => this.#press(e, signal), { signal });
  }

  // Opens the day and week views on the working day, with room above for its hour's label; the
  // same days shown again keep where they were scrolled to.
  #scroll(): void {
    const scroll = this.querySelector<HTMLElement>(".ib-sched-scroll");
    const grid = this.querySelector<HTMLElement>(".ib-sched-grid");
    if (!scroll || !grid) return;
    const cols = this.querySelectorAll<HTMLElement>(".ib-sched-col");
    const key = `${cols[0]?.dataset.ibDay}/${cols.length}`;
    if (key !== this.#scrollKey) {
      this.#scrollKey = key;
      this.#scrollTop = scroll.scrollTop = (grid.offsetHeight * (Number(this.dataset.ibDayStart) || 0)) / 24 - 12;
    } else if (scroll.scrollTop !== this.#scrollTop) {
      scroll.scrollTop = this.#scrollTop;
    }
  }

  // The toolbar's shortcuts, while focus is in the Scheduler and not in a field, a menu (its
  // type-ahead) or the form.
  #key(e: KeyboardEvent): void {
    if (e.defaultPrevented || e.ctrlKey || e.metaKey || e.altKey || !/^[a-z]$/i.test(e.key)) return;
    const t = e.target as Element;
    if (t.closest("input, textarea, select, [contenteditable], dialog, [popover], [role^=menu], [aria-haspopup]:not(.ib-sched-event)")) return;
    const link = this.querySelector<HTMLElement>(`[data-ib-key="${e.key.toLowerCase()}"]`);
    if (!link) return;
    e.preventDefault();
    link.click();
  }

  // Goes to the date in the title's calendar, closing it.
  #jump(e: Event): void {
    const form = (e.target as Element).closest?.("form");
    if (!form?.classList.contains("ib-sched-jump-form")) return;
    // A day chosen, not the month or year picked from the calendar's selects.
    if (e.type === "change" && (e.target as HTMLInputElement).name !== form.dataset.ibDateKey) return;
    const url = new URL(form.action, location.href);
    for (const [k, v] of new FormData(form)) if (typeof v === "string" && !k.startsWith("__")) url.searchParams.append(k, v);
    // Only a whole date: not a half-typed one with no script's input.
    if (!/^\d{4}-\d{2}-\d{2}$/.test(url.searchParams.get(form.dataset.ibDateKey ?? "") ?? "")) return;
    if (e.type === "submit") e.preventDefault();
    form.closest<HTMLElement>("[popover]")?.hidePopover();
    // Interactive: C# goes there (the Calendar's bound value), so only one navigation happens.
    if (!form.hasAttribute("data-ib-interactive")) navigateTo(url, false);
  }

  // Which appointment's form was open: its key, under this Scheduler's key in the address.
  #remember(): void {
    const key = new URL(location.href).searchParams.get(this.dataset.ibEditKey!);
    try {
      // This page and this Scheduler's, so another page (Back, a link) doesn't take it.
      if (key) sessionStorage.setItem(FOCUS, `${this.dataset.ibEditKey}|${location.pathname}|${key}`);
    } catch {
      // No session storage: focus stays where the browser puts it.
    }
  }

  // Once the form's dialog is gone: focus on its appointment, or New.
  #refocus(): void {
    let saved: string | null = null;
    try {
      saved = sessionStorage.getItem(FOCUS);
    } catch {
      return;
    }
    // "key|path|appointment": the Scheduler's key and the path have no "|"; an appointment's key may.
    const [owner, path, ...rest] = saved?.split("|") ?? [];
    const key = rest.join("|");
    if (owner !== this.dataset.ibEditKey || this.querySelector("dialog[open]")) return;
    if (path !== location.pathname) {
      sessionStorage.removeItem(FOCUS);
      return;
    }
    sessionStorage.removeItem(FOCUS);
    const item = key && key !== "new" ? this.querySelector<HTMLElement>(`[data-ib-item="${CSS.escape(key)}"]`) : null;
    (item ?? this.querySelector<HTMLElement>(".ib-sched-new"))?.focus();
  }

  #hovered: HTMLElement | null = null;
  #timer = 0;

  // The card of the appointment under the pointer (or the card itself), if any.
  #cardAt(target: EventTarget | null): HTMLElement | null {
    const el = target as Element | null;
    const card = el?.closest?.<HTMLElement>(".ib-sched-card");
    if (card) return card;
    const next = el?.closest?.(".ib-sched-event")?.nextElementSibling as HTMLElement | null;
    return next?.classList.contains("ib-sched-card") ? next : null;
  }

  #hover(e: PointerEvent): void {
    if (e.pointerType !== "mouse" || e.buttons) return;
    const card = this.#cardAt(e.target);
    if (!card) return;
    clearTimeout(this.#timer);
    if (card.matches(":popover-open")) return;
    this.#timer = window.setTimeout(() => {
      // Not over a menu or another popover the reader opened (showing this one would close it).
      if (!card.isConnected || card.matches(":popover-open") || this.querySelector("[data-ib-dragging]") || document.querySelector(":popover-open:not(.ib-sched-card)") || [...this.querySelectorAll(".ib-sched-card:popover-open")].some((c) => c !== this.#hovered)) return;
      this.#hovered = card;
      card.showPopover();
    }, 500);
  }

  #leave(e: PointerEvent): void {
    if (e.pointerType !== "mouse") return;
    const card = this.#cardAt(e.target);
    // Still on the same appointment or its card.
    if (!card || this.#cardAt(e.relatedTarget) === card) return;
    clearTimeout(this.#timer);
    if (card !== this.#hovered) return;
    this.#timer = window.setTimeout(() => {
      if (card === this.#hovered && card.matches(":popover-open")) card.hidePopover();
    }, 200);
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
    // A press ends a hover's card: the form, or a drag, comes next.
    clearTimeout(this.#timer);
    if (this.#hovered?.matches(":popover-open") && !(e.target as Element).closest(".ib-sched-card")) this.#hovered.hidePopover();
    const target = e.target as HTMLElement;
    // A press in a card (selecting its text) isn't a press on the time under it.
    if (target.closest(".ib-sched-card")) return;
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

    // An appointment: a drag moves it, from its bottom edge resizes it; a click is its link. Not
    // without OnSave (an EditDialogForm alone), or for one cut by the days shown.
    if (!this.hasAttribute("data-ib-movable") || box.hasAttribute("data-ib-cut")) return;
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
    const home = col!;

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
      // Over another day by an offset: the element stays in the column the server drew it in.
      const column = this.querySelector<HTMLElement>(`.ib-sched-col[data-ib-day="${day}"]`) ?? home;
      box.style.translate = `${column.getBoundingClientRect().left - home.getBoundingClientRect().left}px 0`;
      box.style.top = `${(minutes / 1440) * 100}%`;
    };
    // Back where the server drew it: a canceled drag, or a drop that changed nothing.
    const back = () => {
      [box.style.top, box.style.height, box.style.translate] = [top0, height0, ""];
    };
    const done = () => {
      press.abort();
      // Only after a drag: a plain click is the link's.
      if (!dragging) return;
      box.removeAttribute("data-ib-dragging");
      // The click that ends a drag isn't the link's.
      box.addEventListener("click", (c) => c.preventDefault(), { once: true, capture: true });
      const startAt = moved && iso(moved.day, moved.minutes);
      const endAt = moved && iso(moved.day, moved.minutes + moved.length);
      if (!moved || (startAt === start && endAt === finish)) return back();
      // It stays where it was dropped until the server draws it there (or, refused, back: each
      // move changes the boxes' style, so the server's drawing always replaces this one).
      this.#post(box.dataset.ibItem!, startAt!, endAt!);
    };
    const cancel = () => {
      press.abort();
      if (!dragging) return;
      box.removeAttribute("data-ib-dragging");
      back();
    };
    box.addEventListener("pointermove", move, end);
    box.addEventListener("pointerup", done, end);
    box.addEventListener("pointercancel", cancel, end);
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
