/**
 * A BulletChart's script, loaded by <ib-chart> when it holds one (data-ib-bullet): the tooltip for
 * the row under the pointer, and the keyboard. The rows' meters are one Tab stop (a roving
 * tabindex): the arrow keys move focus from meter to meter, so a screen reader says each one's
 * name and words as it arrives, and the row's tooltip shows for everyone else. The rows stay a
 * list of meters, not an application.
 *
 * Nothing Blazor renders is changed but attributes Blazor doesn't set (data-ib-active, tabindex,
 * aria-describedby on the meters) and the tooltip's contents.
 */

// The keyboard argument is the other parts' (an application frame); the rows keep their list of meters instead.
export function bullet(chart: HTMLElement, signal: AbortSignal, _keyboard: (frame: HTMLElement) => void, restart: () => void): void {
  const frame = chart.querySelector<HTMLElement>(".ib-bullet");
  const tooltip = frame?.querySelector<HTMLElement>(".ib-chart-tooltip");
  const hint = chart.querySelector<HTMLElement>("[data-ib-keys]");
  const rows = [...chart.querySelectorAll<HTMLElement>(".ib-bullet-row")];
  if (!frame || !tooltip || rows.length === 0) return;
  const meters = rows.map((r) => r.querySelector<HTMLElement>(".ib-bullet-meter")!);
  const tips = new Map<string, HTMLElement>();
  for (const tip of chart.querySelectorAll<HTMLElement>(".ib-chart-tips > [data-ib-tip]")) tips.set(tip.dataset.ibTip!, tip);
  const across = frame.classList.contains("ib-bullet-v");

  // A new draw from the server sets up again; the tooltip's own moves and contents don't count.
  const changed = new MutationObserver((records) => {
    if (records.some((r) => !tooltip.contains(r.target))) restart();
  });
  changed.observe(chart, { childList: true, subtree: true, characterData: true, attributeFilter: ["style", "class", "aria-valuetext"] });
  signal.addEventListener("abort", () => changed.disconnect());

  let current: HTMLElement | undefined;
  const hide = () => {
    current?.removeAttribute("data-ib-active");
    current = undefined;
    tooltip.hidden = true;
  };

  // Shows a row's tooltip: beside the pointer at (x, y) in the frame, or (from the keys) by its track.
  const show = (row: HTMLElement, x?: number, y?: number) => {
    if (row !== current) {
      current?.removeAttribute("data-ib-active");
      current = row;
      row.setAttribute("data-ib-active", "");
      tooltip.innerHTML = tips.get(row.dataset.ibRow!)?.innerHTML ?? "";
    }
    tooltip.hidden = false;
    const box = frame.getBoundingClientRect();
    const track = row.querySelector<HTMLElement>(".ib-bullet-track")!.getBoundingClientRect();
    const gap = 12;
    const tw = tooltip.offsetWidth;
    const th = tooltip.offsetHeight;
    let left: number;
    let top: number;
    if (x !== undefined && y !== undefined) {
      left = x + gap + tw > box.width ? x - gap - tw : x + gap;
      top = across ? y - th / 2 : track.top - box.top - th - 6;
    } else if (across) {
      left = track.right - box.left + gap;
      if (left + tw > box.width) left = track.left - box.left - gap - tw;
      top = track.top - box.top + track.height / 2 - th / 2;
    } else {
      left = track.left - box.left + track.width / 2 - tw / 2;
      top = track.top - box.top - th - 6;
    }
    // Above the first row there may be no room: under the track instead.
    if (!across && top < 0) top = track.bottom - box.top + 6;
    tooltip.style.left = `${Math.max(0, Math.min(left, box.width - tw))}px`;
    tooltip.style.top = `${Math.max(0, Math.min(top, box.height - th))}px`;
  };

  frame.addEventListener(
    "pointermove",
    (event) => {
      if (frame.contains(document.activeElement) && byKeys) return;
      const row = (event.target as Element).closest<HTMLElement>(".ib-bullet-row");
      if (!row || !rows.includes(row)) return hide();
      const box = frame.getBoundingClientRect();
      show(row, event.clientX - box.left, event.clientY - box.top);
    },
    { signal }
  );
  frame.addEventListener("pointerleave", () => byKeys || hide(), { signal });

  // The roving Tab stop: the first meter, or the one the keys were last on. The hint is said once,
  // on the meter Tab lands on.
  let at = 0;
  let byKeys = false;
  const stop = (i: number) => {
    meters.forEach((m, k) => {
      m.tabIndex = k === i ? 0 : -1;
      if (hint) m.toggleAttribute("aria-describedby", false);
    });
    at = i;
  };
  stop(0);
  if (hint) meters[0].setAttribute("aria-describedby", hint.id);

  for (const [i, meter] of meters.entries()) {
    meter.addEventListener(
      "focus",
      () => {
        if (!meter.matches(":focus-visible")) return;
        byKeys = true;
        at = i;
        show(rows[i]);
      },
      { signal }
    );
  }
  frame.addEventListener(
    "focusout",
    (event) => {
      if (frame.contains(event.relatedTarget as Node)) return;
      byKeys = false;
      hide();
      if (hint) meters[at].setAttribute("aria-describedby", hint.id);
    },
    { signal }
  );
  frame.addEventListener(
    "keydown",
    (event) => {
      if (!(event.target instanceof HTMLElement) || !meters.includes(event.target)) return;
      const [back, next] = across ? ["ArrowLeft", "ArrowRight"] : ["ArrowUp", "ArrowDown"];
      // Left and right turn round on a right-to-left page.
      const rtl = across && getComputedStyle(frame).direction === "rtl";
      let to: number;
      if (event.key === (rtl ? next : back)) to = Math.max(0, at - 1);
      else if (event.key === (rtl ? back : next)) to = Math.min(meters.length - 1, at + 1);
      else if (event.key === "Home") to = 0;
      else if (event.key === "End") to = meters.length - 1;
      else if (event.key === "Escape") return hide();
      else return;
      event.preventDefault();
      stop(to);
      meters[to].focus();
      byKeys = true;
      show(rows[to]);
    },
    { signal }
  );
  document.addEventListener("keydown", (event) => event.key === "Escape" && !tooltip.hidden && hide(), { signal });

  signal.addEventListener("abort", () => {
    hide();
    for (const m of meters) {
      m.removeAttribute("tabindex");
      m.removeAttribute("aria-describedby");
    }
  });
}
