using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>
/// Dialog, Popover and Accordion items against a slow server in Chromium: opens and closes quicker
/// than the server answers end where the user left them, with the server agreeing, rather than
/// each setting the other back for ever or the last one lost, whatever the server's speed.
/// </summary>
/// <remarks>
/// The server is played in the page (a Static page, so no real one answers), as an interactive
/// page's would be: the root is marked interactive, and like ToggleComponentBase each server takes
/// the ibtoggle events for its element, keeps the last <c>seq</c>, changes its state when it differs,
/// and after a delay renders data-open and data-ib-seq, setting only what changed since its last
/// render, as Blazor does.
/// </remarks>
public sealed class SlowServerToggleTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    // Plays a server for each root (with its element) and returns their states.
    private const string Servers = """
        ({ roots, latency }) => {
            window.__servers = roots.map(({ root, target }) => {
                root.setAttribute('data-ib-interactive', '');
                root.setAttribute('data-ib-seq', '0');
                const server = { root, target, state: !!(target.open || target.matches(':popover-open')), seq: 0 };
                server.rendered = { open: server.state, seq: 0 };
                server.state ? root.setAttribute('data-open', '') : root.removeAttribute('data-open');
                // Rendered as the server handles each event, with its state then; it arrives later.
                server.render = () => {
                    const open = server.state, seq = server.seq;
                    setTimeout(() => {
                        if (open !== server.rendered.open) open ? root.setAttribute('data-open', '') : root.removeAttribute('data-open');
                        if (seq !== server.rendered.seq) root.setAttribute('data-ib-seq', String(seq));
                        server.rendered = { open, seq };
                    }, latency);
                };
                target.addEventListener('ibtoggle', e => {
                    if (e.target !== target) return;
                    server.seq = Math.max(server.seq, e.detail.seq);
                    server.state = e.detail.open;
                    server.render();
                });
                return server;
            });
        }
        """;

    // Counts the elements' own opens and closes from now on (the servers' renders aside).
    private const string Changes = """
        () => {
            window.__changes = 0;
            const watch = new MutationObserver(m => window.__changes += m.length);
            for (const { target } of window.__servers) {
                watch.observe(target, { attributes: true, attributeFilter: ['open'] });
                target.addEventListener('toggle', () => window.__changes++);
            }
        }
        """;

    // The root that carries data-open, the element that opens, and how the user opens and closes it.
    private static string Parts(string component) => component switch
    {
        "dialog" => """
            (() => {
                const target = document.getElementById('whats-new');
                return {
                    root: target.parentElement, target,
                    isOpen: () => target.open,
                    open: () => document.querySelector("main button[commandfor='whats-new']:not(dialog button)").click(),
                    close: () => target.close(),
                };
            })()
            """,
        "popover" => """
            (() => {
                const root = document.querySelector('main ib-popover:has(> [popover])');
                const target = root.querySelector(':scope > [popover]');
                return {
                    root, target,
                    isOpen: () => target.matches(':popover-open'),
                    open: () => target.showPopover(),
                    close: () => target.hidePopover(),
                };
            })()
            """,
        _ => """
            (() => {
                const target = document.querySelector('main details.ib-disclosure:not([name])');
                const summary = target.querySelector(':scope > summary');
                return {
                    root: target, target,
                    isOpen: () => target.open,
                    open: () => { if (!target.open) summary.click(); },
                    close: () => { if (target.open) summary.click(); },
                };
            })()
            """,
    };

    private static async Task<IPage> OpenAsync(TestBrowser browser, SiteFixture site, string component)
    {
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 } });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/{component}?mode=static").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        return page;
    }

    [Theory]
    [InlineData("dialog", "close open close", false, 150)]
    [InlineData("dialog", "close open", true, 150)]
    [InlineData("dialog", "close open close", false, 2500)]
    [InlineData("popover", "close open close", false, 150)]
    [InlineData("popover", "close open", true, 150)]
    [InlineData("accordion", "close open close", false, 150)]
    [InlineData("accordion", "close open", true, 150)]
    [InlineData("accordion", "close open close", false, 2500)]
    [InlineData("accordion", "close open", true, 2500)]
    public async Task Quick_opens_and_closes_end_where_the_user_left_them(string component, string steps, bool open, int latency)
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await OpenAsync(browser, site, component);

        await page.EvaluateAsync($"() => window.__parts = {Parts(component)}");
        await page.EvaluateAsync($"latency => ({Servers})({{ roots: [window.__parts], latency }})", latency);

        // Opened and answered, then the steps, each before the server has answered the last.
        await page.EvaluateAsync("() => window.__parts.open()");
        await page.WaitForTimeoutAsync(latency + 500);
        await page.EvaluateAsync("""
            async steps => {
                for (const step of steps.split(' ')) {
                    window.__parts[step]();
                    await new Promise(r => setTimeout(r, 20));
                }
            }
            """, steps);

        // From here on the user's done: the late renders mustn't open or close it again.
        await page.EvaluateAsync(Changes);
        await page.WaitForTimeoutAsync(latency * 3 + 1000);

        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.__changes"));
        Assert.Equal(open, await page.EvaluateAsync<bool>("() => window.__parts.isOpen()"));
        Assert.Equal(open, await page.EvaluateAsync<bool>("() => window.__servers[0].state"));
    }

    // An exclusive Accordion's items, each with its own server (one AccordionItem each).
    private const string GroupRoots = """
        () => {
            const first = document.querySelector('main details.ib-disclosure[name]');
            return [...document.querySelectorAll(`main details.ib-disclosure[name="${first.name}"]`)].map(d => ({ root: d, target: d }));
        }
        """;

    private const string Summary = "i => window.__servers[i].target.querySelector(':scope > summary').click()";

    [Theory]
    [InlineData(150)]
    [InlineData(2500)]
    public async Task Quick_clicks_in_an_exclusive_Accordion_end_where_the_user_left_them(int latency)
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await OpenAsync(browser, site, "accordion");
        await page.EvaluateAsync($"latency => ({Servers})({{ roots: ({GroupRoots})(), latency }})", latency);

        // A open and answered; then B, A and A again quicker than the server: everything closed.
        await page.EvaluateAsync("() => { const a = window.__servers[0].target; if (!a.open) a.querySelector(':scope > summary').click(); }");
        await page.WaitForTimeoutAsync(latency + 500);
        foreach (var i in new[] { 1, 0, 0 })
        {
            await page.EvaluateAsync(Summary, i);
            await page.WaitForTimeoutAsync(20);
        }
        await page.EvaluateAsync(Changes);
        await page.WaitForTimeoutAsync(latency * 3 + 1000);

        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.__changes"));
        Assert.Equal("all closed", await page.EvaluateAsync<string>("() => window.__servers.some(s => s.target.open || s.state) ? JSON.stringify(window.__servers.map(s => [s.target.open, s.state])) : 'all closed'"));
    }

    [Theory]
    [InlineData(300)]
    [InlineData(2500)]
    public async Task Opening_an_exclusive_item_from_the_server_reports_its_sibling_closed(int after)
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await OpenAsync(browser, site, "accordion");
        await page.EvaluateAsync($"latency => ({Servers})({{ roots: ({GroupRoots})(), latency }})", 150);

        // A opened by the user, then, soon or well after, B opened by C#: the browser closes A,
        // and A's server must hear it, or C# can't open A again.
        await page.EvaluateAsync("() => { const a = window.__servers[0].target; if (!a.open) a.querySelector(':scope > summary').click(); }");
        await page.WaitForTimeoutAsync(after);
        await page.EvaluateAsync("() => { const b = window.__servers[1]; b.state = true; b.render(); }");
        await page.WaitForTimeoutAsync(1000);

        var ab = await page.EvaluateAsync<bool[]>("() => [window.__servers[0].target.open, window.__servers[0].state, window.__servers[1].target.open, window.__servers[1].state]");
        Assert.Equal([false, false, true, true], ab);
    }

    [Theory]
    [InlineData("popover")]
    [InlineData("accordion")]
    [InlineData("dialog")]
    public async Task A_render_between_a_close_and_its_report_doesnt_undo_it(string component)
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await OpenAsync(browser, site, component);
        await page.EvaluateAsync($"() => window.__parts = {Parts(component)}");
        await page.EvaluateAsync($"latency => ({Servers})({{ roots: [window.__parts], latency }})", 50);

        // Opened and answered. Then closed, and in the same moment, before the browser's toggle
        // event reports it, a render made before the close arrives: up to date by its count.
        await page.EvaluateAsync("() => window.__parts.open()");
        await page.WaitForTimeoutAsync(500);
        await page.EvaluateAsync("""
            () => {
                const { root } = window.__parts;
                window.__parts.close();
                root.setAttribute('data-ib-seq', root.getAttribute('data-ib-seq'));
                root.setAttribute('data-open', '');
            }
            """);
        await page.WaitForTimeoutAsync(500);

        Assert.False(await page.EvaluateAsync<bool>("() => window.__parts.isOpen()"));
        Assert.False(await page.EvaluateAsync<bool>("() => window.__servers[0].state"));
    }

    [Fact]
    public async Task An_interactive_Disclosure_rendered_open_is_open_before_it_paints()
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await OpenAsync(browser, site, "accordion");

        // As the interactive render brings it after prerendering: data-open, no open attribute.
        var state = await page.EvaluateAsync<string>("""
            () => new Promise(resolve => {
                const d = document.createElement('details');
                d.className = 'ib-disclosure';
                d.id = 'arrived';
                d.setAttribute('data-ib-interactive', '');
                d.setAttribute('data-ib-seq', '0');
                d.setAttribute('data-open', '');
                d.innerHTML = '<summary>Arrived</summary><p>Body</p>';
                document.querySelector('main').append(d);
                requestAnimationFrame(() => resolve(d.open ? 'open' : 'closed'));
            })
            """);

        Assert.Equal("open", state);
    }
}
