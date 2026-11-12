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

    private static string Place(IRenderedComponent<TileLayout> cut, string id)
    {
        var style = cut.Find($"[data-tile-id='{id}']").GetAttribute("style")!;
        string Var(string name) => style.Split(';').First(s => s.StartsWith($"--ib-tile-{name}:", StringComparison.Ordinal)).Split(':')[1];
        return $"{Var("col")},{Var("row")},{Var("cs")},{Var("rs")} #{Var("order")}";
    }

    [Fact]
    public void Tiles_go_where_placed_or_saved_and_the_rest_fill_the_first_free_cells()
    {
        // A 4-column grid: "chart" placed at column 2, two tiles written before it with no place,
        // and a saved layout that moves "notes" and widens it.
        var cut = Render<TileLayout>(p => p
            .Add(l => l.Columns, 4)
            .Add(l => l.State, TileLayoutState.Parse("notes:1,3,4,1;gone:1,1,1,1"))
            .Add(l => l.ChildContent, b =>
            {
                AddTile(b, "first", null, null, 1);
                AddTile(b, "wide", null, null, 2);
                AddTile(b, "chart", 2, 1, 3, 2);
                AddTile(b, "notes", 1, 1, 1);
                AddTile(b, "huge", 1, 4, 9);
            }));

        Assert.Equal("2,1,3,2 #1", Place(cut, "chart"));
        Assert.Equal("1,3,4,1 #2", Place(cut, "notes")); // the saved place wins over Col and Row
        Assert.Equal("1,1,1,1 #0", Place(cut, "first")); // the free cell left of the chart
        Assert.Equal("1,4,4,1 #3", Place(cut, "huge")); // the span kept inside the columns
        Assert.Equal("1,5,2,1 #4", Place(cut, "wide")); // no 2 free cells side by side until row 5

        // A list of tiles, each named by its title.
        Assert.Equal("list", cut.Find(".ib-tiles").GetAttribute("role"));
        var chart = cut.Find("[data-tile-id='chart']");
        Assert.Equal("listitem", chart.GetAttribute("role"));
        Assert.Equal("chart", cut.Find("#" + chart.GetAttribute("aria-labelledby")).TextContent);
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
