/**
 * Checkbox's Indeterminate. The server marks the input with data-indeterminate, which CSS shows as
 * a dash with no script. Here the input also gets the real indeterminate state, so screen readers
 * announce "mixed", and choosing the box clears it: data-indeterminate-cleared tells CSS the dash
 * is gone, and Blazor leaves that attribute alone because it didn't render it.
 */
const SELECTOR = "input[type=checkbox][data-indeterminate]";

function apply(input: HTMLInputElement): void {
  if (input.hasAttribute("data-indeterminate-cleared")) return;
  input.indeterminate = !input.checked;
}

/** Applies indeterminate to every marked checkbox in the document. Safe to call any time. */
export function syncIndeterminate(): void {
  document.querySelectorAll<HTMLInputElement>(SELECTOR).forEach(apply);
}

export function installIndeterminate(): void {
  document.addEventListener(
    "change",
    (event) => {
      const input = event.target as HTMLInputElement;
      if (!input.matches?.(SELECTOR)) return;
      input.indeterminate = false;
      input.setAttribute("data-indeterminate-cleared", "");
    },
    true
  );

  // Interactive pages add or re-add the attribute when C# sets Indeterminate again.
  new MutationObserver((records) => {
    for (const record of records) {
      const target = record.target as Element;
      if (record.type === "attributes" && target instanceof HTMLInputElement && target.matches(SELECTOR)) {
        target.removeAttribute("data-indeterminate-cleared");
        apply(target);
      }
      for (const node of record.addedNodes) {
        if (node instanceof Element) {
          if (node.matches(SELECTOR)) apply(node as HTMLInputElement);
          node.querySelectorAll<HTMLInputElement>(SELECTOR).forEach(apply);
        }
      }
    }
  }).observe(document.documentElement, { subtree: true, childList: true, attributes: true, attributeFilter: ["data-indeterminate"] });

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", syncIndeterminate, { once: true });
  } else {
    syncIndeterminate();
  }
}
