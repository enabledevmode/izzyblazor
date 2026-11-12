using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;
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

    // Two tiles side by side on a 4-column, 2-row board, editable; Block unless given.
    private IRenderedComponent<TileLayout> RenderEditable(Action<ComponentParameterCollectionBuilder<TileLayout>> more, int rows = 2, TileCollision collision = TileCollision.Block) =>
        Render<TileLayout>(p =>
        {
            p.Add(l => l.Columns, new Responsive<int> { Base = 4 })
                .Add(l => l.Rows, rows)
                .Add(l => l.Collision, collision)
                .Add(l => l.Editable, true)
                .Add(l => l.ChildContent, b =>
                {
                    AddTile(b, "a", 1, 1, 2);
                    AddTile(b, "b", 3, 1, 2);
                });
            more(p);
        });

    private static IElement MenuItem(IRenderedComponent<TileLayout> cut, string tile, string text) =>
        cut.Find($"[data-tile-id='{tile}']").QuerySelectorAll("button.ib-menu-item").Single(b => b.TextContent.Contains(text));

    [Fact]
    public void A_static_post_shows_the_move_posts_the_new_layout_and_offers_only_moves_that_fit()
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var cut = RenderEditable(p => p
            .Add(l => l.Name, "layout")
            .Add(l => l.Post, new TileLayoutPost { Tiles = "a:1,1,2,1;b:3,1,2,1", Move = "down:a" }));

        Assert.Equal("1,2,2,1", Place(cut, "a"));
        var posted = cut.Find("input[type=hidden]");
        Assert.Equal("layout.Tiles", posted.GetAttribute("name"));
        Assert.Equal("b:3,1,2,1;a:1,2,2,1", posted.GetAttribute("value"));
        Assert.Equal("Move a", Assert.Single(cut.FindAll(".ib-tile-handle[autofocus]")).GetAttribute("aria-label"));
        Assert.Equal("Moved a to column 1, row 2", cut.Find("[data-tile-id='a'] [role=status]").TextContent);

        var up = MenuItem(cut, "a", "Move up");
        Assert.Equal(("submit", "layout.Move", "up:a"), (up.GetAttribute("type"), up.GetAttribute("name"), up.GetAttribute("value")));
        Assert.True(MenuItem(cut, "a", "Move down").HasAttribute("disabled")); // past Rows
        Assert.True(MenuItem(cut, "b", "Move right").HasAttribute("disabled")); // past the columns
        Assert.False(MenuItem(cut, "b", "Move left").HasAttribute("disabled")); // a moved out of the way
        Assert.True(MenuItem(cut, "b", "Shorter").HasAttribute("disabled")); // already one row
    }

    [Fact]
    public void A_live_move_happens_in_place_and_raises_StateChanged()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        TileLayoutState? changed = null;
        var cut = RenderEditable(p => p.Add(l => l.StateChanged, s => changed = s));

        Assert.Empty(cut.FindAll("input"));
        Assert.True(MenuItem(cut, "a", "Wider").HasAttribute("disabled")); // b is in the way
        MenuItem(cut, "a", "Move down").Click();

        Assert.Equal("b:3,1,2,1;a:1,2,2,1", changed!.ToString());
        Assert.Equal("1,2,2,1", Place(cut, "a"));
        Assert.Equal("Moved a to column 1, row 2", cut.Find("[data-tile-id='a'] [role=status]").TextContent);
        Assert.False(MenuItem(cut, "a", "Wider").HasAttribute("disabled"));
    }

    [Fact]
    public void By_default_a_move_pushes_the_tile_in_the_way_and_moves_that_change_nothing_are_off()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        TileLayoutState? changed = null;
        var cut = RenderEditable(p => p.Add(l => l.StateChanged, s => changed = s), collision: TileCollision.Push);

        // Down onto empty cells would rise straight back.
        Assert.True(MenuItem(cut, "a", "Move down").HasAttribute("disabled"));
        MenuItem(cut, "a", "Wider").Click();

        Assert.Equal("a:1,1,3,1;b:3,2,2,1", changed!.ToString()); // b can't take a's cells, so it moves down
        Assert.Equal("3,2,2,1", Place(cut, "b"));
    }

    // Tiles on a 4-column grid, by id: column, row, column span, row span.
    private static List<(string Key, TilePlace Place)> Tiles(params (string Id, int Col, int Row, int ColSpan, int RowSpan)[] tiles) =>
        [.. tiles.Select(t => (t.Id, new TilePlace(t.Col, t.Row, t.ColSpan, t.RowSpan)))];

    private static string Text(List<(string Key, TilePlace Place)>? layout) =>
        layout is null ? "none" : string.Join(";", layout.OrderBy(p => p.Key).Select(p => $"{p.Key}:{p.Place.Col},{p.Place.Row},{p.Place.ColSpan},{p.Place.RowSpan}"));

    [Fact]
    public void A_tile_in_the_way_swaps_into_the_old_cells_or_moves_down_pushing_the_tiles_below()
    {
        // Two tiles of a size swap.
        var row = Tiles(("a", 1, 1, 2, 1), ("b", 3, 1, 2, 1));
        Assert.Equal("a:3,1,2,1;b:1,1,2,1", Text(TileArrange.Arrange(row, "a", new TilePlace(3, 1, 2, 1), 4, 200, compact: true)));

        // b is too wide for a's cells: it moves below a, and pushes c below itself.
        var stack = Tiles(("a", 1, 1, 1, 1), ("b", 2, 1, 3, 1), ("c", 2, 2, 3, 1));
        Assert.Equal("a:2,1,1,1;b:2,2,3,1;c:2,3,3,1", Text(TileArrange.Arrange(stack, "a", new TilePlace(2, 1, 1, 1), 4, 200, compact: true)));
        Assert.Equal("none", Text(TileArrange.Arrange(stack, "a", new TilePlace(2, 1, 1, 1), 4, 2, compact: true))); // c past 2 rows
    }

    [Fact]
    public void After_a_move_tiles_rise_into_the_space_above_unless_they_float()
    {
        // a gets a row shorter: b rises into the freed row, or stays put when floating.
        var tall = Tiles(("a", 1, 1, 2, 2), ("b", 1, 3, 2, 1));
        Assert.Equal("a:1,1,2,1;b:1,2,2,1", Text(TileArrange.Arrange(tall, "a", new TilePlace(1, 1, 2, 1), 4, 200, compact: true)));
        Assert.Equal("a:1,1,2,1;b:1,3,2,1", Text(TileArrange.Arrange(tall, "a", new TilePlace(1, 1, 2, 1), 4, 200, compact: false)));
    }

    [Fact]
    public void A_live_drag_reported_by_the_script_applies_and_renders_its_seq()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        TileLayoutState? changed = null;
        var cut = RenderEditable(p => p.Add(l => l.StateChanged, s => changed = s));

        // The script dropped "b" a row down, one column wider; it holds the tile there until seq 1 renders.
        cut.Find("ib-tile-layout").TriggerEvent("onibtiles", new IzzyTilesEventArgs { Tiles = "a:1,1,2,1;b:2,2,3,1", Id = "b", Seq = 1 });

        Assert.Equal("a:1,1,2,1;b:2,2,3,1", changed!.ToString());
        Assert.Equal("1", cut.Find("ib-tile-layout").GetAttribute("data-ib-seq"));
        Assert.Equal("2,2,3,1", Place(cut, "b"));
        Assert.Equal("Resized b to 3 by 1 cells", cut.Find("[data-tile-id='b'] [role=status]").TextContent);
    }

    [Fact]
    public void A_posted_layout_cant_overlap_tiles_or_run_past_the_most_rows()
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        // A tile on the cells another tile was saved to, through a posted row span far past any real one.
        var cut = RenderEditable(p => p
            .Add(l => l.Name, "layout")
            .Add(l => l.Post, new TileLayoutPost { Tiles = "a:3,1,2,1;b:3,1,2,9999" }), rows: 0);

        Assert.Equal("3,1,2,1", Place(cut, "a"));
        // b's entry runs past the most rows, so it isn't read; b's own place is a's cells, so it
        // takes the first free ones rather than cover a.
        Assert.Equal("1,1,2,1", Place(cut, "b"));
        Assert.False(MenuItem(cut, "b", "Move down").HasAttribute("disabled")); // apart, both can move
        Assert.Null(TileLayoutState.Parse("a:1,1,1,200000000")); // past the most rows: not a layout
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
