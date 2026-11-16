/**
 * RelativeTime (and Comment's and TimelineItem's times): time[data-ib-relative] elements the server
 * wrote as "2 hours ago". This keeps them current while the page stays open, in the reader's language
 * (the nearest lang attribute) and time zone, picking units as RelativeTime.Text does on the server:
 * now under 45 seconds, minutes under 45 minutes, hours under 22 hours, then calendar days. Past the
 * element's threshold (data-ib-relative, in seconds; empty for none) it shows the date.
 *
 * Loaded by index.ts once a page has one. One timer serves the page: every second while a time is under a minute old, every minute under an
 * hour, else hourly, and at the next midnight so "yesterday" turns over. Updates are silent.
 */
const SELECTOR = "time[data-ib-relative]";
const DAY = 86_400_000;
let timer: ReturnType<typeof setTimeout> | undefined;

function localeOf(element: Element): string | undefined {
  return element.closest("[lang]")?.getAttribute("lang") || undefined;
}

// Days since the epoch on the reader's calendar, for "yesterday" and "3 days ago".
function dayNumber(date: Date): number {
  return Math.floor(Date.UTC(date.getFullYear(), date.getMonth(), date.getDate()) / DAY);
}

function relativeText(value: Date, now: Date, threshold: number | null, locale: string | undefined): string {
  const seconds = (now.getTime() - value.getTime()) / 1000;
  const span = Math.abs(seconds);
  const sign = seconds >= 0 ? -1 : 1;
  if (threshold !== null && span >= threshold) {
    const format: Intl.DateTimeFormatOptions = value.getFullYear() === now.getFullYear()
      ? { month: "long", day: "numeric" }
      : { year: "numeric", month: "numeric", day: "numeric" };
    return new Intl.DateTimeFormat(locale, format).format(value);
  }
  const words = new Intl.RelativeTimeFormat(locale, { numeric: "auto" });
  if (span < 45) return words.format(0, "second");
  if (span < 45 * 60) return words.format(sign * Math.max(1, Math.round(span / 60)), "minute");
  if (span < 22 * 3600) return words.format(sign * Math.max(1, Math.round(span / 3600)), "hour");
  return words.format(sign * Math.max(1, Math.abs(dayNumber(now) - dayNumber(value))), "day");
}

function update(element: HTMLTimeElement, now: Date): number {
  const value = new Date(element.dateTime);
  if (Number.isNaN(value.getTime())) return Infinity;
  const raw = element.getAttribute("data-ib-relative");
  const threshold = raw ? Number(raw) : null;
  const locale = localeOf(element);
  try {
    const text = relativeText(value, now, threshold, locale);
    if (element.textContent !== text) element.textContent = text;
    const title = new Intl.DateTimeFormat(locale, { dateStyle: "full", timeStyle: "short" }).format(value);
    if (element.title !== title) element.title = title;
  } catch {
    // An unknown lang: keep the server's text.
    return Infinity;
  }
  return Math.abs(now.getTime() - value.getTime()) / 1000;
}

/** Updates every relative time on the page and sets the timer for the next. Safe to call any time. */
export function syncRelativeTimes(): void {
  if (timer !== undefined) clearTimeout(timer);
  const now = new Date();
  let nearest = Infinity;
  document.querySelectorAll<HTMLTimeElement>(SELECTOR).forEach((element) => {
    nearest = Math.min(nearest, update(element, now));
  });
  // With none on the page yet, look again in a minute: an interactive page can add one.
  const delay = nearest < 60 ? 1000 : nearest < 3600 ? 60_000 : nearest === Infinity ? 60_000 : 3_600_000;
  const midnight = new Date(now.getFullYear(), now.getMonth(), now.getDate() + 1).getTime() - now.getTime() + 1000;
  timer = setTimeout(syncRelativeTimes, Math.min(delay, midnight));
}

// A tab in the background can miss its timer: catch up when it's seen again, or brought back.
document.addEventListener("visibilitychange", () => {
  if (document.visibilityState === "visible") syncRelativeTimes();
});
addEventListener("pageshow", syncRelativeTimes);
