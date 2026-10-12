/**
 * Opens any NavGroup that contains the current page, so the active link is visible.
 * Runs on load and after every enhanced navigation. NavGroup can also open itself on the
 * server through its ActivePrefix parameter, which covers the no-JavaScript case.
 */
export function openActiveNavGroups(root: ParentNode = document): void {
  root.querySelectorAll<HTMLDetailsElement>("details.ib-nav-group:not([open])").forEach((group) => {
    if (group.querySelector('[aria-current="page"]')) group.open = true;
  });
}
