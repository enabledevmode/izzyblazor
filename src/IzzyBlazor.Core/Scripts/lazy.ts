/**
 * Elements whose code loads only on pages that have one. Each module calls defineElement when it
 * loads, and the browser then upgrades the elements already on the page (connectedCallback runs
 * as usual). Until then they show their server-rendered markup laid out as it will be with script
 * (CSS decides by @media (scripting), not by the element being defined), so nothing moves when
 * the code arrives; their controls are inert for that moment. If the code can't load, the elements
 * get data-ib-failed, and CSS lays them out as with no script, which works on its own.
 *
 * Everything else (fields, popovers, menus, dialogs, tabs, the shell) is in the main module,
 * since nearly every page has one. What every page needs from an element that loads here stays
 * in the main module: the saved color scheme (scheme.ts) and keeping dismissed messages closed
 * (dismiss.ts). Each name below is a file of its own in wwwroot/elements, built separately
 * (build/build-assets.mjs, lazyElements) and loaded relative to this module.
 */
// Each name is ib-<name> and the file elements/<name>.js beside this module (CI checks that the
// built files are exactly these). A list, not an import() each: every page downloads this one.
// Not elements, loaded by other code: password (a password TextField's Caps Lock warning and
// requirements) and mask (a TextField's Mask), by field-control.ts; reconnect (ReconnectDialog's
// code), with the first reconnection state (reconnect.ts); announce (Announcer's messages), by
// index.ts on the first message.
const lazyNames =
  "combobox table grid grid-columns col-picker grid-edit filter-form chat filter-list filter-builder file-upload color-picker color-panel duration-field calendar period-grid time-slots field-clear rating pin-field video code copy on-this-page toasts toolbar context-menu speed-dial leave-guard sortable listview tree treeselect pick-list rich-text splitter carousel chart scheduler tooltip dismiss banner site-nav scheme-switch password mask reconnect announce".split(" ");

const loading = new Map<string, Promise<unknown>>();
const failures = new Map<string, number>();

/**
 * Loads an element's code once; later calls get the same promise. If the request fails (a
 * deploy, a flaky connection), the next call tries again. The browser remembers a failed module
 * URL, so a retry asks for a new one: elements/x.js?retry=1, beside this module, which the
 * server serves without the import map's fingerprint.
 */
export function load<T = unknown>(tag: string): Promise<T> {
  let module = loading.get(tag);
  if (!module) {
    const retry = failures.get(tag) ?? 0;
    module = import(new URL(`elements/${tag.slice(3)}.js${retry ? `?retry=${retry}` : ""}`, import.meta.url).href).catch(
      (error: unknown) => {
        loading.delete(tag);
        failures.set(tag, retry + 1);
        mark(tag, true);
        throw error;
      }
    );
    module.then(() => mark(tag, false), () => {});
    loading.set(tag, module);
  }
  return module as Promise<T>;
}

/** Marks a tag's elements as without their code (or clears it, once a retry loads). */
function mark(tag: string, failed: boolean): void {
  for (const element of document.querySelectorAll(tag)) element.toggleAttribute("data-ib-failed", failed);
}

/** Loads the code for the elements in a part of the page (the whole page by default). */
export function loadElementsIn(root: Element | Document = document): void {
  for (const name of lazyNames) {
    const tag = `ib-${name}`;
    if (loading.has(tag)) continue;
    if (root instanceof Element ? root.localName === tag || root.querySelector(tag) : root.querySelector(tag)) {
      load(tag).catch((error: unknown) => console.warn(`IzzyBlazor: <${tag}> didn't load; it works as without script.`, error));
    }
  }
}

/** Loads the code for elements on the page now, and for any added later (enhanced navigation, interactive renders, streaming). */
export function installLazyElements(): void {
  loadElementsIn();
  new MutationObserver((records) => {
    for (const record of records) {
      for (const node of record.addedNodes) if (node instanceof Element) loadElementsIn(node);
    }
  }).observe(document.documentElement, { childList: true, subtree: true });
}
