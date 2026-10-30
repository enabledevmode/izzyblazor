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

    public DataGridRenderTests() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    private BunitNavigationManager Nav => Services.GetRequiredService<BunitNavigationManager>();

    private static RenderFragment Column<T>(Expression<Func<Row, T>> property, Length? width = null, Length? min = null) => builder =>
    {
        builder.OpenComponent<PropertyColumn<Row, T>>(0);
        builder.AddComponentParameter(1, "Property", property);
        builder.AddComponentParameter(2, "Sortable", true);
        builder.AddComponentParameter(3, "Width", width);
        builder.AddComponentParameter(4, "MinWidth", min);
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
        cut.FindAll("th button.ib-table-sort")[0].Click(new Microsoft.AspNetCore.Components.Web.MouseEventArgs { ShiftKey = true });
        cut.FindAll("th button.ib-table-sort")[1].Click(new Microsoft.AspNetCore.Components.Web.MouseEventArgs { ShiftKey = true });
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
