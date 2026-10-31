using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

public class DataGridFilterRowTests : BunitContext
{
    public enum Kind { A, B }

    public sealed record Row(string Name, int Total, Kind Kind);

    private static readonly Row[] Rows = [new("Ada", 10, Kind.A), new("Grace", 20, Kind.B)];

    private IRenderedComponent<DataGrid<Row>> Grid(string url = "/orders")
    {
        Services.GetRequiredService<BunitNavigationManager>().NavigateTo(url);
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        RenderFragment columns = b =>
        {
            b.OpenComponent<PropertyColumn<Row, string>>(0);
            b.AddComponentParameter(1, "Property", (System.Linq.Expressions.Expression<Func<Row, string>>)(r => r.Name));
            b.AddComponentParameter(2, "Filterable", true);
            b.CloseComponent();
            b.OpenComponent<PropertyColumn<Row, int>>(3);
            b.AddComponentParameter(4, "Property", (System.Linq.Expressions.Expression<Func<Row, int>>)(r => r.Total));
            b.AddComponentParameter(5, "Filterable", true);
            b.CloseComponent();
            b.OpenComponent<PropertyColumn<Row, Kind>>(6);
            b.AddComponentParameter(7, "Property", (System.Linq.Expressions.Expression<Func<Row, Kind>>)(r => r.Kind));
            b.AddComponentParameter(8, "Filterable", true);
            b.CloseComponent();
        };
        return Render<DataGrid<Row>>(p => p.Add(g => g.Items, Rows).Add(g => g.FilterRow, true).Add(g => g.ChildContent, columns));
    }

    [Fact]
    public void A_cell_is_a_field_and_a_list_or_range_opens_its_panel()
    {
        var cut = Grid("/orders?f-total-op=between&f-total=5&f-total-to=15");
        var cells = cut.FindAll(".ib-table-filter-row td");

        var field = cells[0].QuerySelector("input[data-ib-row]")!;
        Assert.Equal("f-name", field.GetAttribute("name"));
        Assert.NotNull(cells[0].QuerySelector("[data-ib-menu]"));

        // Each opens the panel that's really there.
        foreach (var cell in cells.Skip(1))
        {
            Assert.Null(cell.QuerySelector("input[data-ib-row]"));
            var id = cell.QuerySelector(".ib-filter-cell-summary")!.GetAttribute("popovertarget")!;
            Assert.NotNull(cut.Find($"thead th [id='{id}'][popover]"));
        }

        Assert.Equal(new[] { "Ada" }, cut.FindAll("tbody td:first-child").Select(c => c.TextContent.Trim()));
    }

    [Fact]
    public void The_rows_form_carries_every_other_filter_so_it_posts_the_whole_state()
    {
        var cut = Grid("/orders?f-name-op=startswith&f-name=a&f-total-op=between&f-total=5&f-kind=A&sort=name");
        var form = cut.FindAll("form").Single(f => f.Id!.EndsWith("-row", StringComparison.Ordinal)).Id;

        var posted = cut.FindAll($"[form='{form}'][name]").Where(i => i.GetAttribute("type") == "hidden" || i.HasAttribute("data-ib-row"))
            .Select(i => $"{i.GetAttribute("name")}={i.GetAttribute("value")}").Order().ToList();

        Assert.Equal(new[] { "f-kind=A", "f-name-op=startswith", "f-name=a", "f-total-op=between", "f-total=5", "sort=name" }.Order(), posted);
    }

    [Fact]
    public void The_menu_chooses_an_operator_by_a_link_keeping_the_value()
    {
        var cut = Grid("/orders?f-name=a&page=2");

        var starts = cut.FindAll(".ib-table-filter-row td")[0].QuerySelectorAll("a").First(a => a.TextContent.Contains("Starts with"));
        var href = starts.GetAttribute("href")!;

        Assert.Contains("f-name-op=startswith", href);
        Assert.Contains("f-name=a", href);
        Assert.DoesNotContain("page=", href);
    }

    [Fact]
    public void Virtual_rows_count_the_row_in_their_numbers()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        RenderFragment columns = b =>
        {
            b.OpenComponent<PropertyColumn<Row, string>>(0);
            b.AddComponentParameter(1, "Property", (System.Linq.Expressions.Expression<Func<Row, string>>)(r => r.Name));
            b.AddComponentParameter(2, "Filterable", true);
            b.CloseComponent();
        };

        var cut = Render<DataGrid<Row>>(p => p.Add(g => g.Items, Rows).Add(g => g.FilterRow, true).Add(g => g.Virtualize, true).Add(g => g.MaxHeight, Length.Rem(20)).Add(g => g.ChildContent, columns));

        var table = cut.Find("table");
        Assert.Equal("4", table.GetAttribute("aria-rowcount"));
        Assert.Equal("3", table.GetAttribute("data-ib-first"));
        Assert.Equal("2", cut.Find(".ib-table-filter-row").GetAttribute("aria-rowindex"));
    }
}

