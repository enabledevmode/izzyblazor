/**
 * Cookies the server reads on the next request (through IRequestCookies): a collapsed sidebar,
 * a dismissed banner. Scoped to the app's base path and kept for a year, or until the browser
 * closes when not persistent (a session cookie). A scope is a path to keep it to instead, such as a
 * page's. An empty value removes it.
 */
export function writeCookie(name: string, value: string, persistent = true, scope?: string): void {
  // "/app/" wouldn't be sent for "/app", so drop the trailing slash (but keep a bare "/").
  const path = scope ?? new URL(document.baseURI).pathname.replace(/(.)\/$/, "$1");
  if (value === "") {
    document.cookie = `${encodeURIComponent(name)}=; path=${path}; max-age=0`;
    return;
  }
  const secure = location.protocol === "https:" ? "; secure" : "";
  document.cookie = `${encodeURIComponent(name)}=${encodeURIComponent(value)}; path=${path}${persistent ? "; max-age=31536000" : ""}; samesite=lax${secure}`;
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
