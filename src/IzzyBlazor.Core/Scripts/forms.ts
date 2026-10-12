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
