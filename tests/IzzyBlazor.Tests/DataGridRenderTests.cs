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

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"c":[{"w":"10px"}]}""")]
    public void A_broken_cookie_is_no_layout(string json) => Assert.Empty(DataGridLayout.Parse(json)?.Columns ?? []);

    [Theory]
    [InlineData("١٨٠px")]
    [InlineData("10px\n")]
    public void A_width_in_other_digits_or_with_more_after_it_is_dropped(string width) =>
        Assert.Null(DataGridLayout.Parse(Arranged(new DataGridColumnLayout { Key = "a", Width = width }).ToJson())!.Column("a")!.Width);

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
    [InlineData("10%")]
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
            builder.OpenComponent<RowAction>(0);
            builder.AddComponentParameter(1, nameof(RowAction.Label), "Item");
            builder.AddComponentParameter(2, nameof(RowAction.Visible), row.Name == "b");
            builder.CloseComponent();
        };
        RenderFragment columns = builder =>
        {
            builder.AddContent(0, NameAndQty);
            builder.OpenComponent<ActionColumn<Row>>(1);
            builder.AddComponentParameter(2, nameof(ActionColumn<Row>.Overflow), RowActionOverflow.Always);
            builder.AddComponentParameter(3, nameof(ActionColumn<Row>.ChildContent), menu);
            builder.CloseComponent();
        };
        var cut = Grid(columns);
        Assert.Equal(3, cut.FindAll("thead th").Count);
        Assert.Equal(3, cut.FindAll("col").Count);
        Assert.Contains("var(--ib-datagrid-menu)", cut.Find("table").GetAttribute("style"));
        Assert.Single(cut.FindAll("[data-ib-row-menu]"));
        Assert.Equal(3, cut.FindAll("tbody tr")[0].Children.Length);
        Assert.Equal(3, cut.FindAll("tbody tr")[1].Children.Length);
    }

    [Fact]
    public void A_bound_EditKey_holds_the_row_being_edited()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        string? key = null;
        IRenderedComponent<DataGrid<Row>> cut = null!;
        cut = Grid(NameAndQty, p => p
            .Add(x => x.ItemKey, r => r.Name + r.Qty)
            .Add(x => x.EditMode, DataGridEditMode.Dialog)
            .Add(x => x.OnSave, (DataGridSave<Row> _) => { })
            .Add(x => x.EditKey, key)
            .Add(x => x.EditKeyChanged, (string? k) => { key = k; cut.Render(p2 => p2.Add(x => x.EditKey, k)); }));

        Assert.Empty(cut.FindAll("dialog"));
        cut.FindAll("button[data-ib-edit]")[0].Click();
        Assert.Equal("b2", key);
        Assert.Contains("b2", cut.Find("dialog").TextContent);
    }

    [Fact]
    public void A_row_has_fields_only_for_the_columns_shown_so_a_hidden_one_keeps_its_value()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        RenderFragment columns = builder =>
        {
            builder.OpenComponent<PropertyColumn<Row, string>>(0);
            builder.AddComponentParameter(1, "Property", (System.Linq.Expressions.Expression<Func<Row, string>>)(r => r.Name));
            builder.AddComponentParameter(2, "Visible", false);
            builder.CloseComponent();
            builder.OpenComponent<PropertyColumn<Row, int>>(3);
            builder.AddComponentParameter(4, "Property", (System.Linq.Expressions.Expression<Func<Row, int>>)(r => r.Qty));
            builder.CloseComponent();
        };
        DataGridSave<Row>? saved = null;
        var cut = Grid(columns, p => p
            .Add(x => x.ItemKey, r => r.Name + r.Qty)
            .Add(x => x.EditMode, DataGridEditMode.Row)
            .Add(x => x.OnSave, (DataGridSave<Row> s) => { saved = s; })
            .Add(x => x.EditKey, "b2")
            .Add(x => x.EditKeyChanged, (string? _) => { }));

        Assert.Equal(["ib-edit.Qty"], cut.FindAll("tr[data-ib-editing] [name^='ib-edit.']").Select(f => f.GetAttribute("name")));
        var formId = cut.Find("form.ib-grid-edit-form").Id;
        Assert.Equal(formId, cut.Find("tr[data-ib-editing] input").GetAttribute("form"));
        Assert.Equal(formId, cut.Find("tr[data-ib-editing] button[type=submit]").GetAttribute("form"));

        cut.Find("form.ib-grid-edit-form").Submit();
        var target = new Row("changed", 0);
        saved!.CopyTo(target);
        Assert.Equal("changed", target.Name);
        Assert.Equal(2, target.Qty);
    }

    [Fact]
    public void A_layout_places_a_field_once_and_a_field_it_leaves_out_after_it()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        RenderFragment columns = builder =>
        {
            builder.AddContent(0, NameAndQty);
            builder.OpenComponent<EditLayout<Row>>(1);
            builder.AddComponentParameter(2, nameof(EditLayout<Row>.ChildContent), (RenderFragment)(layout =>
            {
                layout.OpenComponent<Fieldset>(0);
                layout.AddComponentParameter(1, nameof(Fieldset.Label), "Section");
                layout.AddComponentParameter(2, nameof(Fieldset.ChildContent), (RenderFragment)(section =>
                {
                    section.OpenComponent<EditField>(0);
                    section.AddComponentParameter(1, nameof(EditField.Key), "qty");
                    section.CloseComponent();
                }));
                layout.CloseComponent();
            }));
            builder.CloseComponent();
        };
        var cut = Grid(columns, p => p
            .Add(x => x.ItemKey, r => r.Name + r.Qty)
            .Add(x => x.EditMode, DataGridEditMode.Dialog)
            .Add(x => x.OnSave, (DataGridSave<Row> _) => { })
            .Add(x => x.EditKey, "b2")
            .Add(x => x.EditKeyChanged, (string? _) => { }));

        var names = cut.FindAll("dialog [name^='ib-edit.']").Select(f => f.GetAttribute("name")).ToList();
        Assert.Equal(["ib-edit.Qty", "ib-edit.Name"], names);
        Assert.NotNull(cut.Find("dialog fieldset [name='ib-edit.Qty']"));
    }

    [Fact]
    public void Export_links_carry_the_sort_and_the_columns_shown_and_no_page()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("?sort=-qty&page=2");
        var cut = Grid(NameAndQty, p => p.Add(x => x.ExportCsvHref, "/o.csv").Add(x => x.ExportExcelHref, "/o.xlsx?t=1").Add(x => x.PageSize, 1));
        // The bar's menu of links, and the toolbar's "…" menu has copies, which download too.
        var links = cut.FindAll(".ib-menu-item[download]");
        Assert.Equal(4, links.Count);
        Assert.Equal("/o.xlsx?t=1&sort=-qty&columns=name,qty", links[0].GetAttribute("href"));
        Assert.Equal("/o.csv?sort=-qty&columns=name,qty", links[1].GetAttribute("href"));
        Assert.Equal(["Export Excel", "Export CSV"], links.Take(2).Select(l => l.TextContent.Trim()));
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
    public void Without_a_runtime_virtual_rows_load_on_scroll_and_are_a_plain_table()
    {
        var cut = Grid(NameAndQty, p => p.Add(x => x.Virtualize, true).Add(x => x.MaxHeight, Length.Rem(20)).Add(x => x.PageSize, 1));
        Assert.True(cut.Find("ib-table").HasAttribute("data-ib-load-on-scroll"));
        Assert.Null(cut.Find("table").GetAttribute("aria-rowcount"));
        Assert.Single(cut.FindAll("tbody tr"));
    }

    // A grid drawing virtual rows, from a provider of many rows. A window (Take isn't the page size)
    // waits for the test, when one is set.
    private (IRenderedComponent<DataGrid<Row>> Cut, List<DataRequest<Row>> Asked) VirtualGrid(Func<DataRequest<Row>, ValueTask<DataResult<Row>>>? answer = null, int pageSize = 1)
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        JSInterop.Mode = JSRuntimeMode.Loose;
        var asked = new List<DataRequest<Row>>();
        var cut = Render<DataGrid<Row>>(p => p
            .Add(x => x.ChildContent, NameAndQty)
            .Add(x => x.Virtualize, true)
            .Add(x => x.MaxHeight, Length.Rem(20))
            .Add(x => x.PageSize, pageSize)
            .Add(x => x.ItemsProvider, request =>
            {
                asked.Add(request);
                return answer?.Invoke(request) ?? new ValueTask<DataResult<Row>>(Answer(request));
            }));
        return (cut, asked);
    }

    private static DataResult<Row> Answer(DataRequest<Row> request, int total = 1000) =>
        new(Enumerable.Range(request.Skip, Math.Min(request.Take ?? 1, 2100)).Select(i => new Row($"r{i}", i)).ToList(), total);

    [Fact]
    public void Virtual_rows_stay_where_they_are_for_a_parents_render_and_dont_ask_again()
    {
        var (cut, asked) = VirtualGrid();
        cut.WaitForAssertion(() => Assert.Equal("1001", cut.Find("table").GetAttribute("aria-rowcount")));
        var load = cut.Find("table").GetAttribute("data-ib-load");
        var times = asked.Count;

        cut.Render(p => p.Add(x => x.Caption, "Another"));
        Assert.Equal(load, cut.Find("table").GetAttribute("data-ib-load"));
        Assert.Equal(times, asked.Count);
    }

    [Fact]
    public void Without_a_runtime_a_providers_total_is_enough_to_load_more_and_the_rows_stop_somewhere()
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var asked = new List<DataRequest<Row>>();
        var cut = Render<DataGrid<Row>>(p => p
            .Add(x => x.ChildContent, NameAndQty)
            .Add(x => x.Virtualize, true)
            .Add(x => x.MaxHeight, Length.Rem(20))
            .Add(x => x.PageSize, 500)
            .Add(x => x.ItemsProvider, request =>
            {
                asked.Add(request);
                return new ValueTask<DataResult<Row>>(request.NeedsTotal ? Answer(request) : new(Answer(request).Items, null));
            }));
        Assert.Single(cut.FindAll("[data-ib-load-next]"));

        Nav.NavigateTo("?page=999999");
        cut.Render();
        cut.WaitForAssertion(() => Assert.Equal(2000, asked[^1].Take));
        Assert.Empty(cut.FindAll("[data-ib-load-next]"));
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
