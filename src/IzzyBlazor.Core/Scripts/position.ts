/**
 * Fallback placement for browsers without CSS anchor positioning. Where anchor positioning
 * is supported the stylesheet does all of this and the script never runs.
 *
 * Placements are logical ("bottom-start", "end"...), so they follow the anchor's direction.
 */
export const supportsAnchor = typeof CSS !== "undefined" && CSS.supports("anchor-name: --a");

const GAP = 6;
const EDGE = 8;

type Side = "top" | "bottom" | "left" | "right";
const opposite: Record<Side, Side> = { top: "bottom", bottom: "top", left: "right", right: "left" };

function clamp(value: number, min: number, max: number): number {
  return max < min ? min : Math.min(Math.max(value, min), max);
}

/** Positions a fixed, top-layer element next to its anchor, flipping to the other side if it doesn't fit. */
export function place(floating: HTMLElement, anchor: Element, placement: string): void {
  floating.classList.add("ib-pop-placed");
  const a = anchor.getBoundingClientRect();
  const f = floating.getBoundingClientRect();
  const rtl = getComputedStyle(anchor).direction === "rtl";
  const viewW = document.documentElement.clientWidth;
  const viewH = document.documentElement.clientHeight;

  const [logicalSide, align = "center"] = placement.split("-");
  let side: Side =
    logicalSide === "start" ? (rtl ? "right" : "left") : logicalSide === "end" ? (rtl ? "left" : "right") : (logicalSide as Side);

  const room: Record<Side, number> = { top: a.top, bottom: viewH - a.bottom, left: a.left, right: viewW - a.right };
  const vertical = side === "top" || side === "bottom";
  const need = (vertical ? f.height : f.width) + GAP + EDGE;
  if (room[side] < need && room[opposite[side]] > room[side]) {
    side = opposite[side];
  }
  // Beside it with no room on either side (a phone): below it, or above, centered.
  let centered = false;
  if (!vertical && room[side] < need) {
    side = room.bottom >= f.height + GAP + EDGE || room.bottom >= room.top ? "bottom" : "top";
    centered = true;
  }

  let top: number;
  let left: number;
  if (side === "top" || side === "bottom") {
    top = side === "bottom" ? a.bottom + GAP : a.top - GAP - f.height;
    const startEdge = rtl ? a.right - f.width : a.left;
    const endEdge = rtl ? a.left : a.right - f.width;
    left = centered || align === "center" ? a.left + (a.width - f.width) / 2 : align === "start" ? startEdge : endEdge;
  } else {
    left = side === "right" ? a.right + GAP : a.left - GAP - f.width;
    // "end-start" (submenus): the top lines up with the anchor's, less the panel's padding.
    top = align === "start" ? a.top - parseFloat(getComputedStyle(floating).paddingTop || "0") - 1 : a.top + (a.height - f.height) / 2;
  }

  floating.style.left = `${clamp(left, EDGE, viewW - f.width - EDGE)}px`;
  floating.style.top = `${clamp(top, EDGE, viewH - f.height - EDGE)}px`;
}

/**
 * Keeps a floating element placed while it is open: places it now and again on scroll and
 * resize. Returns a function that stops tracking.
 */
export function track(floating: HTMLElement, anchor: Element, placement: string): () => void {
  if (supportsAnchor) return () => {};
  let frame = 0;
  const update = () => {
    cancelAnimationFrame(frame);
    frame = requestAnimationFrame(() => place(floating, anchor, placement));
  };
  place(floating, anchor, placement);
  window.addEventListener("scroll", update, { capture: true, passive: true });
  window.addEventListener("resize", update, { passive: true });
  return () => {
    cancelAnimationFrame(frame);
    window.removeEventListener("scroll", update, { capture: true });
    window.removeEventListener("resize", update);
  };
}
