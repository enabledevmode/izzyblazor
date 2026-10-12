/**
 * Select with ItemTemplate. Options hold markup and the closed field holds a
 * <selectedcontent> copy of the chosen one.
 *
 * - Where the customizable select isn't supported, the browser shows an option's text run
 *   together ("Pro$12/month"), so each option's data-ib-label becomes its label.
 * - Where it is, the browser copies the chosen option into <selectedcontent>, but only when the
 *   choice changes. Blazor can replace that copy (enhanced navigation) or the option's markup
 *   (an interactive render), so the copy is refreshed after both.
 */
const supported = typeof CSS !== "undefined" && CSS.supports("appearance", "base-select");

function labelOptions(root: ParentNode): void {
  root.querySelectorAll<HTMLOptionElement>("option[data-ib-label]").forEach((option) => {
    const label = option.dataset.ibLabel ?? "";
    if (option.getAttribute("label") !== label) option.setAttribute("label", label);
  });
}

function refresh(select: HTMLSelectElement): void {
  const target = select.querySelector("selectedcontent");
  const option = select.selectedOptions[0];
  if (!target) return;
  const copy = option ? [...option.childNodes].map((n) => n.cloneNode(true)) : [];
  // Compare markup so the observer below doesn't loop on its own change.
  const html = option?.innerHTML ?? "";
  if (target.innerHTML !== html) target.replaceChildren(...copy);
}

/** Brings every templated select up to date. Called after enhanced navigation. */
export function syncSelects(root: ParentNode = document): void {
  if (supported) root.querySelectorAll<HTMLSelectElement>("select:has(> button > selectedcontent)").forEach(refresh);
  else labelOptions(root);
}

export function installSelects(): void {
  if (typeof document === "undefined") return;

  const run = () => {
    syncSelects();
    // Interactive renders add options or change their markup without any event.
    let queued = false;
    new MutationObserver((records) => {
      if (queued) return;
      const relevant = records.some((r) => {
        const el = r.target instanceof Element ? r.target : r.target.parentElement;
        return el?.closest("select") || [...r.addedNodes].some((n) => n instanceof Element && (n.matches("select, option") || n.querySelector("option[data-ib-label]")));
      });
      if (!relevant) return;
      queued = true;
      queueMicrotask(() => {
        queued = false;
        syncSelects();
      });
    }).observe(document.body, { childList: true, subtree: true, characterData: true, attributes: true, attributeFilter: ["data-ib-label", "value", "selected"] });
  };

  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", run, { once: true });
  else run();

  // Blazor sets select.value directly on interactive pages; that fires no event.
  if (supported) document.addEventListener("change", (e) => e.target instanceof HTMLSelectElement && refresh(e.target), true);
}
