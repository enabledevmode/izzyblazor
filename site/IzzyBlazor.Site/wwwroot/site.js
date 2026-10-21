// The docs site's script. Everything works without it; with it:
//   - each example frame is sized to its content (Off frames too: they're same-origin, so this
//     page can measure them although they run no script), and the preview keeps its tallest
//     mode's height, so the controls under it stay put,
//   - a Live frame loads only when the page's mode is Live and the example is in view, so a page
//     of examples doesn't open a circuit for each, and the mode is remembered in a cookie,
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
  const observed = new WeakSet();
  const near = new WeakSet();

  function shown(element) {
    return element.getClientRects().length > 0;
  }

  function size(frame) {
    let root;
    try {
      root = frame.contentDocument && frame.contentDocument.querySelector(".frame");
    } catch {
      return;
    }

    if (!root || !shown(frame)) return;
    const height = Math.ceil(root.getBoundingClientRect().height);
    if (height <= 0) return;
    frame.style.height = `${height}px`;

    // The preview keeps the tallest mode's height seen so far, so switching to a shorter mode
    // doesn't move the controls under it.
    const viewer = frame.closest(".ex");
    if (viewer) {
      const tallest = Math.max(height, Number(viewer.dataset.tallest || 0));
      viewer.dataset.tallest = String(tallest);
      viewer.style.setProperty("--ex-tallest", `${tallest}px`);
    }
  }

  // A new width lays every mode out again: measure from scratch.
  function forget(viewer) {
    delete viewer.dataset.tallest;
    viewer.style.removeProperty("--ex-tallest");
  }

  function watch(frame) {
    if (measured.has(frame)) return;
    measured.add(frame);
    const settle = () => {
      size(frame);
      scheme(frame);
      // Fonts, the library's script and Live rendering can change the height after load.
      for (const delay of [100, 400, 1200, 3000]) setTimeout(() => size(frame), delay);
      try {
        const root = frame.contentDocument.querySelector(".frame");
        if (root && !observed.has(root) && "ResizeObserver" in window) {
          observed.add(root);
          new ResizeObserver(() => size(frame)).observe(root);
        }
      } catch {
        // Not same-origin (shouldn't happen): keep the fixed height.
      }
    };

    frame.addEventListener("load", settle);
    if (frame.contentDocument && frame.contentDocument.readyState === "complete" && frame.src) settle();
  }

  // Loads Live frames that are picked and near the screen, and resizes frames that just appeared.
  function wake(viewer) {
    for (const frame of viewer.querySelectorAll("iframe.ex-frame")) {
      watch(frame);
      if (frame.dataset.src && shown(frame) && near.has(viewer)) {
        // Start at the height the other modes measured, not the configured one, so loading it
        // doesn't move the controls; it's sized to its own content once it renders.
        if (viewer.dataset.tallest) frame.style.height = `${viewer.dataset.tallest}px`;
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
        }
      }
    }, { rootMargin: "200px" })
    : null;

  function init() {
    for (const viewer of document.querySelectorAll(".ex")) {
      if (visibility) visibility.observe(viewer);
      else near.add(viewer);
      wake(viewer);
    }
  }

  // Picking the page's mode (ModeSwitch) shows another frame in every example; the server reads
  // the cookie, so the next page opens in the same mode.
  document.addEventListener("change", (event) => {
    if (!(event.target instanceof HTMLInputElement) || !event.target.matches(".site-mode-input")) return;
    const path = new URL(document.baseURI).pathname;
    document.cookie = `site-mode=${event.target.value}; path=${path}; max-age=31536000; samesite=lax`;
    requestAnimationFrame(() => {
      for (const viewer of document.querySelectorAll(".ex")) wake(viewer);
    });
  });

  // Picking a width shows the frame at another size.
  for (const type of ["change", "click", "keyup"]) {
    document.addEventListener(type, (event) => {
      const viewer = event.target instanceof Element ? event.target.closest(".ex") : null;
      if (!viewer) return;
      if (type === "change" && event.target.closest(".ex-widths")) forget(viewer);
      requestAnimationFrame(() => wake(viewer));
    });
  }

  window.addEventListener("resize", () => {
    for (const viewer of document.querySelectorAll(".ex")) forget(viewer);
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
