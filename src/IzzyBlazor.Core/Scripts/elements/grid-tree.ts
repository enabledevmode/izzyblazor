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
 * (the top or bottom quarter of a row, or its middle), never into its own branch. The drop says only
 * which row it landed on and where; the server works out the parent and place from the whole tree,
 * as the rows drawn may be a page, a window or only the matches. On an interactive page it raises
 * `ibmove` on the handle (key, the row landed on as zone, 0 before, 1 after, 2 into as index) and C#
 * moves it. On a static page it opens the Move dialog with the drop's parent chosen, to confirm.
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
      window.removeEventListener("pointercancel", end);
      branch.forEach((r) => r.classList.remove("ib-tree-dragging"));
      clear();
      if (!target || event.type !== "pointerup") return;
      const near = target.dataset.ibKey!;
      const key = row.dataset.ibKey!;
      if (host.hasAttribute("data-ib-interactive")) {
        grip.dispatchEvent(new CustomEvent("ibmove", { bubbles: true, detail: { key, to: ["before", "after", "into"].indexOf(zone), zone: near, seq: ++seq } }));
        return;
      }
      const name = host.getAttribute("data-ib-tree-drag")!;
      const url = new URL(location.href);
      url.searchParams.set(name, key);
      url.searchParams.set(`${name}near`, near);
      url.searchParams.set(`${name}at`, zone);
      const link = Object.assign(document.createElement("a"), { href: url.href });
      link.hidden = true;
      host.append(link);
      link.click();
      link.remove();
    };
    window.addEventListener("pointermove", move, { signal });
    window.addEventListener("pointerup", end, { signal });
    window.addEventListener("keydown", end, { signal });
    // A pointer the browser takes away (a gesture, the window losing it) ends the drag with no drop.
    window.addEventListener("pointercancel", end, { signal });
  }, { signal });
}

// Not a Show more row, nor a row's detail, which share their row's level.
const rows = (table: HTMLTableElement) => [...table.querySelectorAll<HTMLTableRowElement>(":scope > tbody > tr[aria-level]:not(.ib-treelist-more, .ib-grid-detail)")];

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
  // Marked while it's set, so a Shift+click range keeps the person's own last box as its start.
  const set = (box: HTMLInputElement | null, on: boolean) => {
    if (!box || box.disabled || box.checked === on) return;
    box.setAttribute("data-ib-cascading", "");
    box.click();
    box.removeAttribute("data-ib-cascading");
  };
  // C# cascades on an interactive page over the whole tree; the host stops asking once it does.
  const on = () => host.hasAttribute("data-ib-cascade");

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
    if (cascading || !on() || box.type !== "checkbox" || !box.matches(".ib-table-select") || box.closest("table") !== table) return;
    // Once this change has reached the selection (the script's, which redraws every box as one
    // changes, or C#'s): the rows it sets would otherwise be drawn before this one counts.
    setTimeout(() => cascadeFrom(box), 0);
  }, { signal });

  return () => {
    if (!on()) return;
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
