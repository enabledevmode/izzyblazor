/**
 * Says things to screen readers through hidden live regions: Announcer's messages (izzyBlazor.announce
 * on interactive pages, an AnnouncerRegion's on static ones) and CopyButton's "Copied". Loaded on
 * demand (index.ts), and bundled into CopyButton's and code blocks' modules.
 *
 * Each message is a new line added to a polite or an assertive region, so the same words twice are
 * read twice and quick messages don't cut each other off; a line is removed after 7 seconds, so a
 * screen reader's browse mode doesn't find old messages at the end of the page. The regions are an
 * AnnouncerRegion's, there from the page's load and kept by enhanced navigation. In an open modal
 * dialog everything outside is inert, so there a region is made in the dialog; with no
 * AnnouncerRegion, one is made at the end of the body. A new region gets its line a beat later
 * than one already there, so it's read: Safari misses text written at once.
 */
const remove = 7000;

export function announce(text: string, assertive = false, from?: Element | null): void {
  if (!text) return;
  const kind = assertive ? "assertive" : "polite";
  // The region is chosen when the line is written, not now: a handler that announces and closes
  // (or opens) a dialog sends the message before the dialog changes.
  const pick = (): { region: HTMLElement; made: boolean } => {
    const dialog = (from?.isConnected ? from.closest("dialog[open]") : null) ?? topModal();
    const existing = dialog
      ? dialog.querySelector<HTMLElement>(`:scope > .ib-announce-${kind}`)
      : [...document.querySelectorAll<HTMLElement>(`.ib-announce-${kind}`)].find((r) => !r.closest("dialog"));
    if (existing) return { region: existing, made: false };
    const region = document.createElement("div");
    region.className = `ib-visually-hidden ib-announce-${kind}`;
    region.setAttribute("aria-live", kind);
    (dialog ?? document.body).append(region);
    return { region, made: true };
  };
  const write = (region: HTMLElement): void => {
    const line = document.createElement("div");
    line.textContent = text;
    region.append(line);
    setTimeout(() => line.remove(), remove);
  };
  setTimeout(() => {
    const { region, made } = pick();
    if (made) setTimeout(() => write(region), 150);
    else write(region);
  }, 100);
}

// The modal on top: the one focus is trapped in, else the last open one in the page.
function topModal(): Element | null {
  try {
    const focused = document.activeElement?.closest("dialog:modal");
    if (focused) return focused;
    const open = document.querySelectorAll("dialog:modal");
    return open.length ? open[open.length - 1] : null;
  } catch {
    return null; // A browser without :modal.
  }
}

// An AnnouncerRegion renders a static response's messages hidden, each with its own id; they're
// read from here once each, after a full load or an enhanced post or navigation. Ids, not emptying
// the region: a streamed response patches the same messages in again.
const read = new Set<string>();

export function announceRendered(afterLoad: boolean): void {
  const fresh = [...document.querySelectorAll<HTMLElement>("[data-ib-announcements] > [data-id]")].filter(
    (message) => !read.has(message.dataset.id!)
  );
  if (!fresh.length) return;
  for (const message of fresh) read.add(message.dataset.id!);
  const say = () => fresh.forEach((message) => announce(message.textContent ?? "", message.hasAttribute("data-assertive")));
  // After a full page load, screen readers are busy announcing the new page and drop polite
  // messages that arrive meanwhile: wait for the load and a moment more. An enhanced update
  // doesn't make them announce a page, so its messages go at once.
  if (!afterLoad) say();
  else if (document.readyState === "complete") setTimeout(say, 500);
  else window.addEventListener("load", () => setTimeout(say, 500), { once: true });
}
