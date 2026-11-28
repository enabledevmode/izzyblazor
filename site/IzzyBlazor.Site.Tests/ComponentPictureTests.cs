using IzzyBlazor.Site.Services;
using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

// Each component's pictures (wwwroot/img/components/{id}.webp, and {id}-dark.webp in the dark scheme), for izzy's
// list and describe --json and the editors that show them: its first example's preview as its docs page draws it,
// 720px wide at 1x (an editor shows it at about half that, so it's sharp on a high-DPI screen too), cut off at
// 900px tall.
// To retake them after changing a component's first example: set IZZY_UPDATE_PREVIEWS=1 and run this test.
public sealed class ComponentPictureTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private const int Width = 720;
    private const int Tallest = 900;

    public static string File(string id, bool dark) => $"img/components/{id}{(dark ? "-dark" : "")}.webp";

    [Fact]
    public async Task Each_component_has_a_picture_of_its_first_example()
    {
        var folder = Path.Combine(RegistryTests.SiteFolder(), "wwwroot");
        var examples = site.Registry.AllComponents.Where(c => c.Examples.Count > 0).Select(c => (Component: c, Example: c.Examples[0])).ToList();
        Assert.NotEmpty(examples);

        if (Environment.GetEnvironmentVariable("IZZY_UPDATE_PREVIEWS") != "1")
        {
            var missing = examples.SelectMany(e => new[] { File(e.Component.Id, false), File(e.Component.Id, true) })
                .Where(p => !System.IO.File.Exists(Path.Combine(folder, p))).ToList();
            Assert.True(missing.Count == 0, $"Components with no picture; set IZZY_UPDATE_PREVIEWS=1 and run this test: {string.Join(", ", missing.Take(20))}{(missing.Count > 20 ? $" and {missing.Count - 20} more" : "")}");
            return;
        }

        Directory.CreateDirectory(Path.Combine(folder, "img", "components"));
        // A picture no component has any more goes.
        var kept = examples.SelectMany(e => new[] { File(e.Component.Id, false), File(e.Component.Id, true) }).Select(p => Path.GetFileName(p)).ToHashSet();
        foreach (var old in Directory.EnumerateFiles(Path.Combine(folder, "img", "components"), "*.webp").Where(p => !kept.Contains(Path.GetFileName(p))))
            System.IO.File.Delete(old);

        var browser = await TestBrowser.OpenAsync();
        await using var _ = browser;
        // Most are on their component's page; a few only on an article (button-state-in-address).
        var shown = ShownOn(Path.Combine(folder, "..", "Docs"));
        var pages = examples.GroupBy(e => Registry.ExamplePageHref(e.Component, e.Example) is var home
            && shown.TryGetValue(e.Example.Id, out var on) && !on.Contains(home) ? on[0] : home).ToList();
        foreach (var (scheme, dark) in new[] { (Microsoft.Playwright.ColorScheme.Light, false), (Microsoft.Playwright.ColorScheme.Dark, true) })
        {
            var page = await browser.NewPageAsync(new()
            {
                ViewportSize = new() { Width = 1440, Height = 900 },
                DeviceScaleFactor = 1,
                ColorScheme = scheme,
                ReducedMotion = ReducedMotion.Reduce,
                Locale = "en-US",
            });
            foreach (var group in pages)
            {
                var response = await page.GotoAsync(new Uri(site.Client.BaseAddress!, group.Key).ToString(), new() { WaitUntil = WaitUntilState.Load });
                Assert.True(response?.Ok, $"{group.Key} answered {response?.Status}");
                // Every preview at the one width, whatever the docs column is; a page example's window too.
                await page.AddStyleTagAsync(new() { Content = $"[data-ex] .ex-stage {{ width: {Width}px !important; max-width: none !important; }}" });
                await page.EvaluateAsync("() => document.fonts.ready");
                // An interactive example's island, once its circuit has drawn it; a page example's window, once loaded.
                var live = group.Where(e => !e.Example.Page && !e.Example.Modes.Contains("static")).Select(e => e.Example.Id).ToArray();
                await page.WaitForFunctionAsync(
                    """
                    (live) => live.every(id => document.querySelector(`section[data-ex='${id}'] [data-ib-live]`))
                      && [...document.querySelectorAll('[data-ex] iframe.ex-frame')].every(f => f.contentDocument?.readyState === 'complete')
                    """, live, new() { PollingInterval = 100, Timeout = 30_000 });
                try { await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 5_000 }); } catch (TimeoutException) { }
                // The site's header stays at the top as the page scrolls: it would be over a tall example's picture.
                await page.EvaluateAsync(
                    """
                    () => { for (const el of document.body.querySelectorAll("*"))
                        if (!el.closest("[data-ex]") && ["sticky", "fixed"].includes(getComputedStyle(el).position)) el.style.setProperty("position", "static", "important"); }
                    """);

                foreach (var (component, example) in group)
                {
                    var stage = page.Locator($"section[data-ex='{example.Id}'] .ex-stage").First;
                    Assert.True(await stage.CountAsync() > 0, $"{group.Key} has no preview for {example.Id}");
                    await stage.ScrollIntoViewIfNeededAsync();
                    var box = (await stage.BoundingBoxAsync())!;
                    var scroll = await page.EvaluateAsync<float>("() => window.scrollY");
                    var clip = new Clip { X = box.X, Y = box.Y + scroll, Width = box.Width, Height = Math.Min(box.Height, Tallest) };
                    var png = Convert.ToBase64String(await page.ScreenshotAsync(new() { Type = ScreenshotType.Png, Clip = clip, FullPage = true }));
                    // Chromium's canvas encodes WebP, so no image library is needed here.
                    var webp = await page.EvaluateAsync<string>(
                        """
                        async (png) => {
                            const image = new Image();
                            image.src = "data:image/png;base64," + png;
                            await image.decode();
                            const canvas = document.createElement("canvas");
                            [canvas.width, canvas.height] = [image.width, image.height];
                            canvas.getContext("2d").drawImage(image, 0, 0);
                            return canvas.toDataURL("image/webp", 0.82).split(",")[1];
                        }
                        """, png);
                    await System.IO.File.WriteAllBytesAsync(Path.Combine(folder, File(component.Id, dark)), Convert.FromBase64String(webp));
                }
            }
            await page.CloseAsync();
        }
    }

    // The hand-written pages (each with an @page of its own) that show an example, by the example's id:
    // those an Overview (no @page: its component's page) shows aren't listed.
    private static Dictionary<string, List<string>> ShownOn(string docs)
    {
        var onOverview = new HashSet<string>(StringComparer.Ordinal);
        var shown = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(docs, "*.razor", SearchOption.AllDirectories))
        {
            var text = System.IO.File.ReadAllText(path);
            var ids = System.Text.RegularExpressions.Regex.Matches(text, "<ExampleViewer Example=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
            if (ids.Count == 0) continue;
            var route = System.Text.RegularExpressions.Regex.Match(text, "@page \"/([^\"{]+)\"");
            if (!text.Contains("@page", StringComparison.Ordinal)) { onOverview.UnionWith(ids); continue; }
            if (!route.Success) continue;
            foreach (var id in ids)
            {
                if (!shown.TryGetValue(id, out var on)) shown[id] = on = [];
                on.Add(route.Groups[1].Value);
            }
        }
        foreach (var id in onOverview) shown.Remove(id);
        return shown;
    }
}
