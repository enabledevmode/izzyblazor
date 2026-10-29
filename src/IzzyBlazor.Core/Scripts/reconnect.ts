import { load } from "./lazy";

/**
 * ReconnectDialog's states, from Blazor: a components-reconnect-state-changed event on the dialog,
 * one that doesn't bubble, so it's caught on the way down. The code that handles them
 * (elements/reconnect.ts) loads with the first one, as most pages never lose a connection; the
 * states that follow wait for it in order.
 */
export function installReconnectDialog(): void {
  document.addEventListener(
    "components-reconnect-state-changed",
    (event) => {
      const dialog = event.target;
      if (!(dialog instanceof HTMLDialogElement) || !dialog.hasAttribute("data-ib-reconnect")) return;
      const detail = (event as CustomEvent).detail;
      void load<typeof import("./elements/reconnect")>("ib-reconnect").then((module) => module.onReconnectState(dialog, detail));
    },
    true
  );
}
