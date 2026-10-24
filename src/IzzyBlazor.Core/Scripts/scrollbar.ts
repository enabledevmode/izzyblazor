/**
 * The width of the page's scrollbar, for pages that reserve room for it (AppShell's page scroll
 * sets `scrollbar-gutter: stable` on the root, so locking the page doesn't widen it).
 *
 * Browsers draw that reserved strip above the top layer, so an open dialog, drawer or panel and
 * its backdrop stopped one scrollbar short of the window's edge. While the page is locked, CSS
 * gives the strip up and pads the page by `--ib-scrollbar-w` instead, so nothing moves and the
 * overlay reaches the edge. This keeps that width current and marks the root with
 * `data-ib-scrollbar` while there is one; without script the strip stays.
 */
export function measureScrollbar(): void {
  const root = document.documentElement;
  // A locked page has given its scrollbar up and measures 0: keep the last width.
  if (getComputedStyle(root).overflowY === "hidden") return;
  const width = window.innerWidth - root.clientWidth;
  const value = width > 0 ? `${width}px` : "";
  if (root.style.getPropertyValue("--ib-scrollbar-w") === value) return;
  if (value) {
    root.style.setProperty("--ib-scrollbar-w", value);
    root.setAttribute("data-ib-scrollbar", "");
  } else {
    root.style.removeProperty("--ib-scrollbar-w");
    root.removeAttribute("data-ib-scrollbar");
  }
}

export function installScrollbarWidth(): void {
  measureScrollbar();
  // Zoom and resizing change it.
  window.addEventListener("resize", measureScrollbar, { passive: true });
  // Measured again just before anything that might lock the page.
  document.addEventListener("pointerdown", measureScrollbar, { capture: true, passive: true });
  document.addEventListener("keydown", (event) => (event.key === "Enter" || event.key === " ") && measureScrollbar(), { capture: true, passive: true });
}
