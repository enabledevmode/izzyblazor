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
