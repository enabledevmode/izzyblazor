using IzzyBlazor.Site.Services;
using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

// Each block's card picture (wwwroot/img/blocks/{id}.webp) is its first preview at a desktop's
// width, taken in its own 1280px viewport so it lays out as on a desktop: the block with a margin
// around it, at the card's 16:10, scaled down.
// To retake them after changing a block: set IZZY_UPDATE_PREVIEWS=1 and run this test.
public sealed class BlockPictureTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private const int Width = 1280;
    private const int Height = 800;
    private const int Saved = 960;
    private const int Margin = 40;

    [Fact]
    public async Task Each_block_has_a_picture_taken_from_its_preview()
    {
        var folder = Path.Combine(RegistryTests.SiteFolder(), "wwwroot");
        var blocks = new BlockCatalog(Path.Combine(folder, "..", "..", "..", "catalog")).All;
        Assert.NotEmpty(blocks);

        if (Environment.GetEnvironmentVariable("IZZY_UPDATE_PREVIEWS") != "1")
        {
            var missing = blocks.Where(b => !File.Exists(Path.Combine(folder, b.Picture))).Select(b => b.Picture).ToList();
            Assert.True(missing.Count == 0, $"Blocks with no picture; set IZZY_UPDATE_PREVIEWS=1 and run this test: {string.Join(", ", missing)}");
            return;
        }

        var browser = await TestBrowser.OpenAsync();
        await using var _ = browser;
        var page = await browser.NewPageAsync(new()
        {
            ViewportSize = new() { Width = Width, Height = Height },
            ColorScheme = Microsoft.Playwright.ColorScheme.Light,
            ReducedMotion = ReducedMotion.Reduce,
            Locale = "en-US",
        });
        foreach (var block in blocks)
        {
            var response = await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"frame/blocks/{block.Id}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
            Assert.True(response?.Ok, $"frame/blocks/{block.Id} answered {response?.Status}");
            await page.EvaluateAsync("() => document.fonts.ready");
            // What the block draws: the boxes of its text, pictures, controls and anything framed (a
            // border, a shadow or a fill narrower than the page), together, with a margin of the page around.
            var box = await page.EvaluateAsync<float[]>(
                """
                (margin) => {
                    const root = document.querySelector(".frame-block");
                    const page = root.getBoundingClientRect();
                    let [l, t, r, b] = [Infinity, Infinity, -Infinity, -Infinity];
                    for (const el of root.querySelectorAll("*")) {
                        if (el.closest("svg") !== null && el.tagName !== "svg") continue;
                        const x = el.getBoundingClientRect();
                        if (x.width === 0 || x.height === 0) continue;
                        const css = getComputedStyle(el);
                        const framed = x.width < page.width * 0.9
                            && (css.borderTopWidth !== "0px" || css.boxShadow !== "none" || css.backgroundColor !== "rgba(0, 0, 0, 0)");
                        const leaf = el.matches("img, svg, input, button, a, iframe") || [...el.childNodes].some((n) => n.nodeType === 3 && n.textContent.trim());
                        if (!framed && !leaf) continue;
                        [l, t, r, b] = [Math.min(l, x.left), Math.min(t, x.top), Math.max(r, x.right), Math.max(b, x.bottom)];
                    }
                    // Margin the page hasn't room for, at its edges, is added on the canvas.
                    const [width, pl, pt] = [document.documentElement.scrollWidth, Math.max(0, margin - l), Math.max(0, margin - t)];
                    const pr = Math.max(0, r + margin - width);
                    [l, t, r, b] = [Math.max(0, l - margin), Math.max(0, t - margin), Math.min(width, r + margin), b + margin];
                    return [l, t, r - l, b - t, pl, pt, pr];
                }
                """, Margin);
            var clip = new Clip { X = box[0], Y = box[1], Width = box[2], Height = box[3] };
            var png = Convert.ToBase64String(await page.ScreenshotAsync(new() { Type = ScreenshotType.Png, Clip = clip, FullPage = true }));

            // Chromium's canvas encodes WebP, so no image library is needed here: the shot centered
            // on the page's background at the card's 16:10, scaled.
            var webp = await page.EvaluateAsync<string>(
                """
                async ([png, width, height, pl, pt, pr]) => {
                    const image = new Image();
                    image.src = "data:image/png;base64," + png;
                    await image.decode();
                    let [w, h] = [image.width + pl + pr, image.height + pt];
                    [w, h] = w / h > width / height ? [w, w * height / width] : [h * width / height, h];
                    const scale = width / w;
                    const canvas = document.createElement("canvas");
                    [canvas.width, canvas.height] = [width, height];
                    const context = canvas.getContext("2d");
                    const fill = getComputedStyle(document.querySelector(".frame-block")).backgroundColor;
                    context.fillStyle = fill === "rgba(0, 0, 0, 0)" ? getComputedStyle(document.body).backgroundColor : fill;
                    context.fillRect(0, 0, width, height);
                    context.imageSmoothingQuality = "high";
                    const [dw, dh] = [image.width * scale, image.height * scale];
                    const [x, y] = [(width - (image.width + pl + pr) * scale) / 2 + pl * scale, (height - (image.height + pt) * scale) / 2 + pt * scale];
                    context.drawImage(image, x, y, dw, dh);
                    return canvas.toDataURL("image/webp", 0.85).split(",")[1];
                }
                """,
                new object[] { png, Saved, Saved * Height / Width, (double)box[4], (double)box[5], (double)box[6] });
            await File.WriteAllBytesAsync(Path.Combine(folder, block.Picture), Convert.FromBase64String(webp));
        }
    }
}
