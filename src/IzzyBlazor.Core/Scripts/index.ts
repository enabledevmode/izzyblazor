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

// Elements register themselves on import. Add new ones here as they are built, or to lazy.ts
// if most pages won't have one.
import "./elements/field-control";
import { resetShellScroll, revealActiveNav, syncShells } from "./elements/shell";
import "./elements/popover";
import "./elements/tooltip";
import { syncTabs } from "./elements/tabs";
import type { ToastDetail } from "./elements/toasts";
import { applySavedScheme } from "./elements/scheme-switch";
import "./elements/site-nav";
import "./elements/banner";
import { installDismissMemory, keepDismissed } from "./elements/dismiss";
import { clearClosedDialogQuery, installCommandFallback, installDialogAutoClose, reopenAfterPost } from "./elements/dialog";
import { installStaleErrors, installSubmitFeedback, installValidityMessages } from "./forms";
import { installSelects, syncSelects } from "./select";
import { installKeepPlace, keepPlaceArrived, openActiveNavGroups } from "./nav";
import { installToggleBridge, type ToggleDetail } from "./toggle";
import { installIndeterminate, installSelectAll, syncIndeterminate } from "./checkbox";
import { focusMarked, installFocusRescue, installSkipLinks } from "./focus";
import { installLazyElements, load, loadElementsIn } from "./lazy";
import { installScrollbarWidth, measureScrollbar } from "./scrollbar";
import { installTabScroll, revealSelectedTabs } from "./tab-scroll";

installLazyElements();
installSubmitFeedback();
installValidityMessages();
installStaleErrors();
installSelects();
installIndeterminate();
installSelectAll();
installScrollbarWidth();
installTabScroll();

// ToastService calls this through JS interop on interactive pages.
(window as unknown as { izzyBlazor?: Record<string, unknown> }).izzyBlazor = {
  ...((window as unknown as { izzyBlazor?: Record<string, unknown> }).izzyBlazor ?? {}),
  showToast: (detail: ToastDetail) =>
    load<typeof import("./elements/toasts")>("ib-toasts").then((toasts) => toasts.showToast(detail)),
};
installToggleBridge();
installCommandFallback();
installDialogAutoClose();
installDismissMemory();
installSkipLinks();
installFocusRescue();
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
      return { id: detail?.id ?? null, open: !!detail?.open, seq: detail?.seq ?? 0 };
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
  installKeepPlace();
  blazor.addEventListener?.("enhancedload", () => {
    keepPlaceArrived();
    openActiveNavGroups();
    resetShellScroll();
    syncShells();
    revealActiveNav();
    syncTabs();
    revealSelectedTabs();
    syncSelects();
    syncIndeterminate();
    reopenAfterPost();
    clearClosedDialogQuery();
    keepDismissed();
    focusMarked();
    applySavedScheme();
    measureScrollbar();
    // Enhanced navigation can keep an element in place whose code failed to load: try again.
    loadElementsIn();
    reconnect("ib-field-control, ib-file-upload, ib-tooltip, ib-combobox, ib-table, ib-popover, ib-dialog, ib-banner, ib-on-this-page, ib-toolbar");
  });
}

/** Initializer hook for standalone Blazor WebAssembly and Blazor Server apps. */
export function afterStarted(blazor: BlazorLike): void {
  registerEvents(blazor);
}
