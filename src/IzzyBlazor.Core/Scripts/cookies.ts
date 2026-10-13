/**
 * Cookies the server reads on the next request (through IRequestCookies): a collapsed sidebar,
 * a dismissed banner. Scoped to the app's base path and kept for a year.
 */
export function writeCookie(name: string, value: string): void {
  // "/app/" wouldn't be sent for "/app", so drop the trailing slash (but keep a bare "/").
  const path = new URL(document.baseURI).pathname.replace(/(.)\/$/, "$1");
  const secure = location.protocol === "https:" ? "; secure" : "";
  document.cookie = `${encodeURIComponent(name)}=${encodeURIComponent(value)}; path=${path}; max-age=31536000; samesite=lax${secure}`;
}

/** A cookie's value, or null when it isn't set. */
export function readCookie(name: string): string | null {
  for (const part of document.cookie.split(";")) {
    const eq = part.indexOf("=");
    if (eq < 0) continue;
    if (decodeURIComponent(part.slice(0, eq).trim()) === name) return decodeURIComponent(part.slice(eq + 1).trim());
  }
  return null;
}
