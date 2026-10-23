// The docs site's script. Everything works without it; with it:
//   - each example frame is sized to its content once loaded (Off frames too: they're
//     same-origin, so this page can measure them although they run no script). Frames already
//     start at their measured height (frame-heights.json), so this only corrects one whose
//     content changed; the controls sit above the box, so a frame changing height moves nothing
//     of the example's,
//   - a Live frame loads only while it's shown and its example is near the screen, so a page of
//     examples doesn't open a circuit for each, and closes when the page leaves Live,
//   - the page's mode is remembered (a cookie the server reads), kept the same in every copy of
//     the switch, and kept in the address when it carries ?mode=,
//   - frames follow the page's color scheme when it changes,
//   - inside a frame, links to other pages open in the whole window rather than the frame.
(() => {
  "use strict";

  // Inside an example frame: send links to other pages to the top window.
  if (window.top !== window) {
    // A frame with its own scheme (?scheme=light) keeps it. IzzyBlazor's script re-applies the
    // visitor's saved choice (the ib-scheme cookie, and the switch's broadcast to open pages),
    // which would otherwise turn a light frame dark.
    const fixed = new URLSearchParams(location.search).get("scheme");
    if (fixed === "light" || fixed === "dark") {
      const root = document.documentElement;
      const keep = () => {
        if (root.getAttribute("data-ib-color-scheme") !== fixed) root.setAttribute("data-ib-color-scheme", fixed);
        const meta = document.head && document.head.querySelector('meta[name="ib-color-scheme"]');
        if (meta && meta.content !== fixed) meta.content = fixed;
      };
      keep();
      new MutationObserver(keep).observe(root, { attributes: true, attributeFilter: ["data-ib-color-scheme"] });
    }

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

  const measured = new WeakSet();
  const settled = new WeakSet();
  const observed = new WeakSet();
  const near = new WeakSet();

  function shown(element) {
    return element.getClientRects().length > 0;
  }

  function size(frame) {
    let root;
    try {
      // Only a loaded frame: while its page is still arriving its styles may not apply yet, and
      // the unstyled content would size the frame many times too tall for a moment.
      if (!settled.has(frame) || !frame.contentDocument || frame.contentDocument.readyState !== "complete") return;
      root = frame.contentDocument.querySelector(".frame");
    } catch {
      return;
    }

    if (!root || !shown(frame)) return;
    const height = Math.ceil(root.getBoundingClientRect().height);
    if (height <= 0) return;
    frame.style.height = `${height}px`;
  }

  function watch(frame) {
    if (measured.has(frame)) return;
    measured.add(frame);
    const settle = () => {
      scheme(frame);
      let root;
      try {
        root = frame.contentDocument.querySelector(".frame");
      } catch {
        return; // Not same-origin (shouldn't happen): keep the fixed height.
      }
      if (!root) return;
      // The frame already has its measured height. The library's script, fonts and Live rendering
      // change the content just after load, so correct it only once the content has held its
      // height for 400ms (3s at most), then follow it as people use the example. Sizing at once
      // would shrink a frame and grow it back as the script finished.
      let last = -1;
      let steady = 0;
      let waited = 0;
      let shortFor = 0;
      const wait = () => {
        const height = Math.ceil(root.getBoundingClientRect().height);
        // Too short (a height measured a few pixels off, a font that loaded late): grow as soon as
        // it's been too short twice running (200ms), so nothing stays cut off. Not at the first
        // look: a script may be about to shrink the content (a filter form hides its button).
        const short = shown(frame) && height > Math.round(frame.getBoundingClientRect().height);
        shortFor = short ? shortFor + 1 : 0;
        if (shortFor >= 2) frame.style.height = `${height}px`;
        steady = height === last ? steady + 100 : 0;
        last = height;
        waited += 100;
        if (steady < 400 && waited < 3000) {
          setTimeout(wait, 100);
          return;
        }
        settled.add(frame);
        size(frame);
        if (!observed.has(root) && "ResizeObserver" in window) {
          observed.add(root);
          new ResizeObserver(() => size(frame)).observe(root);
        }
      };
      wait();
    };

    frame.addEventListener("load", settle);
    if (frame.contentDocument && frame.contentDocument.readyState === "complete" && frame.src) settle();
  }

  // Loads Live frames that are picked and near the screen, and resizes frames that just appeared.
  function wake(viewer) {
    for (const frame of viewer.querySelectorAll("iframe.ex-frame")) {
      watch(frame);
      if (frame.dataset.src && shown(frame) && near.has(viewer)) {
        frame.src = frame.dataset.src;
        delete frame.dataset.src;
      }

      size(frame);
    }
  }

  const visibility = "IntersectionObserver" in window
    ? new IntersectionObserver((entries) => {
      for (const entry of entries) {
        if (entry.isIntersecting) {
          near.add(entry.target);
          wake(entry.target);
        } else {
          near.delete(entry.target);
        }
      }
    }, { rootMargin: "200px" })
    : null;

  function init() {
    // A link that opens the page in a mode (?mode=off) sets the remembered mode too, as picking it
    // with the switch would: the next page stays in it.
    const linked = new URLSearchParams(location.search).get("mode");
    if (linked === "off" || linked === "static" || linked === "live") remember(linked);

    for (const viewer of document.querySelectorAll(".ex")) {
      if (visibility) visibility.observe(viewer);
      else near.add(viewer);
      wake(viewer);
    }
  }

  function remember(mode) {
    const path = new URL(document.baseURI).pathname;
    document.cookie = `site-mode=${mode}; path=${path}; max-age=31536000; samesite=lax`;
  }

  // Picking the page's mode (ModeSwitch) shows another frame in every example. The other copies of
  // the switch follow (each is its own radio group), the cookie remembers it for the next page,
  // and an address that carries ?mode= follows too, so reloading keeps it.
  document.addEventListener("change", (event) => {
    if (!(event.target instanceof HTMLInputElement) || !event.target.matches(".site-mode-input")) return;
    const mode = event.target.value;
    for (const input of document.querySelectorAll(`.site-mode-input[value="${mode}"]`)) input.checked = true;
    remember(mode);
    const url = new URL(location.href);
    if (url.searchParams.has("mode")) {
      url.searchParams.set("mode", mode);
      history.replaceState(history.state, "", url);
    }

    requestAnimationFrame(() => {
      for (const viewer of document.querySelectorAll(".ex")) {
        // A Live frame that's no longer shown closes its circuit; it loads again when shown.
        for (const frame of viewer.querySelectorAll("iframe.ex-frame-live")) {
          if (!shown(frame) && frame.getAttribute("src")) {
            frame.dataset.src = frame.getAttribute("src");
            frame.removeAttribute("src");
          }
        }

        wake(viewer);
      }
    });
  });

  // Picking a width shows the frame at another size.
  for (const type of ["change", "click", "keyup"]) {
    document.addEventListener(type, (event) => {
      const viewer = event.target instanceof Element ? event.target.closest(".ex") : null;
      if (!viewer) return;
      requestAnimationFrame(() => wake(viewer));
    });
  }

  window.addEventListener("resize", () => {
    for (const frame of document.querySelectorAll("iframe.ex-frame")) size(frame);
  });

  // The page's scheme lives on <html> (data-ib-color-scheme) once someone picks one.
  function scheme(frame) {
    let root;
    try {
      root = frame.contentDocument && frame.contentDocument.documentElement;
    } catch {
      return;
    }

    // A frame with its own scheme (?scheme=dark) keeps it.
    if (!root || /[?&]scheme=/.test(frame.getAttribute("src") || frame.dataset.src || "")) return;
    const value = document.documentElement.getAttribute("data-ib-color-scheme");
    if (value) root.setAttribute("data-ib-color-scheme", value);
    else root.removeAttribute("data-ib-color-scheme");
    const meta = root.querySelector('meta[name="ib-color-scheme"]');
    if (meta && !value) meta.remove();
    else if (meta) meta.content = value;
  }

  new MutationObserver(() => {
    for (const frame of document.querySelectorAll("iframe.ex-frame")) scheme(frame);
  }).observe(document.documentElement, { attributes: true, attributeFilter: ["data-ib-color-scheme"] });

  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
  else init();

  // Enhanced navigation swaps the page's content without a reload.
  if (window.Blazor && typeof window.Blazor.addEventListener === "function") {
    window.Blazor.addEventListener("enhancedload", init);
  }
})();
