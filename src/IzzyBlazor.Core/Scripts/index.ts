/**
 * IzzyBlazor's JavaScript initializer. Bundled to wwwroot/IzzyBlazor.Core.lib.module.js,
 * which Blazor loads automatically for any app that references the package; apps
 * add no script tags.
 *
 * Custom elements are defined when this module loads, so they work on static
 * pages too. Custom events are registered with Blazor once it starts, so
 * interactive pages can bind them with @on{event}.
 */
import { reconnect, type IzzyEventDefinition } from "./izzy-element";

// Elements register themselves on import. Add new ones here as they are built.
import "./elements/field-control";
import "./elements/file-upload";
import { resetShellScroll } from "./elements/shell";
import "./elements/popover";
import "./elements/tooltip";
import { syncTabs } from "./elements/tabs";
import { showToast } from "./elements/toasts";
import "./elements/filter-form";
import { applySavedScheme } from "./elements/scheme-switch";
import "./elements/code";
import "./elements/color-picker";
import "./elements/video";
import "./elements/header-nav";
import "./elements/combobox";
import "./elements/table";
import "./elements/banner";
import { installCommandFallback, installDialogAutoClose, reopenAfterPost } from "./elements/dialog";
import { installSubmitFeedback } from "./forms";
import { installSelects, syncSelects } from "./select";
import { openActiveNavGroups } from "./nav";
import { installToggleBridge, type ToggleDetail } from "./toggle";
import { installIndeterminate, syncIndeterminate } from "./checkbox";
import { focusMarked } from "./focus";

installSubmitFeedback();
installSelects();
installIndeterminate();

// ToastService calls this through JS interop on interactive pages.
(window as unknown as { izzyBlazor?: Record<string, unknown> }).izzyBlazor = {
  ...((window as unknown as { izzyBlazor?: Record<string, unknown> }).izzyBlazor ?? {}),
  showToast,
};
installToggleBridge();
installCommandFallback();
installDialogAutoClose();
if (document.readyState === "loading") {
  document.addEventListener("DOMContentLoaded", () => openActiveNavGroups());
} else {
  openActiveNavGroups();
}

/** Custom events Blazor should know about. Elements append to this list. */
export const izzyEvents: IzzyEventDefinition[] = [
  {
    // Disclosures, popovers and dialogs report opening and closing for @bind-Open.
    name: "ibtoggle",
    createEventArgs: (event) => {
      const detail = (event as CustomEvent<ToggleDetail>).detail;
      return { id: detail?.id ?? null, open: !!detail?.open };
    },
  },
  {
    // ColorSchemeSwitch reports the user's choice for @bind-Value.
    name: "ibscheme",
    createEventArgs: (event) => ({ scheme: (event as CustomEvent<{ scheme: string }>).detail?.scheme ?? null }),
  },
];

interface BlazorLike {
  registerCustomEventType(name: string, options: { browserEventName?: string; createEventArgs: (e: Event) => unknown }): void;
  addEventListener?(name: "enhancedload", callback: () => void): void;
}

let registered = false;

function registerEvents(blazor: BlazorLike): void {
  if (registered) return;
  registered = true;
  for (const { name, createEventArgs } of izzyEvents) {
    blazor.registerCustomEventType(name, { createEventArgs });
  }
}

/** Blazor Web App initializer hook. */
export function afterWebStarted(blazor: BlazorLike): void {
  registerEvents(blazor);
  blazor.addEventListener?.("enhancedload", () => {
    openActiveNavGroups();
    resetShellScroll();
    syncTabs();
    syncSelects();
    syncIndeterminate();
    reopenAfterPost();
    focusMarked();
    applySavedScheme();
    reconnect("ib-field-control, ib-file-upload, ib-tooltip, ib-combobox, ib-table, ib-popover, ib-dialog, ib-banner");
  });
}

/** Initializer hook for standalone Blazor WebAssembly and Blazor Server apps. */
export function afterStarted(blazor: BlazorLike): void {
  registerEvents(blazor);
}
