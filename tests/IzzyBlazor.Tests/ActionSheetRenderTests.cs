using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace IzzyBlazor.Tests;

public class ActionSheetRenderTests : BunitContext
{
    // OnClick runs on interactive pages.
    public ActionSheetRenderTests() => SetRendererInfo(new RendererInfo("Server", isInteractive: true));

    [Fact]
    public void Choosing_an_item_runs_it_and_closes_the_sheet()
    {
        string? chosen = null;
        bool? reported = null;
        var cut = Render<ActionSheet>(p => p
            .Add(s => s.Id, "photo")
            .Add(s => s.Title, "IMG_2041.jpg")
            .Add(s => s.Open, true)
            .Add(s => s.OpenChanged, EventCallback.Factory.Create<bool>(this, v => reported = v))
            .AddChildContent<ActionSheetItem>(i => i
                .Add(x => x.OnClick, EventCallback.Factory.Create<MouseEventArgs>(this, () => chosen = "share"))
                .AddChildContent("Share")));

        // A bottom sheet named by its title, Cancel in place of the close button.
        var dialog = cut.Find("dialog.ib-dialog-bottom");
        Assert.Equal("photo-title", dialog.GetAttribute("aria-labelledby"));
        Assert.Empty(cut.FindAll(".ib-dialog-close"));
        Assert.Equal(["Share", "Cancel"], cut.FindAll("dialog button").Select(b => b.TextContent.Trim()));

        cut.FindAll("dialog button")[0].Click();

        Assert.Equal("share", chosen);
        Assert.False(reported);
        Assert.False(cut.Find("ib-dialog").HasAttribute("data-open"));
    }
}
