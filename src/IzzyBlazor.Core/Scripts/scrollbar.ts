/**
 * The width of the page's scrollbar while the page scrolls, for AppShell's page scroll.
 *
 * Locking the page (an open dialog, drawer or panel) takes its scrollbar away, which would widen
 * the page and shift it sideways. While the page is locked, CSS pads it by `--ib-scrollbar-w`
 * instead, so nothing moves. This keeps that width current and marks the root with
 * `data-ib-scrollbar` while there is one: none on a page too short to scroll, or with overlay
 * scrollbars. Without script the page reserves the strip with `scrollbar-gutter: stable`.
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
