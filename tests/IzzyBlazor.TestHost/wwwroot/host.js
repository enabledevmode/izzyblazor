// Host-only helpers: the theme switcher. Uses event delegation on the document,
// so it keeps working after Blazor's enhanced navigation swaps page content.
(function () {
  var KEY = "ib-host-theme";

  function stored() {
    try {
      var v = localStorage.getItem(KEY);
      // An older build also kept the named theme under this key; anything else means system.
      return v === "light" || v === "dark" ? v : "system";
    } catch (e) { return "system"; }
  }

  function apply(theme) {
    var root = document.documentElement;
    if (theme === "light" || theme === "dark") root.setAttribute("data-theme", theme);
    else root.removeAttribute("data-theme");
    document.querySelectorAll("[data-host-theme]").forEach(function (b) {
      b.setAttribute("aria-pressed", String(b.getAttribute("data-host-theme") === theme));
    });
  }

  document.addEventListener("click", function (e) {
    var button = e.target.closest("[data-host-theme]");
    if (!button) return;
    var theme = button.getAttribute("data-host-theme");
    try { localStorage.setItem(KEY, theme); } catch (err) { }
    apply(theme);
  });

  apply(stored());

  // The look (data-ib-theme) and accent (data-ib-accent) on <html>, chosen in the header's
  // selects and kept in localStorage (the light/dark choice is KEY). Default: no attribute.
  var SETS = { look: "data-ib-theme", color: "data-ib-accent" };

  // Older builds kept a named color theme, or accent, gray and radius; drop them.
  try { ["preset", "accent", "gray", "radius"].forEach(function (k) { localStorage.removeItem("ib-host-" + k); }); } catch (e) { }

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
    apply(stored());
    applySets();
  });

  // Enhanced navigation can reset <html> attributes to the server's version.
  if (window.Blazor && Blazor.addEventListener) {
    Blazor.addEventListener("enhancedload", function () {
      document.documentElement.classList.add("js");
      apply(stored());
      applySets();
    });
  }
})();
