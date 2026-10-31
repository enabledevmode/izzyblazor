using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>
/// The Toolbar's width sweep, in Chromium: at every width from a phone's to a desktop's, each
/// toolbar on its Overview shows the same before its script arrives (the server's estimates, as
/// container queries) as after (measured): the same items in the bar, labels collapsed or not, and
/// the "…" or not. So nothing moves when the script arrives. Installs Chromium on its first run
/// (CI installs it, with its system libraries, before the tests).
/// </summary>
public sealed partial class ToolbarWidthTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    // Each toolbar's bar as the reader sees it: its name, the items shown (by text or label), " c"
    // when labels are collapsed, " …" when the menu button shows.
    private const string Read = """
        () => [...document.querySelectorAll("ib-toolbar")].map((t) => {
          const units = [...t.querySelectorAll(".ib-toolbar-unit")].filter((u) => u.closest("ib-toolbar") === t && u.checkVisibility());
          const names = units.map((u) => u.querySelector("[aria-label]")?.getAttribute("aria-label") || u.textContent.trim());
          const label = t.querySelector(".ib-toolbar-collapse > .ib-btn-label");
          const collapsed = label && getComputedStyle(label).position === "absolute" ? " c" : "";
          const more = t.querySelector(":scope > .ib-toolbar-end > .ib-toolbar-more")?.checkVisibility() ? " …" : "";
          return (t.getAttribute("aria-label") ?? "?") + ": " + names.join(", ") + collapsed + more;
        })
        """;

    // Every toolbar with a "…" has measured.
    private const string Measured = """
        () => [...document.querySelectorAll("ib-toolbar")].every((t) => t.hasAttribute("data-ib-measured") || !t.querySelector(".ib-toolbar-more"))
        """;

    [Fact]
    public async Task Nothing_moves_when_the_script_arrives()
    {
        await using var browser = await TestBrowser.OpenAsync();
        var url = new Uri(site.Client.BaseAddress!, "docs/components/toolbar?mode=static").ToString();

        // Before: the page without its scripts, on a browser that runs script (so no `scripting: none`
        // rules), resized for each width: container queries follow.
        var before = await browser.NewPageAsync();
        await before.RouteAsync(Scripts(), route => route.AbortAsync());
        await before.GotoAsync(url);

        // After: a fresh load at each width, as the script measures only once on arrival.
        await using var after = await browser.NewContextAsync();
        var differences = new List<string>();
        for (var width = 320; width <= 1400; width += 10)
        {
            await before.SetViewportSizeAsync(width, 900);
            await before.EvaluateAsync("() => document.fonts.ready");
            var expected = await before.EvaluateAsync<string[]>(Read);

            var page = await after.NewPageAsync();
            await page.SetViewportSizeAsync(width, 900);
            await page.GotoAsync(url);
            await page.WaitForFunctionAsync(Measured);
            var actual = await page.EvaluateAsync<string[]>(Read);
            await page.CloseAsync();

            Assert.Equal(expected.Length, actual.Length);
            differences.AddRange(expected.Zip(actual)
                .Where(pair => pair.First != pair.Second)
                .Select(pair => $"{width}px: before \"{pair.First}\", after \"{pair.Second}\""));
        }

        Assert.Empty(differences);
    }

    // The library's and the site's scripts and Blazor's.
    [GeneratedRegex(@"(blazor\.web|lib\.module|/site(\.[a-z0-9]+)?)\.js")]
    private static partial Regex Scripts();
}
