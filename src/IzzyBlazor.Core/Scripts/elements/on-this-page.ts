import { IzzyElement, defineElement } from "../izzy-element";

/**
 * <ib-on-this-page> marks the section in view (aria-current="location" on its link) as the page
 * scrolls, and fills in headings the server couldn't list: headings with ids in raw HTML (Prose,
 * rendered Markdown), or an order that differs from the page's (a heading nested in another
 * component renders after the ones around it). It then shows its own copy of the list and hides
 * the server's, so it never changes content Blazor renders.
 */
class OnThisPage extends IzzyElement {
  #frame = 0;
  #run: AbortController | null = null;

  protected setup(signal: AbortSignal): void {
    const rebuild = () => {
      this.#run?.abort();
      const run = new AbortController();
      this.#run = run;
      signal.addEventListener("abort", () => run.abort(), { signal: run.signal });
      this.#build(run.signal);
    };
    rebuild();
    // On interactive pages the server's list changes as the page does.
    const served = this.querySelector<HTMLElement>(".ib-toc-list:not(.ib-toc-own)");
    if (served) {
      const observer = new MutationObserver(rebuild);
      observer.observe(served, { childList: true, subtree: true, characterData: true });
      signal.addEventListener("abort", () => observer.disconnect());
    }
  }

  #build(signal: AbortSignal): void {
    const main = this.closest("ib-shell")?.querySelector(":scope > main") ?? document.querySelector("main");
    const served = this.querySelector<HTMLElement>(".ib-toc-list:not(.ib-toc-own)");
    if (!main || !served) return;
    this.querySelector(".ib-toc-own")?.remove();

    const min = Number(this.dataset.min) || 2;
    const max = Number(this.dataset.max) || 3;
    const levels: string[] = [];
    for (let level = min; level <= max; level++) levels.push(`h${level}[id]`);
    const headings = [...main.querySelectorAll<HTMLElement>(levels.join(","))].filter(
      (h) => h.dataset.ibOutline !== "false" && h.id
    );

    const servedLinks = [...served.querySelectorAll<HTMLAnchorElement>("a.ib-toc-link")];
    const same =
      servedLinks.length === headings.length && servedLinks.every((a, i) => decodeURIComponent(a.hash.slice(1)) === headings[i].id);

    let links = servedLinks;
    served.hidden = !same;
    if (!same) {
      const own = served.cloneNode(false) as HTMLElement;
      own.hidden = false;
      own.classList.add("ib-toc-own");
      const page = location.pathname + location.search;
      links = headings.map((h) => {
        const item = document.createElement("li");
        item.className = "ib-toc-item";
        item.style.setProperty("--ib-toc-depth", String(Number(h.localName[1]) - min));
        const link = document.createElement("a");
        link.className = "ib-toc-link";
        link.href = `${page}#${encodeURIComponent(h.id)}`;
        link.textContent = (h.textContent ?? "").replace(/\s+/g, " ").trim();
        item.append(link);
        own.append(item);
        return link;
      });
      served.after(own);
    }

    this.hidden = headings.length === 0;
    this.#track(headings, links, signal);
  }

  /** The section in view: the last heading at or above the top of the visible area. */
  #track(headings: HTMLElement[], links: HTMLAnchorElement[], signal: AbortSignal): void {
    const update = () => {
      this.#frame = 0;
      let current = -1;
      for (let i = 0; i < headings.length; i++) {
        const margin = parseFloat(getComputedStyle(headings[i]).scrollMarginTop) || 0;
        if (headings[i].getBoundingClientRect().top <= margin + 8) current = i;
        else break;
      }
      // At the very bottom the last sections may never reach the top: mark the last one.
      const scroller = document.scrollingElement ?? document.documentElement;
      const main = headings[0]?.closest("main");
      const atEnd =
        scroller.scrollHeight > scroller.clientHeight
          ? Math.ceil(scroller.scrollTop + scroller.clientHeight) >= scroller.scrollHeight - 2
          : !!main && main.scrollHeight > main.clientHeight && Math.ceil(main.scrollTop + main.clientHeight) >= main.scrollHeight - 2;
      // There, a section that was linked to (and is in view) wins, since it couldn't scroll to the top.
      if (atEnd && headings.length && headings[headings.length - 1].getBoundingClientRect().top < innerHeight) {
        const target = headings.findIndex((h) => location.hash && h.id === decodeURIComponent(location.hash.slice(1)));
        const top = target >= 0 ? headings[target].getBoundingClientRect().top : -1;
        current = top >= 0 && top < innerHeight ? target : headings.length - 1;
      }
      links.forEach((link, i) => {
        if (i === current) link.setAttribute("aria-current", "location");
        else link.removeAttribute("aria-current");
      });
    };
    const schedule = () => {
      if (!this.#frame) this.#frame = requestAnimationFrame(update);
    };
    // Capture: the page scrolls, or the main area does (AppShell ScrollMode.Main).
    document.addEventListener("scroll", schedule, { capture: true, passive: true, signal });
    addEventListener("resize", schedule, { passive: true, signal });
    // A link to a section near the end changes the hash without scrolling (Blazor may push it).
    addEventListener("hashchange", schedule, { signal });
    document.addEventListener("click", () => setTimeout(schedule, 50), { capture: true, signal });
    signal.addEventListener("abort", () => cancelAnimationFrame(this.#frame));
    update();
  }
}

defineElement("ib-on-this-page", OnThisPage);
