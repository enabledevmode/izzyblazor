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

  // Read-only checkboxes, radios and sliders (aria-readonly, on the input or its radio group):
  // HTML has no read-only for them, so cancel what would change them: a click (also from Space or
  // the label), and the keys that move a radio group's choice or a slider. CSS stops the pointer
  // without script.
  const readOnly = (target: EventTarget | null): target is HTMLInputElement =>
    target instanceof HTMLInputElement &&
    (target.getAttribute("aria-readonly") === "true" || !!target.closest('[role="radiogroup"][aria-readonly="true"]'));
  document.addEventListener("click", (event) => readOnly(event.target) && event.preventDefault(), true);
  document.addEventListener(
    "keydown",
    (event) =>
      readOnly(event.target) &&
      event.target.type !== "checkbox" &&
      /^(Arrow|Page|Home|End)/.test(event.key) &&
      event.preventDefault(),
    true
  );

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
  const clear = () => ours.forEach((_, field) => release(field));
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
