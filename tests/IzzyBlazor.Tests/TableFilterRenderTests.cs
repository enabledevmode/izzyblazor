using System.Linq.Expressions;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

public enum Paid { Yes, No, Refunded }

// Filterable columns: query-string filters on static pages, component state when bound. Each
// filter is a button in its column's header opening a popover with a list (choices) or fields.
public class TableFilterRenderTests : BunitContext
{
    // Filters wait for a pause in typing (a timer): room for a slow CI machine.
    private static readonly TimeSpan Timer = TimeSpan.FromSeconds(5);

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
        return Render<Table<Row>>(p => p.Add(t => t.Items, Rows).Add(t => t.ChildContent, Columns));
    }

    private static string[] Names(IRenderedComponent<Table<Row>> cut) =>
        cut.FindAll("tbody tr:not(.ib-table-empty)").Select(r => r.Children[0].TextContent).ToArray();

    [Fact]
    public void Filter_fields_are_joined_to_a_get_form()
    {
        var cut = RenderStatic();

        var form = cut.Find("form.ib-table-filter-form");
        Assert.Equal("get", form.GetAttribute("method"));
        Assert.Equal("/orders", form.GetAttribute("action"));

        var name = cut.Find("input[name='f-name']");
        Assert.Equal("search", name.GetAttribute("type"));
        Assert.Equal(form.Id, name.GetAttribute("form"));

        // A number is a range: two fields, Min and Max, each posting under its own key.
        var min = cut.Find("input[name='f-qty']");
        var max = cut.Find("input[name='f-qty-to']");
        Assert.All(new[] { min, max }, i => Assert.Equal("decimal", i.GetAttribute("inputmode")));
        Assert.Equal("Min", cut.Find($"label[for='{min.Id}']").TextContent);
        Assert.Equal("Max", cut.Find($"label[for='{max.Id}']").TextContent);
        var group = min.Closest("[role=group]")!;
        Assert.Equal("Filter Qty", cut.Find($"#{group.GetAttribute("aria-labelledby")}").TextContent);

        // Choices are menus, not fields.
        Assert.Empty(cut.FindAll("select"));
        Assert.All(cut.FindAll("button[type=submit]"), b => Assert.Equal(form.Id, b.GetAttribute("form")));
    }

    [Theory]
    [InlineData("?f-name=AD", new[] { "Ada", "adam" })]
    [InlineData("?f-name=ad&f-shipped=true", new[] { "Ada", "adam" })]
    [InlineData("?f-status=no", new[] { "Grace" })]
    [InlineData("?f-qty=3&f-qty-to=3", new[] { "Grace" })]
    [InlineData("?f-qty=2", new[] { "Ada", "Grace" })]
    [InlineData("?f-qty-to=2", new[] { "Ada", "adam" })]
    [InlineData("?f-qty=1.5&f-qty-to=2.5", new[] { "Ada" })]
    [InlineData("?f-qty=3&f-qty-to=1", new string[0])]
    [InlineData("?f-qty=abc", new string[0])]
    [InlineData("?f-qty=&f-qty-to=", new[] { "Ada", "Grace", "adam" })]
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
        var cut = RenderStatic("/orders?sort=name&page=2&tab=x&f-name=ad&f-status=Yes&f-qty=1");

        Assert.Equal("ad", cut.Find("input[name='f-name']").GetAttribute("value"));
        Assert.Equal("1", cut.Find("input[name='f-qty']").GetAttribute("value"));
        Assert.Equal("", cut.Find("input[name='f-qty-to']").GetAttribute("value") ?? "");
        var kept = cut.FindAll("input[type=hidden]").Select(i => $"{i.GetAttribute("name")}={i.GetAttribute("value")}").ToArray();
        Assert.Equal(new[] { "sort=name", "tab=x" }, kept);
        Assert.True(cut.Find("input[name='f-status'][value='Yes']").HasAttribute("checked"));
        Assert.False(cut.Find("input[name='f-status'][value='No']").HasAttribute("checked"));
    }

    // Each value stays whole, however it's punctuated, through the address and back.
    [Theory]
    [InlineData("Smith, John")]
    [InlineData("a&b=c", "d+e", "100%")]
    public void A_text_filter_round_trips_through_the_address(params string[] values)
    {
        var rows = values.Select(v => new Row(v, 1, Paid.Yes, true, new(2026, 1, 1))).Append(new("Other", 1, Paid.No, true, new(2026, 1, 1))).ToArray();
        var query = string.Join('&', values.Select(v => $"f-name={Uri.EscapeDataString(v)}"));
        Nav.NavigateTo($"/orders?{query}&sort=name");
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        DataRequest<Row>? seen = null;
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.ItemsProvider, request =>
            {
                seen = request;
                return ValueTask.FromResult(request.ToResult(rows.AsQueryable()));
            })
            .Add(t => t.PageSize, 10)
            .Add(t => t.ChildContent, Columns));

        // The rows and the condition, and the condition's text for an export link.
        Assert.Equal(values.Order(StringComparer.OrdinalIgnoreCase), Names(cut));
        var condition = seen!.Filter!.Find("name")!;
        Assert.Equal(values, values.Length == 1 ? [(string)condition.Value!] : condition.Values.Cast<string>().ToArray());
        var back = DataRequest<Row>.Parse(seen.ToQueryString()).ApplyFiltering(rows.AsQueryable(), DataFields.Of<Row>()).Select(r => r.Name).ToArray();
        Assert.Equal(values.Order(StringComparer.OrdinalIgnoreCase), back.Order(StringComparer.OrdinalIgnoreCase));

        // The reader and the writer agree: what's written reads back, and it's the address it came from.
        var column = cut.Instance.Columns[0];
        var read = ColumnFilter.Read(column, "f-name", QueryString.Of(Nav.Uri))!;
        Assert.Equal(values, read.Values);
        var written = Assert.IsType<string[]>(ColumnFilter.Write(column, "f-name", read)["f-name"]);
        Assert.Equal(values, written);
        Assert.Equal(values, ColumnFilter.Read(column, "f-name", QueryString.Parse(string.Join('&', written.Select(v => $"f-name={Uri.EscapeDataString(v)}"))))!.Values);

        // Clear's link clears all of them, and leaves the rest.
        Assert.Contains("http://localhost/orders?sort=name", cut.FindAll("a[data-ib-filter-clear]").Select(a => a.GetAttribute("href")));
    }

    // A day stays a day for a column's own condition, whatever the field's type holds.
    [Fact]
    public void A_custom_condition_on_a_date_column_gets_a_day()
    {
        Nav.NavigateTo("/orders?f-when=2026-01-02");
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        FieldFilter? seen = null;
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.Items, Rows)
            .Add(t => t.ChildContent, b =>
            {
                b.AddContent(0, Col<string>(r => r.Name));
                b.OpenComponent<PropertyColumn<Row, DateTime>>(1);
                b.AddComponentParameter(2, "Property", (Expression<Func<Row, DateTime>>)(r => r.When));
                b.AddComponentParameter(3, "Filterable", true);
                b.AddComponentParameter(4, "Filter", (Func<FieldFilter, Expression<Func<Row, bool>>?>)(f =>
                {
                    seen = f;
                    return null;
                }));
                b.CloseComponent();
            }));

        Assert.Equal(new DateOnly(2026, 1, 2), seen!.Value);
        Assert.Equal(new[] { "Grace" }, Names(cut));
    }

    [Theory]
    [InlineData(FilterOperator.OnDay)]
    [InlineData(FilterOperator.Equals)]
    public void A_day_set_in_code_shows_in_the_date_panel(FilterOperator op)
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.Items, Rows)
            .Add(t => t.Filter, Filter.Where("when", op, new DateOnly(2026, 1, 2)))
            .Add(t => t.FilterChanged, (Filter? _) => { })
            .Add(t => t.ChildContent, b =>
            {
                b.AddContent(0, Col<string>(r => r.Name));
                b.AddContent(1, Col<DateTime>(r => r.When));
            }));

        Assert.Equal(new[] { "Grace" }, Names(cut));
        Assert.Equal("2026-01-02", cut.Find("input[type=date]").GetAttribute("value"));
        Assert.Equal("Filter When: 2026-01-02", cut.FindAll(".ib-table-filter-button")[1].GetAttribute("aria-label"));
    }

    [Theory]
    [InlineData("when", "2026-01-02", new[] { "Grace" })]
    [InlineData("when", "2026-01-03", new[] { "adam" })]
    [InlineData("day", "2026-01-01", new[] { "Ada" })]
    [InlineData("maybe", "2026-01-02", new[] { "Grace" })]
    [InlineData("stamp", "2026-01-02", new[] { "Grace" })]
    [InlineData("when", "not a date", new string[0])]
    public void Date_filters_keep_that_day(string key, string value, string[] expected)
    {
        Nav.NavigateTo($"/orders?f-{key}={Uri.EscapeDataString(value)}");
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var cut = Render<Table<Row>>(p => p
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
        var cut = Render<Table<Row>>(p => p
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
    public void Items_provider_gets_the_filters()
    {
        Nav.NavigateTo("/orders?f-name=gr");
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        DataRequest<Row>? seen = null;
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.ItemsProvider, request =>
            {
                seen = request;
                var query = Rows.AsQueryable();
                return ValueTask.FromResult(new DataResult<Row>(request.Apply(query).ToList(), request.ApplyFiltering(query).Count()));
            })
            .Add(t => t.PageSize, 10)
            .Add(t => t.ChildContent, Columns));

        Assert.Equal(new FieldFilter("name", FilterOperator.Contains, "gr"), seen!.Filter!.Find("name"));
        Assert.Equal(new[] { "Grace" }, Names(cut));
        Assert.Contains("1–1 of 1", cut.Find(".ib-data-summary").TextContent);
    }

    [Fact]
    public void Bound_filters_apply_in_place_after_a_pause_and_go_back_to_page_one()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var reported = new List<Filter?>();
        var pages = new List<int>();
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.Items, Rows)
            .Add(t => t.PageSize, 1)
            .Add(t => t.FilterChanged, (Filter? f) => reported.Add(f))
            .Add(t => t.PageChanged, (int page) => pages.Add(page))
            .Add(t => t.ChildContent, Columns));
        cut.Instance.FilterDelay = TimeSpan.FromMilliseconds(1);
        var url = Nav.Uri;

        cut.FindAll(".ib-data-footer button").First(b => b.TextContent.Trim().EndsWith('2')).Click();
        Assert.Empty(cut.FindAll("[data-ib-filter]"));
        cut.Find(".ib-table-filter input[type=search]").Input("ad");

        // The filter applies on a timer, so wait for all of its effects, not just the rows.
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(new[] { "Ada" }, Names(cut));
            Assert.Equal("ad", Assert.Single(reported)!.Find("name")!.Value);
            Assert.Equal(new[] { 2, 1 }, pages);
            Assert.Contains("1–1 of 2", cut.Find(".ib-data-summary").TextContent);
        }, Timer);
        Assert.Equal(url, Nav.Uri);

        Tick(cut, "Yes", false);
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(new[] { "adam" }, Names(cut));
            Assert.Equal(new object?[] { Paid.No, Paid.Refunded }, reported.Last()!.Find("status")!.Values);
        }, Timer);
    }

    [Fact]
    public void Bound_number_filter_puts_its_two_ends_together()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        Filter? reported = null;
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.Items, Rows)
            .Add(t => t.FilterChanged, (Filter? f) => reported = f)
            .Add(t => t.ChildContent, Columns));
        // Both ends typed within one pause: the filter applies once, with both.
        cut.Instance.FilterDelay = TimeSpan.FromMilliseconds(200);

        cut.FindAll("[role=group] input")[0].Input("2");
        cut.FindAll("[role=group] input")[1].Input("2");
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(new FieldFilter("qty", FilterOperator.Between, 2, 2), reported!.Find("qty"));
            Assert.Equal(new[] { "Ada" }, Names(cut));
            Assert.Equal("Filter Qty: 2", cut.FindAll(".ib-table-filter-button")[1].GetAttribute("aria-label"));
        }, Timer);
    }

    // Unticks or ticks a bound list's row.
    // Again if it fails: the filter applies on a timer, and its render can land between finding the box and ticking it.
    private static void Tick(IRenderedComponent<Table<Row>> cut, string row, bool on) => cut.WaitForAssertion(
        () => cut.FindAll("ib-filter-list .ib-filter-list-items label").First(l => l.TextContent.Trim() == row).QuerySelector("input")!.Change(on), Timer);

    [Theory]
    [InlineData("", "All", new[] { "Ada", "Grace", "adam" })]
    [InlineData("?f-status=No", "No", new[] { "Grace" })]
    [InlineData("?f-status=Yes&f-status=Refunded", "2 selected", new[] { "Ada", "adam" })]
    [InlineData("?f-status=Yes&f-status=No&f-status=Refunded", "All", new[] { "Ada", "Grace", "adam" })]
    public void A_list_keeps_any_ticked_value_and_all_ticked_is_no_filter(string query, string summary, string[] expected)
    {
        var cut = RenderStatic("/orders" + query);

        Assert.Equal(expected, Names(cut));
        Assert.Equal(summary == "All" ? "Filter Status" : $"Filter Status: {summary}", cut.FindAll(".ib-table-filter-button")[2].GetAttribute("aria-label"));
    }

    [Fact]
    public void Unbound_filters_on_an_interactive_page_use_the_url()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var cut = Render<Table<Row>>(p => p.Add(t => t.Items, Rows).Add(t => t.ChildContent, Columns));

        Assert.Equal(8, cut.FindAll("[data-ib-filter]").Count);   // Name, Qty's two ends, and the lists' rows
        Assert.Single(cut.FindAll("form.ib-table-filter-form"));
    }

    [Fact]
    public void Header_placement_marks_filtered_columns_and_each_panel_clears_its_own()
    {
        var cut = RenderHeader("/orders?f-name=ad&f-status=No&sort=name");

        Assert.Empty(Names(cut));
        var name = cut.FindAll(".ib-table-filter-button")[0];
        Assert.True(name.HasAttribute("data-active"));
        Assert.Equal("Filter Name: ad", name.GetAttribute("aria-label"));
        Assert.Equal("Filter Status: No", cut.FindAll(".ib-table-filter-button")[2].GetAttribute("aria-label"));

        var hrefs = cut.FindAll("a[data-ib-filter-clear]").Select(a => a.GetAttribute("href")).ToList();
        Assert.Contains("http://localhost/orders?f-status=No&sort=name", hrefs);
        Assert.Contains("http://localhost/orders?f-name=ad&sort=name", hrefs);
    }

    [Theory]
    [InlineData("?f-qty=2&f-qty-to=3", "2–3")]
    [InlineData("?f-qty=2", "2 or more")]
    [InlineData("?f-qty-to=3", "3 or less")]
    public void Number_filter_reads_as_a_range_and_Clear_clears_both_ends(string query, string summary)
    {
        var cut = RenderHeader("/orders" + query + "&sort=name");

        Assert.Equal($"Filter Qty: {summary}", cut.FindAll(".ib-table-filter-button")[1].GetAttribute("aria-label"));
        Assert.Contains("http://localhost/orders?sort=name", cut.FindAll("a[data-ib-filter-clear]").Select(a => a.GetAttribute("href")));
    }

    [Fact]
    public void Bound_header_filters_change_in_place_and_Clear_removes_them()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        Filter? reported = null;
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.Items, Rows)
            .Add(t => t.FilterChanged, (Filter? f) => reported = f)
            .Add(t => t.ChildContent, Columns));

        cut.Instance.FilterDelay = TimeSpan.FromMilliseconds(1);
        Tick(cut, "Yes", false);
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(new[] { "Grace", "adam" }, Names(cut));
            Assert.Equal(new object?[] { Paid.No, Paid.Refunded }, reported!.Find("status")!.Values);
        }, Timer);

        cut.Find("button[data-ib-filter-clear]").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(3, Names(cut).Length);
            Assert.Null(reported);
        }, Timer);
    }

    private IRenderedComponent<Table<Row>> RenderHeader(string url)
    {
        Nav.NavigateTo(url);
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        return Render<Table<Row>>(p => p.Add(t => t.Items, Rows).Add(t => t.ChildContent, Columns));
    }
}
