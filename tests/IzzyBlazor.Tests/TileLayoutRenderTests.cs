using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor.Tests;

public class TileLayoutRenderTests : BunitContext
{
    private static void AddTile(RenderTreeBuilder b, string id, int? col, int? row, int colSpan, int rowSpan = 1)
    {
        b.OpenComponent<Tile>(0);
        b.AddAttribute(1, nameof(Tile.Id), id);
        b.AddAttribute(2, nameof(Tile.Col), col);
        b.AddAttribute(3, nameof(Tile.Row), row);
        b.AddAttribute(4, nameof(Tile.ColSpan), colSpan);
        b.AddAttribute(5, nameof(Tile.RowSpan), rowSpan);
        b.AddAttribute(6, nameof(Tile.Title), id);
        b.CloseComponent();
    }

    // The tile's column, row and spans at the base width, or from a breakpoint ("-md").
    private static string Place(IRenderedComponent<TileLayout> cut, string id, string at = "")
    {
        var style = cut.Find($"[data-tile-id='{id}']").GetAttribute("style")!;
        string Var(string name) => style.Split(';').First(s => s.StartsWith($"--ib-tile-{name}{at}:", StringComparison.Ordinal)).Split(':')[1];
        return $"{Var("col")},{Var("row")},{Var("cs")},{Var("rs")}";
    }

    [Fact]
    public void Tiles_go_where_placed_or_saved_and_the_rest_fill_the_first_free_cells()
    {
        // A 4-column grid: "chart" placed at column 2, two tiles written before it with no place,
        // and a saved layout that moves "notes" and widens it.
        var cut = Render<TileLayout>(p => p
            .Add(l => l.Columns, new Responsive<int> { Base = 4 })
            .Add(l => l.State, TileLayoutState.Parse("notes:1,3,4,1;gone:1,1,1,1"))
            .Add(l => l.ChildContent, b =>
            {
                AddTile(b, "first", null, null, 1);
                AddTile(b, "wide", null, null, 2);
                AddTile(b, "chart", 2, 1, 3, 2);
                AddTile(b, "notes", 1, 1, 1);
                AddTile(b, "huge", 1, 4, 9);
            }));

        Assert.Equal("2,1,3,2", Place(cut, "chart"));
        Assert.Equal("1,3,4,1", Place(cut, "notes")); // the saved place wins over Col and Row
        Assert.Equal("1,1,1,1", Place(cut, "first")); // the free cell left of the chart
        Assert.Equal("1,4,4,1", Place(cut, "huge")); // the span kept inside the columns
        Assert.Equal("1,5,2,1", Place(cut, "wide")); // no 2 free cells side by side until row 5

        // A list of tiles, each named by its title.
        Assert.Equal("list", cut.Find(".ib-tiles").GetAttribute("role"));
        var chart = cut.Find("[data-tile-id='chart']");
        Assert.Equal("listitem", chart.GetAttribute("role"));
        Assert.Equal("chart", cut.Find("#" + chart.GetAttribute("aria-labelledby")).TextContent);
    }

    [Fact]
    public void At_fewer_columns_spans_shrink_and_the_tiles_wrap_in_reading_order()
    {
        // Written for 12 columns; at 6 (from Md) and 1 (the base) they're worked out from it.
        var cut = Render<TileLayout>(p => p
            .Add(l => l.Columns, new Responsive<int> { Base = 1, Md = 6, Lg = 12 })
            .Add(l => l.Rows, 6)
            .Add(l => l.ChildContent, b =>
            {
                AddTile(b, "chart", 1, 2, 8, 2);
                AddTile(b, "revenue", 1, 1, 4);
                AddTile(b, "orders", 5, 1, 4);
                AddTile(b, "returns", 9, 1, 4);
                AddTile(b, "products", 9, 2, 4, 2);
            }));

        Assert.Equal("1,2,8,2", Place(cut, "chart", "-lg"));
        Assert.Equal("1,1,2,1", Place(cut, "revenue", "-md"));
        Assert.Equal("5,1,2,1", Place(cut, "returns", "-md"));
        Assert.Equal("1,2,4,2", Place(cut, "chart", "-md"));
        Assert.Equal("5,2,2,2", Place(cut, "products", "-md"));
        Assert.Equal("1,4,1,2", Place(cut, "chart")); // one column: stacked in reading order
        Assert.Equal("1,6,1,2", Place(cut, "products"));
        Assert.Contains("ib-tiles-at-md", cut.Find(".ib-tiles").ClassName);
        Assert.Contains("ib-tiles-rows-at-lg", cut.Find(".ib-tiles").ClassName); // all 6 rows, at 12 columns only
    }

    [Fact]
    public void A_layout_round_trips_through_its_string_and_skips_entries_that_dont_read()
    {
        var state = new TileLayoutState([new("revenue;q4", 1, 1, 8, 2), new("orders", 9, 1, 4, 1)]);

        var text = state.ToString();
        Assert.Equal("revenue%3Bq4:1,1,8,2;orders:9,1,4,1", text);
        Assert.Equal(state.Tiles, TileLayoutState.Parse(text)!.Tiles);
        Assert.Equal([new TileState("orders", 9, 1, 4, 1)], TileLayoutState.Parse("old:1,1;orders:9,1,4,1;bad:0,1,1,1")!.Tiles);
    }
}
