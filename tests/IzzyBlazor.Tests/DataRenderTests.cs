using System.Linq.Expressions;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

public class DataRenderTests : BunitContext
{
    public sealed record Row(string Name, decimal UnitPrice, string Status);

    private static readonly Row[] Rows = [new("b", 2.5m, "Paid"), new("a", 10m, "Failed"), new("c", 1m, "Paid")];

    public DataRenderTests() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    private void GoTo(string path) => Services.GetRequiredService<BunitNavigationManager>().NavigateTo(path);

    private static RenderFragment Columns => builder =>
    {
        builder.OpenComponent<PropertyColumn<Row, string>>(0);
        builder.AddComponentParameter(1, "Property", (Expression<Func<Row, string>>)(r => r.Name));
        builder.AddComponentParameter(2, "Sortable", true);
        builder.CloseComponent();
        builder.OpenComponent<PropertyColumn<Row, decimal>>(3);
        builder.AddComponentParameter(4, "Property", (Expression<Func<Row, decimal>>)(r => r.UnitPrice));
        builder.AddComponentParameter(5, "Format", "0.00");
        builder.AddComponentParameter(6, "Sortable", true);
        builder.CloseComponent();
        builder.OpenComponent<TemplateColumn<Row>>(7);
        builder.AddComponentParameter(8, "Title", "Status");
        builder.AddComponentParameter(9, "ChildContent", (RenderFragment<Row>)(row => b => b.AddContent(0, $"[{row.Status}]")));
        builder.CloseComponent();
    };

    private IRenderedComponent<Table<Row>> RenderTable(int? pageSize = null, string? initialSort = null) =>
        Render<Table<Row>>(p => p
            .Add(t => t.Items, Rows)
            .Add(t => t.PageSize, pageSize)
            .Add(t => t.InitialSort, initialSort)
            .Add(t => t.Caption, "Rows")
            .Add(t => t.ChildContent, Columns));

    private static string[] Column(IRenderedComponent<Table<Row>> cut, int index) =>
        cut.FindAll("tbody tr").Select(r => r.Children[index].TextContent).ToArray();

    [Theory]
    [InlineData("/orders", 2)]
    [InlineData("/orders?size=3", 3)]
    [InlineData("/orders?size=1000", 2)]
    public void Page_size_comes_from_the_url_when_its_an_option(string url, int shown)
    {
        GoTo(url);
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.Items, Rows)
            .Add(t => t.PageSize, 2)
            .Add(t => t.PageSizeOptions, new[] { 2, 3 })
            .Add(t => t.ChildContent, Columns));

        Assert.Equal(shown, cut.FindAll("tbody tr").Count);
        var items = cut.FindAll(".ib-data-page-size a.ib-menu-item");
        Assert.Equal(new[] { "2", "3" }, items.Select(i => i.TextContent.Trim()));
        Assert.Equal("http://localhost/orders", items[0].GetAttribute("href"));
        Assert.Equal("http://localhost/orders?size=3", items[1].GetAttribute("href"));
    }

    [Fact]
    public void Columns_take_titles_alignment_and_formats_from_the_property()
    {
        var cut = RenderTable();

        var headers = cut.FindAll("th");
        Assert.Equal(new[] { "Name", "Unit price", "Status" }, headers.Select(h => h.TextContent.Trim()));
        Assert.Equal("ib-cell-end", headers[1].ClassName);
        Assert.Equal(new[] { "2.50", "10.00", "1.00" }, Column(cut, 1));
        Assert.Equal("[Paid]", Column(cut, 2)[0]);
        Assert.Equal("Rows", cut.Find("caption").TextContent);
    }

    [Fact]
    public void Selectable_rows_post_their_keys_and_show_the_selection()
    {
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.Items, Rows)
            .Add(t => t.SelectionMode, SelectionMode.Multiple)
            .Add(t => t.ItemKey, r => r.Name)
            .Add(t => t.Selection, Selection.Of("a"))
            .Add(t => t.ChildContent, Columns));

        var boxes = cut.FindAll("tbody .ib-table-select");
        Assert.Equal(new[] { "b", "a", "c" }, boxes.Select(b => b.GetAttribute("value")));
        Assert.All(boxes, b => Assert.Equal("selected.Keys", b.GetAttribute("name")));
        Assert.Equal(new[] { false, true, false }, boxes.Select(b => b.HasAttribute("checked")));
        Assert.Equal("Select a", boxes[1].GetAttribute("aria-label"));
        Assert.Equal("Select all rows", cut.Find("thead .ib-table-select-all").GetAttribute("aria-label"));
        // Names the column while select-all is hidden (no script yet).
        Assert.Equal("Select", cut.Find("thead .ib-table-select-heading").TextContent);
        Assert.Equal(4, cut.FindAll("thead th").Count);
    }

    [Fact]
    public void Selection_changes_report_the_keys()
    {
        Selection? reported = null;
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.Items, Rows)
            .Add(t => t.SelectionMode, SelectionMode.Multiple)
            .Add(t => t.ItemKey, r => r.Name)
            .Add(t => t.SelectionChanged, (Selection s) => reported = s)
            .Add(t => t.ChildContent, Columns));

        cut.FindAll("tbody .ib-table-select")[2].Change(true);
        Assert.Equal(new[] { "c" }, reported!.Keys);

        cut.FindAll("tbody .ib-table-select")[0].Change(true);
        Assert.Equal(new[] { "b", "c" }, reported!.Keys!.Order());
    }

    [Fact]
    public void Single_selection_is_a_radio_per_row_and_replaces_the_last_one()
    {
        Selection? reported = null;
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.Items, Rows)
            .Add(t => t.SelectionMode, SelectionMode.Single)
            .Add(t => t.ItemKey, r => r.Name)
            .Add(t => t.Selection, Selection.Of("a"))
            .Add(t => t.SelectionChanged, (Selection s) => reported = s)
            .Add(t => t.ChildContent, Columns));

        var radios = cut.FindAll("tbody .ib-table-select");
        Assert.All(radios, r => Assert.Equal("radio", r.GetAttribute("type")));
        Assert.True(radios[1].HasAttribute("checked"));
        // No select-all for one item; the heading still names the column.
        Assert.Empty(cut.FindAll(".ib-table-select-all"));
        Assert.Equal("Select", cut.Find("thead .ib-table-select-heading").TextContent);

        radios[2].Change("c");
        Assert.Equal(new[] { "c" }, reported!.Keys);
    }

    [Fact]
    public void All_matching_shows_every_row_selected_but_the_exceptions()
    {
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.Items, Rows)
            .Add(t => t.SelectionMode, SelectionMode.Multiple)
            .Add(t => t.ItemKey, r => r.Name)
            .Add(t => t.Selection, new Selection { All = true, Keys = ["b"] })
            .Add(t => t.ChildContent, Columns));

        Assert.Equal(new[] { false, true, true }, cut.FindAll("tbody .ib-table-select").Select(b => b.HasAttribute("checked")));
    }

    [Fact]
    public void RowHref_puts_one_named_link_in_each_row()
    {
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.Items, Rows)
            .Add(t => t.RowHref, r => $"/rows/{r.Name}")
            .Add(t => t.RowLabel, r => $"row {r.Name}")
            .Add(t => t.ChildContent, Columns));

        var links = cut.FindAll("tbody .ib-table-row-link");
        Assert.Equal(new[] { "/rows/b", "/rows/a", "/rows/c" }, links.Select(a => a.GetAttribute("href")));
        Assert.Equal("row b", links[0].TextContent);
        Assert.Contains("ib-table-row-linked", cut.Find("tbody tr").ClassName);
    }

    [Fact]
    public void Sort_comes_from_the_url_and_headers_link_to_the_next_sort()
    {
        GoTo("/rows?sort=name&page=2");
        var cut = RenderTable(pageSize: 2);

        Assert.Equal(new[] { "c" }, Column(cut, 0));
        var name = cut.FindAll("th")[0];
        Assert.Equal("ascending", name.GetAttribute("aria-sort"));
        Assert.Equal("http://localhost/rows?sort=-name", name.QuerySelector("a")!.GetAttribute("href"));
        Assert.Equal("http://localhost/rows?sort=unitprice", cut.FindAll("th")[1].QuerySelector("a")!.GetAttribute("href"));
        Assert.Null(cut.FindAll("th")[2].GetAttribute("aria-sort"));
        Assert.Equal("3–3 of 3", cut.Find(".ib-data-summary").TextContent);
    }

    [Fact]
    public void Initial_sort_applies_when_the_url_has_none()
    {
        var cut = RenderTable(initialSort: "-unitprice");

        Assert.Equal(new[] { "a", "b", "c" }, Column(cut, 0));
        Assert.Equal("descending", cut.FindAll("th")[1].GetAttribute("aria-sort"));
    }

    [Fact]
    public void Items_provider_gets_the_request()
    {
        GoTo("/rows?x-sort=-name&x-page=2");
        DataRequest<Row>? seen = null;
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.QueryPrefix, "x-")
            .Add(t => t.PageSize, 1)
            .Add(t => t.ItemsProvider, request =>
            {
                seen = request;
                return ValueTask.FromResult(new DataResult<Row>(request.Apply(Rows.AsQueryable()).ToList(), Rows.Length));
            })
            .Add(t => t.ChildContent, Columns));

        Assert.NotNull(seen);
        Assert.Equal(new[] { new SortField("name", Descending: true) }, seen!.Sort);
        Assert.Equal(2, seen.Page);
        Assert.Equal(1, seen.Skip);
        Assert.Equal(new[] { "b" }, Column(cut, 0));
    }

    private static RenderFragment FilterFields => builder =>
    {
        builder.OpenComponent<FilterSearch>(0);
        builder.CloseComponent();
        builder.OpenComponent<FilterSelect>(1);
        builder.AddComponentParameter(2, nameof(FilterSelect.Name), "status");
        builder.AddComponentParameter(3, nameof(FilterSelect.Label), "Status");
        builder.AddComponentParameter(4, nameof(FilterSelect.Items), new[] { "Paid", "Failed" });
        builder.CloseComponent();
        builder.OpenComponent<FilterCheckbox>(5);
        builder.AddComponentParameter(6, nameof(FilterCheckbox.Name), "large");
        builder.AddComponentParameter(7, nameof(FilterCheckbox.Label), "Large orders");
        builder.CloseComponent();
        builder.OpenComponent<FilterDate>(8);
        builder.AddComponentParameter(9, nameof(FilterDate.Name), "from");
        builder.AddComponentParameter(10, nameof(FilterDate.Label), "From");
        builder.AddComponentParameter(11, nameof(FilterDate.Max), new DateOnly(2026, 12, 31));
        builder.CloseComponent();
    };

    [Fact]
    public void Filter_form_keeps_other_parameters_and_drops_pages()
    {
        GoTo("/orders?sort=-total&page=3&orders-page=2&q=ada&status=Paid&large=true");
        var cut = Render<FilterForm>(p => p.Add(f => f.ChildContent, FilterFields));

        var form = cut.Find("form");
        Assert.Equal("get", form.GetAttribute("method"));
        Assert.Equal("/orders", form.GetAttribute("action"));
        Assert.Equal("search", form.GetAttribute("role"));
        var hidden = cut.FindAll("input[type=hidden]").Select(i => $"{i.GetAttribute("name")}={i.GetAttribute("value")}");
        Assert.Equal(new[] { "sort=-total" }, hidden);
        Assert.Equal("http://localhost/orders?sort=-total", cut.Find("a.ib-filter-clear").GetAttribute("href"));
    }

    [Fact]
    public void Filter_fields_read_their_values_from_the_url()
    {
        GoTo("/orders?q=ada&status=Paid&large=true&from=2026-10-01");
        var cut = Render<FilterForm>(p => p.Add(f => f.ChildContent, FilterFields));

        Assert.Equal("ada", cut.Find("input[type=search]").GetAttribute("value"));
        Assert.Equal("Search", cut.Find("label.ib-visually-hidden").TextContent);
        Assert.Equal("Status: All", cut.Find("select option").TextContent);
        Assert.True(cut.Find("input[type=checkbox]").HasAttribute("checked"));
        var from = cut.Find("input[type=date]");
        Assert.Equal("2026-10-01", from.GetAttribute("value"));
        Assert.Equal("2026-12-31", from.GetAttribute("max"));

        // A date input shows no placeholder, so its label is shown, not only read.
        var label = cut.Find($"label[for='{from.Id}']");
        Assert.Equal("From", label.TextContent);
        Assert.DoesNotContain("ib-visually-hidden", label.ClassName);
    }
}
