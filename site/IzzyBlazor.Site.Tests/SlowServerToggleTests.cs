using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>
/// A Dialog's and a Popover's script against a slow server in Chromium: opens and closes quicker
/// than the server answers end where the user left them, with the server agreeing, rather than
/// each setting the other back for ever or the last one lost. The server is played in the page
/// (Static, so no real one answers): like ToggleComponentBase, it takes each ibtoggle that changes
/// its state and, 150ms later, renders data-open.
/// </summary>
public sealed class SlowServerToggleTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    // The root, the element that opens, and how the page opens and closes it.
    private const string DialogParts = """
        {
            root: document.getElementById('whats-new').parentElement,
            target: document.getElementById('whats-new'),
            isOpen: () => document.getElementById('whats-new').open,
            open: () => document.querySelector("main button[commandfor='whats-new']:not(dialog button)").click(),
            close: () => document.getElementById('whats-new').close(),
        }
        """;

    private const string PopoverParts = """
        {
            root: document.querySelector('main ib-popover:has(> [popover])'),
            target: document.querySelector('main ib-popover:has(> [popover]) > [popover]'),
            isOpen: () => document.querySelector('main ib-popover:has(> [popover]) > [popover]').matches(':popover-open'),
            open: () => document.querySelector('main ib-popover:has(> [popover]) > [popover]').showPopover(),
            close: () => document.querySelector('main ib-popover:has(> [popover]) > [popover]').hidePopover(),
        }
        """;

    [Theory]
    [InlineData("dialog", "close open close", false)]
    [InlineData("dialog", "close open", true)]
    [InlineData("popover", "close open close", false)]
    [InlineData("popover", "close open", true)]
    public async Task Quick_opens_and_closes_end_where_the_user_left_them(string component, string steps, bool open)
    {
        Assert.Equal(0, Microsoft.Playwright.Program.Main(["install", "chromium"]));
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 } });
        var path = component == "dialog" ? "docs/components/dialog" : "docs/components/popover";
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, path + "?mode=static").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });

        var parts = component == "dialog" ? DialogParts : PopoverParts;
        await page.EvaluateAsync($$"""
            () => {
                const parts = window.__parts = {{parts}};
                window.__server = false;
                parts.root.addEventListener('ibtoggle', e => {
                    if (e.detail.id !== parts.target.id || e.detail.open === window.__server) return;
                    const render = window.__server = e.detail.open;
                    setTimeout(() => render ? parts.root.setAttribute('data-open', '') : parts.root.removeAttribute('data-open'), 150);
                });
            }
            """);

        // Opened and answered, then the steps, each before the server has answered the last.
        await page.EvaluateAsync("() => window.__parts.open()");
        await page.WaitForTimeoutAsync(700);
        await page.EvaluateAsync("""
            async steps => {
                for (const step of steps.split(' ')) {
                    window.__parts[step]();
                    await new Promise(r => setTimeout(r, 20));
                }
            }
            """, steps);

        // Count changes once the server has had time to answer.
        await page.WaitForTimeoutAsync(1000);
        await page.EvaluateAsync("""
            () => {
                window.__changes = 0;
                new MutationObserver(m => window.__changes += m.length)
                    .observe(window.__parts.root, { attributes: true, subtree: true, attributeFilter: ['open', 'data-open'] });
                window.__parts.target.addEventListener('toggle', () => window.__changes++);
            }
            """);
        await page.WaitForTimeoutAsync(1000);

        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.__changes"));
        Assert.Equal(open, await page.EvaluateAsync<bool>("() => window.__parts.isOpen()"));
        Assert.Equal(open, await page.EvaluateAsync<bool>("() => window.__server"));
    }
}
