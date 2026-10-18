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

  // A Form with Tabs renders novalidate (data-ib-validate), so with JavaScript off the server
  // checks it: the browser's own check can't reach a field in a closed tab. With script, run the
  // browser's check here; Tabs opens the tab of the first invalid field (tabs.ts).
  document.addEventListener(
    "submit",
    (event) => {
      const form = event.target;
      if (!(form instanceof HTMLFormElement) || !form.hasAttribute("data-ib-validate")) return;
      if ((event as SubmitEvent).submitter?.hasAttribute("formnovalidate") || form.reportValidity()) return;
      event.preventDefault();
      event.stopImmediatePropagation();
    },
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
  const ours = new Set<HTMLInputElement>();
  document.addEventListener(
    "invalid",
    (event) => {
      const field = event.target as HTMLInputElement;
      if (ours.delete(field)) field.setCustomValidity("");
      else if (field.validity.customError) return;
      for (const [check, key] of messageFor) {
        const message = field.dataset[key];
        if (message && field.validity[check]) {
          field.setCustomValidity(message);
          ours.add(field);
          return;
        }
      }
    },
    true
  );
  const clear = () => {
    ours.forEach((field) => field.setCustomValidity(""));
    ours.clear();
  };
  for (const type of ["input", "change", "reset"]) document.addEventListener(type, clear, true);
}
