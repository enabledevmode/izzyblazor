/**
 * Elements whose code loads only on pages that have one. Each module calls defineElement when it
 * loads, and the browser then upgrades the elements already on the page (connectedCallback runs
 * as usual). Until then they show their server-rendered markup laid out as it will be with script
 * (CSS decides by @media (scripting), not by the element being defined), so nothing moves when
 * the code arrives; their controls are inert for that moment. If the code can't load, the elements
 * get data-ib-failed, and CSS lays them out as with no script, which works on its own.
 *
 * Everything else (fields, popovers, menus, dialogs, tabs, the shell) is in the main module,
 * since nearly every page has one. Each import() below is a file of its own in wwwroot/elements,
 * built separately (build/build-assets.mjs, lazyElements) and loaded relative to this module.
 */
const loaders: Record<string, () => Promise<unknown>> = {
  "ib-combobox": () => import("./elements/combobox.js"),
  "ib-table": () => import("./elements/table.js"),
  "ib-filter-form": () => import("./elements/filter-form.js"),
  "ib-file-upload": () => import("./elements/file-upload.js"),
  "ib-color-picker": () => import("./elements/color-picker.js"),
  "ib-video": () => import("./elements/video.js"),
  "ib-code": () => import("./elements/code.js"),
  "ib-copy": () => import("./elements/copy.js"),
  "ib-on-this-page": () => import("./elements/on-this-page.js"),
  "ib-toasts": () => import("./elements/toasts.js"),
  "ib-toolbar": () => import("./elements/toolbar.js"),
};

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
    module = (retry ? import(new URL(`elements/${tag.slice(3)}.js?retry=${retry}`, import.meta.url).href) : loaders[tag]()).catch(
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
  for (const tag in loaders) {
    if (loading.has(tag)) continue;
    if (root instanceof Element ? root.localName === tag || root.querySelector(tag) : root.querySelector(tag)) {
      load(tag).catch((error: unknown) => console.warn(`IzzyBlazor: couldn't load <${tag}>; it works as without script until it loads.`, error));
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
