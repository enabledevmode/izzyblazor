/**
 * Print buttons (Button, IconButton or MenuItem with Print): a click opens the browser's print
 * dialog, where Save as PDF is a destination. One listener on the document, so buttons an enhanced
 * load or an interactive render adds work too. With JavaScript off, CSS hides them.
 */
export function installPrint(): void {
  document.addEventListener("click", (event) => {
    // An enabled button, not a disabled or loading one.
    if ((event.target as Element | null)?.closest?.("button[data-ib-print]:not(:disabled, [aria-disabled=true])")) window.print();
  });
}
