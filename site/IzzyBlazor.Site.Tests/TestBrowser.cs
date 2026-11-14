using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>
/// A test's use of the one Chromium the run shares: starting one per test ran 20 of them at once, on
/// a machine someone's working on. Each page or context a test opens is its own, with its own cookies
/// and storage, and closes when the test ends. Installs Chromium on the run's first use.
/// <para>
/// For a test that fails only somewhere else (CI's Linux runners): set IZZY_TRACE to names of test
/// classes or methods, comma-separated, and each of their contexts saves a Playwright trace to
/// <c>traces/{test}-{n}.zip</c> beside the tests (open one with <c>playwright show-trace</c>).
/// </para>
/// </summary>
internal sealed class TestBrowser : IAsyncDisposable
{
    private static readonly Lazy<Task<IBrowser>> Shared = new(StartAsync);

    private readonly IBrowser _browser;
    private readonly List<IBrowserContext> _contexts = [];
    private readonly string? _trace;

    private TestBrowser(IBrowser browser, string? trace) => (_browser, _trace) = (browser, trace);

    public static async Task<TestBrowser> OpenAsync(
        [System.Runtime.CompilerServices.CallerFilePath] string file = "",
        [System.Runtime.CompilerServices.CallerMemberName] string test = "")
    {
        var type = Path.GetFileNameWithoutExtension(file.Replace('\\', '/').Split('/')[^1]);
        var traced = (Environment.GetEnvironmentVariable("IZZY_TRACE") ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(name => name == type || name == test);
        return new(await Shared.Value, traced ? $"{type}.{test}" : null);
    }

    private static async Task<IBrowser> StartAsync()
    {
        Assert.Equal(0, Microsoft.Playwright.Program.Main(["install", "chromium"]));
        // CI's runners share their CPU among four test threads and the site: there an enhanced
        // navigation of a docs page (~700 KB) can take 5 to 8 seconds, past the 5-second default.
        Microsoft.Playwright.Assertions.SetDefaultExpectTimeout(15_000);
        var playwright = await Playwright.CreateAsync();
        return await playwright.Chromium.LaunchAsync();
    }

    public async Task<IPage> NewPageAsync(BrowserNewPageOptions? options = null)
    {
        var page = await _browser.NewPageAsync(options);
        await TrackAsync(page.Context);
        return page;
    }

    public async Task<IBrowserContext> NewContextAsync(BrowserNewContextOptions? options = null)
    {
        var context = await _browser.NewContextAsync(options);
        await TrackAsync(context);
        return context;
    }

    /// <summary>
    /// On a <c>?mode=interactive</c> page, waits for each example's circuit (ExampleHost's
    /// <c>data-ib-live</c>). Until then the page is its prerender: what a test types is drawn over
    /// when the circuit starts, and a submit posts as a static form.
    /// </summary>
    public static async Task WaitForLiveAsync(IPage page)
    {
        if (!page.Url.Contains("mode=interactive", StringComparison.Ordinal)) return;
        await page.WaitForFunctionAsync(
            "() => [...document.querySelectorAll('[data-ex] .ex-stage')].every(stage => stage.querySelector('[data-ib-live]'))",
            null, new() { PollingInterval = 100 });
    }

    private async Task TrackAsync(IBrowserContext context)
    {
        _contexts.Add(context);
        if (_trace is not null)
        {
            await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true });
        }
    }

    public async ValueTask DisposeAsync()
    {
        for (var i = 0; i < _contexts.Count; i++)
        {
            if (_trace is not null)
            {
                await _contexts[i].Tracing.StopAsync(new() { Path = Path.Combine(AppContext.BaseDirectory, "traces", $"{_trace}-{i + 1}.zip") });
            }

            await _contexts[i].CloseAsync();
        }
    }
}
