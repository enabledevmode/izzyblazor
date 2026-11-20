/**
 * The rule every For parameter follows: a bare value is an id; a value that starts with "#", "."
 * or "[" is a CSS selector. The components' side is ForTarget (ForTarget.cs).
 */
export function forSelector(value: string): string {
  return /^[#.[]/.test(value) ? value : `#${CSS.escape(value)}`;
}
