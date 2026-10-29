// The docs site's script. Everything works without it; with it:
//   - picking another mode (ModeSwitch) keeps the reader's place and, from the keyboard, focus,
//   - a link or form in an example that loads this page again (?page=2, a post, a scheme switch's
//     link with JavaScript off) brings it back to that example, where it was on the screen, rather
//     than to the top,
//   - a no-script page that enhanced navigation patched into a page still running Blazor is reloaded,
//     so its examples really run without it,
//   - Ctrl+K opens the header's search, which shows results as you type,
//   - a demo's frame follows the page's color scheme when it changes,
//   - inside a demo's frame, links to other pages open in the whole window rather than the frame.
(() => {
  "use strict";

  // Inside a demo's frame: send links to other pages to the top window.
  if (window.top !== window) {
    document.addEventListener("click", (event) => {
      const link = event.target instanceof Element ? event.target.closest("a[href]") : null;
      if (!link || link.target || event.defaultPrevented || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey) return;
      const url = new URL(link.href, location.href);
      if (url.protocol !== "http:" && url.protocol !== "https:") return;
      if (url.origin === location.origin && url.pathname === location.pathname) return;
      // IzzyBlazor's own endpoints (the scheme switch's links) belong to the example: its script
      // handles them, and without it they return to the frame.
      if (url.origin === location.origin && url.pathname.startsWith("/_izzy/")) return;
      event.preventDefault();
      event.stopPropagation();
      try {
        window.top.location.href = url.href;
      } catch {
        window.open(url.href, "_blank", "noopener");
      }
    }, true);
    return;
  }

  // Where to come back to after this page loads again, for 15 seconds: long enough for a slow
  // post, short enough that a click that didn't navigate (a link a script handled) isn't used by
  // some later visit.
  const KEY = "site-return";
  // The example that last posted or followed a link: a toast's action link (ToastRegion is in the
  // layout, outside the example) comes back to it.
  const LAST = "site-last-example";

  function save(value) {
    try {
      sessionStorage.setItem(KEY, JSON.stringify({ path: location.pathname, at: Date.now(), ...value }));
      if (value.id) sessionStorage.setItem(LAST, JSON.stringify({ path: location.pathname, id: value.id }));
    } catch {
      // Storage off: the page opens at the top, as without script.
    }
  }

  function read(key) {
    try {
      return JSON.parse(sessionStorage.getItem(key) || "null");
    } catch {
      return null;
    }
  }

  // Back where the reader was: for a mode switch, the section they were reading at the same place
  // on the screen (the content above it differs between modes), and focus back on the switch if
  // it had it; for an example's own link or form, the example at the same place.
  function restore() {
    const saved = read(KEY);
    try {
      sessionStorage.removeItem(KEY);
    } catch {
      // Nothing to clear.
    }

    if (!saved || saved.path !== location.pathname || !(Date.now() - saved.at < 15000)) return;
    const target = saved.id && document.getElementById(saved.id);
    if (target) window.scrollBy(0, target.getBoundingClientRect().top - saved.top);
    else if (typeof saved.y === "number") window.scrollTo(0, saved.y);

    // A browser skips autofocus when the address names an element (#ex-...): focus it here, as it
    // would have without the fragment (a dialog reopened with the post's error, say).
    if (saved.id && location.hash === `#${saved.id}`) {
      const auto = [...document.querySelectorAll("[autofocus]")].find((element) => element.getClientRects().length > 0);
      if (auto) {
        // Now, and again once the page has loaded: going to the #fragment drops focus.
        auto.focus({ preventScroll: true });
        const again = () => {
          if (document.activeElement === document.body || !document.activeElement) auto.focus({ preventScroll: true });
        };
        if (document.readyState === "complete") setTimeout(again);
        else window.addEventListener("load", () => setTimeout(again), { once: true });
      }
    }

    if (saved.focus) {
      const current = [...document.querySelectorAll('.site-modeswitch a[data-mode][aria-current]')].find((link) => link.getClientRects().length > 0);
      if (current) current.focus({ preventScroll: true });
    }
  }

  // The first section heading or example at least partly on the screen, under the sticky header.
  function reading() {
    for (const element of document.querySelectorAll("main :is(h2, h3)[id], main .ex[id]")) {
      // The page's own headings, not an example's.
      if (!element.matches(".ex") && element.closest(".ex")) continue;
      const box = element.getBoundingClientRect();
      if (box.bottom > 72) return { id: element.id, top: Math.round(box.top) };
    }

    return null;
  }

  // Links and forms in an example that load this page again. A full page load (a no-script page, a
  // post that isn't enhanced) goes to the example's id (#ex-...), and App's script in <head> sets
  // that example's scroll-margin to where it was on the screen, so the browser lands it there as
  // it opens the page. Enhanced navigation keeps the scroll of a page that loads itself, and
  // restore() corrects for anything above the example that changed.
  function anchor(example, url, full) {
    if (full) url.hash = example.id;
    save({ id: example.id, top: Math.round(example.getBoundingClientRect().top) });
    return url;
  }

  const blazor = () => !!(window.Blazor && typeof window.Blazor.addEventListener === "function");

  // The example a navigating link or form belongs to: its own, or for a toast's action, the one
  // that last posted on this page.
  function exampleOf(element) {
    const own = element.closest(".ex[id]");
    if (own) return own;
    if (!element.closest(".ib-toast-region")) return null;
    const last = read(LAST);
    return last && last.path === location.pathname ? document.getElementById(last.id) : null;
  }

  document.addEventListener("click", (event) => {
    const link = event.target instanceof Element ? event.target.closest("a[href]") : null;
    if (!link || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
    if (link.hasAttribute("download") || (link.target && link.target !== "_self")) return;
    if (link.matches(".site-modeswitch a[data-mode]")) {
      // A no-script page points an example's own switch at the example for JavaScript off
      // (NoScriptExampleAnchors); here the saved place puts it back where it was instead.
      const target = new URL(link.href, location.href);
      if (target.hash) {
        target.hash = "";
        link.href = target.href;
      }
      // event.detail is 0 when Enter or Space followed the link.
      save({ ...(reading() ?? { y: window.scrollY }), focus: event.detail === 0 });
      return;
    }

    const example = exampleOf(link);
    if (!example) return;
    const url = new URL(link.href, location.href);
    if (url.origin === location.origin && url.pathname === "/_izzy/scheme") {
      // A ColorSchemeSwitch's link, followed only without IzzyBlazor's script (a no-script page): the
      // endpoint redirects to its return path, and the browser keeps this link's fragment.
      const back = new URL(url.searchParams.get("return") || "", document.baseURI);
      if (blazor() || back.origin !== location.origin || back.pathname !== location.pathname) return;
      if (url.hash && url.hash !== `#${example.id}`) return;
      link.href = anchor(example, url, true).href;
      return;
    }

    if (url.origin !== location.origin || url.pathname !== location.pathname) return;
    // A link with its own #fragment goes there; on a no-script page the server already points the
    // example's links at the example (NoScriptExampleAnchors), which is ours.
    if (url.hash && url.hash !== `#${example.id}`) return;
    link.href = anchor(example, url, !blazor() || !!link.closest('[data-enhance-nav="false"]')).href;
  }, true);

  document.addEventListener("submit", (event) => {
    const form = event.target instanceof HTMLFormElement ? event.target : null;
    const example = form && exampleOf(form);
    if (!example) return;
    // The button's formaction and formmethod win over the form's. Attributes, not form.action
    // and form.method: a field named "action" or "method" would hide those.
    const submitter = event.submitter;
    const own = submitter && submitter.hasAttribute("formaction");
    const url = new URL((own ? submitter.getAttribute("formaction") : form.getAttribute("action")) || location.href, location.href);
    if (url.origin !== location.origin || url.pathname !== location.pathname || (url.hash && url.hash !== `#${example.id}`)) return;
    const enhanced = blazor() && form.hasAttribute("data-enhance");
    const href = anchor(example, url, !enhanced).href;
    if (own) submitter.setAttribute("formaction", href);
    else form.setAttribute("action", href);
  }, true);

  // The page's scheme lives on <html> (data-ib-color-scheme) once someone picks one; a demo's
  // frame follows it.
  function scheme(frame) {
    let root;
    try {
      root = frame.contentDocument && frame.contentDocument.documentElement;
    } catch {
      return;
    }

    if (!root) return;
    const value = document.documentElement.getAttribute("data-ib-color-scheme");
    if (value) root.setAttribute("data-ib-color-scheme", value);
    else root.removeAttribute("data-ib-color-scheme");
    const meta = root.querySelector('meta[name="ib-color-scheme"]');
    if (meta && !value) meta.remove();
    else if (meta) meta.content = value;
  }

  const watched = new WeakSet();

  function frames() {
    for (const frame of document.querySelectorAll("iframe.ex-frame")) {
      scheme(frame);
      if (watched.has(frame)) continue;
      watched.add(frame);
      frame.addEventListener("load", () => scheme(frame));
    }
  }

  new MutationObserver(() => {
    for (const frame of document.querySelectorAll("iframe.ex-frame")) scheme(frame);
  }).observe(document.documentElement, { attributes: true, attributeFilter: ["data-ib-color-scheme"] });

  // Search (SiteSearch in the header). Its buttons open the dialog without script; here Ctrl+K
  // (⌘K) opens it too, results show as you type (the best of each group, rendered by the server
  // at /search/results: the ranking is all in C#), and ↓ and ↑ move between the field and them.
  const SEARCH = "site-search";
  let searchShown = "";
  let searchRequest = null;
  let searchTimer = 0;

  function searchParts() {
    const dialog = document.getElementById(SEARCH);
    if (!(dialog instanceof HTMLDialogElement)) return null;
    return {
      dialog,
      field: dialog.querySelector("input[data-site-search-field]"),
      results: dialog.querySelector("[data-site-search-results]"),
      count: dialog.querySelector("[data-site-search-count]"),
    };
  }

  // Shows the results for the field's text, unless they're already showing. A failed request
  // changes nothing: Enter still goes to the search page.
  async function searchFor(query) {
    const parts = searchParts();
    if (!parts || !parts.results) return;
    query = query.trim();
    // Drop a request still on its way first: backspacing to what's shown mustn't let it land.
    if (searchRequest) searchRequest.abort();
    searchRequest = null;
    if (query === searchShown) return;
    const request = (searchRequest = new AbortController());
    let html;
    try {
      const response = await fetch(new URL(`search/results?q=${encodeURIComponent(query)}`, document.baseURI), { signal: request.signal });
      if (!response.ok) return;
      html = await response.text();
    } catch {
      return;
    }

    if (request !== searchRequest) return;
    searchShown = query;
    parts.results.innerHTML = html;
    parts.results.scrollTop = 0;
    if (parts.count) parts.count.textContent = parts.results.firstElementChild?.getAttribute("data-count") ?? "";
  }

  function openSearch() {
    const parts = searchParts();
    if (!parts) return;
    if (!parts.dialog.open) parts.dialog.showModal();
    if (parts.field) {
      parts.field.focus();
      parts.field.select();
      searchFor(parts.field.value);
    }
  }

  document.addEventListener("keydown", (event) => {
    const parts = searchParts();
    if (!parts) return;
    if ((event.ctrlKey || event.metaKey) && !event.altKey && !event.shiftKey && event.key.toLowerCase() === "k") {
      // Not from inside an example, whose own controls may use the keys.
      if (event.target instanceof Element && event.target.closest(".ex")) return;
      event.preventDefault();
      openSearch();
      return;
    }

    // Escape closes at once: in a search field the browser would only clear the text first.
    if (event.key === "Escape" && !event.isComposing && parts.dialog.open && event.target === parts.field) {
      event.preventDefault();
      parts.dialog.close();
      return;
    }

    if (!parts.dialog.open || (event.key !== "ArrowDown" && event.key !== "ArrowUp") || !(event.target instanceof Element)) return;
    const links = [...parts.dialog.querySelectorAll("[data-site-search-results] a[href]")];
    if (links.length === 0) return;
    if (event.target === parts.field) {
      if (event.key === "ArrowDown") {
        event.preventDefault();
        links[0].focus();
      }
      return;
    }

    const index = links.indexOf(event.target);
    if (index < 0) return;
    event.preventDefault();
    if (event.key === "ArrowDown") links[Math.min(index + 1, links.length - 1)].focus();
    else if (index === 0) parts.field?.focus();
    else links[index - 1].focus();
  });

  document.addEventListener("input", (event) => {
    if (!(event.target instanceof HTMLInputElement) || !event.target.matches("[data-site-search-field]")) return;
    const field = event.target;
    clearTimeout(searchTimer);
    searchTimer = setTimeout(() => searchFor(field.value), 150);
  });

  document.addEventListener("click", (event) => {
    if (!(event.target instanceof Element)) return;
    // A search button opened the dialog natively: bring its results up to date with the field.
    if (event.target.closest("[data-site-search-open]")) {
      setTimeout(() => {
        const parts = searchParts();
        if (parts?.dialog.open && parts.field) searchFor(parts.field.value);
      });
      return;
    }

    // Following a result closes the dialog: enhanced navigation keeps the header, and the open
    // dialog with it. IzzyBlazor's script does this too, but a no-script page doesn't load it.
    const link = event.target.closest(`#${SEARCH} a[href]`);
    if (link && event.button === 0 && !event.ctrlKey && !event.metaKey && !event.shiftKey && !event.altKey) {
      searchParts()?.dialog.close();
    }
  });

  // Back or Forward with the dialog open: close it at once, rather than when enhanced navigation
  // patches the page (the library's Dialog then closes one the patch left modal).
  window.addEventListener("popstate", () => {
    const parts = searchParts();
    if (parts?.dialog.open) parts.dialog.close();
  });

  restore();
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", frames);
  else frames();

  // Enhanced navigation swaps the page's content without a reload. A no-script page sends no Blazor,
  // so one patched into a page that runs it (a link followed from a Static page, with no script picked
  // in another tab) loads again on its own.
  if (blazor()) {
    window.Blazor.addEventListener("enhancedload", () => {
      if (document.querySelector("[data-site-noscript]")) {
        location.reload();
        return;
      }

      restore();
      frames();
      // The header came back from the server with its empty results: show them again on opening.
      searchShown = "";
    });
  }
})();
