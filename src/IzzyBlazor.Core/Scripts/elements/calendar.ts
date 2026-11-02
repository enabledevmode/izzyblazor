import { IzzyElement, defineElement } from "../izzy-element";
import { timeParts } from "../time-parts";

/**
 * <ib-calendar> moves a month and writes the chosen day into its date input, the one that posts and
 * that @bind listens to. CSS shows the month's controls in place of that input, which stays focusable
 * so its label and error links still lead here, and passes focus on to the day that holds the grid's
 * tab stop. It's read-only under script, so the browser doesn't check a value it can't show.
 *
 * This element changes the content Blazor renders, the one exception to that rule (CONTRIBUTING):
 * the server draws the first month and C# keeps that render fixed, so Blazor's diff never writes
 * the cells, and they're this element's from then on. What can change comes in attributes on the
 * element. Enhanced navigation resets the cells to the server's, and the observer draws them again.
 *
 * In a DateField it's inside a Popover, beside the date input: that input stays as it is, for typing,
 * choosing a day closes the panel, and the panel reads what was typed each time it opens. With a
 * time, a day is only the panel's until OK, so anything else that closes it drops the choice.
 *
 * In a DateRangeField there are months side by side (one on a phone), moved together. The first day
 * chosen only waits here, and the second writes both inputs and closes, so anything else that closes
 * it changes nothing. On an interactive page both reach C# through one hidden input, as a change on
 * each would make an app see a range with one new end.
 *
 * Dates are whole days since 1970, so moving one is adding to it.
 */
const DAY = 86400000;
const at = (year: number, month: number, day: number): number => {
  const time = new Date(0);
  time.setUTCFullYear(year, month, day);
  return time.getTime() / DAY;
};
const date = (n: number): Date => new Date(n * DAY);
const parse = (text: string | undefined): number | null => {
  if (!text) return null;
  const [year, month, day] = text.slice(0, 10).split("-").map(Number);
  return at(year, month - 1, day);
};
const FIRST = at(1, 0, 1);
const LAST = at(9999, 11, 31);
const clamp = (n: number, low: number, high: number): number => Math.min(Math.max(n, low), high);

// What the server wrote: "2027-03-06/2027-03-07,2027-03-13".
type Runs = number[][];
const parseRuns = (text: string): Runs =>
  text.split(",").map((run) => {
    const [from, to] = run.split("/");
    return [parse(from)!, parse(to ?? from)!];
  });
const inRuns = (runs: Runs, n: number): boolean => runs.some(([from, to]) => n >= from && n <= to);

// These write only what changed. A draw that changes nothing leaves nothing for an observer to see,
// here or in anything else on the page, such as a browser extension answering what changes: answering
// each other, two observers never let the browser paint or take input again.
const setText = (node: Node, text: string): void => {
  // Keeps the text node Blazor made, if it did.
  if (node.textContent === text) return;
  if (node.firstChild) node.firstChild.nodeValue = text;
  else node.textContent = text;
};
const set = <T extends Element, K extends keyof T>(element: T, name: K, value: T[K]): void => {
  if (element[name] !== value) element[name] = value;
};
const put = (element: Element, name: string, value: string | null): void => {
  if (element.getAttribute(name) === value) return;
  if (value === null) element.removeAttribute(name);
  else element.setAttribute(name, value);
};

// What the server can change, and what enhanced navigation resets.
const WATCHED = ["data-ib-value", "data-ib-value-end", "data-ib-min", "data-ib-max", "data-ib-off", "data-ib-classes", "data-ib-culture", "data-ib-first-day", "data-ib-week-anchor", "data-ib-hide-adjacent", "data-ib-disabled", "data-ib-readonly", "data-ready", "tabindex", "readonly", "aria-hidden"];

class Calendar extends IzzyElement {
  protected setup(signal: AbortSignal): void {
    const panel = this.closest<HTMLElement>("[popover]");
    const toggle = panel?.previousElementSibling as HTMLButtonElement | null | undefined;
    const control = panel?.closest(".ib-field-control");
    const text = (control ?? this).querySelector<HTMLInputElement>("input");
    const time = panel?.querySelector(".ib-calendar-time");
    const parts = timeParts(this);
    const okButton = this.querySelector<HTMLButtonElement>("[data-ib-ok]");
    const end = control?.querySelector<HTMLInputElement>("[data-ib-end]");
    const rangeValue = control?.querySelector<HTMLInputElement>("[data-ib-range-value]");
    const summary = this.querySelector("[data-ib-summary]");
    const presets = [...this.querySelectorAll<HTMLButtonElement>("[data-start]")];
    const sheets = this.querySelectorAll(".ib-calendar-sheet");
    const views = (sheets.length ? [...sheets] : [this as Element]).map((root) => ({
      grid: root.querySelector("table")!,
      days: [...root.querySelectorAll<HTMLButtonElement>(".ib-calendar-day")],
      weeks: [...root.querySelectorAll("tbody th")],
      title: root.querySelector(".ib-calendar-month")!,
      start: 0,
    }));
    const [prev, next] = this.querySelectorAll<HTMLButtonElement>(".ib-calendar-nav");
    const [months, years] = this.querySelectorAll("select");
    const live = this.querySelector("[data-ib-live]");
    if (!text || !prev || !next || !live || views.some((view) => !view.grid || !view.title)) return;

    // In the field's zone if it has one. en-CA writes a date as an input does.
    const findToday = (): number => {
      const now = new Date();
      const zone = this.dataset.ibTimeZone;
      try {
        if (zone) return parse(new Intl.DateTimeFormat("en-CA", { timeZone: zone }).format(now))!;
      } catch {
        // A browser that doesn't know the zone: its own day is the nearest.
      }
      return at(now.getFullYear(), now.getMonth(), now.getDate());
    };

    const data = this.dataset;
    let seen = false;
    let served: string | undefined;
    let chosen: number | null = null;
    let cursor = 0;
    // The first month shown, as year * 12 + month; below 0 until the cursor says.
    let shown = -1;
    // A range's first day, waiting for its second, and the day a preview runs to.
    let pending: number | null = null;
    let hover: number | null = null;
    let wall: number | null = null;
    let min: number | null = null;
    let max: number | null = null;
    let culture: string | undefined;
    let dayName: Intl.DateTimeFormat;
    let monthName: Intl.DateTimeFormat;
    let off: Runs = [];
    let offText: string | undefined;
    let classed: [Runs, string][] = [];
    let classedText: string | undefined;

    const monthOf = (n: number): number => date(n).getUTCFullYear() * 12 + date(n).getUTCMonth();
    const across = (): number => (matchMedia("(min-width: 40rem)").matches ? views.length : 1);
    const rule = (name: string | undefined, fallback: number): number => (name ? Number(name) : fallback);
    const tooLong = (n: number): boolean => pending !== null && n >= pending && (n - pending + 1 < rule(data.ibMinDays, 1) || n - pending + 1 > rule(data.ibMaxDays, Infinity) || (wall !== null && n > wall));
    const blocked = (n: number): boolean => (min !== null && n < min) || (max !== null && n > max) || inRuns(off, n) || tooLong(n);

    const observer = new MutationObserver(() => draw());
    observer.observe(this, { attributes: true, subtree: true, attributeFilter: WATCHED });
    signal.addEventListener("abort", () => observer.disconnect());

    // Its own writes aren't news, even if it stops halfway with an error.
    const draw = (): void => {
      try {
        paint();
      } finally {
        observer.takeRecords();
      }
    };

    const paint = (): void => {
      const today = findToday();
      min = parse(data.ibMin);
      max = parse(data.ibMax);
      if (data.ibOff !== offText) {
        offText = data.ibOff;
        off = offText ? parseRuns(offText) : [];
      }
      if (data.ibClasses !== classedText) {
        classedText = data.ibClasses;
        classed = (classedText ?? "").split("\n").filter(Boolean).map((line) => {
          const at = line.indexOf("=");
          return [parseRuns(line.slice(0, at)), line.slice(at + 1)];
        });
      }
      // Only a new value from the server: on a static page this one stays while a choice waits to
      // post, and an interactive page's echo of a choice mustn't pull focus back from where it's gone.
      if (!seen || data.ibValue !== served) {
        served = data.ibValue;
        // A new value from the app: a first day waiting would write over it.
        pending = hover = null;
        const server = parse(served);
        if (!seen || server !== chosen) {
          chosen = server;
          if (chosen !== null) cursor = chosen;
          else if (!seen) cursor = clamp(today, min ?? FIRST, max ?? LAST);
        }
        seen = true;
      }
      if (!dayName || data.ibCulture !== culture) {
        culture = data.ibCulture;
        const options = { timeZone: "UTC", calendar: "gregory" };
        dayName = new Intl.DateTimeFormat(culture || undefined, { ...options, dateStyle: "full" });
        monthName = new Intl.DateTimeFormat(culture || undefined, { ...options, year: "numeric", month: "long" });
      }

      // Past Min's and Max's months there's nothing to choose, so the month stops there.
      const low = min === null ? FIRST : at(date(min).getUTCFullYear(), date(min).getUTCMonth(), 1);
      const high = max === null ? LAST : at(date(max).getUTCFullYear(), date(max).getUTCMonth() + 1, 0);
      cursor = clamp(cursor, low, high);

      // The months move only when the cursor leaves them, and the last can't pass the limit.
      const count = across();
      const cursorMonth = monthOf(cursor);
      if (shown < 0 || cursorMonth < shown) shown = cursorMonth;
      else if (cursorMonth >= shown + count) shown = cursorMonth - count + 1;
      if (shown + count - 1 > monthOf(high)) shown = Math.max(monthOf(low), monthOf(high) - count + 1);
      const year = Math.floor(shown / 12);
      const month = shown % 12;
      const firstDay = Number(data.ibFirstDay ?? 0);

      // A range's first day stops at the next day that's off, unless it may span them.
      wall = pending === null || data.ibAllowOff !== undefined ? null : off.find(([from]) => from > pending!)?.[0] ?? null;
      const started = end ? parse(text.value) : null;
      const ended = end ? parse(end.value) : null;
      const preview = pending === null ? null : Math.min(Math.max(pending, hover ?? cursor), pending + rule(data.ibMaxDays, Infinity) - 1, wall === null ? Infinity : wall - 1);
      const [spanFrom, spanTo] = pending === null ? [started, ended] : [pending, preview];

      const disabled = data.ibDisabled !== undefined;
      const hideAdjacent = data.ibHideAdjacent !== undefined;
      views.forEach((view, sheet) => {
        const sheetYear = Math.floor((shown + sheet) / 12);
        const sheetMonth = (shown + sheet) % 12;
        const first = at(sheetYear, sheetMonth, 1);
        view.start = first - ((date(first).getUTCDay() - firstDay + 7) % 7);
        view.days.forEach((day, index) => {
          const n = view.start + index;
          const real = n >= FIRST && n <= LAST;
          const outside = real && date(n).getUTCMonth() !== sheetMonth;
          const cell = day.parentElement!;
          const stop = n === cursor && !outside;
          set(day, "hidden", !real || (outside && hideAdjacent));
          setText(day, real ? String(date(n).getUTCDate()) : "");
          set(day, "tabIndex", stop ? 0 : -1);
          set(day, "disabled", disabled);
          if (real) put(day, "aria-label", dayName.format(date(n)));
          put(day, "aria-disabled", blocked(n) ? "true" : null);
          set(day, "className", ["ib-calendar-day", inRuns(off, n) ? "ib-calendar-off" : "", ...classed.filter(([runs]) => inRuns(runs, n)).map(([, name]) => name)].join(" ").trim());
          put(day, "aria-current", n === today ? "date" : null);
          // Where the popover puts focus as it opens.
          if (panel) put(day, "autofocus", stop ? "" : null);
          const spanned = spanFrom !== null && spanTo !== null && n >= spanFrom && n <= spanTo && !outside;
          put(cell, "class", [outside ? "ib-calendar-outside" : "", spanned ? "ib-calendar-span" : ""].join(" ").trim() || null);
          put(cell, "aria-selected", (end ? !outside && (n === spanFrom || (pending === null && n === spanTo)) : n === chosen) ? "true" : null);
        });

        const anchor = data.ibWeekAnchor;
        if (anchor !== undefined) {
          view.weeks.forEach((week, index) => {
            const day = view.start + index * 7 + Number(anchor);
            setText(week, view.start + index * 7 < FIRST || day > LAST ? "" : String(Math.floor((day - at(date(day).getUTCFullYear(), 0, 1)) / 7) + 1));
          });
        }

        setText(view.title, monthName.format(date(first)));
        put(view.grid, "aria-readonly", data.ibReadonly !== undefined ? "true" : null);
      });
      set(prev, "disabled", disabled || at(year, month, 1) <= low);
      set(next, "disabled", disabled || at(year, month + count, 0) >= high);

      if (months && years) {
        // The years: Min's to Max's, or a long way back and a little ahead, always with the one shown.
        const thisYear = date(today).getUTCFullYear();
        const fromYear = min === null ? Math.min(thisYear - 100, year) : Math.min(date(min).getUTCFullYear(), year);
        const toYear = max === null ? Math.max(thisYear + 20, year) : Math.max(date(max).getUTCFullYear(), year);
        const howMany = toYear - fromYear + 1;
        while (years.options.length > howMany) years.options[years.options.length - 1].remove();
        while (years.options.length < howMany) years.add(new Option());
        [...years.options].forEach((option, index) => {
          set(option, "value", String(fromYear + index));
          setText(option, option.value);
        });
        set(years, "value", String(year));
        set(years, "disabled", disabled);
        [...months.options].forEach((option, index) => {
          set(option, "disabled", at(year, index + 1, 0) < low || at(year, index, 1) > high);
        });
        set(months, "value", String(month));
        set(months, "disabled", disabled);
      }

      if (panel) {
        // It lines up with the field, not its button: the server anchors a popover to that.
        panel.style.setProperty("position-anchor", (control as HTMLElement).style.getPropertyValue("anchor-name"));
        // What the input holds, not a choice still waiting for OK.
        const written = parse(text.value);
        const named = end && ended !== null && written !== null ? `${dayName.format(date(written))} – ${dayName.format(date(ended))}` : written !== null && !end ? dayName.format(date(written)) + (time ? `, ${text.value.slice(11)}` : "") : null;
        if (toggle) put(toggle, "aria-label", named !== null && data.ibChange ? data.ibChange.replace("{0}", named) : data.ibChoose ?? null);
        if (okButton) set(okButton, "disabled", chosen === null);
        for (const preset of presets) put(preset, "aria-pressed", String(preset.dataset.start === text.value && preset.dataset.end === end!.value));
        if (summary) {
          const length = started !== null && ended !== null ? ended - started + 1 : 0;
          setText(summary, pending !== null ? data.ibPickEnd ?? "" : length === 1 ? data.ibOneDay ?? "" : length > 1 ? (data.ibDays ?? "").replace("{0}", String(length)) : "");
        }
        // A phone's own calendar would open on a tap, and could choose what this field turns off.
        if (control?.classList.contains("ib-date-own") && matchMedia("(pointer: coarse)").matches) {
          set(text, "readOnly", true);
          if (end) set(end, "readOnly", true);
        }
      } else {
        set(text, "readOnly", true);
        set(text, "tabIndex", -1);
        put(text, "aria-hidden", "true");
      }
      if (data.ready === undefined) data.ready = "";
    };

    const focusCursor = (): void => {
      const view = views[clamp(monthOf(cursor) - shown, 0, views.length - 1)];
      view.days[cursor - view.start].focus();
    };

    const move = (n: number, focus: boolean, announce = true): void => {
      const before = views[0].title.textContent;
      cursor = n;
      // The keys' preview, or the mouse's would stay.
      if (focus) hover = null;
      draw();
      if (focus) focusCursor();
      if (announce && views[0].title.textContent !== before) live.textContent = views[0].title.textContent;
    };

    // The same day in another month, or that month's last day.
    const moveMonths = (by: number): number => {
      const from = date(cursor);
      const [year, month] = [from.getUTCFullYear(), from.getUTCMonth() + by];
      return Math.min(at(year, month, from.getUTCDate()), at(year, month + 1, 0));
    };

    // Both at once, and one change where C# is listening.
    const writeRange = (from: number | null, to: number | null): void => {
      const texts = [from, to].map((n) => (n === null ? "" : date(n).toISOString().slice(0, 10)));
      chosen = from;
      text.value = texts[0];
      end!.value = texts[1];
      for (const input of rangeValue ? [rangeValue] : [text, end!]) {
        if (rangeValue) input.value = texts.join("/");
        input.dispatchEvent(new Event("input", { bubbles: true }));
        input.dispatchEvent(new Event("change", { bubbles: true }));
      }
    };

    const write = (n: number | null): void => {
      if (end) return writeRange(n, n);
      chosen = n;
      text.value = n === null ? "" : date(n).toISOString().slice(0, 10) + (parts ? `T${parts.read()}` : "");
      text.dispatchEvent(new Event("input", { bubbles: true }));
      text.dispatchEvent(new Event("change", { bubbles: true }));
    };

    // Focus first, so the browser has nothing to restore and a Tab goes on from there.
    let opener: HTMLElement | null | undefined = toggle;
    const close = (to = opener): void => {
      to?.focus();
      panel?.hidePopover();
    };

    const choose = (n: number): void => {
      const allowed = data.ibDisabled === undefined && data.ibReadonly === undefined && !blocked(n);
      if (allowed && end) {
        if (pending !== null && n >= pending) {
          const from = pending;
          pending = hover = null;
          writeRange(from, n);
          draw();
          close();
          return;
        }
        // The first day, or one before it, which starts again.
        pending = n;
      } else if (allowed && time) {
        chosen = n;
      } else if (allowed) {
        write(n);
        if (panel) {
          draw();
          close();
          return;
        }
      }
      // A day that's off takes focus, but doesn't change the month.
      move(allowed || date(n).getUTCMonth() === date(cursor).getUTCMonth() ? n : cursor, true);
    };

    draw();

    if (panel) {
      panel.addEventListener(
        "beforetoggle",
        (event) => {
          // Opening or closing, a choice that wasn't written is gone.
          chosen = parse(text.value);
          pending = hover = null;
          shown = -1;
          if ((event as ToggleEvent).newState === "open") {
            // Back to the input if the keys opened it from there.
            opener = document.activeElement === text || document.activeElement === end ? (document.activeElement as HTMLElement) : toggle;
            cursor = chosen ?? (end && parse(end.value)) ?? clamp(findToday(), min ?? FIRST, max ?? LAST);
            parts?.show(text.value.slice(11));
          }
          draw();
        },
        { signal }
      );
      // Past either end it closes, and Tab goes on from its button; backward, it stops on the button.
      panel.addEventListener(
        "keydown",
        (event) => {
          // In the time's boxes, Enter would post the form.
          if (event.key === "Enter" && time && (event.target as Element).matches(".ib-calendar-time input")) {
            event.preventDefault();
            okButton?.click();
          }
          if (event.key !== "Tab") return;
          const stops = [...panel.querySelectorAll<HTMLElement>("button, select, input")].filter((stop) => stop.tabIndex >= 0 && !stop.matches(":disabled") && stop.checkVisibility());
          if (document.activeElement !== (event.shiftKey ? stops[0] : stops[stops.length - 1])) return;
          if (event.shiftKey) event.preventDefault();
          close(toggle);
        },
        { signal }
      );
      for (const input of end ? [text, end] : [text]) {
        input.addEventListener(
          "change",
          () => {
            chosen = parse(text.value);
            // The end can't be before the start.
            if (end) put(end, "min", text.value || text.getAttribute("min"));
            draw();
          },
          { signal }
        );
        // In place of the browser's own calendar, once this one's button is showing.
        input.addEventListener(
          "keydown",
          (event) => {
            if (event.altKey && event.key === "ArrowDown" && toggle?.checkVisibility()) {
              event.preventDefault();
              toggle.click();
            }
          },
          { signal }
        );
      }
      // The span runs to the day under the mouse while a first day waits.
      if (end) {
        this.addEventListener(
          "mouseover",
          (event) => {
            const day = (event.target as Element).closest<HTMLButtonElement>(".ib-calendar-day");
            const view = day && views.find((candidate) => candidate.days.includes(day));
            if (pending === null || !view) return;
            hover = view.start + view.days.indexOf(day!);
            draw();
          },
          { signal }
        );
      }
    } else {
      text.addEventListener("focus", focusCursor, { signal });
    }
    this.addEventListener(
      "click",
      (event) => {
        const target = (event.target as Element).closest("button");
        if (!target) return;
        if (target.dataset.start) {
          writeRange(parse(target.dataset.start), parse(target.dataset.end));
          draw();
          close();
        } else if (target === prev || target === next) {
          // Together, so a cursor in a later month doesn't leave the months where they are.
          shown += target === prev ? -1 : 1;
          move(moveMonths(target === prev ? -1 : 1), false);
          if (target.disabled) (target === prev ? next : prev).focus();
        } else if (target.classList.contains("ib-calendar-day")) {
          const view = views.find((candidate) => candidate.days.includes(target as HTMLButtonElement))!;
          choose(view.start + view.days.indexOf(target as HTMLButtonElement));
        } else if (target === okButton) {
          write(chosen);
          draw();
          close();
        }
      },
      { signal }
    );
    months?.addEventListener("change", () => move(moveMonths(Number(months.value) - date(cursor).getUTCMonth()), false, false), { signal });
    years?.addEventListener("change", () => move(moveMonths((Number(years.value) - date(cursor).getUTCFullYear()) * 12), false, false), { signal });
    this.addEventListener(
      "keydown",
      (event) => {
        if (event.altKey || event.ctrlKey || event.metaKey || !(event.target as Element).classList.contains("ib-calendar-day")) return;
        const ahead = getComputedStyle(this).direction === "rtl" ? -1 : 1;
        const column = (date(cursor).getUTCDay() - Number(data.ibFirstDay ?? 0) + 7) % 7;
        const moves: Record<string, () => number> = {
          ArrowLeft: () => cursor - ahead,
          ArrowRight: () => cursor + ahead,
          ArrowUp: () => cursor - 7,
          ArrowDown: () => cursor + 7,
          Home: () => cursor - column,
          End: () => cursor + 6 - column,
          PageUp: () => moveMonths(event.shiftKey ? -12 : -1),
          PageDown: () => moveMonths(event.shiftKey ? 12 : 1),
        };
        const target = moves[event.key];
        if (!target) return;
        event.preventDefault();
        move(target(), true);
      },
      { signal }
    );
  }
}

defineElement("ib-calendar", Calendar);
