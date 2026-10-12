/**
 * Base class for IzzyBlazor's custom elements.
 *
 * Rules every element follows (see the Component Proposals doc):
 * - Light DOM only. The server renders the real markup; the element only adds behavior.
 * - Change attributes, classes and ARIA state, never content that Blazor renders,
 *   so Blazor's DOM diffing on interactive pages never fights the element.
 * - Set up in connectedCallback and tear down in disconnectedCallback. The browser
 *   calls these again when enhanced navigation swaps content in, so elements
 *   re-initialize on their own.
 */
export abstract class IzzyElement extends HTMLElement {
  #abort: AbortController | null = null;

  connectedCallback(): void {
    this.#abort?.abort();
    const abort = new AbortController();
    this.#abort = abort;
    // Interactive Blazor inserts an element before its children, so wait until the
    // current render batch has finished before looking inside.
    queueMicrotask(() => {
      if (this.#abort === abort && this.isConnected) this.setup(abort.signal);
    });
  }

  disconnectedCallback(): void {
    this.#abort?.abort();
    this.#abort = null;
    this.teardown();
  }

  /**
   * Attach listeners here, passing `{ signal }` so they are removed automatically
   * when the element disconnects.
   */
  protected abstract setup(signal: AbortSignal): void;

  /** Optional extra cleanup. */
  protected teardown(): void {}

  /** Dispatches a bubbling custom event that Blazor can bind to on interactive pages. */
  protected emit<T>(name: string, detail: T): void {
    this.dispatchEvent(new CustomEvent<T>(name, { bubbles: true, detail }));
  }
}

/** A custom event Blazor should understand, registered in afterWebStarted. */
export interface IzzyEventDefinition {
  /** DOM event name, such as "tabchange". Blazor binds it as @ontabchange. */
  name: string;
  /** Builds the object passed to the C# EventArgs type. */
  createEventArgs: (event: Event) => unknown;
}

const registry = new Map<string, CustomElementConstructor>();

/**
 * Runs setup again for elements that enhanced navigation kept in place. Enhanced navigation
 * resets attributes to the server's markup but doesn't reconnect elements, so anything an
 * element set (aria-describedby, a revealed button) is lost until it sets up again.
 */
export function reconnect(selector: string): void {
  document.querySelectorAll(selector).forEach((el) => {
    if (el instanceof IzzyElement && el.isConnected) el.connectedCallback();
  });
}

/** Registers a custom element once, even if the module is evaluated twice. */
export function defineElement(tag: string, ctor: CustomElementConstructor): void {
  if (!customElements.get(tag)) {
    customElements.define(tag, ctor);
  }
  registry.set(tag, ctor);
}
