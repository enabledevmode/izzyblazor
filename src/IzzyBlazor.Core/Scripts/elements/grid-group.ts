// A DataGrid's bar of what's grouped: moving a chip among the others, by dragging it or by Alt+Left
// and Alt+Right on its remove control; a click opens its menu (Move left, Move right, Remove).
// Loaded by grid-columns.ts on the first press or key on a chip.
import { openPanelAt } from "../context-menu";

/** What grid-columns.ts lends this part: the bar, its chips, and the grouping they make. */
export interface ChipHost {
  host: HTMLElement;
  bar(): HTMLElement | null;
  chips(): HTMLElement[];
  group(tokens: string[]): void;
  mark(at: number, name?: string): void;
  inside(el: Element, event: PointerEvent): boolean;
  dropAt(event: PointerEvent, slots: DOMRect[]): number;
  until(signal: AbortSignal): AbortController;
}

const DRAG_START = 6;
const tokensOf = (chips: HTMLElement[]) => chips.map((chip) => chip.dataset.ibGroupToken!);

// A grouping level one place on (Alt+Right) or back, then focus follows its chip into the new render
// (a navigation's merge, or Blazor's; a full reload, with no Blazor, starts at the top).
export function moveChip(h: ChipHost, chip: HTMLElement, right: boolean): void {
  const chips = h.chips();
  const tokens = tokensOf(chips);
  const from = chips.indexOf(chip);
  const to = from + (right !== (getComputedStyle(chip).direction === "rtl") ? 1 : -1);
  if (from < 0 || to < 0 || to >= tokens.length) return;
  const token = tokens[from];
  [tokens[from], tokens[to]] = [tokens[to], token];
  const watch = new MutationObserver(() => {
    const now = h.chips();
    const moved = now.find((c) => c.dataset.ibGroupToken === token);
    if (!moved || now.indexOf(moved) !== to) return;
    watch.disconnect();
    moved.querySelector<HTMLElement>("a, button")?.focus();
  });
  watch.observe(h.host, { childList: true, subtree: true, attributes: true, attributeFilter: ["data-ib-group-token"] });
  // Not tied to the element's signal: a navigation's merge reconnects it before the chips move.
  setTimeout(() => watch.disconnect(), 5000);
  h.group(tokens);
}

// A chip menu's Move left and Move right, once this part is here (a click on a chip opens the menu
// through it): moved as Alt+arrows move it, so focus follows the chip, in place of the item's link
// or call: on the way up, after the menu has closed and before the document, where Blazor listens.
const taken = new WeakSet<HTMLElement>();
function takeMenu(h: ChipHost): void {
  if (taken.has(h.host)) return;
  taken.add(h.host);
  h.host.addEventListener("click", (event) => {
    const item = (event.target as Element).closest<HTMLElement>("[data-ib-chip-move]");
    const chip = item?.closest<HTMLElement>(".ib-grid-group-chip");
    if (!item || !chip || !h.chips().includes(chip)) return;
    event.preventDefault();
    event.stopPropagation();
    moveChip(h, chip, item.dataset.ibChipMove === "1");
  });
}

// Dragging a chip among the others changes which level comes first.
export function dragChip(h: ChipHost, down: PointerEvent, chip: HTMLElement, signal: AbortSignal, up = false): void {
  takeMenu(h);
  const panel = chip.querySelector<HTMLElement>("[popover]");
  // A click: the chip's menu, after the pointerup's light dismiss.
  const menu = () => panel && setTimeout(() => openPanelAt(panel, chip, down.clientX, chip.getBoundingClientRect().bottom));
  // Let go before this part arrived.
  if (up) return void menu();
  const bar = h.bar();
  const chips = h.chips();
  const from = chips.indexOf(chip);
  if (!bar || from < 0) return;
  const slots = chips.map((c) => c.getBoundingClientRect());
  let dragging = false;
  let at = from;
  const abort = h.until(signal);
  const options = { signal: abort.signal };
  const finish = (commit: boolean) => {
    abort.abort();
    chip.removeAttribute("data-ib-lifted");
    h.mark(-1);
    if (!dragging) {
      if (commit) menu();
      return;
    }
    const swallow = (event: Event) => {
      event.preventDefault();
      event.stopPropagation();
    };
    document.addEventListener("click", swallow, { capture: true, once: true });
    setTimeout(() => document.removeEventListener("click", swallow, { capture: true }), 0);
    if (!commit || at === from || at === from + 1) return;
    const tokens = tokensOf(chips);
    const [token] = tokens.splice(from, 1);
    tokens.splice(at > from ? at - 1 : at, 0, token);
    h.group(tokens);
  };
  document.addEventListener("pointermove", (event) => {
    // Let go before this part arrived: there's no drag.
    if (!(event.buttons & 1)) return finish(false);
    const dx = event.clientX - down.clientX;
    if (!dragging) {
      if (Math.abs(dx) < DRAG_START) return;
      dragging = true;
      chip.setAttribute("data-ib-lifted", "");
    }
    // Let go off the bar, it stays where it was.
    at = h.inside(bar, event) ? h.dropAt(event, slots) : from;
    h.mark(at === from || at === from + 1 ? -1 : at, chip.querySelector(".ib-tag-label")?.textContent?.trim());
  }, options);
  document.addEventListener("pointerup", () => finish(true), options);
  document.addEventListener("pointercancel", () => finish(false), options);
  document.addEventListener("keydown", (event) => {
    if (event.key !== "Escape") return;
    event.preventDefault();
    event.stopPropagation();
    finish(false);
  }, { ...options, capture: true });
}
