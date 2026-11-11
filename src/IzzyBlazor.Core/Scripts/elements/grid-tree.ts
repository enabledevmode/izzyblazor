/**
 * A TreeList's cascade (data-ib-cascade): a part of <ib-grid>, loaded for a tree list with
 * checkboxes. Checking a row checks the rows drawn under it, unchecking one unchecks the rows
 * above it, a row with all its rows drawn and checked is checked ("up" only, not while filtering),
 * and a row with some checked shows a mixed box. Each box is set as a person would, by a click, so
 * the selection (the script's on a static page, C#'s on an interactive one) hears every change.
 * Rows drawn under a checked row come in checked: it stands for its branch.
 */
const levelOf = (row: HTMLTableRowElement): number => Number(row.getAttribute("aria-level"));

/** A TreeList's own behavior, by what its host asks for; the function it returns checks rows just drawn. */
export function setupTree(table: HTMLTableElement, host: HTMLElement, signal: AbortSignal): () => void {
  if (host.hasAttribute("data-ib-tree-drag")) setupDrag(table, host, signal);
  return host.hasAttribute("data-ib-cascade") ? setupCascade(table, host, signal) : () => {};
}

/**
 * MoveByDrag: a row's handle drags it, and its branch with it, before, after or into another row
 * (the top or bottom quarter of a row, or its middle), never into its own branch. On an interactive
 * page the drop raises `ibmove` on the handle (key, the new parent's key as zone, "" for the top, and
 * its place among its new siblings, -1 for after them) and C# moves it. On a static page the drop
 * opens the Move dialog with the new parent chosen, by the address, to confirm.
 */
function setupDrag(table: HTMLTableElement, host: HTMLElement, signal: AbortSignal): void {
  const marks = ["ib-tree-drop-before", "ib-tree-drop-after", "ib-tree-drop-into"];
  let seq = 0;
  table.addEventListener("pointerdown", (down) => {
    const grip = (down.target as Element).closest<HTMLElement>(".ib-treelist-grip");
    const row = grip?.closest("tr");
    if (!grip || !row || down.button !== 0) return;
    down.preventDefault();
    const all = rows(table);
    const at = all.indexOf(row);
    const branch = [row, ...under(all, at)];
    let target: HTMLTableRowElement | null = null;
    let zone = "";
    const clear = () => target?.classList.remove(...marks);
    const move = (event: PointerEvent) => {
      branch.forEach((r) => r.classList.add("ib-tree-dragging"));
      const over = document.elementFromPoint(event.clientX, event.clientY)?.closest("tr");
      clear();
      target = over && all.includes(over) && !branch.includes(over) ? over : null;
      if (!target) return;
      const box = target.getBoundingClientRect();
      const y = (event.clientY - box.top) / box.height;
      zone = y < 0.25 ? "before" : y > 0.75 ? "after" : "into";
      target.classList.add(`ib-tree-drop-${zone}`);
    };
    const end = (event: Event) => {
      window.removeEventListener("pointermove", move);
      window.removeEventListener("pointerup", end);
      window.removeEventListener("keydown", end);
      branch.forEach((r) => r.classList.remove("ib-tree-dragging"));
      clear();
      if (!target || event.type !== "pointerup") return;
      // The new parent, and the place among its rows the dragged one takes once it's left its own.
      const i = all.indexOf(target);
      const parent = zone === "into" ? target : above(all, i)[0];
      const siblings = parent ? under(all, all.indexOf(parent)).filter((r) => levelOf(r) === levelOf(parent) + 1) : all.filter((r) => levelOf(r) === 1);
      const others = siblings.filter((r) => r !== row);
      const index = zone === "into" ? -1 : others.indexOf(target) + (zone === "after" ? 1 : 0);
      const parentKey = parent?.dataset.ibKey ?? "";
      const key = row.dataset.ibKey!;
      if (host.hasAttribute("data-ib-interactive")) {
        grip.dispatchEvent(new CustomEvent("ibmove", { bubbles: true, detail: { key, to: index, zone: parentKey, seq: ++seq } }));
        return;
      }
      const name = host.getAttribute("data-ib-tree-drag")!;
      const url = new URL(location.href);
      url.searchParams.set(name, key);
      url.searchParams.set(`${name}under`, parentKey || "-");
      url.searchParams.set(`${name}at`, String(index));
      const link = Object.assign(document.createElement("a"), { href: url.href });
      link.hidden = true;
      host.append(link);
      link.click();
      link.remove();
    };
    window.addEventListener("pointermove", move, { signal });
    window.addEventListener("pointerup", end, { signal });
    window.addEventListener("keydown", end, { signal });
  }, { signal });
}

const rows = (table: HTMLTableElement) => [...table.querySelectorAll<HTMLTableRowElement>(":scope > tbody > tr[aria-level]:not(.ib-treelist-more)")];

// The rows drawn under one, in order, and the rows above it, nearest first.
const under = (all: HTMLTableRowElement[], at: number) => {
  const level = levelOf(all[at]);
  const end = all.findIndex((row, i) => i > at && levelOf(row) <= level);
  return all.slice(at + 1, end < 0 ? all.length : end);
};
const above = (all: HTMLTableRowElement[], at: number) => {
  const out: HTMLTableRowElement[] = [];
  let level = levelOf(all[at]);
  for (let i = at - 1; i >= 0 && level > 1; i--) {
    if (levelOf(all[i]) < level) {
      out.push(all[i]);
      level = levelOf(all[i]);
    }
  }
  return out;
};

function setupCascade(table: HTMLTableElement, host: HTMLElement, signal: AbortSignal): () => void {
  // Boxes being set by a cascade, whose own changes don't cascade again.
  let cascading = false;
  const boxOf = (row: HTMLTableRowElement) => row.querySelector<HTMLInputElement>(":scope > td > .ib-table-select");
  const set = (box: HTMLInputElement | null, on: boolean) => {
    if (box && !box.disabled && box.checked !== on) box.click();
  };

  const mixed = (all: HTMLTableRowElement[]) => {
    all.forEach((row, i) => {
      const box = boxOf(row);
      if (box) box.indeterminate = !box.checked && under(all, i).some((r) => boxOf(r)?.checked);
    });
  };

  const cascadeFrom = (box: HTMLInputElement) => {
    const all = rows(table);
    const at = all.indexOf(box.closest("tr")!);
    if (at < 0) return;
    const on = box.checked;
    cascading = true;
    try {
      for (const row of under(all, at)) set(boxOf(row), on);
      for (const row of above(all, at)) {
        const own = boxOf(row);
        // A row kept only for a match under it has no box: the rows above it still follow.
        if (!on) {
          set(own, false);
          continue;
        }
        const children = under(all, all.indexOf(row)).filter((r) => levelOf(r) === levelOf(row) + 1);
        const whole = children.length === Number(children[0]?.getAttribute("aria-setsize"));
        if (host.getAttribute("data-ib-cascade") !== "up" || !own || !whole || children.some((r) => !boxOf(r)?.checked)) break;
        set(own, true);
      }
    } finally {
      cascading = false;
    }
    mixed(all);
  };

  table.addEventListener("change", (event) => {
    const box = event.target as HTMLInputElement;
    if (cascading || box.type !== "checkbox" || !box.matches(".ib-table-select") || box.closest("table") !== table) return;
    // Once this change has reached the selection (the script's, which redraws every box as one
    // changes, or C#'s): the rows it sets would otherwise be drawn before this one counts.
    setTimeout(() => cascadeFrom(box), 0);
  }, { signal });

  return () => {
    const all = rows(table);
    const checked: number[] = [];
    cascading = true;
    try {
      for (const row of all) {
        const level = levelOf(row);
        while (checked.length && checked[checked.length - 1] >= level) checked.pop();
        const box = boxOf(row);
        if (checked.length) set(box, true);
        if (box?.checked) checked.push(level);
      }
    } finally {
      cascading = false;
    }
    mixed(all);
  };
}
