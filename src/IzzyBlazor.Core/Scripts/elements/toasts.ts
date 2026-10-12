import { IzzyElement, defineElement } from "../izzy-element";

interface DotNetInvoker {
  invokeMethodAsync(method: string, ...args: unknown[]): Promise<unknown>;
}

export interface ToastDetail {
  message: string;
  status?: string;
  title?: string | null;
  duration?: number | null;
  /** A button on the toast: a link (href) or a C# callback (id, run through invoker). */
  action?: { label: string; href?: string | null; id?: string | null; invoker?: DotNetInvoker | null } | null;
}

interface Timer {
  remaining: number;
  started: number;
  handle: number;
}

/**
 * <ib-toasts> adds timing to a ToastRegion: each toast closes itself after its duration, and
 * the timers pause while a mouse is over the region, while it has focus, and while the tab is
 * hidden. Closing a toast checks its close checkbox, so CSS hides it exactly as it does with
 * no script. Error toasts stay until closed unless a duration is given.
 *
 * It also adds toasts sent from interactive pages (showToast), cloned from the region's
 * templates into its live container. It never removes toasts the server rendered; it only
 * removes the ones it added.
 */
class Toasts extends IzzyElement {
  #timers = new Map<HTMLElement, Timer>();
  #hovered = false;
  #focused = false;
  #paused = false;
  #count = 0;
  #returnFocus: HTMLElement | null = null;

  protected setup(signal: AbortSignal): void {
    this.armAll();

    // Toasts arriving with enhanced navigation or an enhanced form post.
    const observer = new MutationObserver(() => this.armAll());
    observer.observe(this, { childList: true, subtree: true });
    signal.addEventListener("abort", () => {
      observer.disconnect();
      this.#timers.forEach((t) => clearTimeout(t.handle));
      this.#timers.clear();
    });

    // Choosing an action closes its toast (a link navigates as well).
    this.addEventListener(
      "click",
      (event) => {
        const action = (event.target as Element | null)?.closest?.<HTMLElement>(".ib-toast-action");
        const toast = action?.closest<HTMLElement>(".ib-toast");
        if (action && toast) queueMicrotask(() => this.dismiss(toast));
      },
      { signal }
    );

    this.addEventListener(
      "change",
      (event) => {
        const input = event.target as HTMLInputElement;
        const toast = input.closest<HTMLElement>(".ib-toast");
        if (toast && input.matches(".ib-alert-dismiss") && input.checked) this.closed(toast);
      },
      { signal }
    );

    // Hover pauses only for a mouse or pen: on touch screens hover sticks after a tap.
    this.addEventListener(
      "pointerenter",
      (event) => {
        if (event.pointerType !== "touch") {
          this.#hovered = true;
          this.updatePause();
        }
      },
      { signal }
    );
    this.addEventListener(
      "pointerleave",
      () => {
        this.#hovered = false;
        this.updatePause();
      },
      { signal }
    );
    this.addEventListener(
      "focusin",
      (event) => {
        if (!this.#focused && event.relatedTarget instanceof HTMLElement && !this.contains(event.relatedTarget)) {
          this.#returnFocus = event.relatedTarget;
        }
        this.#focused = true;
        this.updatePause();
      },
      { signal }
    );
    this.addEventListener(
      "focusout",
      (event) => {
        if (!(event.relatedTarget instanceof Node) || !this.contains(event.relatedTarget)) {
          this.#focused = false;
          this.updatePause();
        }
      },
      { signal }
    );
    document.addEventListener("visibilitychange", () => this.updatePause(), { signal });
  }

  /** Adds a toast from an interactive page. */
  show(detail: ToastDetail): void {
    const status = detail.status ?? "info";
    const template =
      this.querySelector<HTMLTemplateElement>(`:scope > template[data-status="${CSS.escape(status)}"]`) ??
      this.querySelector<HTMLTemplateElement>(":scope > template[data-status='info']");
    const live = this.querySelector<HTMLElement>(":scope > .ib-toast-live");
    const source = template?.content.firstElementChild ?? template?.firstElementChild;
    if (!source || !live) return;

    const toast = source.cloneNode(true) as HTMLElement;
    const id = `ib-toast-${Date.now().toString(36)}-${++this.#count}`;
    toast.id = id;
    toast.querySelector("input.ib-alert-dismiss")?.setAttribute("id", `${id}-dismiss`);
    toast.querySelector("label.ib-alert-close")?.setAttribute("for", `${id}-dismiss`);

    const title = toast.querySelector<HTMLElement>(".ib-alert-title");
    if (detail.title) {
      if (title) title.textContent = detail.title;
    } else {
      title?.remove();
    }
    const message = toast.querySelector<HTMLElement>("[data-ib-toast-message]");
    if (message) message.textContent = detail.message;
    this.fillAction(toast, detail.action);
    const regionDuration = Number(this.dataset.duration ?? 5000);
    const duration =
      detail.duration ?? (status === "danger" ? 0 : detail.action && regionDuration > 0 ? regionDuration * 2 : null);
    if (duration != null) toast.dataset.duration = String(duration);
    toast.dataset.ibAdded = "";

    live.append(toast);
    this.arm(toast);
  }

  // The template has an empty action button: label it and wire it to C#, swap it for a link,
  // or remove the action row when the toast has none.
  private fillAction(toast: HTMLElement, action: ToastDetail["action"]): void {
    const slot = toast.querySelector<HTMLButtonElement>("[data-ib-toast-action]");
    if (!slot) return;
    if (!action) {
      slot.closest(".ib-alert-actions")?.remove();
      return;
    }
    slot.removeAttribute("data-ib-toast-action");
    if (action.id && action.invoker) {
      const { id, invoker } = action;
      slot.textContent = action.label;
      slot.addEventListener("click", () => {
        invoker.invokeMethodAsync("Run", id).catch(() => {
          // The circuit went away; there's nothing to run it on.
        });
      });
    } else if (action.href) {
      const link = document.createElement("a");
      link.className = slot.className;
      link.href = action.href;
      link.textContent = action.label;
      slot.replaceWith(link);
    } else {
      slot.closest(".ib-alert-actions")?.remove();
    }
  }

  private armAll(): void {
    this.querySelectorAll<HTMLElement>(".ib-toast").forEach((toast) => {
      if (!toast.closest("template")) this.arm(toast);
    });
  }

  // Armed by state, not identity: enhanced navigation can reuse a closed toast's element for a
  // new one, resetting its checkbox, and that toast needs a timer of its own.
  private arm(toast: HTMLElement): void {
    if (this.#timers.has(toast) || this.dismissInput(toast)?.checked !== false) return;
    const duration = Number(toast.dataset.duration ?? this.dataset.duration ?? 5000);
    if (!(duration > 0)) return;
    this.#timers.set(toast, { remaining: duration, started: Date.now(), handle: this.#paused ? 0 : this.start(toast, duration) });
  }

  private dismissInput(toast: HTMLElement): HTMLInputElement | null {
    return toast.querySelector<HTMLInputElement>("input.ib-alert-dismiss");
  }

  private start(toast: HTMLElement, ms: number): number {
    return window.setTimeout(() => this.dismiss(toast), ms);
  }

  private updatePause(): void {
    this.setPaused(this.#hovered || this.#focused || document.visibilityState === "hidden");
  }

  private setPaused(paused: boolean): void {
    if (this.#paused === paused) return;
    this.#paused = paused;
    const now = Date.now();
    for (const [toast, timer] of this.#timers) {
      if (paused) {
        clearTimeout(timer.handle);
        timer.remaining -= now - timer.started;
      } else {
        timer.started = now;
        timer.handle = this.start(toast, Math.max(timer.remaining, 1000));
      }
    }
  }

  private dismiss(toast: HTMLElement): void {
    const input = this.dismissInput(toast);
    if (!input || input.checked) return;
    input.checked = true;
    input.dispatchEvent(new Event("change", { bubbles: true }));
  }

  private closed(toast: HTMLElement): void {
    const timer = this.#timers.get(toast);
    if (timer) clearTimeout(timer.handle);
    this.#timers.delete(toast);

    // Keep keyboard users in place: the next open toast, or wherever they came from.
    if (toast.contains(document.activeElement)) {
      const next = [...this.querySelectorAll<HTMLElement>(".ib-toast")]
        .filter((t) => t !== toast && !t.closest("template") && this.dismissInput(t)?.checked === false)
        .map((t) => this.dismissInput(t))
        .find(Boolean);
      const target = next ?? (this.#returnFocus?.isConnected ? this.#returnFocus : null);
      if (target) target.focus();
      else (document.activeElement as HTMLElement).blur();
    }

    // Only toasts this element added are removed; the server's stay in the DOM, hidden by CSS.
    if (toast.hasAttribute("data-ib-added")) setTimeout(() => toast.remove(), 400);
  }
}

defineElement("ib-toasts", Toasts);

/** Shows a toast in the page's ToastRegion. Called by ToastService on interactive pages. */
export function showToast(detail: ToastDetail): void {
  const region = document.querySelector<HTMLElement & { show?: (d: ToastDetail) => void }>("ib-toasts");
  if (region?.show) region.show(detail);
  else console.warn("IzzyBlazor: add a <ToastRegion /> to the layout to show toasts.");
}
