// These write only what changed. A draw that changes nothing leaves nothing for an observer to see,
// here or in anything else on the page, such as a browser extension answering what changes: answering
// each other, two observers never let the browser paint or take input again.
export const setText = (node: Node, text: string): void => {
  // Keeps the text node Blazor made, if it did.
  if (node.textContent === text) return;
  if (node.firstChild) node.firstChild.nodeValue = text;
  else node.textContent = text;
};
export const set = <T extends Element, K extends keyof T>(element: T, name: K, value: T[K]): void => {
  if (element[name] !== value) element[name] = value;
};
export const put = (element: Element, name: string, value: string | null): void => {
  if (element.getAttribute(name) === value) return;
  if (value === null) element.removeAttribute(name);
  else element.setAttribute(name, value);
};
