using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

// A DataGrid's or Scheduler's toolbar parts placed outside it: in a tile's header, or anywhere by For.
public class OutsidePartsTests : BunitContext
{
    public OutsidePartsTests()
    {
        Services.AddIzzyBlazorCore();
        Services.AddSingleton<IRequestCookies>(new NoCookies());
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
    }

    [Fact]
    public void A_search_in_a_tiles_header_posts_the_grids_form_and_the_grid_leaves_its_own_toolbar_out()
    {
        var cut = Render<PartsProbe>(p => p.Add(c => c.Kind, "grid-in-tile"));

        var search = cut.Find(".ib-tile-end input[type=search]");
        var grid = cut.Find("ib-table");
        // Drawn before the grid, it still posts the grid's form and names the grid for its script.
        Assert.NotNull(cut.Find($"form#{search.GetAttribute("form")}"));
        Assert.Equal(grid.GetAttribute("data-ib-grid-id"), search.Closest(".ib-part")!.GetAttribute("data-ib-grid"));
        // Only the export is left in the grid's toolbar.
        Assert.Empty(grid.QuerySelectorAll("input[type=search]"));
        Assert.Single(grid.QuerySelectorAll(".ib-grid-toolbar"));
        Assert.NotNull(grid.QuerySelector(".ib-grid-toolbar a[href^='/orders.csv']"));
    }

    [Fact]
    public void Parts_that_name_a_scheduler_by_For_draw_its_navigation_and_it_draws_no_toolbar_of_its_own()
    {
        var cut = Render<PartsProbe>(p => p.Add(c => c.Kind, "scheduler-for"));

        var bar = cut.Find(".page-bar");
        Assert.Equal("Today", bar.QuerySelector("a")!.TextContent.Trim());
        Assert.Equal(3, bar.QuerySelectorAll(".ib-part").Length);
        Assert.Empty(cut.FindAll("ib-scheduler .ib-sched-bar"));
    }

    private sealed class NoCookies : IRequestCookies
    {
        public string? Get(string name) => null;
    }
}
