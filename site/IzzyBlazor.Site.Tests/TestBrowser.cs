using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>
/// A test's use of the one Chromium the run shares: starting one per test ran 20 of them at once, on
/// a machine someone's working on. Each page or context a test opens is its own, with its own cookies
/// and storage, and closes when the test ends. Installs Chromium on the run's first use.
/// </summary>
internal sealed class TestBrowser : IAsyncDisposable
{
    private static readonly Lazy<Task<IBrowser>> Shared = new(StartAsync);

    private readonly IBrowser _browser;
    private readonly List<IBrowserContext> _contexts = [];

    private TestBrowser(IBrowser browser) => _browser = browser;

    public static async Task<TestBrowser> OpenAsync() => new(await Shared.Value);

    private static async Task<IBrowser> StartAsync()
    {
        Assert.Equal(0, Microsoft.Playwright.Program.Main(["install", "chromium"]));
        var playwright = await Playwright.CreateAsync();
        return await playwright.Chromium.LaunchAsync();
    }

    public async Task<IPage> NewPageAsync(BrowserNewPageOptions? options = null)
    {
        var page = await _browser.NewPageAsync(options);
        _contexts.Add(page.Context);
        return page;
    }

    public async Task<IBrowserContext> NewContextAsync(BrowserNewContextOptions? options = null)
    {
        var context = await _browser.NewContextAsync(options);
        _contexts.Add(context);
        return context;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var context in _contexts)
        {
            await context.CloseAsync();
        }
    }
}
