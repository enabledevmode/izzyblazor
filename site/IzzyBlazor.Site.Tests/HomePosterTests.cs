using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

// The home page's hero shows the store dashboard in two frames, a desktop's and a phone's. Until a
// frame paints, a picture of it stands in its place (wwwroot/img/home/store-{desktop|phone}-{light|dark}.webp,
// the frame's background in site.css), so the hero looks whole at the page's first paint.
// To retake them after changing the dashboard: set IZZY_UPDATE_PREVIEWS=1 and run this test.
public sealed class HomePosterTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private static readonly (string Name, int Width, int Height, string Query)[] Frames =
    [
        ("desktop", 1280, 940, "mode=static&scrollbar=none"),
        ("phone", 390, 780, "mode=static&scrollbar=none"),
    ];

    [Fact]
    public async Task The_hero_frames_have_pictures_taken_from_them()
    {
        var folder = Path.Combine(RegistryTests.SiteFolder(), "wwwroot", "img", "home");
        var files = Frames.SelectMany(f => new[] { $"store-{f.Name}-light.webp", $"store-{f.Name}-dark.webp" }).ToList();

        if (Environment.GetEnvironmentVariable("IZZY_UPDATE_PREVIEWS") != "1")
        {
            var missing = files.Where(f => !File.Exists(Path.Combine(folder, f))).ToList();
            Assert.True(missing.Count == 0, $"Hero pictures missing; set IZZY_UPDATE_PREVIEWS=1 and run this test: {string.Join(", ", missing)}");
            return;
        }

        Directory.CreateDirectory(folder);
        var browser = await TestBrowser.OpenAsync();
        await using var _ = browser;
        foreach (var (name, width, height, query) in Frames)
        {
            foreach (var scheme in new[] { Microsoft.Playwright.ColorScheme.Light, Microsoft.Playwright.ColorScheme.Dark })
            {
                var page = await browser.NewPageAsync(new()
                {
                    ViewportSize = new() { Width = width, Height = height },
                    ColorScheme = scheme,
                    ReducedMotion = ReducedMotion.Reduce,
                    Locale = "en-US",
                });
                var response = await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"frame/home-store?{query}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
                Assert.True(response?.Ok, $"frame/home-store answered {response?.Status}");
                await page.EvaluateAsync("() => document.fonts.ready");
                var png = Convert.ToBase64String(await page.ScreenshotAsync(new() { Type = ScreenshotType.Png }));

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
                        return canvas.toDataURL("image/webp", 0.8).split(",")[1];
                    }
                    """, png);
                var file = $"store-{name}-{(scheme == Microsoft.Playwright.ColorScheme.Dark ? "dark" : "light")}.webp";
                await File.WriteAllBytesAsync(Path.Combine(folder, file), Convert.FromBase64String(webp));
                await page.CloseAsync();
            }
        }
    }
}
