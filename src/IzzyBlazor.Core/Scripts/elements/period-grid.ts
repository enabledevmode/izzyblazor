import { IzzyElement, defineElement } from "../izzy-element";
import { put, set, setText } from "../quiet-write";

/**
 * <ib-period-grid> is a page of twelve months or twelve years to choose one from, for a field whose
 * Type is Month or Year, and writes it into the field's input, the one that posts and that @bind
 * listens to. As <ib-calendar> does, it changes the content Blazor renders (CONTRIBUTING): the server
 * draws the first page and C# keeps that render fixed, so what can change comes in attributes on the
 * element, and enhanced navigation's reset is caught by the observer.
 *
 * Both are numbers: a year is itself, and a month is year * 12 + month. So a page starts at a multiple
 * of twelve, moving a page is adding twelve, and everything below works for both.
 *
 * In a DateField it's inside a Popover. A month's input is clipped there on a desktop, as a browser
 * without its own month input would show a text box with no format, and the button shows the month.
 * On its own, in a Calendar, the input is clipped and passes focus on to the grid.
 */
const PAGE = 12;

class PeriodGrid extends IzzyElement {
  protected setup(signal: AbortSignal): void {
    const data = this.dataset;
    const years = data.ibUnit === "year";
    const panel = this.closest<HTMLElement>("[popover]");
    const toggle = panel?.previousElementSibling as HTMLButtonElement | null | undefined;
    const control = panel?.closest(".ib-field-control");
    const input = (control ?? this).querySelector<HTMLInputElement>("input");
    const cells = [...this.querySelectorAll<HTMLButtonElement>(".ib-calendar-day")];
    const [prev, next] = this.querySelectorAll<HTMLButtonElement>(".ib-calendar-nav");
    const yearSelect = this.querySelector("select");
    const title = this.querySelector(".ib-calendar-month");
    const live = this.querySelector("[data-ib-live]");
    const shownText = toggle?.querySelector<HTMLElement>(".ib-date-text");
    if (!input || !prev || !next || !title || !live || cells.length !== PAGE) return;

    const FIRST = years ? 1 : 12;
    const LAST = years ? 9999 : 9999 * 12 + 11;
    const parse = (text: string | undefined): number | null => {
      if (!text) return null;
      const [year, month] = text.split("-").map(Number);
      return years ? year : year * 12 + month - 1;
    };
    const format = (n: number): string => (years ? String(n) : `${String(Math.floor(n / 12)).padStart(4, "0")}-${String((n % 12) + 1).padStart(2, "0")}`);
    const clamp = (n: number, low: number, high: number): number => Math.min(Math.max(n, low), high);

    let seen = false;
    let served: string | undefined;
    let chosen: number | null = null;
    let cursor = 0;
    let page = 0;
    let min = FIRST;
    let max = LAST;
    let culture: string | undefined;
    let names: Intl.DateTimeFormat;

    const thisOne = (): number => {
      const now = new Date();
      return years ? now.getFullYear() : now.getFullYear() * 12 + now.getMonth();
    };
    const blocked = (n: number): boolean => n < min || n > max;
    const named = (n: number): string => {
      if (years) return String(n);
      const at = new Date(0);
      at.setUTCFullYear(Math.floor(n / 12), n % 12, 1);
      return names.format(at);
    };

    const observer = new MutationObserver(() => draw());
    observer.observe(this, { attributes: true, subtree: true, attributeFilter: ["data-ib-value", "data-ib-min", "data-ib-max", "data-ib-culture", "data-ib-disabled", "data-ib-readonly", "data-ready", "data-ib-cover", "tabindex"] });
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
      const now = thisOne();
      min = parse(data.ibMin) ?? FIRST;
      max = parse(data.ibMax) ?? LAST;
      // Only a new value from the server, as on a static page this one stays while a choice waits to post.
      if (!seen || data.ibValue !== served) {
        served = data.ibValue;
        const server = parse(served);
        if (!seen || server !== chosen) {
          chosen = server;
          cursor = chosen ?? (seen ? cursor : now);
        }
        seen = true;
      }
      if (!names || data.ibCulture !== culture) {
        culture = data.ibCulture;
        names = new Intl.DateTimeFormat(culture || undefined, { timeZone: "UTC", calendar: "gregory", year: "numeric", month: "long" });
      }
      cursor = clamp(cursor, min, max);
      page = cursor - (cursor % PAGE);

      const disabled = data.ibDisabled !== undefined;
      cells.forEach((cell, index) => {
        const n = page + index;
        const real = n >= FIRST && n <= LAST;
        const stop = n === cursor;
        set(cell, "hidden", !real);
        if (years) setText(cell, real ? String(n) : "");
        if (real) put(cell, "aria-label", named(n));
        set(cell, "tabIndex", stop ? 0 : -1);
        set(cell, "disabled", disabled);
        put(cell, "aria-disabled", real && blocked(n) ? "true" : null);
        put(cell, "aria-current", n === now ? "date" : null);
        if (panel) put(cell, "autofocus", stop ? "" : null);
        put(cell.parentElement!, "aria-selected", n === chosen ? "true" : null);
      });
      setText(title, years ? `${Math.max(page, FIRST)} – ${Math.min(page + PAGE - 1, LAST)}` : String(page / 12));
      set(prev, "disabled", disabled || page <= min);
      set(next, "disabled", disabled || page + PAGE > max);

      if (yearSelect) {
        // Min's to Max's, or a long way back and a little ahead, always with the one shown.
        const year = page / 12;
        const thisYear = Math.floor(now / 12);
        const from = Math.min(min > FIRST ? Math.floor(min / 12) : thisYear - 100, year);
        const to = Math.max(max < LAST ? Math.floor(max / 12) : thisYear + 20, year);
        while (yearSelect.options.length > to - from + 1) yearSelect.options[yearSelect.options.length - 1].remove();
        while (yearSelect.options.length < to - from + 1) yearSelect.add(new Option());
        [...yearSelect.options].forEach((option, index) => {
          set(option, "value", String(from + index));
          setText(option, option.value);
        });
        set(yearSelect, "value", String(year));
        set(yearSelect, "disabled", disabled);
      }

      const written = parse(input.value);
      if (panel) {
        // It lines up with the field, not its button: the server anchors a popover to that.
        panel.style.setProperty("position-anchor", (control as HTMLElement).style.getPropertyValue("anchor-name"));
        if (toggle) put(toggle, "aria-label", written !== null && data.ibChange ? data.ibChange.replace("{0}", named(written)) : data.ibChoose ?? null);
        if (shownText) {
          setText(shownText, written !== null ? named(written) : data.ibChoose ?? "");
          put(shownText, "data-ib-empty", written === null ? "" : null);
        }
      }
      // Where there's something to show in its place. A phone's own month input is a good one.
      if (!panel || (shownText && !matchMedia("(pointer: coarse)").matches)) {
        set(input, "readOnly", true);
        set(input, "tabIndex", -1);
        put(input, "aria-hidden", "true");
        if (panel) put(this, "data-ib-cover", "");
      }
      if (data.ready === undefined) data.ready = "";
    };

    const move = (n: number, focus: boolean): void => {
      const before = title.textContent;
      cursor = n;
      draw();
      if (focus) cells[cursor - page]?.focus();
      if (title.textContent !== before) live.textContent = title.textContent;
    };

    // Focus first, so the browser has nothing to restore and a Tab goes on from there.
    let opener: HTMLElement | null | undefined = toggle;
    const close = (to = opener): void => {
      to?.focus();
      panel?.hidePopover();
    };

    const choose = (n: number): void => {
      if (data.ibDisabled !== undefined || data.ibReadonly !== undefined || blocked(n)) return move(clamp(n, min, max), true);
      chosen = n;
      input.value = format(n);
      input.dispatchEvent(new Event("input", { bubbles: true }));
      input.dispatchEvent(new Event("change", { bubbles: true }));
      if (!panel) return move(n, true);
      cursor = n;
      draw();
      close();
    };

    draw();

    input.addEventListener(
      "change",
      () => {
        chosen = parse(input.value);
        draw();
      },
      { signal }
    );
    if (panel) {
      panel.addEventListener(
        "beforetoggle",
        (event) => {
          chosen = parse(input.value);
          if ((event as ToggleEvent).newState === "open") {
            // Back to the input if the keys opened it from there.
            opener = document.activeElement === input ? input : toggle;
            cursor = chosen ?? clamp(thisOne(), min, max);
          }
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
    // A label or an error's link leads to what's showing in the input's place.
    input.addEventListener(
      "focus",
      () => {
        if (input.tabIndex < 0) (panel ? toggle : cells[cursor - page])?.focus();
      },
      { signal }
    );
    this.addEventListener(
      "click",
      (event) => {
        const target = (event.target as Element).closest("button");
        if (target === prev || target === next) {
          move(clamp(cursor + (target === prev ? -PAGE : PAGE), min, max), false);
          if (target.disabled) (target === prev ? next : prev).focus();
        } else if (target && cells.includes(target)) {
          choose(page + cells.indexOf(target));
        }
      },
      { signal }
    );
    yearSelect?.addEventListener("change", () => move(clamp(Number(yearSelect.value) * 12 + (cursor % 12), min, max), false), { signal });
    this.addEventListener(
      "keydown",
      (event) => {
        if (event.altKey || event.ctrlKey || event.metaKey || !cells.includes(event.target as HTMLButtonElement)) return;
        const ahead = getComputedStyle(this).direction === "rtl" ? -1 : 1;
        const column = (cursor - page) % 3;
        const moves: Record<string, number> = {
          ArrowLeft: cursor - ahead,
          ArrowRight: cursor + ahead,
          ArrowUp: cursor - 3,
          ArrowDown: cursor + 3,
          Home: cursor - column,
          End: cursor + 2 - column,
          PageUp: cursor - PAGE,
          PageDown: cursor + PAGE,
        };
        const to = moves[event.key];
        if (to === undefined) return;
        event.preventDefault();
        move(clamp(to, min, max), true);
      },
      { signal }
    );
  }
}

defineElement("ib-period-grid", PeriodGrid);
