/**
 * Reports open and close changes as a bubbling `ibtoggle` event with `{ id, open }`, which
 * Blazor binds as @onibtoggle on interactive pages so `@bind-Open` follows the browser.
 *
 * The native `toggle` event doesn't bubble, so one capturing listener on the document covers
 * every <details class="ib-disclosure"> and every [popover].ib-pop outside <ib-popover>.
 * Dialogs and <ib-popover> panels report their own changes, leaving out the ones the server
 * asked for (see elements/dialog.ts).
 */
export interface ToggleDetail {
  id: string;
  open: boolean;
}

export function emitToggle(element: HTMLElement, open: boolean): void {
  element.dispatchEvent(new CustomEvent<ToggleDetail>("ibtoggle", { bubbles: true, detail: { id: element.id, open } }));
}

let installed = false;

export function installToggleBridge(): void {
  if (installed) return;
  installed = true;

  document.addEventListener(
    "toggle",
    (event) => {
      const target = event.target;
      if (target instanceof HTMLDetailsElement && target.classList.contains("ib-disclosure")) {
        emitToggle(target, target.open);
      } else if (target instanceof HTMLElement && target.classList.contains("ib-pop") && target.parentElement?.localName !== "ib-popover") {
        emitToggle(target, (event as Event & { newState?: string }).newState === "open");
      }
    },
    true
  );

  // Disabled accordion items: CSS removes the pointer; this also stops keyboard and script clicks.
  document.addEventListener(
    "click",
    (event) => {
      const summary = (event.target as Element | null)?.closest?.("summary[aria-disabled='true']");
      if (summary) event.preventDefault();
    },
    true
  );
}
