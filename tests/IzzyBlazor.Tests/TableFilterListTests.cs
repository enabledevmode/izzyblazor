using System.Linq.Expressions;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

// A choice column's list of checkboxes: its blank row, the none marker, and what script adds to it.
public class TableFilterListTests : BunitContext
{
    private static readonly TimeSpan Timer = TimeSpan.FromSeconds(5);

    public enum Size { A, B, C, D, E, F, G, H, I, J, K }

    public sealed record Row(string Name, Paid? Status, Size Size);

    private static readonly Row[] Rows =
    [
        new("Ada", Paid.Yes, Size.A),
        new("Grace", Paid.No, Size.B),
        new("Alan", null, Size.C),
    ];

    private BunitNavigationManager Nav => Services.GetRequiredService<BunitNavigationManager>();

    private static RenderFragment Columns => b =>
    {
        b.AddContent(0, Col<string>(r => r.Name, filterable: false));
        b.AddContent(1, Col<Paid?>(r => r.Status));
        b.AddContent(2, Col<Size>(r => r.Size));
    };

    private static RenderFragment Col<TValue>(Expression<Func<Row, TValue>> property, bool filterable = true) => b =>
    {
        b.OpenComponent<PropertyColumn<Row, TValue>>(0);
        b.AddComponentParameter(1, "Property", property);
        b.AddComponentParameter(2, "Filterable", filterable);
        b.CloseComponent();
    };

    private IRenderedComponent<Table<Row>> Static(string url = "/orders")
    {
        Nav.NavigateTo(url);
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        return Render<Table<Row>>(p => p.Add(t => t.Items, Rows).Add(t => t.ChildContent, Columns));
    }

    private IRenderedComponent<Table<Row>> Bound(Action<Filter?> reported, Filter? filter = null)
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.Items, Rows)
            .Add(t => t.Filter, filter)
            .Add(t => t.FilterChanged, reported)
            .Add(t => t.ChildContent, Columns));
        cut.Instance.FilterDelay = TimeSpan.FromMilliseconds(30);
        return cut;
    }

    private static string[] Names<T>(IRenderedComponent<T> cut) where T : IComponent =>
        cut.FindAll("tbody tr:not(.ib-table-empty) td:first-child").Select(c => c.TextContent.Trim()).ToArray();

    private static string Label(IRenderedComponent<Table<Row>> cut) => cut.FindAll(".ib-table-filter-button")[0].GetAttribute("aria-label")!;

    // Again if it fails: the filter applies on a timer, and its render can land between finding the box and ticking it.
    private static void Tick(IRenderedComponent<Table<Row>> cut, string row, bool on) => cut.WaitForAssertion(
        () => cut.FindAll("ib-filter-list .ib-filter-list-items label").First(l => l.TextContent.Trim() == row).QuerySelector("input")!.Change(on), Timer);

    [Fact]
    public void A_nullable_column_has_a_blank_row_under_its_own_key()
    {
        var cut = Static();

        var blank = cut.Find("input[name='f-status-blank']");
        Assert.Equal("1", blank.GetAttribute("value"));
        Assert.True(blank.HasAttribute("checked"));
        Assert.Equal("(Blank)", blank.ParentElement!.TextContent.Trim());
        Assert.Empty(cut.FindAll("input[name='f-size-blank']"));
    }

    [Theory]
    [InlineData("?f-status-blank=1", "(Blank)", new[] { "Alan" })]
    [InlineData("?f-status=Yes&f-status-blank=1", "2 selected", new[] { "Ada", "Alan" })]
    [InlineData("?f-status=Yes&f-status=No&f-status=Refunded", "3 selected", new[] { "Ada", "Grace" })]
    public void The_blank_row_means_no_value(string query, string summary, string[] expected)
    {
        var cut = Static("/orders" + query);

        Assert.Equal(expected, Names(cut));
        Assert.Equal($"Filter Status: {summary}", Label(cut));
    }

    [Fact]
    public void Every_row_ticked_with_the_blank_is_no_filter()
    {
        var cut = Static("/orders?f-status=Yes&f-status=No&f-status=Refunded&f-status-blank=1");

        Assert.Equal(3, Names(cut).Length);
        Assert.Equal("Filter Status", Label(cut));
    }

    [Fact]
    public void Nothing_ticked_keeps_no_rows_and_says_why()
    {
        var cut = Static("/orders?f-status-none=1");

        Assert.Empty(Names(cut));
        Assert.Equal("Filter Status: none", Label(cut));
        Assert.StartsWith("Nothing is selected under Status.", cut.Find(".ib-table-empty-content").TextContent.Trim());
        Assert.All(cut.FindAll("input[name='f-status'], input[name='f-status-blank']"), b => Assert.False(b.HasAttribute("checked")));
    }

    [Theory]
    [InlineData("?f-status=yes", "Yes")]
    [InlineData("?f-status=%20Yes%20", "Yes")]
    [InlineData("?f-status=0", "Yes")]
    [InlineData("?f-status=Yes&f-status=YES", "Yes")]
    [InlineData("?f-status=Yes&f-status=Bogus", "Yes")]
    [InlineData("?f-status-op=notin&f-status=Yes", "Yes")]
    public void An_address_written_by_hand_ticks_the_rows_it_means(string query, string summary)
    {
        var cut = Static("/orders" + query);

        Assert.Equal($"Filter Status: {summary}", Label(cut));
        Assert.Equal(new[] { "Ada" }, Names(cut));
        Assert.True(cut.Find("input[name='f-status'][value='Yes']").HasAttribute("checked"));
        Assert.False(cut.Find("input[name='f-status'][value='No']").HasAttribute("checked"));
    }

    [Fact]
    public void A_value_with_no_row_ticks_nothing()
    {
        var cut = Static("/orders?f-status=Bogus");

        Assert.Equal("Filter Status: none", Label(cut));
        Assert.Empty(Names(cut));
        Assert.StartsWith("Nothing is selected under Status.", cut.Find(".ib-table-empty-content").TextContent.Trim());
    }

    [Fact]
    public void A_tick_outweighs_the_none_marker()
    {
        var cut = Static("/orders?f-status-none=1&f-status=No");

        Assert.Equal(new[] { "Grace" }, Names(cut));
        Assert.Equal("Filter Status: No", Label(cut));
    }

    [Fact]
    public void Clear_removes_every_key_of_its_list()
    {
        var cut = Static("/orders?f-status=Yes&f-status-blank=1&tab=x");

        Assert.Equal("http://localhost/orders?tab=x", cut.Find("a[data-ib-filter-clear]").GetAttribute("href"));
    }

    [Fact]
    public void The_form_carries_the_address_without_the_lists_keys()
    {
        var cut = Static("/orders?f-status=Yes&f-status-blank=1&f-size-none=1&tab=x");

        var kept = cut.FindAll("input[type=hidden]").Select(i => i.GetAttribute("name")).ToArray();
        Assert.Equal(new[] { "tab" }, kept);
    }

    [Fact]
    public void A_static_list_says_its_none_key_and_a_bound_one_does_not()
    {
        Assert.Equal("f-status-none", Static().Find("ib-filter-list").GetAttribute("data-ib-none"));
        Assert.Null(Bound(_ => { }).Find("ib-filter-list").GetAttribute("data-ib-none"));
    }

    [Fact]
    public void Select_all_has_no_name_and_the_search_comes_with_more_than_ten_rows()
    {
        var cut = Static();
        var (status, size) = (cut.FindAll("ib-filter-list")[0], cut.FindAll("ib-filter-list")[1]);

        Assert.Null(status.QuerySelector("[data-ib-list-search]"));
        Assert.True(status.QuerySelector("[data-ib-list-all]")!.HasAttribute("autofocus"));
        Assert.False(status.QuerySelector("[data-ib-list-all]")!.HasAttribute("name"));

        var search = size.QuerySelector("[data-ib-list-search]")!;
        Assert.False(search.HasAttribute("name"));
        Assert.False(search.HasAttribute("form"));
        Assert.True(search.HasAttribute("autofocus"));
        Assert.False(size.QuerySelector("[data-ib-list-all]")!.HasAttribute("autofocus"));
    }

    [Fact]
    public void Bound_unticking_every_row_reports_a_filter_that_keeps_nothing()
    {
        Filter? reported = null;
        var cut = Bound(f => reported = f);

        foreach (var row in new[] { "Yes", "No", "Refunded", "(Blank)" })
        {
            Tick(cut, row, false);
        }

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(Names(cut));
            Assert.Empty(reported!.Find("status")!.Values);
            Assert.Equal("Filter Status: none", Label(cut));
        }, Timer);

        Tick(cut, "(Blank)", true);
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(new[] { "Alan" }, Names(cut));
            Assert.Equal(new object?[] { null }, reported!.Find("status")!.Values);
        }, Timer);
    }

    [Theory]
    [InlineData(new string[0], "Filter Status: none")]
    [InlineData(new[] { "No" }, "Filter Status: No")]
    public void A_bound_list_shows_a_filter_set_in_code(string[] values, string label)
    {
        var cut = Bound(_ => { }, Filter.In("status", values));

        Assert.Equal(label, Label(cut));
    }

    [Fact]
    public void A_bound_blank_shows_as_its_row()
    {
        var cut = Bound(_ => { }, Filter.In("status", [null]));

        Assert.Equal("Filter Status: (Blank)", Label(cut));
        Assert.Equal(new[] { "Alan" }, Names(cut));
    }

    [Fact]
    public void A_bound_list_with_every_row_is_no_filter()
    {
        var cut = Bound(_ => { }, Filter.In("status", ["Yes", "No", "Refunded", null]));

        Assert.Equal("Filter Status", Label(cut));
        Assert.Equal(3, Names(cut).Length);
        Assert.Empty(cut.FindAll(".ib-table-filter-button[data-active]"));
    }

    [Fact]
    public void A_blank_on_a_column_that_cant_be_null_ticks_nothing()
    {
        var cut = Bound(_ => { }, Filter.In("size", [null]));

        Assert.Equal("Filter Size: none", cut.FindAll(".ib-table-filter-button")[1].GetAttribute("aria-label"));
        Assert.Empty(Names(cut));
    }

    [Fact]
    public void A_panels_Clear_links_to_the_address_without_that_column()
    {
        var cut = Static("/orders?f-status=Yes&f-status-blank=1&f-size=A&tab=x");

        var clears = cut.FindAll("a[data-ib-filter-clear]");
        Assert.Equal(2, clears.Count);
        Assert.Equal("http://localhost/orders?f-size=A&tab=x", clears[0].GetAttribute("href"));
    }

    [Fact]
    public void A_panel_with_nothing_to_clear_has_its_Clear_disabled()
    {
        var cut = Static("/orders?f-size=A");

        var actions = cut.FindAll(".ib-table-filter-panel-actions");
        Assert.True(actions[0].QuerySelector("button[type=button]")!.HasAttribute("disabled"));
        Assert.NotNull(actions[1].QuerySelector("a[data-ib-filter-clear]"));
    }

    [Fact]
    public void A_bound_panels_Clear_removes_its_filter()
    {
        Filter? reported = Filter.In("status", ["No"]);
        var cut = Bound(f => reported = f, reported);

        cut.Find("button[data-ib-filter-clear]").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Null(reported);
            Assert.Equal(3, Names(cut).Length);
            Assert.Equal("Filter Status", Label(cut));
        }, Timer);
    }

    [Fact]
    public void A_table_draws_no_tags_for_what_its_funnels_say()
    {
        Assert.Empty(Static("/orders?f-status=Yes").FindAll(".ib-table-filter-tags"));
        Assert.Empty(Bound(_ => { }, Filter.In("status", ["Yes"])).FindAll(".ib-table-filter-tags"));
    }

    [Fact]
    public void A_condition_no_panel_shows_keeps_its_tag_above_the_table()
    {
        var cut = Bound(_ => { }, Filter.Any(Filter.Where("status", FilterOperator.Equals, "Yes"), Filter.Where("status", FilterOperator.Equals, "No")));

        Assert.Single(cut.FindAll(".ib-table-filter-tags .ib-tag"));
        Assert.Equal(new[] { "Ada", "Grace" }, Names(cut));
    }

    [Fact]
    public void A_condition_no_panel_shows_is_removed_by_its_own_tag_alone()
    {
        Filter? reported = Filter.All(
            Filter.In("size", ["A", "B"]),
            Filter.Any(Filter.Where("status", FilterOperator.Equals, "Yes"), Filter.Where("status", FilterOperator.Equals, "No")));
        var cut = Bound(f => reported = f, reported);

        Assert.Empty(cut.FindAll(".ib-table-filter-tags .ib-btn"));
        cut.Find(".ib-table-filter-tags .ib-tag button").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll(".ib-table-filter-tags .ib-tag"));
            Assert.NotNull(reported!.Find("size"));
        }, Timer);
    }

    [Fact]
    public void No_rows_under_a_filter_offers_the_way_back()
    {
        var cut = Static("/orders?f-size=K&tab=x");

        Assert.Empty(Names(cut));
        var clear = cut.Find(".ib-table-empty-clear a");
        Assert.Equal("Clear filters", clear.TextContent.Trim());
        Assert.Equal("http://localhost/orders?tab=x", clear.GetAttribute("href"));
    }

    [Fact]
    public void No_rows_with_no_filter_offers_nothing_to_clear()
    {
        Nav.NavigateTo("/orders");
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var cut = Render<Table<Row>>(p => p.Add(t => t.Items, []).Add(t => t.ChildContent, Columns));

        Assert.Empty(cut.FindAll(".ib-table-empty-clear"));
    }

    [Fact]
    public void A_filter_on_a_hidden_column_keeps_a_tag_on_a_grid()
    {
        Nav.NavigateTo("/orders?f-status=Yes");
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var cut = Render<DataGrid<Row>>(p => p
            .Add(t => t.Items, Rows)
            .Add(t => t.ItemKey, r => r.Name)
            .Add(t => t.Layout, new DataGridLayout { Columns = [new() { Key = "status", Hidden = true }] })
            .Add(t => t.ChildContent, Columns));

        Assert.Equal(new[] { "Ada" }, Names(cut));
        var tags = cut.Find(".ib-table-filter-tags");
        Assert.False(tags.HasAttribute("hidden"));
        Assert.Contains("Status: Yes", tags.TextContent);
        Assert.Equal("http://localhost/orders", tags.QuerySelector("a")!.GetAttribute("href"));
    }

    [Fact]
    public void A_grids_row_of_tags_is_there_but_hidden_while_every_filter_has_its_funnel()
    {
        var cut = Grid("/orders?f-status=Yes");

        Assert.True(cut.Find(".ib-table-filter-tags").HasAttribute("hidden"));
        Assert.Empty(cut.FindAll(".ib-table-filter-tags .ib-tag"));
    }

    private IRenderedComponent<DataGrid<Row>> Grid(string url, bool filterPanel = false)
    {
        Nav.NavigateTo(url);
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        return Render<DataGrid<Row>>(p => p.Add(t => t.Items, Rows).Add(t => t.ItemKey, r => r.Name).Add(t => t.FilterPanel, filterPanel).Add(t => t.ChildContent, Columns));
    }

    private IRenderedComponent<Table<Row>> WithPanel(string url)
    {
        Nav.NavigateTo(url);
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        return Render<Table<Row>>(p => p.Add(t => t.Items, Rows).Add(t => t.FilterPanel, true).Add(t => t.ChildContent, Columns));
    }

    [Fact]
    public void FilterPanel_is_there_with_nothing_filtered_and_comes_after_the_rows()
    {
        var cut = WithPanel("/orders");

        var row = cut.Find(".ib-table-filter-tags");
        Assert.False(row.HasAttribute("hidden"));
        Assert.Contains("No filters", row.TextContent);
        Assert.Empty(row.QuerySelectorAll(".ib-tag"));
        Assert.True(row.QuerySelector("button")!.HasAttribute("disabled"));
        Assert.NotNull(cut.Find(".ib-table-scroll ~ .ib-table-filter-tags"));
    }

    [Fact]
    public void FilterPanel_lists_each_filter_to_remove_or_clear()
    {
        var cut = WithPanel("/orders?f-status=Yes&f-size=A&tab=x");

        var row = cut.Find(".ib-table-filter-tags");
        Assert.DoesNotContain("No filters", row.TextContent);
        var tags = row.QuerySelectorAll(".ib-tag");
        Assert.Equal(2, tags.Length);
        Assert.Contains("Status: Yes", tags[0].TextContent);
        Assert.Equal("http://localhost/orders?f-size=A&tab=x", tags[0].QuerySelector("a")!.GetAttribute("href"));
        Assert.Equal("http://localhost/orders?tab=x", row.QuerySelector("a[data-ib-filter-clear]")!.GetAttribute("href"));
    }

    [Fact]
    public void With_FilterPanel_no_rows_leaves_Clear_to_it()
    {
        var cut = WithPanel("/orders?f-size=K");

        Assert.Empty(cut.FindAll(".ib-table-empty-clear"));
        Assert.Single(cut.FindAll(".ib-table-filter-tags a[data-ib-filter-clear]"));
    }

    [Fact]
    public void A_grid_has_FilterPanel_too()
    {
        var cut = Grid("/orders?f-status=Yes", filterPanel: true);

        Assert.Contains("Status: Yes", cut.Find(".ib-table-filter-tags .ib-tag").TextContent);
    }

    [Fact]
    public void A_filter_link_keeps_the_meaning_of_nothing_ticked()
    {
        var request = new DataRequest<Row> { Filter = Filter.Parse(Filter.In("status", []).ToString()) };

        Assert.Empty(request.Apply(Rows.AsQueryable(), DataFields.Of<Row>()));
    }
}
