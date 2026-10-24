/**
 * Elements the server marks with data-ib-autofocus (FormErrors after a failed post) also carry the
 * native autofocus attribute, which covers full page loads, with or without script. An enhanced
 * form post patches the page in place instead, so autofocus doesn't fire: this moves focus there.
 */
export function focusMarked(): void {
  const target = document.querySelector<HTMLElement>("[data-ib-autofocus]");
  if (!target || target.contains(document.activeElement)) return;
  target.focus();
}

/**
 * A skip link (AppShell's "Skip to content") moves focus to its target natively, but Blazor's
 * enhanced navigation handles the click itself and only scrolls. Focus the target after it, so
 * the next Tab starts in the main area. The target needs tabindex="-1" (AppMain has it).
 */
export function installSkipLinks(): void {
  document.addEventListener("click", (event) => {
    const link = (event.target as Element | null)?.closest?.<HTMLAnchorElement>("a.ib-skip-link");
    const id = link?.hash.slice(1);
    if (!id) return;
    requestAnimationFrame(() => {
      const target = document.getElementById(decodeURIComponent(id));
      if (target && target !== document.activeElement) target.focus();
    });
  });
}

const FOCUSABLE =
  'a[href], button:not([disabled]), input:not([disabled]):not([type="hidden"]), select:not([disabled]), ' +
  'textarea:not([disabled]), summary, [tabindex]:not([tabindex="-1"])';

/**
 * When the focused control is removed (a Tag's remove button, a row's Delete, a Restore link that
 * hides itself), the browser drops focus to the page, and keyboard and screen reader users lose
 * their place. On an interactive page Blazor removes it; on a static page an enhanced post or
 * link patches it away. This moves focus to the control that took its place: the first one after
 * where it was in the same container (one the render just added counts), else the last one before
 * it there, else the next one on the page.
 *
 * It acts only when the focused element left the document while it still had focus (a blur
 * first, by the user or by code, clears it), and only on the same page: a navigation elsewhere
 * starts at the top as usual, and code that moves focus itself (dialogs, toasts, menus) has done
 * so before this looks.
 */
export function installFocusRescue(): void {
  let last: HTMLElement | null = null;
  let page = "";
  document.addEventListener(
    "focusin",
    (event) => {
      if (event.target instanceof HTMLElement && event.target !== document.body) {
        last = event.target;
        page = location.pathname;
      }
    },
    true
  );
  // A real blur leaves the element in the document; a removal doesn't. Only the removal counts.
  document.addEventListener(
    "focusout",
    (event) => {
      const target = event.target;
      queueMicrotask(() => {
        if (last === target && last.isConnected) last = null;
      });
    },
    true
  );

  new MutationObserver((records) => {
    if (!last || last.isConnected) return;
    const lost = last;
    last = null;
    const active = document.activeElement;
    if ((active && active !== document.body) || location.pathname !== page) return;
    const record = records.find((r) => [...r.removedNodes].some((n) => n === lost || n.contains(lost)));
    // Toasts move focus themselves, and the region puts its own toasts back.
    if (!record || (record.target instanceof Element && record.target.closest("ib-toasts"))) return;
    rescue(record);
  }).observe(document.documentElement, { childList: true, subtree: true });
}

// The records of one batch are all applied by now, so the removed node's siblings may be gone
// too (Blazor removes its marker comments beside it): search the container as it is now, from a
// sibling still in it.
function rescue(record: MutationRecord): void {
  const container = record.target;
  if (!(container instanceof Element) || !container.isConnected) return;
  const usable = (el: HTMLElement) => !el.closest("template, [inert]") && el.checkVisibility();
  const inside = [...container.querySelectorAll<HTMLElement>(FOCUSABLE)].filter(usable);
  const anchor = [record.previousSibling, record.nextSibling].find((n) => n?.isConnected && n.parentNode === container);
  const isAfter = (el: HTMLElement) =>
    !anchor ||
    (anchor === record.previousSibling
      ? !anchor.contains(el) && !!(anchor.compareDocumentPosition(el) & Node.DOCUMENT_POSITION_FOLLOWING)
      : anchor.contains(el) || !!(anchor.compareDocumentPosition(el) & Node.DOCUMENT_POSITION_FOLLOWING));
  const after = inside.find(isAfter);
  const before = inside.filter((el) => !isAfter(el)).pop();
  // No sibling left to measure from, but it was the last in its container: the last control there
  // took its place (one the render added at the end, or the one before it).
  const target = !anchor && record.nextSibling === null ? inside[inside.length - 1] : after ?? before;
  if (target) {
    target.focus();
    return;
  }
  // Nothing left in its container: the next control after it on the page, without scrolling there.
  [...document.querySelectorAll<HTMLElement>(FOCUSABLE)]
    .find((el) => usable(el) && !container.contains(el) && !!(container.compareDocumentPosition(el) & Node.DOCUMENT_POSITION_FOLLOWING))
    ?.focus({ preventScroll: true });
}
