// Host-only helpers: the preset and brand selects. Uses event delegation on the document, so it
// keeps working after Blazor's enhanced navigation swaps page content. Light and dark is the
// library's ColorSchemeSwitch (its cookie, read by IzzyTheme), not this.
(function () {
  // The preset (data-ib-preset) and brand (data-ib-brand) on <html>, chosen in the header's
  // selects and kept in localStorage. Default: no attribute.
  var SETS = { preset: "data-ib-preset", brand: "data-ib-brand" };

  // Older builds kept a look, a named color theme, or accent, gray and radius; drop them.
  try { ["look", "color", "accent", "gray", "radius", "theme"].forEach(function (k) { localStorage.removeItem("ib-host-" + k); }); } catch (e) { }

  function applySets() {
    var root = document.documentElement;
    Object.keys(SETS).forEach(function (k) {
      var v = null;
      try { v = localStorage.getItem("ib-host-" + k); } catch (e) { }
      if (v) root.setAttribute(SETS[k], v);
      else root.removeAttribute(SETS[k]);
      document.querySelectorAll("[data-host-set='" + k + "']").forEach(function (s) { s.value = v || ""; });
    });
    ["gray", "radius"].forEach(function (k) { root.removeAttribute("data-ib-" + k); });
  }

  document.addEventListener("change", function (e) {
    var select = e.target.closest && e.target.closest("[data-host-set]");
    if (!select) return;
    var key = "ib-host-" + select.getAttribute("data-host-set");
    try {
      if (select.value) localStorage.setItem(key, select.value);
      else localStorage.removeItem(key);
    } catch (err) { }
    applySets();
  });

  applySets();

  // Chrome treats a click on a select as keyboard-style focus, so the header pickers would show a
  // focus ring after every click. Mark pointer use; a key press clears it (host.css).
  document.addEventListener("pointerdown", function (e) {
    var select = e.target.closest && e.target.closest(".host-select");
    if (select) select.setAttribute("data-pointer", "");
  });
  document.addEventListener("keydown", function (e) {
    if (e.target.classList && e.target.classList.contains("host-select")) e.target.removeAttribute("data-pointer");
  });
  document.addEventListener("focusout", function (e) {
    if (e.target.classList && e.target.classList.contains("host-select")) e.target.removeAttribute("data-pointer");
  });

  // Another document of this site changed a choice: the page around a demo frame (the frame hears
  // it here), or another tab. localStorage raises "storage" in every document but the one that
  // wrote, so open frames follow the header without reloading.
  window.addEventListener("storage", function (e) {
    if (e.key && e.key.indexOf("ib-host-") !== 0) return;
    applySets();
  });

  // Enhanced navigation can reset <html> attributes to the server's version.
  if (window.Blazor && Blazor.addEventListener) {
    Blazor.addEventListener("enhancedload", function () {
      document.documentElement.classList.add("js");
      applySets();
    });
  }
})();
