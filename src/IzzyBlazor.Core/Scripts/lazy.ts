/**
 * Elements whose code loads only on pages that have one. Each module calls defineElement when it
 * loads, and the browser then upgrades the elements already on the page (connectedCallback runs
 * as usual). Until then they show their server-rendered markup, which works without script.
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
  "ib-toasts": () => import("./elements/toasts.js"),
};

const loading = new Map<string, Promise<unknown>>();

/** Loads an element's code once; later calls get the same promise. */
export function load<T = unknown>(tag: string): Promise<T> {
  let module = loading.get(tag);
  if (!module) {
    module = loaders[tag]();
    loading.set(tag, module);
  }
  return module as Promise<T>;
}

function scan(): void {
  for (const tag in loaders) {
    if (!loading.has(tag) && document.querySelector(tag)) load(tag);
  }
}

/** Loads the code for elements on the page now, and for any added later (enhanced navigation, interactive renders, streaming). */
export function installLazyElements(): void {
  scan();
  const observer = new MutationObserver(() => {
    scan();
    if (loading.size === Object.keys(loaders).length) observer.disconnect();
  });
  observer.observe(document.documentElement, { childList: true, subtree: true });
}
