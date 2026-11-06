/**
 * Places a rich text editor's dialog (link, image) at what it's about, and keeps it there: under
 * it, or above it when there's no room below, within the window, following the page as it scrolls
 * or resizes until the dialog closes. Bundled into each part that has a dialog.
 */
export function place(dialog: HTMLElement, anchor: () => DOMRect): void {
  const position = () => {
    const rect = anchor();
    const height = dialog.offsetHeight;
    const below = rect.bottom + 6;
    const top = below + height > innerHeight - 8 && rect.top - 6 - height > 8 ? rect.top - 6 - height : below;
    dialog.style.top = `${Math.round(top)}px`;
    dialog.style.left = `${Math.round(Math.max(8, Math.min(rect.left, innerWidth - dialog.offsetWidth - 8)))}px`;
  };
  dialog.style.position = "fixed";
  dialog.style.margin = "0";
  dialog.showPopover();
  position();
  const watch = new AbortController();
  addEventListener("scroll", position, { capture: true, passive: true, signal: watch.signal });
  addEventListener("resize", position, { signal: watch.signal });
  dialog.addEventListener("toggle", (event) => {
    if ((event as ToggleEvent).newState === "closed") watch.abort();
  }, { signal: watch.signal });
}
