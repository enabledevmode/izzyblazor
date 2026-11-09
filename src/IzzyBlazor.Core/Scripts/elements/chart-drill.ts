/**
 * Drill-down for the charts' own parts (PieChart, TreemapChart): an item's link to the level
 * below (the data table's, data-ib-drill) followed from a click or Enter, and arriving by the keys,
 * the plot focused and the level said. Chart has the same inline.
 */

/** Follows item `index`'s link to the level below, if it has one; true when it did. */
export function followDrill(host: HTMLElement, index: number, byKeys: boolean): boolean {
  const link = host.querySelector<HTMLAnchorElement>(`.ib-chart-drills a[data-ib-drill="${index}"]`);
  if (!link) return false;
  if (byKeys) {
    try {
      sessionStorage.setItem("ib-drill", `${location.pathname} ${host.dataset.ibDrill}`);
    } catch {}
  }
  link.click();
  return true;
}

/** Arrived by the keys from the level above: the plot has focus, and says where it is; true when it did. */
export function arrived(host: HTMLElement, frame: HTMLElement, live: HTMLElement | null): boolean {
  try {
    if (host.dataset.ibDrilled && sessionStorage.getItem("ib-drill") === `${location.pathname} ${host.dataset.ibDrill}`) {
      sessionStorage.removeItem("ib-drill");
      frame.focus();
      if (live) live.textContent = host.dataset.ibDrilled;
      return true;
    }
  } catch {}
  return false;
}
