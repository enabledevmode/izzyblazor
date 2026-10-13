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
