/**
 * ReconnectDialog: Blazor finds the dialog by its id (components-reconnect-modal) when an
 * interactive server page loses its connection, and reports each state of the reconnection with
 * a components-reconnect-state-changed event on it. This opens and closes the dialog, writes the
 * message from its data attributes, and runs Retry, Resume and Reload, as Blazor's own template
 * (ReconnectModal.razor.js) does. On demand: ../reconnect.ts loads it with the first state, as
 * most pages never lose a connection.
 */

interface ReconnectState {
  state: "show" | "hide" | "retrying" | "paused" | "failed" | "resume-failed" | "rejected";
  currentAttempt?: number;
  secondsToNextAttempt?: number;
}

interface BlazorReconnect {
  reconnect(): Promise<boolean>;
  resumeCircuit(): Promise<boolean>;
}

const blazor = (): BlazorReconnect | undefined => (window as unknown as { Blazor?: BlazorReconnect }).Blazor;

const isReconnectDialog = (target: EventTarget | null): target is HTMLDialogElement =>
  target instanceof HTMLDialogElement && target.hasAttribute("data-ib-reconnect");

/** The dialog's look and words for a state: data-ib-state picks the buttons (CSS), the message is written here. */
function show(dialog: HTMLDialogElement, state: string, message: string): void {
  const changed = dialog.dataset.ibState !== state;
  dialog.dataset.ibState = state;
  const text = dialog.querySelector(".ib-reconnect-message");
  if (text) text.textContent = message;
  if (!dialog.open) {
    warnIfNotAlone();
    dialog.showModal();
  }
  dialog.querySelector<HTMLButtonElement>(`[data-ib-reconnect-action="${actionFor[state] ?? ""}"]`)?.focus();
  // Read once per state, not on every tick of the countdown; written once the dialog shows, as a
  // live region that appears with its text already in it often isn't read.
  const status = dialog.querySelector(".ib-reconnect-status");
  if (status && changed) requestAnimationFrame(() => (status.textContent = message));
}

let warned = false;

/** Two dialogs (or the template's ReconnectModal kept beside this one) would both answer Blazor. */
function warnIfNotAlone(): void {
  if (warned) return;
  if (document.querySelectorAll("#components-reconnect-modal").length > 1 || document.getElementById("components-reconnect-button")) {
    warned = true;
    console.warn("IzzyBlazor: more than one reconnect dialog on the page. Keep one ReconnectDialog and remove the template's ReconnectModal.");
  }
}

function stopRetryWhenVisible(): void {
  if (retryWhenVisible) document.removeEventListener("visibilitychange", retryWhenVisible);
  retryWhenVisible = null;
}

/** Failed to rejoin: Retry, and try again by itself when the reader comes back to the tab, as Blazor's own dialog does. */
function failed(dialog: HTMLDialogElement): void {
  show(dialog, "failed", dialog.dataset.failed ?? "");
  stopRetryWhenVisible();
  retryWhenVisible = () => {
    if (document.visibilityState === "visible") void retry(dialog);
  };
  document.addEventListener("visibilitychange", retryWhenVisible);
}

const actionFor: Record<string, string> = { failed: "retry", paused: "resume", "resume-failed": "reload" };

let retryWhenVisible: (() => void) | null = null;

async function retry(dialog: HTMLDialogElement): Promise<void> {
  stopRetryWhenVisible();
  try {
    const api = blazor();
    if (!api) return location.reload();
    // Rejoin the circuit; if the server has dropped it, resume one from its persisted state.
    if (await api.reconnect()) return;
    if (await api.resumeCircuit()) dialog.close();
    else location.reload();
  } catch {
    // The server still can't be reached: still failed, and still worth a try later.
    failed(dialog);
  }
}

async function resume(dialog: HTMLDialogElement): Promise<void> {
  try {
    if (!(await blazor()?.resumeCircuit())) location.reload();
  } catch {
    show(dialog, "resume-failed", dialog.dataset.resumeFailed ?? "");
  }
}

/** One state of the reconnection, as Blazor reported it on the dialog. */
export function onReconnectState(dialog: HTMLDialogElement, detail: ReconnectState | undefined): void {
  const { state, currentAttempt = 0, secondsToNextAttempt = 0 } = detail ?? {};
  const data = dialog.dataset;
  switch (state) {
    case "show":
      show(dialog, "rejoining", data.rejoining ?? "");
      break;
    case "retrying":
      if (currentAttempt > 1 && secondsToNextAttempt > 0) {
        const template = (secondsToNextAttempt === 1 ? data.retryingOne : data.retrying) ?? "";
        show(dialog, "retrying", template.replace("{0}", String(secondsToNextAttempt)));
      } else {
        show(dialog, "rejoining", data.rejoining ?? "");
      }
      break;
    case "paused":
      show(dialog, "paused", data.paused ?? "");
      break;
    case "failed":
      failed(dialog);
      break;
    case "resume-failed":
      show(dialog, "resume-failed", data.resumeFailed ?? "");
      break;
    case "hide": {
      stopRetryWhenVisible();
      delete dialog.dataset.ibState;
      const status = dialog.querySelector(".ib-reconnect-status");
      if (status) status.textContent = "";
      if (dialog.open) dialog.close();
      break;
    }
    case "rejected":
      location.reload();
      break;
  }
}

// Retry, Resume and Reload. Installed when the module loads, with the first state, before the
// dialog shows.
document.addEventListener("click", (event) => {
  const button = (event.target as Element | null)?.closest?.<HTMLButtonElement>("[data-ib-reconnect-action]");
  const dialog = button?.closest("dialog");
  if (!button || !isReconnectDialog(dialog)) return;
  const action = button.dataset.ibReconnectAction;
  if (action === "retry") void retry(dialog);
  else if (action === "resume") void resume(dialog);
  else location.reload();
});

// Escape can't close it where closedby isn't supported: the page behind can't respond.
document.addEventListener(
  "cancel",
  (event) => {
    if (isReconnectDialog(event.target)) event.preventDefault();
  },
  true
);
