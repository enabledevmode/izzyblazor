// Host-only helpers: the theme switcher. Uses event delegation on the document,
// so it keeps working after Blazor's enhanced navigation swaps page content.
(function () {
  var KEY = "ib-host-theme";

  function stored() {
    try { return localStorage.getItem(KEY) || "system"; } catch (e) { return "system"; }
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

  // Enhanced navigation can reset <html> attributes to the server's version.
  if (window.Blazor && Blazor.addEventListener) {
    Blazor.addEventListener("enhancedload", function () {
      document.documentElement.classList.add("js");
      apply(stored());
    });
  }
})();
