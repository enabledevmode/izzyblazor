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

    private static string[] Names(IRenderedComponent<Table<Row>> cut) =>
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
        Assert.True(cut.Find(".ib-table-filter-tags").HasAttribute("hidden"));
    }

    [Fact]
    public void Nothing_ticked_keeps_no_rows_and_says_why()
    {
        var cut = Static("/orders?f-status-none=1");

        Assert.Empty(Names(cut));
        Assert.Equal("Filter Status: none", Label(cut));
        Assert.Contains("Status: none", cut.Find(".ib-table-filter-tags .ib-tag").TextContent);
        Assert.Equal("Nothing is selected under Status.", cut.Find(".ib-table-empty-content").TextContent.Trim());
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
        Assert.Equal("Nothing is selected under Status.", cut.Find(".ib-table-empty-content").TextContent.Trim());
    }

    [Fact]
    public void A_tick_outweighs_the_none_marker()
    {
        var cut = Static("/orders?f-status-none=1&f-status=No");

        Assert.Equal(new[] { "Grace" }, Names(cut));
        Assert.Equal("Filter Status: No", Label(cut));
    }

    [Fact]
    public void A_tag_removes_every_key_of_its_list()
    {
        var cut = Static("/orders?f-status=Yes&f-status-blank=1&tab=x");

        Assert.Equal("http://localhost/orders?tab=x", cut.Find(".ib-table-filter-tags .ib-tag a").GetAttribute("href"));
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
        Assert.True(cut.Find(".ib-table-filter-tags").HasAttribute("hidden"));
    }

    [Fact]
    public void A_blank_on_a_column_that_cant_be_null_ticks_nothing()
    {
        var cut = Bound(_ => { }, Filter.In("size", [null]));

        Assert.Equal("Filter Size: none", cut.FindAll(".ib-table-filter-button")[1].GetAttribute("aria-label"));
        Assert.Empty(Names(cut));
    }

    [Fact]
    public void A_filter_link_keeps_the_meaning_of_nothing_ticked()
    {
        var request = new DataRequest<Row> { Filter = Filter.Parse(Filter.In("status", []).ToString()) };

        Assert.Empty(request.Apply(Rows.AsQueryable(), DataFields.Of<Row>()));
    }
}
