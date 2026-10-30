using System.Linq.Expressions;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

// DataGrid on Table's base: widths set by the server, and a sort of several columns.
public class DataGridRenderTests : BunitContext
{
    public sealed record Row(string Name, int Qty);

    private static readonly Row[] Rows = [new("b", 2), new("a", 3), new("a", 1)];

    private readonly Cookies _cookies = new();

    public DataGridRenderTests()
    {
        Services.AddSingleton<IRequestCookies>(_cookies);
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
    }

    private BunitNavigationManager Nav => Services.GetRequiredService<BunitNavigationManager>();

    private static RenderFragment Column<T>(Expression<Func<Row, T>> property, Length? width = null, Length? min = null, ColumnFrozen frozen = ColumnFrozen.None) => builder =>
    {
        builder.OpenComponent<PropertyColumn<Row, T>>(0);
        builder.AddComponentParameter(1, "Property", property);
        builder.AddComponentParameter(2, "Sortable", true);
        builder.AddComponentParameter(3, "Width", width);
        builder.AddComponentParameter(4, "MinWidth", min);
        builder.AddComponentParameter(5, "Frozen", frozen);
        builder.CloseComponent();
    };

    private static RenderFragment NameAndQty => builder =>
    {
        builder.AddContent(0, Column(r => r.Name));
        builder.AddContent(1, Column(r => r.Qty));
    };

    private IRenderedComponent<DataGrid<Row>> Grid(RenderFragment columns, Action<ComponentParameterCollectionBuilder<DataGrid<Row>>>? more = null) =>
        Render<DataGrid<Row>>(p =>
        {
            p.Add(x => x.Items, Rows);
            p.Add(x => x.ChildContent, columns);
            more?.Invoke(p);
        });

    private static string[] Cells(IRenderedComponent<DataGrid<Row>> cut) =>
        cut.FindAll("tbody tr").Select(r => $"{r.Children[0].TextContent}{r.Children[1].TextContent}").ToArray();

    [Fact]
    public void Widths_are_variables_and_unset_columns_set_the_minimum()
    {
        var cut = Grid(builder =>
        {
            builder.AddContent(0, Column(r => r.Name, min: Length.Rem(12)));
            builder.AddContent(1, Column(r => r.Qty, width: Length.Rem(8)));
        });

        var style = cut.Find("table").GetAttribute("style");
        Assert.Contains("--ib-col-1:8rem", style);
        Assert.Contains("min-width:calc(var(--ib-col-1) + 1 * max(12rem))", style);
        Assert.Equal(2, cut.FindAll("col").Count);
        Assert.Null(cut.FindAll("col")[0].GetAttribute("style"));
        Assert.Equal("width:var(--ib-col-1)", cut.FindAll("col")[1].GetAttribute("style"));
        Assert.All(cut.FindAll("th"), th => Assert.Null(th.GetAttribute("style")));
    }

    [Fact]
    public void Every_column_set_makes_the_grid_their_sum()
    {
        var cut = Grid(builder =>
        {
            builder.AddContent(0, Column(r => r.Name, width: Length.Rem(8)));
            builder.AddContent(1, Column(r => r.Qty, width: Length.Rem(4)));
        });

        Assert.Contains(";width:calc(var(--ib-col-0) + var(--ib-col-1))", cut.Find("table").GetAttribute("style"));
    }

    [Fact]
    public void Mixed_minimums_take_the_largest_and_selection_counts()
    {
        var cut = Grid(builder =>
        {
            builder.AddContent(0, Column(r => r.Name, min: Length.Rem(12)));
            builder.AddContent(1, Column(r => r.Qty));
        }, p => p.Add(x => x.SelectionMode, SelectionMode.Multiple));

        Assert.Contains("min-width:calc(var(--ib-datagrid-select) + 2 * max(12rem, 8rem))", cut.Find("table").GetAttribute("style"));
        Assert.Equal(3, cut.FindAll("col").Count);
        Assert.Equal("ib-datagrid-select-col", cut.FindAll("col")[0].ClassName);
    }

    [Fact]
    public void Frozen_columns_are_placed_by_the_widths_before_them()
    {
        var cut = Grid(builder =>
        {
            builder.AddContent(0, Column(r => r.Name, Length.Rem(8), frozen: ColumnFrozen.Start));
            builder.AddContent(1, Column(r => r.Qty, Length.Rem(4), frozen: ColumnFrozen.Start));
            builder.AddContent(2, Column(r => r.Name.Length));
            builder.AddContent(3, Column(r => r.Qty + 1, Length.Rem(5), frozen: ColumnFrozen.End));
            builder.AddContent(4, Column(r => r.Qty + 2, Length.Rem(6), frozen: ColumnFrozen.End));
        }, p => p.Add(x => x.SelectionMode, SelectionMode.Multiple));

        var cells = cut.Find("tbody tr").Children;
        Assert.Contains("ib-frozen-start", cells[0].ClassName);
        Assert.Null(cells[0].GetAttribute("style"));
        Assert.Equal("--ib-frozen-at:calc(var(--ib-datagrid-select))", cells[1].GetAttribute("style"));
        Assert.DoesNotContain("ib-frozen-edge", cells[1].ClassName);
        Assert.Equal("--ib-frozen-at:calc(var(--ib-datagrid-select) + var(--ib-col-0))", cells[2].GetAttribute("style"));
        Assert.Contains("ib-frozen-edge", cells[2].ClassName);
        Assert.DoesNotContain("ib-frozen", cells[3].ClassName ?? "");
        Assert.Equal("--ib-frozen-at:calc(var(--ib-col-4))", cells[4].GetAttribute("style"));
        Assert.Contains("ib-frozen-edge", cells[4].ClassName);
        Assert.Null(cells[5].GetAttribute("style"));

        var head = cut.Find("thead tr").Children;
        Assert.Equal(cells[2].GetAttribute("style"), head[2].GetAttribute("style"));
    }

    [Fact]
    public void Frozen_needs_a_width_and_its_place_and_a_grid()
    {
        Assert.Contains("Width", Assert.Throws<InvalidOperationException>(() => Grid(Column(r => r.Name, frozen: ColumnFrozen.Start))).Message);

        // Declared after a column that scrolls, it's still drawn at its edge.
        var cut = Grid(builder =>
        {
            builder.AddContent(0, Column(r => r.Name));
            builder.AddContent(1, Column(r => r.Qty, Length.Rem(4), frozen: ColumnFrozen.Start));
        });
        Assert.Contains("ib-frozen-start", cut.Find("tbody tr").Children[0].ClassName);
        Assert.Contains("--ib-col-0:4rem", cut.Find("table").GetAttribute("style"));

        Assert.Contains("RowHref", Assert.Throws<InvalidOperationException>(() => Grid(Column(r => r.Name, Length.Rem(4), frozen: ColumnFrozen.Start), p => p.Add(x => x.RowHref, r => "/x"))).Message);

        Assert.Contains("Frozen", Assert.Throws<InvalidOperationException>(() => Render<Table<Row>>(p => p
            .Add(x => x.Items, Rows)
            .Add(x => x.ChildContent, Column(r => r.Name, Length.Rem(4), frozen: ColumnFrozen.Start)))).Message);
    }

    private sealed class Cookies : IRequestCookies
    {
        public string? Value { get; set; }

        public string? Name { get; private set; }

        public string? Get(string name)
        {
            Name = name;
            return Value;
        }
    }

    private static string[] Headers(IRenderedComponent<DataGrid<Row>> cut) =>
        cut.FindAll("thead th .ib-table-title").Select(t => t.TextContent.Trim()).ToArray();

    private static DataGridLayout Arranged(params DataGridColumnLayout[] columns) => new() { Columns = columns };

    [Fact]
    public void A_layout_sets_widths_order_hidden_and_frozen()
    {
        var cut = Grid(NameAndQty, p => p.Add(x => x.Layout, Arranged(
            new DataGridColumnLayout { Key = "qty", Position = 0, Width = "120px", Frozen = ColumnFrozen.End },
            new() { Key = "name", Position = 1 })));
        Assert.Equal(["Name", "Qty"], Headers(cut));
        Assert.Contains("--ib-col-1:120px", cut.Find("table").GetAttribute("style"));
        Assert.Contains("ib-frozen-end", cut.FindAll("thead th")[1].ClassName);

        cut = Grid(NameAndQty, p => p.Add(x => x.Layout, Arranged(new DataGridColumnLayout { Key = "qty", Position = 0 }, new() { Key = "name", Position = 1 })));
        Assert.Equal(["Qty", "Name"], Headers(cut));

        cut = Grid(NameAndQty, p => p.Add(x => x.Layout, Arranged(new DataGridColumnLayout { Key = "qty", Hidden = true })));
        Assert.Equal(["Name"], Headers(cut));
        Assert.Single(cut.FindAll("tbody tr")[0].Children);
    }

    [Fact]
    public void A_layout_that_hides_every_column_or_freezes_one_with_no_width_is_ignored_for_that()
    {
        var cut = Grid(NameAndQty, p => p.Add(x => x.Layout, Arranged(new DataGridColumnLayout { Key = "name", Hidden = true }, new() { Key = "qty", Hidden = true })));
        Assert.Equal(2, Headers(cut).Length);

        cut = Grid(NameAndQty, p => p.Add(x => x.Layout, Arranged(new DataGridColumnLayout { Key = "name", Frozen = ColumnFrozen.Start })));
        Assert.DoesNotContain("ib-frozen", cut.Find("thead th").ClassName ?? "");
    }

    [Fact]
    public void Unknown_columns_in_a_layout_are_dropped_and_new_columns_stay_in_their_place()
    {
        var cut = Grid(NameAndQty, p => p.Add(x => x.Layout, Arranged(new DataGridColumnLayout { Key = "gone", Position = 0, Width = "99px" })));
        Assert.Equal(["Name", "Qty"], Headers(cut));
        Assert.DoesNotContain("99px", cut.Find("table").GetAttribute("style") ?? "");
    }

    [Fact]
    public void The_cookie_is_read_on_the_servers_first_render()
    {
        _cookies.Value = Arranged(new DataGridColumnLayout { Key = "qty", Hidden = true }).ToJson();
        var cut = Grid(NameAndQty, p => p.Add(x => x.LayoutKey, "orders"));
        Assert.Equal("ib-grid-orders", _cookies.Name);
        Assert.Equal(["Name"], Headers(cut));
    }

    [Fact]
    public void No_key_reads_no_cookie_and_a_bound_layout_wins()
    {
        _cookies.Value = Arranged(new DataGridColumnLayout { Key = "qty", Hidden = true }).ToJson();
        Assert.Equal(2, Headers(Grid(NameAndQty)).Length);
        Assert.Null(_cookies.Name);

        var cut = Grid(NameAndQty, p => p.Add(x => x.LayoutKey, "orders").Add(x => x.Layout, new DataGridLayout()));
        Assert.Equal(2, Headers(cut).Length);
    }

    [Fact]
    public void A_key_that_cant_name_a_cookie_is_refused() =>
        Assert.Contains("LayoutKey", Assert.Throws<InvalidOperationException>(() => Grid(NameAndQty, p => p.Add(x => x.LayoutKey, "a;b"))).Message);

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("""{"c":null}""")]
    [InlineData("""{"c":[null]}""")]
    [InlineData("""{"x":1}""")]
    [InlineData("""{"c":[{"w":"10px"}]}""")]
    public void A_broken_cookie_is_no_layout(string json) => Assert.Empty(DataGridLayout.Parse(json)?.Columns ?? []);

    [Theory]
    [InlineData("١٨٠px")]
    [InlineData("10px\n")]
    public void A_width_in_other_digits_or_with_more_after_it_is_dropped(string width) =>
        Assert.Null(DataGridLayout.Parse(Arranged(new DataGridColumnLayout { Key = "a", Width = width }).ToJson())!.Column("a")!.Width);

    [Fact]
    public void A_stored_layout_cant_freeze_a_grid_with_row_links()
    {
        var cut = Grid(Column(r => r.Name, Length.Rem(4)), p => p
            .Add(x => x.RowHref, r => "/x")
            .Add(x => x.Layout, Arranged(new DataGridColumnLayout { Key = "name", Frozen = ColumnFrozen.Start })));
        Assert.DoesNotContain("ib-frozen", cut.Find("thead th").ClassName ?? "");
    }

    [Fact]
    public void Rearranging_needs_a_key_of_its_own_per_column()
    {
        RenderFragment twins = builder =>
        {
            builder.AddContent(0, Column(r => r.Name));
            builder.AddContent(1, Column(r => r.Name));
        };
        Assert.Contains("SortKey", Assert.Throws<InvalidOperationException>(() => Grid(twins, p => p.Add(x => x.ResizableColumns, true).Add(x => x.LayoutKey, "o"))).Message);
    }

    [Fact]
    public void A_layout_given_one_way_gives_way_to_what_people_choose()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var cut = Grid(NameAndQty, p => p.Add(x => x.ResizableColumns, true).Add(x => x.Layout, new DataGridLayout()));
        cut.Find("ib-grid-columns").TriggerEvent("oniblayout", new IzzyLayoutEventArgs { Layout = """{"c":[{"k":"qty","h":true}]}""" });
        Assert.Equal(["Name"], Headers(cut));
    }

    [Fact]
    public void A_column_the_page_wont_let_people_hide_stays()
    {
        RenderFragment fixedColumn = builder =>
        {
            builder.OpenComponent<PropertyColumn<Row, string>>(0);
            builder.AddComponentParameter(1, "Property", (Expression<Func<Row, string>>)(r => r.Name));
            builder.AddComponentParameter(2, "Pickable", false);
            builder.CloseComponent();
            builder.AddContent(3, Column(r => r.Qty));
        };
        var cut = Grid(fixedColumn, p => p.Add(x => x.Layout, Arranged(new DataGridColumnLayout { Key = "name", Hidden = true })));
        Assert.Equal(["Name", "Qty"], Headers(cut));
    }

    [Theory]
    [InlineData("1px;background:url(x)")]
    [InlineData("calc(1px)")]
    [InlineData("10%")]
    [InlineData("0px")]
    [InlineData("-5px")]
    public void A_cookies_width_that_isnt_plain_is_dropped(string width)
    {
        var layout = DataGridLayout.Parse(Arranged(new DataGridColumnLayout { Key = "a", Width = width }).ToJson());
        Assert.Null(layout!.Column("a")!.Width);
    }

    [Fact]
    public void A_layout_round_trips_and_only_says_what_differs()
    {
        var json = Arranged(new DataGridColumnLayout { Key = "a", Width = "12.5rem", Position = 2, Hidden = true, Frozen = ColumnFrozen.Start }, new DataGridColumnLayout { Key = "b" }).ToJson();
        Assert.Equal("""{"c":[{"k":"a","w":"12.5rem","p":2,"h":true,"f":1},{"k":"b"}]}""", json);
        var back = DataGridLayout.Parse(json)!;
        Assert.Equal("12.5rem", back.Column("A")!.Width);
        Assert.Equal(ColumnFrozen.Start, back.Column("a")!.Frozen);
        Assert.Null(back.Column("b")!.Width);
    }

    [Fact]
    public void Rearranging_needs_somewhere_to_keep_it_on_a_static_page()
    {
        var cut = Grid(NameAndQty, p => p.Add(x => x.ResizableColumns, true).Add(x => x.ReorderableColumns, true).Add(x => x.ColumnPicker, true));
        Assert.Empty(cut.FindAll("ib-grid-columns"));
        Assert.Empty(cut.FindAll(".ib-col-resize"));
        Assert.Empty(cut.FindAll(".ib-col-menu"));

        cut = Grid(NameAndQty, p => p.Add(x => x.ResizableColumns, true).Add(x => x.ReorderableColumns, true).Add(x => x.ColumnPicker, true).Add(x => x.LayoutKey, "orders"));
        Assert.Equal("orders", cut.Find("ib-grid-columns").GetAttribute("data-ib-layout-key"));
        Assert.Equal(2, cut.FindAll("th .ib-col-resize").Count);
        Assert.Equal("name", cut.Find("th").GetAttribute("data-ib-col"));
        Assert.NotNull(cut.Find("th").GetAttribute("data-ib-reorder"));
        Assert.Equal(2, cut.FindAll("[data-ib-col-show]").Count);
    }

    [Fact]
    public void A_column_can_opt_out_of_each()
    {
        RenderFragment fixedColumn = builder =>
        {
            builder.OpenComponent<PropertyColumn<Row, string>>(0);
            builder.AddComponentParameter(1, "Property", (Expression<Func<Row, string>>)(r => r.Name));
            builder.AddComponentParameter(2, "Resizable", false);
            builder.AddComponentParameter(3, "Reorderable", false);
            builder.AddComponentParameter(4, "Pickable", false);
            builder.CloseComponent();
            builder.AddContent(5, Column(r => r.Qty));
        };
        var cut = Grid(fixedColumn, p => p.Add(x => x.ResizableColumns, true).Add(x => x.ReorderableColumns, true).Add(x => x.ColumnPicker, true).Add(x => x.LayoutKey, "o"));
        var head = cut.FindAll("th");
        Assert.Null(head[0].GetAttribute("data-ib-reorder"));
        Assert.Empty(head[0].QuerySelectorAll(".ib-col-resize, [data-ib-menu]"));
        Assert.NotEmpty(head[1].QuerySelectorAll(".ib-col-resize"));
        Assert.Single(cut.FindAll("[data-ib-col-show]"));
    }

    [Fact]
    public void Layout_events_redraw_and_tell_a_bound_parent()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        DataGridLayout? told = null;
        var cut = Grid(NameAndQty, p => p.Add(x => x.ResizableColumns, true).Add(x => x.LayoutChanged, (DataGridLayout? l) => told = l));
        cut.Find("ib-grid-columns").TriggerEvent("oniblayout", new IzzyLayoutEventArgs { Layout = """{"c":[{"k":"qty","h":true}]}""" });
        Assert.Equal(["Name"], Headers(cut));
        Assert.True(told!.Column("qty")!.Hidden);
    }

    [Fact]
    public void Searching_is_in_the_address_and_keeps_the_rest_of_it()
    {
        Nav.NavigateTo("http://localhost/?search=b&sort=name&page=1");
        var cut = Grid(NameAndQty, p => p.Add(x => x.Searchable, true));
        Assert.Equal(["b2"], Cells(cut));
        var box = cut.Find(".ib-grid-toolbar input[type=search]");
        Assert.Equal("b", box.GetAttribute("value"));
        Assert.Equal("search", box.GetAttribute("name"));
        Assert.Equal(box.GetAttribute("form"), cut.Find("form.ib-table-filter-form").Id);
        var kept = cut.FindAll(".ib-table-filter-kept input").Select(i => i.GetAttribute("name")).ToArray();
        Assert.Contains("sort", kept);
        Assert.DoesNotContain("search", kept);
        Assert.DoesNotContain("page", kept);
    }

    [Fact]
    public void A_bound_search_is_in_the_component()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        string? told = null;
        var cut = Grid(NameAndQty, p => p.Add(x => x.Searchable, true).Add(x => x.Search, "a").Add(x => x.SearchChanged, (string? s) => told = s));
        Assert.Equal(2, cut.FindAll("tbody tr").Count);
        var box = cut.Find(".ib-grid-toolbar input[type=search]");
        Assert.Null(box.GetAttribute("name"));
        box.Input("b");
        cut.WaitForAssertion(() => Assert.Equal("b", told), TimeSpan.FromSeconds(5));
        cut.WaitForAssertion(() => Assert.Equal(["b2"], Cells(cut)), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Typing_doesnt_redraw_the_box_and_an_echo_doesnt_overwrite_it()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var cut = Grid(NameAndQty, p => p.Add(x => x.Searchable, true).Add(x => x.Search, "a").Add(x => x.SearchChanged, (string? _) => { }));
        Assert.Equal("a", cut.Find("input[type=search]").GetAttribute("value"));
        cut.Find("input[type=search]").Input("ab");
        Assert.Equal("a", cut.Find("input[type=search]").GetAttribute("value"));

        // C# setting something else does redraw it.
        cut.Render(p => p.Add(x => x.Search, "b"));
        Assert.Equal("b", cut.Find("input[type=search]").GetAttribute("value"));
    }

    [Fact]
    public void A_bound_search_works_with_a_box_of_the_pages_own()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var cut = Grid(NameAndQty, p => p.Add(x => x.Search, "b").Add(x => x.SearchChanged, (string? _) => { }));
        Assert.Empty(cut.FindAll("input[type=search]"));
        Assert.Equal(["b2"], Cells(cut));
    }

    [Fact]
    public void Row_menus_are_a_last_column_and_only_rows_with_items_have_a_button()
    {
        RenderFragment<Row> menu = row => builder =>
        {
            if (row.Name == "b")
            {
                builder.AddMarkupContent(0, "<span>Item</span>");
            }
        };
        var cut = Grid(NameAndQty, p => p.Add(x => x.RowMenu, menu));
        Assert.Equal(3, cut.FindAll("thead th").Count);
        Assert.Equal(3, cut.FindAll("col").Count);
        Assert.Contains("var(--ib-datagrid-menu)", cut.Find("table").GetAttribute("style"));
        Assert.Single(cut.FindAll("[data-ib-row-menu]"));
        Assert.Equal(3, cut.FindAll("tbody tr")[0].Children.Length);
        Assert.Equal(3, cut.FindAll("tbody tr")[1].Children.Length);
    }

    [Fact]
    public void With_frozen_end_columns_the_menu_column_stays_with_them()
    {
        var cut = Grid(Column(r => r.Qty, Length.Rem(4), frozen: ColumnFrozen.End), p => p.Add(x => x.RowMenu, _ => b => b.AddMarkupContent(0, "<span>Item</span>")));
        var cells = cut.Find("tbody tr").Children;
        Assert.Equal("--ib-frozen-at:calc(var(--ib-datagrid-menu))", cells[0].GetAttribute("style"));
        Assert.Contains("ib-frozen-end", cells[1].ClassName);
    }

    [Fact]
    public void A_row_with_no_name_of_its_own_has_a_short_menu_name()
    {
        var cut = Grid(NameAndQty, p => p.Add(x => x.RowMenu, _ => b => b.AddMarkupContent(0, "<span>Item</span>")));
        Assert.Equal("Actions", cut.Find("[data-ib-row-menu]").GetAttribute("aria-label"));
        cut = Grid(NameAndQty, p => p.Add(x => x.ItemKey, r => r.Name).Add(x => x.RowMenu, _ => b => b.AddMarkupContent(0, "<span>Item</span>")));
        Assert.Equal("Actions for b", cut.Find("[data-ib-row-menu]").GetAttribute("aria-label"));
    }

    [Fact]
    public void Export_links_carry_the_sort_and_the_columns_shown_and_no_page()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("?sort=-qty&page=2");
        var cut = Grid(NameAndQty, p => p.Add(x => x.ExportCsvHref, "/o.csv").Add(x => x.ExportExcelHref, "/o.xlsx?t=1").Add(x => x.PageSize, 1));
        // The bar's menu of links, and the toolbar's "…" menu has copies, which download too.
        var links = cut.FindAll(".ib-menu-item[download]");
        Assert.Equal(4, links.Count);
        Assert.Equal("/o.csv?sort=-qty&columns=name,qty", links[0].GetAttribute("href"));
        Assert.Equal("/o.xlsx?t=1&sort=-qty&columns=name,qty", links[1].GetAttribute("href"));
        Assert.Equal(["Export CSV", "Export Excel"], links.Take(2).Select(l => l.TextContent.Trim()));
    }

    [Fact]
    public void Export_links_leave_out_a_title_used_as_a_key_and_a_fragment()
    {
        var titled = new RenderFragment(b =>
        {
            b.OpenComponent<TemplateColumn<Row>>(0);
            b.AddComponentParameter(1, nameof(TemplateColumn<Row>.Title), "Name, note");
            b.CloseComponent();
            b.OpenComponent<PropertyColumn<Row, int>>(2);
            b.AddComponentParameter(3, nameof(PropertyColumn<Row, int>.Property), (System.Linq.Expressions.Expression<Func<Row, int>>)(r => r.Qty));
            b.CloseComponent();
        });
        var cut = Grid(titled, p => p.Add(x => x.ExportCsvHref, "/o.csv#top"));
        Assert.Equal("/o.csv?columns=qty", cut.Find(".ib-btn[download]").GetAttribute("href"));
    }

    private static RenderFragment Tools(params Action<Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder>[] items) => b =>
    {
        foreach (var item in items)
        {
            item(b);
        }
    };

    private static void Add<T>(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder b, params (string Name, object Value)[] parameters) where T : IComponent
    {
        b.OpenComponent<T>(0);
        foreach (var (name, value) in parameters)
        {
            b.AddComponentParameter(1, name, value);
        }

        b.CloseComponent();
    }

    [Fact]
    public void The_grids_tools_are_icons_named_for_what_they_do_unless_told_otherwise()
    {
        var cut = Grid(NameAndQty, p => p.Add(x => x.ExportCsvHref, "/o.csv"));
        var link = cut.Find(".ib-btn[download]");
        Assert.Equal("Export CSV", link.GetAttribute("aria-label"));
        Assert.Equal("Export CSV", link.GetAttribute("title"));
        Assert.DoesNotContain("Export CSV", link.TextContent);

        link = Grid(NameAndQty, p => p.Add(x => x.ExportCsvHref, "/o.csv").Add(x => x.ToolbarLabels, true)).Find(".ib-btn[download]");
        Assert.Contains("Export CSV", link.TextContent);

        // One tool's own word goes first.
        var content = Tools(b => Add<DataGridExport>(b, (nameof(DataGridExport.ShowText), (bool?)false)));
        link = Grid(NameAndQty, p => p.Add(x => x.ExportCsvHref, "/o.csv").Add(x => x.ToolbarLabels, true).Add(x => x.ToolbarContent, content)).Find(".ib-btn[download]");
        Assert.DoesNotContain("Export CSV", link.TextContent);
    }

    [Fact]
    public void A_tool_written_in_the_content_is_drawn_there_and_not_again()
    {
        var content = Tools(
            b => Add<ToolbarText>(b, (nameof(ToolbarText.ChildContent), (RenderFragment)(t => t.AddContent(0, "Mine")))),
            b => Add<DataGridSearch>(b),
            b => Add<ToolbarText>(b, (nameof(ToolbarText.ChildContent), (RenderFragment)(t => t.AddContent(0, "Yours")))));
        var cut = Grid(NameAndQty, p => p.Add(x => x.Searchable, true).Add(x => x.ToolbarContent, content));
        Assert.Single(cut.FindAll("input[type=search]"));
        var units = cut.FindAll(".ib-toolbar-start > .ib-toolbar-unit");
        Assert.Equal(3, units.Count);
        Assert.Equal("Mine", units[0].TextContent.Trim());
        Assert.Single(units[1].QuerySelectorAll("input[type=search]"));
        Assert.Equal("Yours", units[2].TextContent.Trim());
    }

    [Fact]
    public void A_tool_the_grid_hasnt_turned_on_draws_nothing()
    {
        var content = Tools(b => Add<DataGridSearch>(b), b => Add<DataGridExport>(b), b => Add<DataGridColumnPicker>(b));
        var cut = Grid(NameAndQty, p => p.Add(x => x.ToolbarContent, content));
        Assert.Empty(cut.FindAll(".ib-toolbar-unit"));
    }

    [Fact]
    public void A_download_stays_one_in_the_toolbars_menu()
    {
        var content = Tools(b =>
        {
            b.OpenComponent<ToolbarIconButton>(0);
            b.AddComponentParameter(1, nameof(ToolbarIconButton.Icon), Lucide.Download);
            b.AddComponentParameter(2, nameof(ToolbarIconButton.Label), "Report");
            b.AddComponentParameter(3, nameof(ToolbarIconButton.Href), "/report.pdf");
            b.AddAttribute(4, "download", "report.pdf");
            b.CloseComponent();
        });
        var cut = Grid(NameAndQty, p => p.Add(x => x.ToolbarContent, content));
        Assert.Equal("report.pdf", cut.Find(".ib-menu-item[href='/report.pdf']").GetAttribute("download"));
    }

    [Fact]
    public void The_tools_follow_the_grids_parameters()
    {
        var content = Tools(b => Add<DataGridSearch>(b));
        var cut = Grid(NameAndQty, p => p.Add(x => x.ToolbarContent, content));
        Assert.Empty(cut.FindAll("input[type=search]"));
        Assert.Empty(cut.FindAll("[download]"));

        cut.Render(p => p.Add(x => x.Searchable, true).Add(x => x.ExportCsvHref, "/o.csv"));
        Assert.Single(cut.FindAll("input[type=search]"));
        Assert.NotEmpty(cut.FindAll("[download]"));

        // Cleared, the link goes rather than draw with nowhere to point.
        cut.Render(p => p.Add(x => x.Searchable, false).Add(x => x.ExportCsvHref, null));
        Assert.Empty(cut.FindAll("input[type=search]"));
        Assert.Empty(cut.FindAll("[download]"));
    }

    [Fact]
    public void A_tool_takes_no_attributes_it_would_only_ignore()
    {
        var content = Tools(b =>
        {
            b.OpenComponent<DataGridExport>(0);
            b.AddAttribute(1, "class", "mine");
            b.CloseComponent();
        });
        Assert.ThrowsAny<InvalidOperationException>(() => Grid(NameAndQty, p => p.Add(x => x.ExportCsvHref, "/o.csv").Add(x => x.ToolbarContent, content)));
    }

    [Fact]
    public void An_icon_link_keeps_its_name_and_look_when_it_becomes_active()
    {
        var cut = Render<IconButton>(p => p.Add(x => x.Icon, Lucide.Download).Add(x => x.Label, "Export").Add(x => x.Href, "/o.csv").Add(x => x.Disabled, true).AddUnmatched("download", ""));
        Assert.Equal("link", cut.Find("a").GetAttribute("role"));

        cut.Render(p => p.Add(x => x.Disabled, false));
        var link = cut.Find("a");
        Assert.Equal("/o.csv", link.GetAttribute("href"));
        Assert.Null(link.GetAttribute("role"));
        Assert.Equal("Export", link.GetAttribute("aria-label"));
        Assert.Equal("Export", link.GetAttribute("title"));
        Assert.True(link.HasAttribute("download"));
        Assert.Contains("ib-btn", link.ClassName);
    }

    [Fact]
    public void A_tool_outside_a_grid_says_where_it_goes()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Render<DataGridSearch>());
        Assert.Contains("DataGrid's ToolbarContent", error.Message);
    }

    [Fact]
    public void The_empty_row_spans_the_menu_column_too()
    {
        var cut = Render<DataGrid<Row>>(p => p.Add(x => x.Items, Array.Empty<Row>()).Add(x => x.ChildContent, NameAndQty).Add(x => x.RowMenu, _ => _ => { }));
        Assert.Equal("3", cut.Find("tbody td").GetAttribute("colspan"));
    }

    [Fact]
    public void No_toolbar_without_something_to_put_in_it_and_a_table_has_none()
    {
        Assert.Empty(Grid(NameAndQty).FindAll(".ib-grid-toolbar"));
        var cut = Grid(NameAndQty, p => p.Add(x => x.ToolbarContent, b => b.AddMarkupContent(0, "<span id=mine></span>")));
        Assert.Single(cut.FindAll(".ib-grid-toolbar"));
        Assert.Empty(Render<Table<Row>>(p => p.Add(x => x.Items, Rows).Add(x => x.ChildContent, NameAndQty)).FindAll(".ib-grid-toolbar"));
    }

    [Fact]
    public void A_table_refuses_a_minimum_width()
    {
        var thrown = Assert.Throws<InvalidOperationException>(() => Render<Table<Row>>(p => p
            .Add(x => x.Items, Rows)
            .Add(x => x.ChildContent, Column(r => r.Name, min: Length.Rem(12)))));
        Assert.Contains("MinWidth", thrown.Message);
    }

    [Fact]
    public void The_address_sorts_by_several_columns_and_the_first_has_aria_sort()
    {
        Nav.NavigateTo("http://localhost/?sort=name,-qty");
        var cut = Grid(NameAndQty);

        Assert.Equal(["a3", "a1", "b2"], Cells(cut));
        var headers = cut.FindAll("th");
        Assert.Equal("ascending", headers[0].GetAttribute("aria-sort"));
        Assert.Equal("none", headers[1].GetAttribute("aria-sort"));
        Assert.Contains("Sort 1, ascending", headers[0].TextContent);
        Assert.Contains("Sort 2, descending", headers[1].TextContent);
    }

    [Fact]
    public void One_sorted_column_shows_no_place()
    {
        Nav.NavigateTo("http://localhost/?sort=name");
        var cut = Grid(NameAndQty);
        Assert.Empty(cut.FindAll(".ib-table-sort-order"));
    }

    [Fact]
    public void Unknown_and_repeated_sort_keys_are_dropped()
    {
        Nav.NavigateTo("http://localhost/?sort=nope,qty,-qty");
        var cut = Grid(NameAndQty);
        Assert.Equal(["a1", "b2", "a3"], Cells(cut));
        Assert.Empty(cut.FindAll(".ib-table-sort-order"));
    }

    [Fact]
    public void A_link_replaces_the_sort_and_its_add_link_goes_on_through_the_directions()
    {
        Nav.NavigateTo("http://localhost/?sort=name");
        var cut = Grid(NameAndQty);
        var links = cut.FindAll("th a.ib-table-sort");

        Assert.Contains("sort=-name", links[0].GetAttribute("href"));
        Assert.Contains("sort=qty", links[1].GetAttribute("href"));
        Assert.Contains("sort=name,qty", Uri.UnescapeDataString(links[1].GetAttribute("data-ib-sort-add")!));
        Assert.Contains("sort=-name", links[0].GetAttribute("data-ib-sort-add"));

        Nav.NavigateTo("http://localhost/?sort=-name,qty");
        cut.Render();
        Assert.Contains("sort=qty", Uri.UnescapeDataString(cut.FindAll("th a.ib-table-sort")[0].GetAttribute("data-ib-sort-add")!));
    }

    [Fact]
    public void A_table_keeps_one_sort_and_no_add_link()
    {
        Nav.NavigateTo("http://localhost/?sort=name,-qty");
        var cut = Render<Table<Row>>(p => p.Add(x => x.Items, Rows).Add(x => x.ChildContent, NameAndQty));

        Assert.Empty(cut.FindAll("[data-ib-sort-add]"));
        Assert.All(cut.FindAll("th"), th => Assert.Equal("none", th.GetAttribute("aria-sort")));
    }

    [Fact]
    public void A_table_takes_a_comma_in_its_sort_key()
    {
        RenderFragment columns = builder =>
        {
            builder.OpenComponent<PropertyColumn<Row, string>>(0);
            builder.AddComponentParameter(1, "Property", (Expression<Func<Row, string>>)(r => r.Name));
            builder.AddComponentParameter(2, "Sortable", true);
            builder.AddComponentParameter(3, "SortKey", "a,b");
            builder.CloseComponent();
        };
        Nav.NavigateTo("http://localhost/?sort=a,b");
        var cut = Render<Table<Row>>(p => p.Add(x => x.Items, Rows).Add(x => x.ChildContent, columns));
        Assert.Equal("ascending", cut.Find("th").GetAttribute("aria-sort"));

        var thrown = Assert.Throws<InvalidOperationException>(() => Grid(columns));
        Assert.Contains("SortKey", thrown.Message);
    }

    [Theory]
    [InlineData("100%")]
    [InlineData("auto")]
    [InlineData("fit-content")]
    public void Widths_a_grid_cant_add_up_are_refused(string css)
    {
        Assert.Contains("Width", Assert.Throws<InvalidOperationException>(() => Grid(Column(r => r.Name, width: Length.Of(css)))).Message);
        Assert.Contains("MinWidth", Assert.Throws<InvalidOperationException>(() => Grid(Column(r => r.Name, min: Length.Of(css)))).Message);
    }

    [Fact]
    public void InitialSort_takes_several_and_a_lone_dash_is_no_sort()
    {
        var cut = Grid(NameAndQty, p => p.Add(x => x.InitialSort, "name,-qty"));
        Assert.Equal(["a3", "a1", "b2"], Cells(cut));

        Nav.NavigateTo("http://localhost/?sort=-");
        cut.Render();
        Assert.Equal(["b2", "a3", "a1"], Cells(cut));
        Assert.Empty(cut.FindAll("[aria-sort=ascending], [aria-sort=descending]"));
    }

    [Fact]
    public void Shift_past_descending_on_the_initial_sort_turns_it_off()
    {
        var cut = Grid(NameAndQty, p => p.Add(x => x.InitialSort, "-name"));
        Assert.Contains("sort=-&", Uri.UnescapeDataString(cut.FindAll("th a.ib-table-sort")[0].GetAttribute("data-ib-sort-add")!) + "&");
    }

    [Fact]
    public void A_removed_column_leaves_the_sort()
    {
        Nav.NavigateTo("http://localhost/?sort=name,qty");
        var show = true;
        var cut = Render<DataGrid<Row>>(p => p.Add(x => x.Items, Rows).Add(x => x.ChildContent, builder =>
        {
            if (show)
            {
                builder.AddContent(0, Column(r => r.Name));
            }

            builder.AddContent(1, Column(r => r.Qty));
        }));
        Assert.Equal(2, cut.FindAll(".ib-table-sort-order").Count);

        show = false;
        cut.Render();
        Assert.Empty(cut.FindAll(".ib-table-sort-order"));
        Assert.Equal("ascending", cut.Find("th").GetAttribute("aria-sort"));
    }

    [Fact]
    public void With_no_columns_yet_a_message_takes_the_whole_width()
    {
        var cut = Grid(_ => { }, p => p.Add(x => x.SelectionMode, SelectionMode.Multiple));
        Assert.Empty(cut.FindAll("col"));
        Assert.Equal("table-layout:auto", cut.Find("table").GetAttribute("style"));
    }

    [Fact]
    public void Shift_click_during_a_load_builds_on_the_sort_asked_for()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var answer = new TaskCompletionSource<DataResult<Row>>();
        var cut = Render<DataGrid<Row>>(p => p
            .Add(x => x.ChildContent, NameAndQty)
            .Add(x => x.ItemsProvider, _ => new ValueTask<DataResult<Row>>(answer.Task))
            .Add(x => x.SortChanged, (string? _) => { }));
        answer.SetResult(new DataResult<Row>(Rows, Rows.Length));
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("tbody tr").Count));

        answer = new TaskCompletionSource<DataResult<Row>>();
        // The first click starts a load, which renders: the second may need to find its button again.
        cut.FindAll("th button.ib-table-sort")[0].Click(new Microsoft.AspNetCore.Components.Web.MouseEventArgs { ShiftKey = true });
        cut.WaitForAssertion(() => cut.FindAll("th button.ib-table-sort")[1].Click(new Microsoft.AspNetCore.Components.Web.MouseEventArgs { ShiftKey = true }));
        answer.SetResult(new DataResult<Row>(Rows, Rows.Length));
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".ib-table-sort-order").Count));
    }

    [Fact]
    public void Shift_click_on_a_bound_grid_adds_to_the_sort()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        string? sort = null;
        var cut = Grid(NameAndQty, p => p
            .Add(x => x.Sort, "name")
            .Add(x => x.SortChanged, (string? s) => sort = s));

        cut.FindAll("th button.ib-table-sort")[1].Click(new Microsoft.AspNetCore.Components.Web.MouseEventArgs { ShiftKey = true });
        Assert.Equal("name,qty", sort);

        cut.FindAll("th button.ib-table-sort")[0].Click();
        Assert.Equal("-name", sort);
    }
}
