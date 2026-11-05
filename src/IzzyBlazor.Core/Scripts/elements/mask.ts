/**
 * A TextField's Mask, loaded by <ib-field-control> on pages that have one (not an element).
 * The same algorithm as TextMask.cs, which formats on the server too: keep the two in step.
 *
 * Every edit (typing, paste, drop, autofill, a phone's predictions) is let through, then the whole
 * value is fitted to the mask again and the caret put after as many slot characters as were before
 * it. Backspace or Delete beside a literal widens the selection to the slot character beyond it, so
 * the key removes that character, not a literal that comes straight back.
 */

interface Slot {
  test: (c: string) => boolean;
  optional: boolean;
  casing: 0 | 1 | 2; // none, upper, lower
}
type Element = Slot | string;

interface Fit {
  text: string;
  raw: string;
  /** Where each slot character landed in text. */
  slots: number[];
}

const digit = (c: string) => c >= "0" && c <= "9";
const letter = (c: string) => /\p{L}/u.test(c);
const builtIn: Record<string, [(c: string) => boolean, boolean]> = {
  "0": [digit, false],
  "9": [digit, true],
  L: [letter, false],
  "?": [letter, true],
  A: [(c) => digit(c) || letter(c), false],
  a: [(c) => digit(c) || letter(c), true],
  "&": [(c) => /\S/.test(c), false],
  C: [(c) => /\S/.test(c), true],
};

/** "X[A-HJ-NP-Z]Y[0-5]": each rule's character, then its class. */
function parseRules(text: string): Map<string, (c: string) => boolean> {
  const rules = new Map<string, (c: string) => boolean>();
  for (let i = 0; i + 1 < text.length; ) {
    const key = text[i];
    let end = i + 2;
    while (end < text.length && text[end] !== "]") end += text[end] === "\\" ? 2 : 1;
    const pattern = new RegExp(`^${text.slice(i + 1, end + 1)}$`, "u");
    rules.set(key, (c) => pattern.test(c));
    i = end + 1;
  }
  return rules;
}

function parse(mask: string, rulesText: string): Element[] {
  const rules = parseRules(rulesText);
  const elements: Element[] = [];
  let casing: 0 | 1 | 2 = 0;
  for (let i = 0; i < mask.length; i++) {
    const c = mask[i];
    const custom = rules.get(c);
    if (custom) {
      elements.push({ test: custom, optional: false, casing });
    } else if (c === ">") casing = 1;
    else if (c === "<") casing = 2;
    else if (c === "|") casing = 0;
    else if (c === "\\") {
      if (i + 1 < mask.length) elements.push(mask[++i]);
    } else if (builtIn[c]) {
      elements.push({ test: builtIn[c][0], optional: builtIn[c][1], casing });
    } else elements.push(c);
  }
  return elements;
}

function fit(elements: Element[], text: string): Fit {
  let out = "";
  let raw = "";
  let pending = "";
  let trailing = "";
  let complete = true;
  const slots: number[] = [];
  let i = 0;
  for (const element of elements) {
    if (typeof element === "string") {
      if (text[i] === element) i++;
      pending += element;
      trailing += element;
      continue;
    }
    trailing = "";
    let taken: string | null = null;
    while (i < text.length) {
      const c = element.casing === 1 ? text[i].toUpperCase() : element.casing === 2 ? text[i].toLowerCase() : text[i];
      if (c.length === 1 && element.test(c)) {
        taken = c;
        i++;
        break;
      }
      if (element.optional) break;
      i++;
    }
    if (taken !== null) {
      out += pending;
      pending = "";
      slots.push(out.length);
      out += taken;
      raw += taken;
    } else if (!element.optional) complete = false;
  }
  if (complete && raw) out += trailing;
  return { text: out, raw, slots };
}

export function setupMask(input: HTMLInputElement, signal: AbortSignal): void {
  // Read each time: an interactive page can change the mask.
  let source = "";
  let elements: Element[] = [];
  const current = () => {
    const mask = input.dataset.ibMask ?? "";
    const rules = input.dataset.ibMaskRules ?? "";
    if (mask + "\u0000" + rules !== source) {
      source = mask + "\u0000" + rules;
      elements = parse(mask, rules);
    }
    return elements;
  };

  const reformat = () => {
    if (!input.dataset.ibMask) return;
    const caret = input.selectionStart ?? input.value.length;
    const before = fit(current(), input.value.slice(0, caret)).raw.length;
    const result = fit(current(), input.value);
    if (result.text !== input.value) input.value = result.text;
    const at = before === 0 ? 0 : before >= result.slots.length ? result.text.length : result.slots[before - 1] + 1;
    if (document.activeElement === input) input.setSelectionRange(at, at);
  };

  input.addEventListener(
    "input",
    (event) => {
      if (!(event as InputEvent).isComposing) reformat();
    },
    { signal }
  );
  input.addEventListener("compositionend", reformat, { signal });

  input.addEventListener(
    "keydown",
    (event) => {
      if ((event.key !== "Backspace" && event.key !== "Delete") || !input.dataset.ibMask) return;
      const start = input.selectionStart;
      if (start === null || start !== input.selectionEnd) return;
      const slots = fit(current(), input.value).slots;
      if (event.key === "Backspace") {
        if (start === 0 || slots.includes(start - 1)) return;
        const target = slots.filter((s) => s < start).pop();
        if (target !== undefined) input.setSelectionRange(target, start);
      } else {
        if (start >= input.value.length || slots.includes(start)) return;
        const target = slots.find((s) => s > start);
        if (target !== undefined) input.setSelectionRange(start, target + 1);
      }
    },
    { signal }
  );
}
