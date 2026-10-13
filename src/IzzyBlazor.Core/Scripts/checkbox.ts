/**
 * Checkbox's Indeterminate. The server marks the input with data-indeterminate, which CSS shows as
 * a dash with no script. Here the input also gets the real indeterminate state, so screen readers
 * announce "mixed", and choosing the box clears it: data-indeterminate-cleared tells CSS the dash
 * is gone, and Blazor leaves that attribute alone because it didn't render it.
 */
const SELECTOR = "input[type=checkbox][data-indeterminate]";

// Boxes this script made indeterminate, so they can be reset when the server stops marking them
// (the property isn't an attribute: Blazor's diff and enhanced navigation never touch it).
const marked = new Set<HTMLInputElement>();

function apply(input: HTMLInputElement): void {
  if (input.hasAttribute("data-indeterminate-cleared")) return;
  input.indeterminate = !input.checked;
  if (input.indeterminate) marked.add(input);
}

function reset(input: HTMLInputElement): void {
  input.indeterminate = false;
  input.removeAttribute("data-indeterminate-cleared");
  marked.delete(input);
}

/** Applies indeterminate to marked boxes and resets boxes no longer marked. Safe to call any time. */
export function syncIndeterminate(): void {
  for (const input of [...marked]) {
    if (!input.isConnected) marked.delete(input);
    else if (!input.matches(SELECTOR)) reset(input);
  }
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
      if (record.type === "attributes" && target instanceof HTMLInputElement) {
        if (target.matches(SELECTOR)) {
          target.removeAttribute("data-indeterminate-cleared");
          apply(target);
        } else if (target.type === "checkbox") {
          // C# or an enhanced post turned Indeterminate off.
          reset(target);
        }
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
