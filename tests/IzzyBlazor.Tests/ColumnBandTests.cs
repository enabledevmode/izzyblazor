using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

// Headers over the columns inside them.
public class ColumnBandTests : BunitContext
{
    public ColumnBandTests()
    {
        Services.AddSingleton<IRequestCookies>(new NoCookies());
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
    }

    // A band's columns join after the columns beside it: they still come in page order, and the
    // address can sort by one on the first load.
    [Fact]
    public void Bands_draw_a_header_row_per_level_over_columns_in_page_order()
    {
        Services.GetRequiredService<BunitNavigationManager>().NavigateTo("http://localhost/?sort=-q1");
        var cut = Render<ColumnBandProbe>();
        cut.WaitForAssertion(() => Assert.Contains(cut.FindAll("tbody > tr > td:first-child"), c => c.TextContent.Trim() == "North"));
        var head = cut.FindAll("thead > tr");

        // Each band right above its columns: the shallower Q1 has a gap above it.
        Assert.Equal(3, head.Count);
        Assert.Equal(["", "", "", "Year", ""], head[0].Children.Select(c => c.TextContent.Trim()));
        Assert.Equal(["", "Q1", "H2", ""], head[1].Children.Select(c => c.TextContent.Trim()));
        Assert.Equal("2", head[1].QuerySelector("th")!.GetAttribute("colspan"));
        // The outermost bands name their column groups; a band inside one covers its columns.
        Assert.Equal(["colgroup", "colgroup", "col"], head.SelectMany(r => r.QuerySelectorAll("th")).Take(3).Select(c => c.GetAttribute("scope")));
        Assert.Equal(["Region", "Revenue", "Margin", "Q3", "Total"], head[2].Children.Select(c => c.TextContent.Trim()));

        // A column group per band, which scope="colgroup" names, and one per column with none.
        Assert.Equal([1, 2, 1, 1], cut.FindAll("table > colgroup").Select(g => g.Children.Length));

        Assert.Equal(["South", "West", "North"], cut.FindAll("tbody > tr > td:first-child").Select(c => c.TextContent.Trim()));
    }

    // A saved layout places and freezes columns one at a time: a band still keeps its columns
    // together, and is frozen as its first column is.
    [Fact]
    public void A_saved_layout_keeps_a_band_together_and_freezes_it_as_one()
    {
        var layout = new DataGridLayout
        {
            Columns =
            [
                new() { Key = "q1", Frozen = ColumnFrozen.Start, Width = "120px" },
                new() { Key = "q1margin", Position = 9, Width = "120px" },
                new() { Key = "region", Position = 5 },
                new() { Key = "q3", Position = 0 },
            ],
        };
        var cut = Render<ColumnBandProbe>(p => p.Add(x => x.Layout, layout));
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("thead > tr").Count));

        var heads = cut.FindAll("thead > tr")[2].Children;
        Assert.Equal(["Revenue", "Margin", "Q3", "Total", "Region"], heads.Select(c => c.TextContent.Trim()));
        Assert.Equal([true, true, false, false, false], heads.Select(c => c.ClassList.Contains("ib-frozen-start")));
        Assert.Contains("ib-frozen-start", cut.FindAll("th.ib-table-band").Single(c => c.TextContent.Trim() == "Q1").ClassName);
    }

    private sealed class NoCookies : IRequestCookies
    {
        public string? Get(string name) => null;
    }
}
