/**
 * The boxes for the parts of a time in a panel (CalendarGrid.TimeBoxes): an hour, a minute, perhaps a
 * second and AM or PM. They read and show a time as a time input writes it.
 */
export interface TimeParts {
  read(): string;
  show(value: string): void;
}

const two = (n: number): string => String(n).padStart(2, "0");

export function timeParts(root: Element): TimeParts | null {
  const group = root.querySelector(".ib-calendar-time");
  if (!group) return null;
  const [hourBox, minuteBox, secondBox] = group.querySelectorAll("input");
  const dayPart = group.querySelector("select");
  // A box left empty is nought, and past its limits it's the limit.
  const boxed = (box: HTMLInputElement): number => Math.min(Math.max(Number(box.value), Number(box.min)), Number(box.max));
  return {
    read() {
      const hour = dayPart ? (hourBox.value ? boxed(hourBox) % 12 : 0) + (dayPart.value === "pm" ? 12 : 0) : boxed(hourBox);
      return `${two(hour)}:${two(boxed(minuteBox))}${secondBox ? `:${two(boxed(secondBox))}` : ""}`;
    },
    show(value) {
      const [hour, minute, second] = value.split(":").map(Number);
      const has = value !== "";
      hourBox.value = !has ? "" : dayPart ? String(hour % 12 || 12) : two(hour);
      minuteBox.value = has ? two(minute) : "";
      if (secondBox) secondBox.value = has ? two(second || 0) : "";
      if (dayPart) dayPart.value = has && hour >= 12 ? "pm" : "am";
    },
  };
}
