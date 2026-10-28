using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

public class ReconnectDialogRenderTests : BunitContext
{
    [Fact]
    public void It_is_the_dialog_blazor_looks_for_named_by_its_title()
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var cut = Render<ReconnectDialog>();

        var dialog = cut.Find("dialog");
        Assert.Equal("components-reconnect-modal", dialog.Id);
        Assert.Equal("none", dialog.GetAttribute("closedby"));
        Assert.Equal("Connection lost", cut.Find("#" + dialog.GetAttribute("aria-labelledby")).TextContent);
        Assert.Contains("ib-dialog", dialog.ClassName);
        Assert.Null(dialog.GetAttribute("open"));
    }

    [Fact]
    public void Messages_come_from_izzy_text_for_the_script()
    {
        // Services before anything starts the renderer, SetRendererInfo included.
        Services.AddSingleton(new IzzyText { ReconnectRetrying = "Neuer Versuch in {0} Sekunden.", Retry = "Erneut" });
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var cut = Render<ReconnectDialog>(p => p.Add(d => d.Title, "Offline"));

        var dialog = cut.Find("dialog");
        Assert.Equal("Neuer Versuch in {0} Sekunden.", dialog.GetAttribute("data-retrying"));
        Assert.Equal("Couldn't rejoin. Trying again in 1 second.", dialog.GetAttribute("data-retrying-one"));
        Assert.Equal("Erneut", cut.Find("[data-ib-reconnect-action=retry]").TextContent);
        Assert.Equal("Offline", cut.Find("h2").TextContent);
        // One status region, empty until the script reports a state; the spinner has no role of its own.
        Assert.Single(cut.FindAll("[role=status]"));
        Assert.Equal("true", cut.Find(".ib-reconnect-spinner").GetAttribute("aria-hidden"));
    }
}
