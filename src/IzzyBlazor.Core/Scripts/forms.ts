/**
 * Submit feedback for IzzyBlazor forms on static pages: the button that submitted the form
 * shows its loading state and blocks a second click while the post is in flight.
 *
 * Only static forms (those with Blazor's "_handler" field) are touched. On interactive pages
 * Blazor owns the button, and C# sets Loading itself.
 *
 * The browser fires "submit" only after its own validation passes, so an invalid form never
 * shows a spinner. Enhanced form handling replaces the markup with the server's response,
 * which clears the loading state; a full page post clears it by navigating.
 */
export function installSubmitFeedback(): void {
  // A second submit while the first is in flight (Enter in a field, a double click) is dropped.
  document.addEventListener(
    "submit",
    (event) => {
      const form = event.target;
      if (form instanceof HTMLFormElement && form.classList.contains("ib-form") && form.querySelector(".ib-btn-loading[aria-busy]")) {
        event.preventDefault();
        event.stopImmediatePropagation();
      }
    },
    true
  );

  document.addEventListener("submit", (event) => {
    const form = event.target;
    if (!(form instanceof HTMLFormElement) || !form.classList.contains("ib-form")) return;
    if (!form.querySelector('input[name="_handler"]')) return;
    if (event.defaultPrevented) return;
    // A form that opens elsewhere or downloads a file never comes back to clear the spinner.
    const target = ((event as SubmitEvent).submitter?.getAttribute("formtarget") ?? form.target) || "_self";
    if (target !== "_self") return;

    const button = (event as SubmitEvent).submitter;
    if (button instanceof HTMLElement && button.classList.contains("ib-btn")) {
      button.classList.add("ib-btn-loading");
      button.setAttribute("aria-busy", "true");
    }
  });

  // Coming back with the Back button can restore a page from the cache mid-submit.
  window.addEventListener("pageshow", (event) => {
    if (!event.persisted) return;
    document.querySelectorAll(".ib-form .ib-btn-loading[aria-busy]").forEach((el) => {
      el.classList.remove("ib-btn-loading");
      el.removeAttribute("aria-busy");
    });
  });
}

/** Which of a field's messages answers each failed browser check. */
const messageFor: [keyof ValidityState, string][] = [
  ["valueMissing", "ibRequired"],
  ["tooShort", "ibLength"],
  ["tooLong", "ibLength"],
  ["rangeUnderflow", "ibRange"],
  ["rangeOverflow", "ibRange"],
  ["typeMismatch", "ibType"],
];

/**
 * The model's error messages for the browser's checks before posting. Fields render the custom
 * ErrorMessage of [Required], [StringLength], [Range], [EmailAddress] and [Url] as data-ib-*
 * attributes; when the browser finds a field invalid, this swaps its text for the field's own.
 *
 * A message set here is cleared on the next edit anywhere, so the browser checks afresh on the
 * next submit. A custom error set by other code (FileUpload's file checks) is left alone.
 */
export function installValidityMessages(): void {
  // The messages this set, by field. A field's custom error is only ours while its message is
  // still the one we set: other code (FileUpload's file checks) may have replaced it since.
  const ours = new Map<HTMLInputElement, string>();
  const release = (field: HTMLInputElement) => {
    if (field.validationMessage === ours.get(field)) field.setCustomValidity("");
    ours.delete(field);
  };
  document.addEventListener(
    "invalid",
    (event) => {
      const field = event.target as HTMLInputElement;
      if (ours.has(field)) release(field);
      if (field.validity.customError) return;
      for (const [check, key] of messageFor) {
        const message = field.dataset[key];
        if (message && field.validity[check]) {
          field.setCustomValidity(message);
          ours.set(field, message);
          return;
        }
      }
    },
    true
  );
  const clear = (event?: Event) => {
    // Switching tabs changes no field (tabs.ts announces it while the browser shows a message).
    if ((event?.target as Element | null)?.classList?.contains("ib-tab-input")) return;
    ours.forEach((_, field) => release(field));
  };
  for (const type of ["input", "change", "reset"]) document.addEventListener(type, clear, true);
  // A field can become valid with no event (C# sets its value on an interactive page). The
  // browser checks before "invalid" fires, so clear ahead of a submit: a click on a submit
  // button, or Enter in a field.
  document.addEventListener(
    "click",
    (event) => (event.target as Element).closest?.('button:not([type]), [type="submit"]') && clear(),
    true
  );
  document.addEventListener("keydown", (event) => event.key === "Enter" && clear(), true);
}

/**
 * On a static page a field's errors come from the last post, and nothing checks them again until
 * the next one. Once the field is edited they may no longer be true, so the first edit hides them:
 * the message, the field's invalid look, aria-invalid, and the error's id from aria-describedby.
 * A form reset puts the posted values back, and with them their errors. An interactive page
 * validates each change in C# and renders the errors itself.
 *
 * The form's FormErrors summary stays: it reports the last post.
 */
export function installStaleErrors(): void {
  // How to show each hidden field's errors again.
  const restore = new Map<HTMLElement, () => void>();
  const edited = (event: Event) => {
    const target = event.target;
    if (!(target instanceof Element)) return;
    // Typing in a Combobox or MultiSelect searches its list; choosing changes the value.
    if (target.classList.contains("ib-combobox-input")) return;
    const field = target.closest<HTMLElement>(".ib-field-invalid");
    // Only static forms: Blazor owns an interactive form's markup.
    if (!field || !field.closest("form")?.querySelector('input[name="_handler"]')) return;
    const error = field.querySelector<HTMLElement>(":scope > .ib-field-error");
    const saved: [Element, string | null, string | null][] = [];
    field.classList.remove("ib-field-invalid");
    for (const el of [field, ...field.querySelectorAll("[aria-invalid], [aria-describedby]")]) {
      const invalid = el.getAttribute("aria-invalid");
      const ids = el.getAttribute("aria-describedby");
      saved.push([el, invalid, ids]);
      el.removeAttribute("aria-invalid");
      if (!error?.id || !ids) continue;
      const rest = ids.split(/\s+/).filter((id) => id && id !== error.id);
      if (rest.length) el.setAttribute("aria-describedby", rest.join(" "));
      else el.removeAttribute("aria-describedby");
    }
    if (error) error.hidden = true;
    // Only while the page still shows this post: an enhanced post since may have changed the
    // field, removing the error or showing a new one.
    restore.set(field, () => {
      if (!error?.isConnected || !error.hidden || field.classList.contains("ib-field-invalid")) return;
      field.classList.add("ib-field-invalid");
      for (const [el, invalid, ids] of saved) {
        if (invalid !== null) el.setAttribute("aria-invalid", invalid);
        if (ids !== null) el.setAttribute("aria-describedby", ids);
      }
      error.hidden = false;
    });
  };
  for (const type of ["input", "change"]) document.addEventListener(type, edited, true);
  document.addEventListener(
    "reset",
    (event) => {
      for (const [field, show] of restore) {
        if (!field.isConnected) restore.delete(field);
        else if (event.target instanceof Node && event.target.contains(field)) {
          show();
          restore.delete(field);
        }
      }
    },
    true
  );
}

/**
 * A DateField's time zone on a static post. A date-time input holds a time with no zone, and
 * Blazor's form binding would read it in the server's zone, so a field with data-ib-zone posts
 * its time in that zone: with the zone's offset for a DateTimeOffset (data-ib-post="offset"),
 * or converted to UTC for a DateTime ("utc"). Building the post's FormData fires "formdata",
 * for enhanced and full posts alike. With JavaScript off the browser posts the time as typed.
 */
export function installDateZones(): void {
  document.addEventListener(
    "formdata",
    (event) => {
      const form = event.target;
      if (!(form instanceof HTMLFormElement)) return;
      const data = (event as FormDataEvent).formData;
      for (const field of form.elements) {
        if (!(field instanceof HTMLInputElement) || !field.dataset.ibZone || !field.name || field.disabled) continue;
        const match = /^(\d{4,})-(\d\d)-(\d\d)(?:T(\d\d):(\d\d)(?::(\d\d)(\.\d+)?)?)?$/.exec(field.value);
        if (!match) continue;
        const [y, mo, d, h, mi, s] = match.slice(1, 7).map((part) => Number(part ?? 0));
        const wall = utc(y, mo, d, h, mi, s);
        const offset = zoneOffset(field.dataset.ibZone, wall);
        const time = field.value.length === 10 ? `${field.value}T00:00` : field.value;
        data.set(field.name, field.dataset.ibPost === "offset" ? time + formatOffset(offset) : iso(new Date(wall - offset * 60000)));
      }
    },
    true
  );
}

/** Milliseconds since 1970 for a UTC date and time; Date.UTC alone reads years 0-99 as 1900-1999. */
function utc(y: number, mo: number, d: number, h: number, mi: number, s: number): number {
  const date = new Date(0);
  date.setUTCFullYear(y, mo - 1, d);
  date.setUTCHours(h, mi, s, 0);
  return date.getTime();
}

/**
 * The zone's offset from UTC in minutes for a wall-clock time in it (given as if it were UTC):
 * a fixed offset ("+05:30") as written, an IANA zone from Intl. Near a change of offset, the one
 * that gives back the same wall-clock time; for a time the change skips or repeats, the
 * standard (smaller) offset, as .NET's TimeZoneInfo.GetUtcOffset picks.
 */
function zoneOffset(zone: string, wall: number): number {
  const fixed = /^([+-])(\d\d):(\d\d)$/.exec(zone);
  // Before 1900 Intl gives local mean time (New York -4:56), where .NET keeps the zone's standard
  // rules: use 1900's offset, so a time posted back unchanged stays the same.
  wall = Math.max(wall, utc(1900, 1, 1, 0, 0, 0));
  if (fixed) return (fixed[1] === "-" ? -1 : 1) * (Number(fixed[2]) * 60 + Number(fixed[3]));
  const at = (instant: number) => {
    const parts = new Intl.DateTimeFormat("en-US", {
      timeZone: zone, hourCycle: "h23", year: "numeric", month: "numeric", day: "numeric",
      hour: "numeric", minute: "numeric", second: "numeric", era: "short",
    }).formatToParts(new Date(instant));
    const part = (type: string) => Number(parts.find((p) => p.type === type)?.value ?? 0);
    const year = parts.find((p) => p.type === "era")?.value === "BC" ? 1 - part("year") : part("year");
    return Math.round((utc(year, part("month"), part("day"), part("hour"), part("minute"), part("second")) - instant) / 60000);
  };
  const before = at(wall - 43200000);
  const after = at(wall + 43200000);
  const fits = [before, after].filter((offset) => at(wall - offset * 60000) === offset);
  return fits.length === 1 ? fits[0] : Math.min(before, after);
}

function formatOffset(minutes: number): string {
  const abs = Math.abs(minutes);
  return `${minutes < 0 ? "-" : "+"}${String(Math.floor(abs / 60)).padStart(2, "0")}:${String(abs % 60).padStart(2, "0")}`;
}

/** A UTC instant as a date-time input writes one, with seconds. */
function iso(date: Date): string {
  return date.toISOString().slice(0, 19);
}
