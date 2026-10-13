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

  // The named theme (data-ib-theme on <html>), chosen in the header's select and kept in
  // localStorage under its own key (the light/dark choice is KEY). Default: no attribute.
  var PRESET = "ib-host-preset";

  // Older builds kept accent, gray and radius separately; drop them so they don't linger.
  try { ["accent", "gray", "radius"].forEach(function (k) { localStorage.removeItem("ib-host-" + k); }); } catch (e) { }

  function applySets() {
    var root = document.documentElement;
    var v = null;
    try { v = localStorage.getItem(PRESET); } catch (e) { }
    if (v) root.setAttribute("data-ib-theme", v);
    else root.removeAttribute("data-ib-theme");
    ["accent", "gray", "radius"].forEach(function (k) { root.removeAttribute("data-ib-" + k); });
    document.querySelectorAll("[data-host-preset]").forEach(function (s) { s.value = v || ""; });
  }

  document.addEventListener("change", function (e) {
    var select = e.target.closest && e.target.closest("[data-host-preset]");
    if (!select) return;
    try {
      if (select.value) localStorage.setItem(PRESET, select.value);
      else localStorage.removeItem(PRESET);
    } catch (err) { }
    applySets();
  });

  applySets();

  // Enhanced navigation can reset <html> attributes to the server's version.
  if (window.Blazor && Blazor.addEventListener) {
    Blazor.addEventListener("enhancedload", function () {
      document.documentElement.classList.add("js");
      apply(stored());
      applySets();
    });
  }
})();
