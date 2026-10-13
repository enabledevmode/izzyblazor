// Host-only helpers: the look and accent selects. Uses event delegation on the document, so it
// keeps working after Blazor's enhanced navigation swaps page content. Light and dark is the
// library's ColorSchemeSwitch (its cookie, read by IzzyTheme), not this.
(function () {
  // The look (data-ib-theme) and accent (data-ib-accent) on <html>, chosen in the header's
  // selects and kept in localStorage. Default: no attribute.
  var SETS = { look: "data-ib-theme", color: "data-ib-accent" };

  // Older builds kept a named color theme, or accent, gray and radius; drop them.
  try { ["preset", "accent", "gray", "radius", "theme"].forEach(function (k) { localStorage.removeItem("ib-host-" + k); }); } catch (e) { }

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
