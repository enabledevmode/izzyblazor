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

/**
 * CheckboxGroup's SelectAll: the box above the options checks or clears them all, firing `change`
 * on each one it flips (so an interactive page hears each, as if clicked), and follows them:
 * checked when all are, a dash when some are. It has no name, so it never posts. Read-only boxes
 * don't change (forms.ts cancels the click), and the server renders the starting state.
 */
const SELECT_ALL = "input[type=checkbox][data-ib-select-all]";

function optionsOf(all: HTMLInputElement): HTMLInputElement[] {
  const field = all.closest(".ib-field");
  return field ? [...field.querySelectorAll<HTMLInputElement>(".ib-radio-options input[type=checkbox]")] : [];
}

function followOptions(all: HTMLInputElement): void {
  const options = optionsOf(all);
  const checked = options.filter((box) => box.checked).length;
  const some = checked > 0 && checked < options.length;
  all.checked = options.length > 0 && checked === options.length;
  all.indeterminate = some;
  // The server's dash (data-indeterminate) shows until this says otherwise.
  all.toggleAttribute("data-indeterminate-cleared", !some);
}

export function installSelectAll(): void {
  document.addEventListener("change", (event) => {
    const target = event.target;
    if (!(target instanceof HTMLInputElement) || target.type !== "checkbox") return;
    if (target.matches(SELECT_ALL)) {
      // Read once: each option's change below brings this box back in line with the options.
      const on = target.checked;
      for (const box of optionsOf(target)) {
        if (box.checked === on || box.disabled) continue;
        box.checked = on;
        box.dispatchEvent(new Event("change", { bubbles: true }));
      }
      followOptions(target);
    } else if (target.closest(".ib-radio-options")) {
      const all = target.closest(".ib-field")?.querySelector<HTMLInputElement>(SELECT_ALL);
      if (all) followOptions(all);
    }
  });
}
