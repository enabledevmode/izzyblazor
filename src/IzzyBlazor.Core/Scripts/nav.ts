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

/**
 * Since .NET 10, Blazor's enhanced navigation scrolls to the top as soon as a link to another page
 * is followed, before that page arrives, so the old page shows from its top until then
 * (dotnet/aspnetcore#64015). Its handler is on the document: after it, this puts the place back,
 * and keepPlaceArrived() goes to the top once the new page is patched in. Once Blazor scrolls
 * after the patch instead, neither does anything.
 */
let from: { path: string; y: number } | null = null;
let leaving = false;

export function installKeepPlace(): void {
  document.addEventListener(
    "click",
    () => {
      from = { path: location.pathname, y: window.scrollY };
    },
    true
  );
  window.addEventListener("click", (event) => {
    // Only Blazor's own scroll as it followed the link: the interactive router scrolls after it
    // renders, and a link a script handled leaves the address alone.
    if (!from || !event.defaultPrevented || location.pathname === from.path || window.scrollY === from.y) return;
    window.scrollTo(0, from.y);
    leaving = true;
  });
  window.addEventListener("popstate", () => {
    leaving = false;
  });
}

export function keepPlaceArrived(): void {
  if (!leaving) return;
  leaving = false;
  // A link to a #fragment: Blazor has scrolled to it.
  const id = location.hash && decodeURIComponent(location.hash.slice(1));
  if (!id || !document.getElementById(id)) window.scrollTo(0, 0);
}
