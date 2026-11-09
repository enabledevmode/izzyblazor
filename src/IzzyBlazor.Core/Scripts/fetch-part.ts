/**
 * Fetches a page and takes one element out of it, for a part that asks the server for its own
 * content on a static page (a table's value list, a field's suggestions). On a stream-rendered page
 * the answer arrives in a template after the first flush, so the last streamed match wins. Null when
 * the response isn't OK or holds no match; a network failure or abort throws.
 */
export async function fetchPart(url: URL, selector: string, signal: AbortSignal, init?: RequestInit): Promise<Element | null> {
  const response = await fetch(url, { ...init, signal });
  if (!response.ok) return null;
  const doc = new DOMParser().parseFromString(await response.text(), "text/html");
  const streamed = [...doc.querySelectorAll<HTMLTemplateElement>("blazor-ssr template")].map((t) => t.content.querySelector(selector)).filter(Boolean);
  return streamed.pop() ?? doc.querySelector(selector);
}
