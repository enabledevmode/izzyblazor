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
}
