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

// Keeps the text node Blazor made, if it did.
const setText = (node: Node, text: string): void => {
  if (node.firstChild) node.firstChild.nodeValue = text;
  else node.textContent = text;
};

class Calendar extends IzzyElement {
  protected setup(signal: AbortSignal): void {
    const text = this.querySelector<HTMLInputElement>("input[type=date]");
    const grid = this.querySelector("table");
    const days = [...this.querySelectorAll<HTMLButtonElement>(".ib-calendar-day")];
    const weeks = [...this.querySelectorAll("tbody th")];
    const [prev, next] = this.querySelectorAll<HTMLButtonElement>(".ib-calendar-nav");
    const [months, years] = this.querySelectorAll("select");
    const title = this.querySelector(".ib-calendar-month");
    const live = this.querySelector("[data-ib-live]");
    if (!text || !grid || !prev || !next || !months || !years || !title || !live) return;

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
    observer.observe(this, { attributes: true, subtree: true });
    signal.addEventListener("abort", () => observer.disconnect());

    const draw = (): void => {
      const now = new Date();
      const today = at(now.getFullYear(), now.getMonth(), now.getDate());
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
        day.hidden = !shown || (outside && hideAdjacent);
        setText(day, shown ? String(date(n).getUTCDate()) : "");
        day.tabIndex = n === cursor ? 0 : -1;
        day.disabled = disabled;
        if (shown) day.setAttribute("aria-label", dayName.format(date(n)));
        if (blocked(n)) day.setAttribute("aria-disabled", "true");
        else day.removeAttribute("aria-disabled");
        day.className = ["ib-calendar-day", inRuns(off, n) ? "ib-calendar-off" : "", ...classed.filter(([runs]) => inRuns(runs, n)).map(([, name]) => name)].join(" ").trim();
        if (n === today) day.setAttribute("aria-current", "date");
        else day.removeAttribute("aria-current");
        cell.classList.toggle("ib-calendar-outside", outside);
        if (n === chosen) cell.setAttribute("aria-selected", "true");
        else cell.removeAttribute("aria-selected");
      });

      const anchor = data.ibWeekAnchor;
      if (anchor !== undefined) {
        weeks.forEach((week, index) => {
          const day = start + index * 7 + Number(anchor);
          setText(week, start + index * 7 < FIRST || day > LAST ? "" : String(Math.floor((day - at(date(day).getUTCFullYear(), 0, 1)) / 7) + 1));
        });
      }

      setText(title, monthName.format(date(first)));
      if (data.ibReadonly !== undefined) grid.setAttribute("aria-readonly", "true");
      else grid.removeAttribute("aria-readonly");
      prev.disabled = disabled || first <= low;
      next.disabled = disabled || at(year, month + 1, 0) >= high;

      // The years: Min's to Max's, or a long way back and a little ahead, always with the one shown.
      const thisYear = date(today).getUTCFullYear();
      const fromYear = min === null ? Math.min(thisYear - 100, year) : Math.min(date(min).getUTCFullYear(), year);
      const toYear = max === null ? Math.max(thisYear + 20, year) : Math.max(date(max).getUTCFullYear(), year);
      const count = toYear - fromYear + 1;
      while (years.options.length > count) years.options[years.options.length - 1].remove();
      while (years.options.length < count) years.add(new Option());
      [...years.options].forEach((option, index) => {
        option.value = String(fromYear + index);
        setText(option, option.value);
      });
      years.value = String(year);
      years.disabled = disabled;
      [...months.options].forEach((option, index) => {
        option.disabled = at(year, index + 1, 0) < low || at(year, index, 1) > high;
      });
      months.value = String(month);
      months.disabled = disabled;

      text.readOnly = true;
      text.tabIndex = -1;
      text.setAttribute("aria-hidden", "true");
      data.ready = "";
      observer.takeRecords();
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

    const choose = (n: number): void => {
      const allowed = data.ibDisabled === undefined && data.ibReadonly === undefined && !blocked(n);
      if (allowed) {
        chosen = n;
        text.value = date(n).toISOString().slice(0, 10);
        text.dispatchEvent(new Event("input", { bubbles: true }));
        text.dispatchEvent(new Event("change", { bubbles: true }));
      }
      // A day that's off takes focus, but doesn't change the month.
      move(allowed || date(n).getUTCMonth() === date(cursor).getUTCMonth() ? n : cursor, true);
    };

    draw();

    text.addEventListener("focus", focusCursor, { signal });
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
