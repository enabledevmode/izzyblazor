/**
 * Says things to screen readers through hidden live regions: Announcer's messages (izzyBlazor.announce
 * on interactive pages, an AnnouncerRegion's on static ones) and CopyButton's "Copied".
 *
 * Each message is a new line added to a polite or an assertive region, so the same words twice are
 * read twice and quick messages don't cut each other off; a line is removed after 7 seconds, so a
 * screen reader's browse mode doesn't find old messages at the end of the page. The regions are an
 * AnnouncerRegion's (there from the page's load, kept by enhanced navigation), else made at the end
 * of the body; in an open modal dialog, made there (everything outside one is inert, so it wouldn't
 * be read). Loaded on demand (index.ts), and bundled into CopyButton's and code blocks' modules. They're made when first needed (enhanced navigation would remove ones made earlier), and
 * a line is added a beat after the region exists, so it's read: Safari misses text written at once.
 */
const remove = 7000;

export function announce(text: string, assertive = false, from?: Element | null): void {
  if (!text) return;
  const dialog = from?.closest("dialog[open]") ?? topModal();
  const kind = assertive ? "assertive" : "polite";
  const host = dialog ?? document.body;
  let region = dialog
    ? dialog.querySelector<HTMLElement>(`:scope > .ib-announce-${kind}`)
    : [...document.querySelectorAll<HTMLElement>(`.ib-announce-${kind}`)].find((r) => !r.closest("dialog"));
  const made = !region;
  if (!region) {
    region = document.createElement("div");
    region.className = `ib-visually-hidden ib-announce-${kind}`;
    region.setAttribute("aria-live", kind);
    host.append(region);
  }
  const target = region;
  setTimeout(() => {
    const line = document.createElement("div");
    line.textContent = text;
    target.append(line);
    setTimeout(() => line.remove(), remove);
  }, made ? 200 : 100);
}

function topModal(): Element | null {
  try {
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

export function announceRendered(): void {
  for (const message of document.querySelectorAll<HTMLElement>("[data-ib-announcements] > [data-id]")) {
    const id = message.dataset.id!;
    if (read.has(id)) continue;
    read.add(id);
    announce(message.textContent ?? "", message.hasAttribute("data-assertive"));
  }
}
