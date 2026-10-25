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
        var items = cut.FindAll(".ib-table-page-size a.ib-menu-item");
        Assert.Equal(new[] { "2", "3" }, items.Select(i => i.TextContent.Trim()));
        Assert.Equal("http://localhost/orders", items[0].GetAttribute("href"));
        Assert.Equal("http://localhost/orders?size=3", items[1].GetAttribute("href"));
    }

    [Fact]
    public void Line_settings_add_their_classes()
    {
        var plain = RenderTable().Find("ib-table");
        Assert.DoesNotContain("ib-table-no-row-lines", plain.ClassName);
        Assert.DoesNotContain("ib-table-column-lines", plain.ClassName);
        Assert.DoesNotContain("ib-table-borderless", plain.ClassName);

        var lined = Render<Table<Row>>(p => p
            .Add(t => t.Items, Rows)
            .Add(t => t.RowLines, false)
            .Add(t => t.ColumnLines, true)
            .Add(t => t.Bordered, false)
            .Add(t => t.ChildContent, Columns)).Find("ib-table");
        Assert.Contains("ib-table-no-row-lines", lined.ClassName);
        Assert.Contains("ib-table-column-lines", lined.ClassName);
        Assert.Contains("ib-table-borderless", lined.ClassName);
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
            .Add(t => t.Selectable, true)
            .Add(t => t.ItemKey, r => r.Name)
            .Add(t => t.SelectedKeys, new[] { "a" })
            .Add(t => t.ChildContent, Columns));

        var boxes = cut.FindAll("tbody .ib-table-select");
        Assert.Equal(new[] { "b", "a", "c" }, boxes.Select(b => b.GetAttribute("value")));
        Assert.All(boxes, b => Assert.Equal("selected", b.GetAttribute("name")));
        Assert.Equal(new[] { false, true, false }, boxes.Select(b => b.HasAttribute("checked")));
        Assert.Equal("Select a", boxes[1].GetAttribute("aria-label"));
        Assert.Equal("Select all rows", cut.Find("thead .ib-table-select-all").GetAttribute("aria-label"));
        Assert.Equal(4, cut.FindAll("thead th").Count);
    }

    [Fact]
    public void Selection_changes_report_the_keys()
    {
        IReadOnlyCollection<string>? reported = null;
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.Items, Rows)
            .Add(t => t.Selectable, true)
            .Add(t => t.ItemKey, r => r.Name)
            .Add(t => t.SelectedKeysChanged, (IReadOnlyCollection<string> keys) => reported = keys)
            .Add(t => t.ChildContent, Columns));

        cut.FindAll("tbody .ib-table-select")[2].Change(true);
        Assert.Equal(new[] { "c" }, reported);

        cut.FindAll("tbody .ib-table-select")[0].Change(true);
        Assert.Equal(new[] { "b", "c" }, reported!.Order());
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
    public void Column_footers_add_a_footer_row()
    {
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.Items, Rows)
            .Add(t => t.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<PropertyColumn<Row, string>>(0);
                b.AddComponentParameter(1, "Property", (Expression<Func<Row, string>>)(r => r.Name));
                b.AddComponentParameter(2, "FooterText", "Total");
                b.CloseComponent();
                b.OpenComponent<PropertyColumn<Row, decimal>>(3);
                b.AddComponentParameter(4, "Property", (Expression<Func<Row, decimal>>)(r => r.UnitPrice));
                b.AddComponentParameter(5, "Footer", (RenderFragment)(f => f.AddMarkupContent(0, "<b>13.50</b>")));
                b.CloseComponent();
            })));

        var cells = cut.FindAll("tfoot td");
        Assert.Equal("Total", cells[0].TextContent);
        Assert.Equal("13.50", cells[1].QuerySelector("b")!.TextContent);
        Assert.Equal("ib-cell-end", cells[1].ClassName);
    }

    [Fact]
    public void Loading_dims_rows_or_shows_placeholders()
    {
        var loaded = Render<Table<Row>>(p => p.Add(t => t.Items, Rows).Add(t => t.Loading, true).Add(t => t.ChildContent, Columns));
        Assert.Equal("true", loaded.Find("table").GetAttribute("aria-busy"));
        Assert.Equal(3, loaded.FindAll("tbody tr").Count);

        var empty = Render<Table<Row>>(p => p.Add(t => t.Items, Array.Empty<Row>()).Add(t => t.Loading, true).Add(t => t.PageSize, 4).Add(t => t.ChildContent, Columns));
        Assert.Equal(4, empty.FindAll("tbody .ib-table-skeleton-row").Count);
        Assert.Empty(empty.FindAll(".ib-table-empty"));
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
        Assert.Equal("3–3 of 3", cut.Find(".ib-table-summary").TextContent);
    }

    [Fact]
    public void Initial_sort_applies_when_the_url_has_none()
    {
        var cut = RenderTable(initialSort: "-unitprice");

        Assert.Equal(new[] { "a", "b", "c" }, Column(cut, 0));
        Assert.Equal("descending", cut.FindAll("th")[1].GetAttribute("aria-sort"));
    }

    [Fact]
    public void Unknown_sort_keys_are_ignored()
    {
        GoTo("/rows?sort=password");
        var cut = RenderTable();

        Assert.Equal(new[] { "b", "a", "c" }, Column(cut, 0));
        Assert.All(cut.FindAll("th[aria-sort]"), th => Assert.Equal("none", th.GetAttribute("aria-sort")));
    }

    [Fact]
    public void Items_provider_gets_the_request()
    {
        GoTo("/rows?x-sort=-name&x-page=2");
        TableRequest<Row>? seen = null;
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.QueryPrefix, "x-")
            .Add(t => t.PageSize, 1)
            .Add(t => t.ItemsProvider, request =>
            {
                seen = request;
                return ValueTask.FromResult(new TableResult<Row>(request.Apply(Rows.AsQueryable()).ToList(), Rows.Length));
            })
            .Add(t => t.ChildContent, Columns));

        Assert.NotNull(seen);
        Assert.Equal("name", seen!.SortKey);
        Assert.True(seen.Descending);
        Assert.Equal(2, seen.Page);
        Assert.Equal(1, seen.Skip);
        Assert.Equal(new[] { "b" }, Column(cut, 0));
    }

    [Fact]
    public void Empty_table_shows_the_empty_text()
    {
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.Items, Array.Empty<Row>())
            .Add(t => t.EmptyText, "Nothing here")
            .Add(t => t.ChildContent, Columns));

        var cell = cut.Find(".ib-table-empty td");
        Assert.Equal("3", cell.GetAttribute("colspan"));
        Assert.Equal("Nothing here", cell.TextContent.Trim());
    }

    [Fact]
    public void Linked_card_puts_the_link_on_the_title()
    {
        var card = Render<Card>(p => p
            .Add(c => c.Title, "Website")
            .Add(c => c.TitleLevel, 2)
            .Add(c => c.Href, "/projects/website")
            .Add(c => c.Variant, CardVariant.Elevated)
            .AddChildContent("Body")).Find(".ib-card");

        Assert.Equal("ib-card ib-card-elevated ib-card-linked", card.ClassName);
        Assert.Equal("/projects/website", card.QuerySelector("h2.ib-card-title > a.ib-card-link")!.GetAttribute("href"));
        Assert.Equal("Body", card.QuerySelector(".ib-card-body")!.TextContent);
    }

    [Fact]
    public void Card_draws_its_parts_in_place_wherever_they_are_written()
    {
        var cut = Render<Card>(p => p
            .Add(c => c.Title, "Orders")
            .AddChildContent<CardFooter>(f => f.AddChildContent("Footer"))
            .AddChildContent<CardActions>(a => a.AddChildContent("<button>More</button>"))
            .AddChildContent<CardMedia>(m => m.AddChildContent("<img alt=\"\">")));

        var card = cut.Find(".ib-card");
        Assert.Equal(new[] { "ib-card-media", "ib-card-header", "ib-card-body" }, card.Children.Select(c => c.ClassName));
        Assert.Equal("More", card.QuerySelector(".ib-card-header .ib-card-actions button")!.TextContent);
        Assert.Equal("Footer", card.QuerySelector(".ib-card-body > .ib-card-footer")!.TextContent);
        Assert.Throws<InvalidOperationException>(() => Render<CardActions>());
    }

    [Fact]
    public void Card_parts_follow_their_content_when_swapped_added_and_removed()
    {
        static RenderFragment Content(string? actions) => b =>
        {
            // Different sequence numbers: one part replaces another, as with @if/else.
            if (actions == "A")
            {
                b.OpenComponent<CardActions>(0);
                b.AddComponentParameter(1, nameof(CardActions.ChildContent), (RenderFragment)(c => c.AddContent(0, "A")));
                b.CloseComponent();
            }
            else if (actions == "B")
            {
                b.OpenComponent<CardActions>(2);
                b.AddComponentParameter(3, nameof(CardActions.ChildContent), (RenderFragment)(c => c.AddContent(0, "B")));
                b.CloseComponent();
            }

            b.AddContent(4, "Body");
        };

        var cut = Render<Card>(p => p.Add(c => c.Title, "Orders").Add(c => c.ChildContent, Content("A")));
        Assert.Equal("A", cut.Find(".ib-card-actions").TextContent);

        cut.Render(p => p.Add(c => c.ChildContent, Content("B")));
        Assert.Equal("B", cut.Find(".ib-card-actions").TextContent);

        cut.Render(p => p.Add(c => c.ChildContent, Content(null)));
        Assert.Empty(cut.FindAll(".ib-card-actions"));
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
        GoTo("/orders?q=ada&status=Paid&large=true");
        var cut = Render<FilterForm>(p => p.Add(f => f.ChildContent, FilterFields));

        Assert.Equal("ada", cut.Find("input[type=search]").GetAttribute("value"));
        Assert.Equal("Search", cut.Find("label.ib-visually-hidden").TextContent);
        Assert.Equal("Status: All", cut.Find("select option").TextContent);
        Assert.True(cut.Find("input[type=checkbox]").HasAttribute("checked"));
    }

    [Fact]
    public void Filter_form_without_values_has_no_clear_link()
    {
        GoTo("/orders");
        var cut = Render<FilterForm>(p => p.Add(f => f.ChildContent, FilterFields));

        Assert.Empty(cut.FindAll("a.ib-filter-clear"));
        Assert.NotNull(cut.Find("button.ib-filter-apply"));
    }
}
