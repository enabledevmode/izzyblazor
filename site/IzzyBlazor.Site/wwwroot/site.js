// The docs site's script. Everything works without it; with it:
//   - each example frame is sized to its content (Off frames too: they're same-origin, so this
//     page can measure them although they run no script),
//   - a Live frame loads only when its mode is picked and the example is in view, so a page of
//     examples doesn't open a circuit for each,
//   - frames follow the page's color scheme when it changes,
//   - inside a frame, links to other pages open in the whole window rather than the frame.
(() => {
  "use strict";

  // Inside an example frame: send links to other pages to the top window.
  if (window.top !== window) {
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
    if (height > 0) frame.style.height = `${height}px`;
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

  // Picking a mode (a radio in the Tabs) or a width shows another frame.
  for (const type of ["change", "click", "keyup"]) {
    document.addEventListener(type, (event) => {
      const viewer = event.target instanceof Element ? event.target.closest(".ex") : null;
      if (viewer) requestAnimationFrame(() => wake(viewer));
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

    if (!root) return;
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
