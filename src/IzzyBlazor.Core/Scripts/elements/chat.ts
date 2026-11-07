import { IzzyElement, defineElement } from "../izzy-element";

// How far from the newest entry the reader must have scrolled for "Jump to latest" to show.
const AWAY_PX = 48;

/**
 * <ib-chat> adds the keyboard and scrolling to a Chat. The server renders a form that posts and
 * a log that starts at its newest entry and stays there as entries arrive (column-reverse), so
 * this only adds what needs script:
 * - Enter sends and Shift+Enter adds a line, as in every chat; not while an IME is composing
 *   (Enter picks a word there), and not while a reply is on its way (Send isn't there).
 * - The box grows with what's typed, where CSS field-sizing can't.
 * - "Jump to latest" shows when the reader has scrolled up, and scrolls back down.
 * - Sending scrolls to the newest entry wherever the reader was, and keeps focus in the box,
 *   which also takes it back when Stop, focused, goes.
 */
class Chat extends IzzyElement {
  #sent = false;
  #stopFocused = false;

  private get log(): HTMLElement | null {
    return this.querySelector<HTMLElement>(".ib-chat-log");
  }

  private get input(): HTMLTextAreaElement | null {
    return this.querySelector<HTMLTextAreaElement>(".ib-chat-input");
  }

  private get jump(): HTMLButtonElement | null {
    return this.querySelector<HTMLButtonElement>(".ib-chat-jump");
  }

  protected setup(signal: AbortSignal): void {
    // The keys hint is part of the box's description once the keys work.
    const hint = this.querySelector<HTMLElement>(".ib-chat-hint");
    const input = this.input;
    if (hint && input) {
      hint.hidden = false;
      input.setAttribute("aria-describedby", hint.id);
    }

    this.addEventListener("keydown", (event) => {
      const box = event.target as HTMLElement;
      if (!box.matches?.(".ib-chat-input") || event.key !== "Enter" || event.shiftKey || event.altKey || event.ctrlKey || event.metaKey) return;
      if (event.isComposing || event.keyCode === 229) return;
      event.preventDefault();
      const form = (box as HTMLTextAreaElement).form;
      // Send has no name; Stop (on a static page, also a submit button) does.
      const send = form?.querySelector<HTMLButtonElement>("button[type=submit]:not([name])");
      if (!form || !send || !(box as HTMLTextAreaElement).value.trim()) return;
      form.requestSubmit(send);
    }, { signal });

    this.addEventListener("submit", () => {
      this.#sent = true;
    }, { signal, capture: true });

    this.addEventListener("input", (event) => {
      if ((event.target as Element).matches?.(".ib-chat-input")) this.grow(event.target as HTMLTextAreaElement);
    }, { signal });

    this.addEventListener("focusin", (event) => {
      this.#stopFocused = (event.target as Element).matches?.(".ib-chat-action:not([type=submit]), button[name=ib-chat-stop]") ?? false;
    }, { signal });

    this.log?.addEventListener("scroll", () => this.away(), { signal, passive: true });
    this.addEventListener("click", (event) => {
      if (!(event.target as Element).closest?.(".ib-chat-jump")) return;
      this.toEnd();
      this.input?.focus();
    }, { signal });

    // New entries, a reply growing, the form rendered again after a post.
    const observer = new MutationObserver(() => this.changed());
    observer.observe(this, { childList: true, subtree: true, characterData: true });
    signal.addEventListener("abort", () => observer.disconnect());
    this.away();
  }

  private changed(): void {
    if (this.#sent) {
      this.#sent = false;
      this.toEnd();
      this.input?.focus({ preventScroll: true });
    }

    // Stop had focus and went: the box takes it.
    if (this.#stopFocused && (document.activeElement === document.body || !this.contains(document.activeElement))) {
      this.#stopFocused = false;
      this.input?.focus({ preventScroll: true });
    }

    const input = this.input;
    if (input && !input.value) input.style.removeProperty("block-size");
    this.away();
  }

  /** The log reversed: its end is scrollTop 0, and scrolling up goes negative. */
  private toEnd(): void {
    const log = this.log;
    if (log) log.scrollTop = 0;
  }

  private away(): void {
    const log = this.log;
    const jump = this.jump;
    if (log && jump) jump.hidden = -log.scrollTop <= AWAY_PX;
  }

  /** Grows the box to what's typed, up to its CSS max, where field-sizing doesn't. */
  private grow(box: HTMLTextAreaElement): void {
    if (CSS.supports("field-sizing", "content")) return;
    box.style.blockSize = "auto";
    box.style.blockSize = `${box.scrollHeight}px`;
  }
}

defineElement("ib-chat", Chat);
