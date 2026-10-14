using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

public class DialogServiceTests : BunitContext
{
    private sealed class Content : ComponentBase
    {
        [CascadingParameter] public DialogInstance? Dialog { get; set; }

        [Parameter] public string? Greeting { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "p");
            builder.AddAttribute(1, "class", "greeting");
            builder.AddContent(2, Greeting);
            builder.CloseElement();
            builder.OpenElement(3, "button");
            builder.AddAttribute(4, "class", "save");
            builder.AddAttribute(5, "onclick", EventCallback.Factory.Create(this, () => Dialog!.Close("saved")));
            builder.CloseElement();
            builder.OpenElement(6, "button");
            builder.AddAttribute(7, "class", "cancel");
            builder.AddAttribute(8, "onclick", EventCallback.Factory.Create(this, () => Dialog!.Cancel()));
            builder.CloseElement();
        }
    }

    private DialogService Setup()
    {
        Services.AddIzzyBlazorCore();
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        return Services.GetRequiredService<DialogService>();
    }

    [Fact]
    public async Task Opening_from_other_threads_while_the_host_reads_the_list_is_safe()
    {
        // Dialogs opened off the dispatcher (a timer, a hub callback) while the host renders.
        var service = new DialogService();
        service.AddHost(new object());
        var opening = Enumerable.Range(0, 8).Select(n => Task.Run(() =>
        {
            for (var i = 0; i < 200; i++)
            {
                _ = service.OpenAsync(typeof(Content), "Hi");
            }
        })).ToArray();

        var all = Task.WhenAll(opening);
        while (!all.IsCompleted)
        {
            foreach (var entry in service.Open)
            {
                Assert.NotNull(entry.Instance);
            }
        }

        await Task.WhenAll(opening);
        Assert.Equal(1600, service.Open.Count);
        Assert.Equal(1600, service.Open.Select(e => e.Instance.Id).Distinct().Count());
    }

    [Fact]
    public async Task Opens_a_component_and_returns_what_it_closes_with()
    {
        var service = Setup();
        var host = Render<DialogHost>();

        Task<DialogResult> task = null!;
        await host.InvokeAsync(() => { task = service.OpenAsync<Content>("Edit", new() { ["Greeting"] = "Hello" }); });

        host.WaitForAssertion(() => Assert.Equal("Hello", host.Find("dialog .greeting").TextContent));
        Assert.Equal("Edit", host.Find("dialog .ib-dialog-title").TextContent);
        Assert.True(host.Find("ib-dialog").HasAttribute("data-open"));

        host.Find("dialog .save").Click();
        var result = await task;

        Assert.False(result.Canceled);
        Assert.Equal("saved", result.DataAs<string>());
        // Closed first (so the browser returns focus), removed shortly after.
        host.WaitForAssertion(() => Assert.False(host.Find("ib-dialog").HasAttribute("data-open")));
        host.WaitForAssertion(() => Assert.Empty(host.FindAll("ib-dialog")), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Cancel_comes_back_as_canceled()
    {
        var service = Setup();
        var host = Render<DialogHost>();
        Task<DialogResult> task = null!;
        await host.InvokeAsync(() => { task = service.OpenAsync<Content>("Edit"); });

        host.WaitForElement("dialog .cancel").Click();

        Assert.True((await task).Canceled);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Confirm_returns_the_answer(bool confirm)
    {
        var service = Setup();
        var host = Render<DialogHost>();
        Task<bool> task = null!;
        await host.InvokeAsync(() => { task = service.ConfirmAsync("Remove Ada?", "Remove member", new() { ConfirmText = "Remove", Accent = Accent.Danger }); });

        host.WaitForAssertion(() => Assert.Equal("Remove Ada?", host.Find("dialog .ib-confirm-message").TextContent));
        var buttons = host.FindAll("dialog .ib-dialog-actions button");
        Assert.Equal("Remove", buttons[1].TextContent.Trim());
        Assert.Contains("ib-btn-danger", buttons[1].ClassName);
        buttons[confirm ? 1 : 0].Click();

        Assert.Equal(confirm, await task);
    }

    [Fact]
    public void Without_an_interactive_host_it_says_what_is_missing()
    {
        Services.AddIzzyBlazorCore();
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var service = Services.GetRequiredService<DialogService>();
        Render<DialogHost>();

        // It throws straight away, before any dialog exists.
        var error = Assert.Throws<InvalidOperationException>(() => { _ = service.OpenAsync<Content>("Edit"); });
        Assert.Contains("DialogHost", error.Message);
    }

    [Fact]
    public async Task Two_hosts_show_a_dialog_once()
    {
        var service = Setup();
        var first = Render<DialogHost>();
        var second = Render<DialogHost>();
        await second.InvokeAsync(() => { _ = service.OpenAsync<Content>("Edit", new() { ["Greeting"] = "Hello" }); });

        second.WaitForAssertion(() => Assert.Single(second.FindAll("dialog .greeting")));
        Assert.Empty(first.FindAll("dialog"));

        // When the newer host goes, the one before takes over.
        await second.InvokeAsync(second.Instance.Dispose);
        first.WaitForAssertion(() => Assert.Single(first.FindAll("dialog .greeting")));
    }

    [Fact]
    public async Task A_dialog_can_take_the_color_scheme_and_density_of_where_it_was_opened()
    {
        var service = Setup();
        var host = Render<DialogHost>();
        await host.InvokeAsync(() => { _ = service.OpenAsync<Content>("Edit", null, new DialogOptions { ColorScheme = ColorScheme.Dark, Density = ThemeDensity.Compact }); });

        var dialog = host.WaitForElement("dialog");
        Assert.Contains("ib-scheme-dark", dialog.ClassName);
        Assert.Equal("compact", dialog.GetAttribute("data-ib-density"));
    }

    [Fact]
    public async Task A_dialog_can_take_the_brand_of_where_it_was_opened()
    {
        var service = Setup();
        var host = Render<DialogHost>();
        await host.InvokeAsync(() =>
        {
            _ = service.OpenAsync<Content>("Named", null, new DialogOptions { Brand = "rose" });
            _ = service.OpenAsync<Content>("Hex", null, new DialogOptions { Brand = "#be123c" });
        });

        host.WaitForAssertion(() => Assert.Equal(2, host.FindAll("dialog").Count));
        var dialogs = host.FindAll("dialog");
        Assert.Equal("rose", dialogs[0].GetAttribute("data-ib-brand"));
        Assert.Contains("--ib-color-primary: light-dark(#be123c", dialogs[1].GetAttribute("style"));
    }
}
