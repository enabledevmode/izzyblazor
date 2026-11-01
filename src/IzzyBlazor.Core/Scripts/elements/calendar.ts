import { IzzyElement, defineElement } from "../izzy-element";

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
 * choosing a day closes the panel, and the panel reads what was typed each time it opens.
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
  const [year, month, day] = text.split("-").map(Number);
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
const WATCHED = ["data-ib-value", "data-ib-min", "data-ib-max", "data-ib-off", "data-ib-classes", "data-ib-culture", "data-ib-first-day", "data-ib-week-anchor", "data-ib-hide-adjacent", "data-ib-disabled", "data-ib-readonly", "data-ready", "tabindex", "readonly", "aria-hidden"];

class Calendar extends IzzyElement {
  protected setup(signal: AbortSignal): void {
    const panel = this.closest<HTMLElement>("[popover]");
    const toggle = panel?.previousElementSibling as HTMLButtonElement | null | undefined;
    const control = panel?.closest(".ib-field-control");
    const text = (control ?? this).querySelector<HTMLInputElement>("input[type=date]");
    const todayButton = this.querySelector<HTMLButtonElement>("[data-ib-today]");
    const clearButton = this.querySelector<HTMLButtonElement>("[data-ib-clear]");
    const grid = this.querySelector("table");
    const days = [...this.querySelectorAll<HTMLButtonElement>(".ib-calendar-day")];
    const weeks = [...this.querySelectorAll("tbody th")];
    const [prev, next] = this.querySelectorAll<HTMLButtonElement>(".ib-calendar-nav");
    const [months, years] = this.querySelectorAll("select");
    const title = this.querySelector(".ib-calendar-month");
    const live = this.querySelector("[data-ib-live]");
    if (!text || !grid || !prev || !next || !months || !years || !title || !live) return;

    const findToday = (): number => {
      const now = new Date();
      return at(now.getFullYear(), now.getMonth(), now.getDate());
    };

    const data = this.dataset;
    let seen = false;
    let served: string | undefined;
    let chosen: number | null = null;
    let cursor = 0;
    let start = 0;
    let min: number | null = null;
    let max: number | null = null;
    let culture: string | undefined;
    let dayName: Intl.DateTimeFormat;
    let monthName: Intl.DateTimeFormat;
    let off: Runs = [];
    let offText: string | undefined;
    let classed: [Runs, string][] = [];
    let classedText: string | undefined;

    const blocked = (n: number): boolean => (min !== null && n < min) || (max !== null && n > max) || inRuns(off, n);

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
      const year = date(cursor).getUTCFullYear();
      const month = date(cursor).getUTCMonth();
      const first = at(year, month, 1);
      const firstDay = Number(data.ibFirstDay ?? 0);
      start = first - ((date(first).getUTCDay() - firstDay + 7) % 7);

      const disabled = data.ibDisabled !== undefined;
      const hideAdjacent = data.ibHideAdjacent !== undefined;
      days.forEach((day, index) => {
        const n = start + index;
        const shown = n >= FIRST && n <= LAST;
        const outside = shown && date(n).getUTCMonth() !== month;
        const cell = day.parentElement!;
        set(day, "hidden", !shown || (outside && hideAdjacent));
        setText(day, shown ? String(date(n).getUTCDate()) : "");
        set(day, "tabIndex", n === cursor ? 0 : -1);
        set(day, "disabled", disabled);
        if (shown) put(day, "aria-label", dayName.format(date(n)));
        put(day, "aria-disabled", blocked(n) ? "true" : null);
        set(day, "className", ["ib-calendar-day", inRuns(off, n) ? "ib-calendar-off" : "", ...classed.filter(([runs]) => inRuns(runs, n)).map(([, name]) => name)].join(" ").trim());
        put(day, "aria-current", n === today ? "date" : null);
        // Where the popover puts focus as it opens.
        if (panel) put(day, "autofocus", n === cursor ? "" : null);
        put(cell, "class", outside ? "ib-calendar-outside" : null);
        put(cell, "aria-selected", n === chosen ? "true" : null);
      });

      const anchor = data.ibWeekAnchor;
      if (anchor !== undefined) {
        weeks.forEach((week, index) => {
          const day = start + index * 7 + Number(anchor);
          setText(week, start + index * 7 < FIRST || day > LAST ? "" : String(Math.floor((day - at(date(day).getUTCFullYear(), 0, 1)) / 7) + 1));
        });
      }

      setText(title, monthName.format(date(first)));
      put(grid, "aria-readonly", data.ibReadonly !== undefined ? "true" : null);
      set(prev, "disabled", disabled || first <= low);
      set(next, "disabled", disabled || at(year, month + 1, 0) >= high);

      // The years: Min's to Max's, or a long way back and a little ahead, always with the one shown.
      const thisYear = date(today).getUTCFullYear();
      const fromYear = min === null ? Math.min(thisYear - 100, year) : Math.min(date(min).getUTCFullYear(), year);
      const toYear = max === null ? Math.max(thisYear + 20, year) : Math.max(date(max).getUTCFullYear(), year);
      const count = toYear - fromYear + 1;
      while (years.options.length > count) years.options[years.options.length - 1].remove();
      while (years.options.length < count) years.add(new Option());
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

      if (panel) {
        if (toggle) put(toggle, "aria-label", chosen !== null && data.ibChange ? data.ibChange.replace("{0}", dayName.format(date(chosen))) : data.ibChoose ?? null);
        if (todayButton) set(todayButton, "disabled", blocked(today));
        // A phone's own calendar would open on a tap, and could choose what this field turns off.
        if (control?.classList.contains("ib-date-own") && matchMedia("(pointer: coarse)").matches) set(text, "readOnly", true);
      } else {
        set(text, "readOnly", true);
        set(text, "tabIndex", -1);
        put(text, "aria-hidden", "true");
      }
      if (data.ready === undefined) data.ready = "";
    };

    const focusCursor = (): void => days[cursor - start].focus();

    const move = (n: number, focus: boolean, announce = true): void => {
      const before = title.textContent;
      cursor = n;
      draw();
      if (focus) focusCursor();
      if (announce && title.textContent !== before) live.textContent = title.textContent;
    };

    // The same day in another month, or that month's last day.
    const moveMonths = (by: number): number => {
      const from = date(cursor);
      const [year, month] = [from.getUTCFullYear(), from.getUTCMonth() + by];
      return Math.min(at(year, month, from.getUTCDate()), at(year, month + 1, 0));
    };

    const write = (n: number | null): void => {
      chosen = n;
      text.value = n === null ? "" : date(n).toISOString().slice(0, 10);
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
      if (allowed) {
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
          if ((event as ToggleEvent).newState !== "open") return;
          // Back to the input if the keys opened it from there.
          opener = document.activeElement === text ? text : toggle;
          chosen = parse(text.value);
          cursor = chosen ?? clamp(findToday(), min ?? FIRST, max ?? LAST);
          draw();
        },
        { signal }
      );
      // Past either end it closes, and Tab goes on from its button; backward, it stops on the button.
      panel.addEventListener(
        "keydown",
        (event) => {
          if (event.key !== "Tab") return;
          const stops = [...panel.querySelectorAll<HTMLElement>("button, select")].filter((stop) => stop.tabIndex >= 0 && !stop.matches(":disabled") && stop.checkVisibility());
          if (document.activeElement !== (event.shiftKey ? stops[0] : stops[stops.length - 1])) return;
          if (event.shiftKey) event.preventDefault();
          close(toggle);
        },
        { signal }
      );
      text.addEventListener(
        "change",
        () => {
          chosen = parse(text.value);
          draw();
        },
        { signal }
      );
      // In place of the browser's own calendar, once this one's button is showing.
      text.addEventListener(
        "keydown",
        (event) => {
          if (event.altKey && event.key === "ArrowDown" && toggle?.checkVisibility()) {
            event.preventDefault();
            toggle.click();
          }
        },
        { signal }
      );
    } else {
      text.addEventListener("focus", focusCursor, { signal });
    }
    this.addEventListener(
      "click",
      (event) => {
        const target = (event.target as Element).closest("button");
        if (!target) return;
        if (target === prev || target === next) {
          move(moveMonths(target === prev ? -1 : 1), false);
          if (target.disabled) (target === prev ? next : prev).focus();
        } else if (target.classList.contains("ib-calendar-day")) {
          choose(start + days.indexOf(target));
        } else if (target === todayButton) {
          cursor = findToday();
          choose(cursor);
        } else if (target === clearButton) {
          write(null);
          draw();
          close();
        }
      },
      { signal }
    );
    months.addEventListener("change", () => move(moveMonths(Number(months.value) - date(cursor).getUTCMonth()), false, false), { signal });
    years.addEventListener("change", () => move(moveMonths((Number(years.value) - date(cursor).getUTCFullYear()) * 12), false, false), { signal });
    grid.addEventListener(
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
