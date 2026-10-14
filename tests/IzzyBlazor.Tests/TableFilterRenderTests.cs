using System.Linq.Expressions;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

public enum Paid { Yes, No, Refunded }

// Filterable columns: query-string filters on static pages, component state when bound. These
// cover the filter row; the header placement (the default) has its own tests at the end.
public class TableFilterRenderTests : BunitContext
{
    public sealed record Row(string Name, int Qty, Paid Status, bool Shipped, DateTime When)
    {
        public DateOnly Day => DateOnly.FromDateTime(When);

        public DateTime? Maybe => Name == "adam" ? null : When;

        public DateTimeOffset Stamp => new(When, TimeSpan.FromHours(-4));

        public TimeSpan Took => TimeSpan.FromMinutes(Qty);
    }

    private static readonly Row[] Rows =
    [
        new("Ada", 2, Paid.Yes, true, new(2026, 1, 1, 9, 30, 0)),
        new("Grace", 3, Paid.No, false, new(2026, 1, 2, 23, 59, 0)),
        new("adam", 1, Paid.Refunded, true, new(2026, 1, 3, 0, 0, 0)),
    ];

    private BunitNavigationManager Nav => Services.GetRequiredService<BunitNavigationManager>();

    private static RenderFragment Columns => builder =>
    {
        builder.AddContent(0, Col<string>(r => r.Name));
        builder.AddContent(1, Col<int>(r => r.Qty));
        builder.AddContent(2, Col<Paid>(r => r.Status));
        builder.AddContent(3, Col<bool>(r => r.Shipped));
    };

    // A filterable, sortable column, added with b.AddContent(n, Col(...)) (a region of its own).
    private static RenderFragment Col<TValue>(Expression<Func<Row, TValue>> property) => builder =>
    {
        builder.OpenComponent<PropertyColumn<Row, TValue>>(0);
        builder.AddComponentParameter(1, "Property", property);
        builder.AddComponentParameter(2, "Filterable", true);
        builder.AddComponentParameter(3, "Sortable", true);
        builder.CloseComponent();
    };

    private IRenderedComponent<Table<Row>> RenderStatic(string url = "/orders")
    {
        Nav.NavigateTo(url);
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        return Render<Table<Row>>(p => p.Add(t => t.FilterPlacement, FilterPlacement.Row).Add(t => t.Items, Rows).Add(t => t.ChildContent, Columns));
    }

    private static string[] Names(IRenderedComponent<Table<Row>> cut) =>
        cut.FindAll("tbody tr:not(.ib-table-empty)").Select(r => r.Children[0].TextContent).ToArray();

    [Fact]
    public void Filter_row_has_a_control_per_column_joined_to_a_get_form()
    {
        var cut = RenderStatic();

        var form = cut.Find("form.ib-table-filter-form");
        Assert.Equal("get", form.GetAttribute("method"));
        Assert.Equal("/orders", form.GetAttribute("action"));

        var name = cut.Find("input[name='f-name']");
        Assert.Equal("search", name.GetAttribute("type"));
        Assert.Equal(form.Id, name.GetAttribute("form"));
        Assert.Equal("Filter Name", name.GetAttribute("aria-label"));
        Assert.Equal("decimal", cut.Find("input[name='f-qty']").GetAttribute("inputmode"));

        var status = cut.Find("select[name='f-status']");
        Assert.Equal(new[] { "", "Yes", "No", "Refunded" }, status.QuerySelectorAll("option").Select(o => o.GetAttribute("value")));
        Assert.Equal(new[] { "All", "Yes", "No" }, cut.Find("select[name='f-shipped']").QuerySelectorAll("option").Select(o => o.TextContent));
        Assert.Equal(form.Id, cut.Find("button[type=submit]").GetAttribute("form"));
    }

    [Theory]
    [InlineData("?f-name=AD", new[] { "Ada", "adam" })]
    [InlineData("?f-name=ad&f-shipped=true", new[] { "Ada", "adam" })]
    [InlineData("?f-status=no", new[] { "Grace" })]
    [InlineData("?f-qty=3", new[] { "Grace" })]
    [InlineData("?f-qty=2.5", new string[0])]
    [InlineData("?f-qty=abc", new string[0])]
    [InlineData("?f-status=Unknown", new string[0])]
    [InlineData("?f-shipped=false", new[] { "Grace" })]
    public void Url_filters_keep_matching_rows(string query, string[] expected)
    {
        var cut = RenderStatic("/orders" + query);

        Assert.Equal(expected, Names(cut));
    }

    [Fact]
    public void Filters_show_their_values_and_the_form_carries_the_rest_of_the_url()
    {
        var cut = RenderStatic("/orders?sort=name&page=2&tab=x&f-name=ad&f-status=Yes");

        Assert.Equal("ad", cut.Find("input[name='f-name']").GetAttribute("value"));
        Assert.True(cut.Find("select[name='f-status'] option[value='Yes']").HasAttribute("selected"));
        var kept = cut.FindAll("input[type=hidden]").Select(i => $"{i.GetAttribute("name")}={i.GetAttribute("value")}").ToArray();
        Assert.Equal(new[] { "sort=name", "tab=x" }, kept);
        Assert.DoesNotContain("f-name", cut.Find(".ib-table-filter-actions a").GetAttribute("href"));
    }

    [Fact]
    public void A_column_type_that_cant_filter_says_so()
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var ex = Assert.Throws<InvalidOperationException>(() => Render<Table<Row>>(p => p.Add(t => t.FilterPlacement, FilterPlacement.Row)
            .Add(t => t.Items, Rows)
            .Add(t => t.ChildContent, Col<TimeSpan>(r => r.Took))));
        Assert.Contains("TemplateColumn with FilterBy", ex.Message);
    }

    [Theory]
    [InlineData("when", "2026-01-02", new[] { "Grace" })]
    [InlineData("when", "2026-01-03", new[] { "adam" })]
    [InlineData("day", "2026-01-01", new[] { "Ada" })]
    [InlineData("maybe", "2026-01-03", new string[0])]
    [InlineData("maybe", "2026-01-02", new[] { "Grace" })]
    [InlineData("stamp", "2026-01-02", new[] { "Grace" })]
    [InlineData("when", "not a date", new string[0])]
    public void Date_filters_keep_that_day(string key, string value, string[] expected)
    {
        Nav.NavigateTo($"/orders?f-{key}={Uri.EscapeDataString(value)}");
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var cut = Render<Table<Row>>(p => p.Add(t => t.FilterPlacement, FilterPlacement.Row)
            .Add(t => t.Items, Rows)
            .Add(t => t.ChildContent, b =>
            {
                b.AddContent(0, Col<string>(r => r.Name));
                b.AddContent(1, Col<DateTime>(r => r.When));
                b.AddContent(2, Col<DateOnly>(r => r.Day));
                b.AddContent(3, Col<DateTime?>(r => r.Maybe));
                b.AddContent(4, Col<DateTimeOffset>(r => r.Stamp));
            }));

        Assert.Equal(expected, Names(cut));
        var input = cut.Find($"input[name='f-{key}']");
        Assert.Equal("date", input.GetAttribute("type"));
        Assert.Equal(expected.Length > 0 || value.StartsWith("2026", StringComparison.Ordinal) ? value : "", input.GetAttribute("value") ?? "");
    }

    [Fact]
    public void Template_column_filters_by_its_FilterBy_text()
    {
        Nav.NavigateTo("/orders?f-who=RAC");
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var cut = Render<Table<Row>>(p => p.Add(t => t.FilterPlacement, FilterPlacement.Row)
            .Add(t => t.Items, Rows)
            .Add(t => t.ChildContent, b =>
            {
                b.OpenComponent<TemplateColumn<Row>>(0);
                b.AddComponentParameter(1, "Title", "Who");
                b.AddComponentParameter(2, "SortKey", "who");
                b.AddComponentParameter(3, "FilterBy", (Expression<Func<Row, string?>>)(r => r.Name));
                b.AddComponentParameter(4, "Filterable", true);
                b.AddComponentParameter(5, "ChildContent", (RenderFragment<Row>)(r => c => c.AddContent(0, r.Name)));
                b.CloseComponent();
            }));

        Assert.Equal(new[] { "Grace" }, Names(cut));
    }

    [Fact]
    public void Template_column_without_FilterBy_says_so()
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var ex = Assert.Throws<InvalidOperationException>(() => Render<Table<Row>>(p => p.Add(t => t.FilterPlacement, FilterPlacement.Row)
            .Add(t => t.Items, Rows)
            .Add(t => t.ChildContent, b =>
            {
                b.OpenComponent<TemplateColumn<Row>>(0);
                b.AddComponentParameter(1, "Title", "Who");
                b.AddComponentParameter(2, "Filterable", true);
                b.CloseComponent();
            })));
        Assert.Contains("FilterBy", ex.Message);
    }

    [Fact]
    public void Items_provider_gets_the_filters()
    {
        Nav.NavigateTo("/orders?f-name=gr");
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        TableRequest<Row>? seen = null;
        var cut = Render<Table<Row>>(p => p.Add(t => t.FilterPlacement, FilterPlacement.Row)
            .Add(t => t.ItemsProvider, request =>
            {
                seen = request;
                var query = Rows.AsQueryable();
                return ValueTask.FromResult(new TableResult<Row>(request.Apply(query).ToList(), request.ApplyFiltering(query).Count()));
            })
            .Add(t => t.PageSize, 10)
            .Add(t => t.ChildContent, Columns));

        Assert.Equal("gr", seen!.Filters["name"]);
        Assert.Equal(new[] { "Grace" }, Names(cut));
        Assert.Contains("1–1 of 1", cut.Find(".ib-table-summary").TextContent);
    }

    [Fact]
    public void Bound_filters_apply_in_place_after_a_pause_and_go_back_to_page_one()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var reported = new List<IReadOnlyDictionary<string, string>>();
        var pages = new List<int>();
        var cut = Render<Table<Row>>(p => p.Add(t => t.FilterPlacement, FilterPlacement.Row)
            .Add(t => t.Items, Rows)
            .Add(t => t.PageSize, 1)
            .Add(t => t.FiltersChanged, (IReadOnlyDictionary<string, string> f) => reported.Add(f))
            .Add(t => t.PageChanged, (int page) => pages.Add(page))
            .Add(t => t.ChildContent, Columns));
        cut.Instance.FilterDelay = TimeSpan.FromMilliseconds(1);
        var url = Nav.Uri;

        cut.FindAll(".ib-table-footer button").First(b => b.TextContent.Trim().EndsWith('2')).Click();
        Assert.Empty(cut.FindAll("[data-ib-filter]"));
        cut.Find(".ib-table-filter input").Input("ad");

        cut.WaitForAssertion(() => Assert.Equal(new[] { "Ada" }, Names(cut)));
        Assert.Equal("ad", reported.Single()["name"]);
        Assert.Equal(new[] { 2, 1 }, pages);
        Assert.Contains("1–1 of 2", cut.Find(".ib-table-summary").TextContent);
        Assert.Equal(url, Nav.Uri);

        cut.Find("select.ib-field-select").Change("Refunded");
        cut.WaitForAssertion(() => Assert.Equal(new[] { "adam" }, Names(cut)));
        Assert.Equal("Refunded", reported.Last()["status"]);
    }

    private static RenderFragment MultiColumns => b =>
    {
        b.AddContent(0, Col<string>(r => r.Name));
        b.OpenComponent<PropertyColumn<Row, Paid>>(1);
        b.AddComponentParameter(2, "Property", (Expression<Func<Row, Paid>>)(r => r.Status));
        b.AddComponentParameter(3, "Filterable", true);
        b.AddComponentParameter(4, "FilterMultiple", true);
        b.CloseComponent();
    };

    [Theory]
    [InlineData("", "All", new[] { "Ada", "Grace", "adam" })]
    [InlineData("?f-status=No", "No", new[] { "Grace" })]
    [InlineData("?f-status=Yes&f-status=Refunded", "2 selected", new[] { "Ada", "adam" })]
    [InlineData("?f-status=Yes&f-status=Bogus", "2 selected", new[] { "Ada" })]
    public void Multiple_filter_keeps_any_chosen_value(string query, string summary, string[] expected)
    {
        Nav.NavigateTo("/orders" + query);
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var cut = Render<Table<Row>>(p => p.Add(t => t.FilterPlacement, FilterPlacement.Row).Add(t => t.Items, Rows).Add(t => t.ChildContent, MultiColumns));

        Assert.Equal(expected, Names(cut));
        Assert.Equal(summary, cut.Find(".ib-table-filter-summary").TextContent);
        Assert.Equal($"Filter Status: {summary}", cut.Find(".ib-table-filter-trigger").GetAttribute("aria-label"));
    }

    [Fact]
    public void Multiple_filter_items_link_to_the_url_with_their_value_toggled()
    {
        Nav.NavigateTo("/orders?f-status=Yes&page=2");
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var cut = Render<Table<Row>>(p => p.Add(t => t.FilterPlacement, FilterPlacement.Row).Add(t => t.Items, Rows).Add(t => t.ChildContent, MultiColumns));

        var items = cut.FindAll("a.ib-menu-item");
        Assert.Equal(new[] { "All", "Yes", "No", "Refunded" }, items.Select(i => i.TextContent.Trim()));
        Assert.Equal("http://localhost/orders", items[0].GetAttribute("href"));
        Assert.Equal("true", items[1].GetAttribute("aria-current"));
        Assert.Equal("http://localhost/orders", items[1].GetAttribute("href"));
        Assert.Equal("http://localhost/orders?f-status=Yes&f-status=No", items[2].GetAttribute("href"));
        Assert.All(items.Skip(1), i => Assert.True(i.HasAttribute("data-ib-keep-open")));
    }

    [Fact]
    public void Bound_multiple_filter_toggles_in_place()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        IReadOnlyDictionary<string, string>? reported = null;
        var cut = Render<Table<Row>>(p => p.Add(t => t.FilterPlacement, FilterPlacement.Row)
            .Add(t => t.Items, Rows)
            .Add(t => t.FiltersChanged, (IReadOnlyDictionary<string, string> f) => reported = f)
            .Add(t => t.ChildContent, MultiColumns));

        cut.FindAll("button.ib-menu-item").First(b => b.TextContent.Trim() == "No").Click();
        cut.FindAll("button.ib-menu-item").First(b => b.TextContent.Trim() == "Refunded").Click();

        Assert.Equal(new[] { "Grace", "adam" }, Names(cut));
        Assert.Equal("No,Refunded", reported!["status"]);
        Assert.Null(cut.FindAll("button.ib-menu-item").First(b => b.TextContent.Trim() == "No").GetAttribute("popovertargetaction"));

        cut.FindAll("button.ib-menu-item").First(b => b.TextContent.Trim() == "All").Click();
        Assert.Equal(3, Names(cut).Length);
    }

    [Fact]
    public void Unbound_filters_on_an_interactive_page_use_the_url()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var cut = Render<Table<Row>>(p => p.Add(t => t.FilterPlacement, FilterPlacement.Row).Add(t => t.Items, Rows).Add(t => t.ChildContent, Columns));

        Assert.Equal(4, cut.FindAll("[data-ib-filter]").Count);
        Assert.Single(cut.FindAll("form.ib-table-filter-form"));
    }

    [Fact]
    public void Header_placement_is_the_default_with_a_filter_button_per_column()
    {
        var cut = RenderHeader("/orders");

        Assert.Empty(cut.FindAll(".ib-table-filter-row"));
        var buttons = cut.FindAll("th .ib-table-head .ib-table-filter-button");
        Assert.Equal(new[] { "Filter Name", "Filter Qty", "Filter Status", "Filter Shipped" }, buttons.Select(b => b.GetAttribute("aria-label")));
        Assert.All(buttons, b => Assert.False(b.HasAttribute("data-active")));

        // Text opens a popover with the same field, joined to the GET form.
        var input = cut.Find("[popover] input[name='f-name']");
        Assert.Equal(cut.Find("form.ib-table-filter-form").Id, input.GetAttribute("form"));

        // A choice opens a menu whose items set that value (and close the menu).
        var items = cut.FindAll("a.ib-menu-item").Where(a => a.GetAttribute("href")!.Contains("f-status", StringComparison.Ordinal) || a.TextContent.Trim() == "All").ToList();
        Assert.Contains(items, a => a.GetAttribute("href") == "http://localhost/orders?f-status=No");
        Assert.DoesNotContain(cut.FindAll("a.ib-menu-item"), a => a.HasAttribute("data-ib-keep-open"));
        Assert.True(cut.Find(".ib-table-filter-tags").HasAttribute("hidden"));
    }

    [Fact]
    public void Header_filter_button_follows_the_title_in_one_group()
    {
        var cut = RenderHeader("/orders");

        // Title (or sort control) first, then the funnel; CSS mirrors the group for end-aligned
        // columns (Qty here), so the markup order is the same for every column.
        foreach (var head in cut.FindAll("th .ib-table-head"))
        {
            var children = head.Children;
            Assert.Equal(2, children.Length);
            Assert.NotNull(children[0].QuerySelector(".ib-table-title") ?? (children[0].ClassList.Contains("ib-table-title") ? children[0] : null));
            Assert.Contains("ib-table-filter-button", children[1].QuerySelector(".ib-table-filter-button")?.ClassName ?? children[1].ClassName);
        }

        Assert.Contains("ib-cell-end", cut.FindAll("th")[1].ClassName);
    }

    [Fact]
    public void Header_placement_marks_filtered_columns_and_lists_them_as_tags()
    {
        var cut = RenderHeader("/orders?f-name=ad&f-status=No&sort=name");

        Assert.Empty(Names(cut));
        var name = cut.FindAll(".ib-table-filter-button")[0];
        Assert.True(name.HasAttribute("data-active"));
        Assert.Equal("Filter Name: ad", name.GetAttribute("aria-label"));
        Assert.Equal("Filter Status: No", cut.FindAll(".ib-table-filter-button")[2].GetAttribute("aria-label"));

        var tags = cut.Find(".ib-table-filter-tags");
        Assert.Contains("Name: ad", tags.TextContent);
        Assert.Contains("Status: No", tags.TextContent);
        var hrefs = tags.QuerySelectorAll("a").Select(a => a.GetAttribute("href")).ToList();
        Assert.Contains("http://localhost/orders?f-status=No&sort=name", hrefs);
        Assert.Contains("http://localhost/orders?sort=name", hrefs);
    }

    [Fact]
    public void Bound_header_filters_change_in_place_and_tags_remove_them()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        IReadOnlyDictionary<string, string>? reported = null;
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.Items, Rows)
            .Add(t => t.FiltersChanged, (IReadOnlyDictionary<string, string> f) => reported = f)
            .Add(t => t.ChildContent, Columns));

        cut.FindAll("button.ib-menu-item").First(b => b.TextContent.Trim() == "No").Click();
        Assert.Equal(new[] { "Grace" }, Names(cut));
        Assert.Equal("No", reported!["status"]);

        cut.Find(".ib-table-filter-tags .ib-btn").Click();   // Clear
        Assert.Equal(3, Names(cut).Length);
        Assert.Empty(reported!);
    }

    private IRenderedComponent<Table<Row>> RenderHeader(string url)
    {
        Nav.NavigateTo(url);
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        return Render<Table<Row>>(p => p.Add(t => t.Items, Rows).Add(t => t.ChildContent, Columns));
    }
}
