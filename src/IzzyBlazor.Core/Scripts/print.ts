/**
 * Print buttons (Button, IconButton or MenuItem with Print): a click opens the browser's print
 * dialog, where Save as PDF is a destination. One listener on the document, so buttons an enhanced
 * load or an interactive render adds work too. With JavaScript off, CSS hides them.
 */
export function installPrint(): void {
  document.addEventListener("click", (event) => {
    const button = (event.target as Element | null)?.closest?.("[data-ib-print]");
    if (!(button instanceof HTMLButtonElement) || button.disabled || button.getAttribute("aria-disabled") === "true") return;
    window.print();
  });
}
