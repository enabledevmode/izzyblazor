/**
 * A TreeList's cascade (data-ib-cascade): a part of <ib-grid>, loaded for a tree list with
 * checkboxes. Checking a row checks the rows drawn under it, unchecking one unchecks the rows
 * above it, a row with all its rows drawn and checked is checked ("up" only, not while filtering),
 * and a row with some checked shows a mixed box. Each box is set as a person would, by a click, so
 * the selection (the script's on a static page, C#'s on an interactive one) hears every change.
 * Rows drawn under a checked row come in checked: it stands for its branch.
 */
const levelOf = (row: HTMLTableRowElement): number => Number(row.getAttribute("aria-level"));

/** Sets the cascade up; the function it returns checks rows that have just been drawn. */
export function setupCascade(table: HTMLTableElement, host: HTMLElement, signal: AbortSignal): () => void {
  // Boxes being set by a cascade, whose own changes don't cascade again.
  let cascading = false;
  const rows = () => [...table.querySelectorAll<HTMLTableRowElement>(":scope > tbody > tr[aria-level]:not(.ib-treelist-more)")];
  const boxOf = (row: HTMLTableRowElement) => row.querySelector<HTMLInputElement>(":scope > td > .ib-table-select");
  const set = (box: HTMLInputElement | null, on: boolean) => {
    if (box && !box.disabled && box.checked !== on) box.click();
  };

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

  const mixed = (all: HTMLTableRowElement[]) => {
    all.forEach((row, i) => {
      const box = boxOf(row);
      if (box) box.indeterminate = !box.checked && under(all, i).some((r) => boxOf(r)?.checked);
    });
  };

  const cascadeFrom = (box: HTMLInputElement) => {
    const all = rows();
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
    const all = rows();
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
