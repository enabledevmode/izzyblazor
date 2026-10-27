/**
 * Reports open and close changes as a bubbling `ibtoggle` event with `{ id, open, seq }`, which
 * Blazor binds as @onibtoggle on interactive pages so `@bind-Open` follows the browser.
 *
 * On interactive pages C# renders the state it wants as data-open, and the script opens or closes
 * to match (sync). Both sides count: `seq` numbers each change reported for an element, and C#
 * renders the last it has handled as data-ib-seq. A render whose count is behind left before the
 * user's latest change, so it's ignored (it would undo it); one that has caught up is applied.
 * What sync applies isn't reported back: the server asked for it.
 *
 * The native `toggle` event doesn't bubble, so one capturing listener on the document covers
 * every <details class="ib-disclosure"> and every [popover].ib-pop outside <ib-popover>.
 * Dialogs and <ib-popover> panels report their own changes (see elements/dialog.ts).
 */
export interface ToggleDetail {
  id: string;
  open: boolean;
  seq: number;
}

// How many changes have been reported for each element.
const reported = new WeakMap<HTMLElement, number>();

export function emitToggle(element: HTMLElement, open: boolean): void {
  const seq = (reported.get(element) ?? 0) + 1;
  reported.set(element, seq);
  element.dispatchEvent(new CustomEvent<ToggleDetail>("ibtoggle", { bubbles: true, detail: { id: element.id, open, seq } }));
}

/**
 * Whether the server's render on `root` (its data-open) is from before the latest change
 * reported for `element`. Only on interactive pages, where a server answers. A change the server
 * never got (the circuit was down) leaves it behind until the user's next one for that element,
 * which C# counts from: until then, its renders for it are ignored.
 */
export function isBehind(root: HTMLElement, element: HTMLElement): boolean {
  if (!root.hasAttribute("data-ib-interactive")) return false;
  return Number(root.getAttribute("data-ib-seq") ?? 0) < (reported.get(element) ?? 0);
}

// Disclosures on interactive pages: C# renders data-open rather than open, which Blazor would
// otherwise set from a late render that changes it.
const DISCLOSURE = "details.ib-disclosure[data-ib-interactive]";
// What sync set, so its toggle event isn't reported back.
const synced = new WeakMap<HTMLDetailsElement, boolean>();
// Opened or closed by the user (or the browser) and not reported yet: the toggle event comes a
// moment after the change, and a render arriving in between would undo it.
const unreported = new WeakSet<HTMLDetailsElement>();

function syncDisclosure(details: HTMLDetailsElement): void {
  if (unreported.has(details) || isBehind(details, details)) return;
  const want = details.hasAttribute("data-open");
  if (details.open === want) return;
  synced.set(details, want);
  details.open = want;
}

function syncDisclosuresIn(node: Node): void {
  if (!(node instanceof Element)) return;
  if (node.matches(DISCLOSURE)) syncDisclosure(node as HTMLDetailsElement);
  if (node.firstElementChild) node.querySelectorAll<HTMLDetailsElement>(DISCLOSURE).forEach(syncDisclosure);
}

let installed = false;

export function installToggleBridge(): void {
  if (installed) return;
  installed = true;

  document.addEventListener(
    "toggle",
    (event) => {
      const target = event.target;
      if (target instanceof HTMLDetailsElement && target.classList.contains("ib-disclosure")) {
        const fromSync = synced.get(target) === target.open;
        synced.delete(target);
        unreported.delete(target);
        if (!fromSync) emitToggle(target, target.open);
      } else if (target instanceof HTMLElement && target.classList.contains("ib-pop") && target.parentElement?.localName !== "ib-popover") {
        emitToggle(target, (event as Event & { newState?: string }).newState === "open");
      }
    },
    true
  );

  // Interactive Disclosures follow data-open and data-ib-seq, and open as rendered when they arrive.
  // Blazor inserts each element and then its children, so the batch holds every one: look inside
  // only those whose parent isn't in it as well.
  new MutationObserver((records) => {
    const added = new Set<Node>();
    for (const record of records) record.addedNodes.forEach((node) => added.add(node));
    for (const node of added) if (!node.parentNode || !added.has(node.parentNode)) syncDisclosuresIn(node);
    for (const record of records) {
      if (record.type !== "attributes") continue;
      if (record.attributeName !== "open") syncDisclosuresIn(record.target);
      else if (record.target instanceof HTMLDetailsElement && record.target.matches(DISCLOSURE) && !synced.has(record.target)) unreported.add(record.target);
    }
  }).observe(document.documentElement, { subtree: true, childList: true, attributes: true, attributeFilter: ["data-open", "data-ib-seq", "open"] });
  syncDisclosuresIn(document.documentElement);

  // Disabled accordion items: CSS removes the pointer; this also stops keyboard and script clicks.
  document.addEventListener(
    "click",
    (event) => {
      const summary = (event.target as Element | null)?.closest?.("summary[aria-disabled='true']");
      if (summary) event.preventDefault();
    },
    true
  );
}
