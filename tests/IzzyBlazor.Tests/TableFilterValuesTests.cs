using System.Linq.Expressions;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

// A list of looked-up values on an interactive page: when it asks, what it asks for, and what ticks write.
public class TableFilterValuesTests : BunitContext
{
    private static readonly TimeSpan Timer = TimeSpan.FromSeconds(5);

    public sealed record Row(string? Name, string City, int Id);

    private static readonly Row[] Rows =
    [
        new("Ada", "Oslo", 1),
        new("Grace", "Rome", 2),
        new("Alan", "Oslo", 3),
        new(null, "Rome", 4),
        new("", "Lima", 5),
    ];

    private BunitNavigationManager Nav => Services.GetRequiredService<BunitNavigationManager>();

    private readonly List<FilterValuesRequest> _asked = [];

    private ValueTask<FilterValuesResult> Counting(FilterValuesRequest request)
    {
        _asked.Add(request);
        return ValueTask.FromResult(request.ToResult(Rows.AsQueryable(), DataFields.Of<Row>()));
    }

    private static RenderFragment Col<TValue>(Expression<Func<Row, TValue>> property, FilterValuesLoader? loader = null, bool list = false) => b =>
    {
        b.OpenComponent<PropertyColumn<Row, TValue>>(0);
        b.AddComponentParameter(1, "Property", property);
        b.AddComponentParameter(2, "Filterable", true);
        b.AddComponentParameter(3, "FilterValues", loader);
        b.AddComponentParameter(4, "FilterList", list);
        b.CloseComponent();
    };

    private RenderFragment Columns => b =>
    {
        b.AddContent(0, Col<string?>(r => r.Name, Counting));
        b.AddContent(1, Col<string>(r => r.City, list: true));
    };

    private IRenderedComponent<Table<Row>> Bound(Action<Filter?> reported, Filter? filter = null, RenderFragment? columns = null)
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.Items, Rows)
            .Add(t => t.Filter, filter)
            .Add(t => t.FilterChanged, reported)
            .Add(t => t.Columns, columns ?? Columns));
        cut.Instance.FilterDelay = TimeSpan.FromMilliseconds(30);
        return cut;
    }

    private IRenderedComponent<Table<Row>> Unbound(string url = "/orders")
    {
        Nav.NavigateTo(url);
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var cut = Render<Table<Row>>(p => p.Add(t => t.Items, Rows).Add(t => t.Columns, Columns));
        cut.Instance.FilterDelay = TimeSpan.FromMilliseconds(30);
        return cut;
    }

    private static string[] Names(IRenderedComponent<Table<Row>> cut) =>
        cut.FindAll("tbody tr:not(.ib-table-empty) td:first-child").Select(c => c.TextContent.Trim()).ToArray();

    // Again if it fails, as with a tick: a render on a timer can land between finding the panel and telling it.
    private static void Toggle(IRenderedComponent<Table<Row>> cut, int column, bool open) => cut.WaitForAssertion(() =>
    {
        var panel = cut.FindAll("[popover]")[column];
        panel.TriggerEvent("onibtoggle", new IzzyToggleEventArgs { Id = panel.Id!, Open = open, Seq = open ? 1 : 2 });
    }, Timer);

    private static string[] Listed(IRenderedComponent<Table<Row>> cut, int column) =>
        cut.FindAll("[popover]")[column].QuerySelectorAll("ib-filter-list .ib-filter-list-items label").Select(l => l.TextContent.Trim()).ToArray();

    private static void Tick(IRenderedComponent<Table<Row>> cut, int column, string row, bool on) => cut.WaitForAssertion(
        () => cut.FindAll("[popover]")[column].QuerySelectorAll("ib-filter-list .ib-filter-list-items label").First(l => l.TextContent.Trim() == row).QuerySelector("input")!.Change(on), Timer);

    private static bool Ticked(IRenderedComponent<Table<Row>> cut, int column, string row) =>
        cut.FindAll("[popover]")[column].QuerySelectorAll("ib-filter-list .ib-filter-list-items label").First(l => l.TextContent.Trim() == row).QuerySelector("input")!.HasAttribute("checked");

    [Fact]
    public void A_render_with_the_panel_closed_asks_for_nothing()
    {
        var cut = Bound(_ => { });
        cut.Render();

        Assert.Empty(_asked);
        Assert.Empty(Listed(cut, 0));
    }

    private IRenderedComponent<Table<Row>> Static(string url, RenderFragment? columns = null)
    {
        Nav.NavigateTo(url);
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        return Render<Table<Row>>(p => p.Add(t => t.Items, Rows).Add(t => t.Columns, columns ?? Columns));
    }

    private static IEnumerable<string?> Answered(IRenderedComponent<Table<Row>> cut, string key) =>
        cut.Find($"ib-filter-list[data-ib-list-key='{key}']").QuerySelectorAll(".ib-filter-list-items > label input")
            .Select(i => i.HasAttribute("data-ib-blank") ? null : i.GetAttribute("value"));

    [Fact]
    public void A_static_page_draws_an_empty_list_for_the_script_and_the_usual_panel_for_no_script()
    {
        var cut = Static("/orders");

        Assert.Empty(_asked);
        var list = cut.Find("ib-filter-list[data-ib-list-key='f-name']");
        Assert.Equal("f-name", list.GetAttribute("data-permanent"));
        Assert.False(list.HasAttribute("data-ib-answered"));
        Assert.Empty(Answered(cut, "f-name"));
        Assert.NotNull(cut.Find(".ib-filter-with-script"));
        Assert.NotNull(cut.Find(".ib-filter-no-script input[name='f-name']"));
        Assert.Equal(5, Names(cut).Length);
    }

    [Fact]
    public void The_script_asking_gets_the_list_and_no_rows()
    {
        var cut = Static("/orders?fv=name");

        Assert.Equal(new string?[] { null, "Ada", "Alan", "Grace" }, Answered(cut, "f-name"));
        Assert.True(cut.Find("ib-filter-list[data-ib-list-key='f-name']").HasAttribute("data-ib-answered"));
        Assert.True(cut.Find("ib-filter-list[data-ib-list-key='f-name']").HasAttribute("data-ib-whole"));
        Assert.Empty(Answered(cut, "f-city"));
        Assert.Empty(Names(cut));
    }

    [Fact]
    public void Asking_doesnt_run_the_tables_query()
    {
        var loads = 0;
        Nav.NavigateTo("/orders?fv=name");
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        Render<Table<Row>>(p => p
            .Add(t => t.ItemsProvider, r => { loads++; return ValueTask.FromResult(new DataResult<Row>([], 0)); })
            .Add(t => t.Columns, (RenderFragment)(b => b.AddContent(0, Col<string?>(r => r.Name, Counting)))));

        Assert.Equal(0, loads);
        Assert.Single(_asked);
    }

    [Theory]
    [InlineData("/orders?fv=nope")]
    public void A_key_naming_no_list_is_only_a_page(string url)
    {
        var cut = Static(url);

        Assert.Empty(_asked);
        Assert.Equal(5, Names(cut).Length);
    }

    [Fact]
    public void Asking_carries_the_search_page_and_filters_from_the_address()
    {
        var cut = Static("/orders?fv=name&fv-search=a&fv-skip=1&f-city-op=in&f-city=Oslo&f-name-op=notin&f-name=Ada");

        var request = Assert.Single(_asked);
        Assert.Equal("a", request.Search);
        Assert.Equal(1, request.Skip);
        Assert.Equal("city in ('Oslo')", request.OtherFilters?.ToString());
        Assert.Equal(new[] { "Ada" }, request.Unticked);
        Assert.False(cut.Find("ib-filter-list[data-ib-list-key='f-name']").HasAttribute("data-ib-whole"));
    }

    [Fact]
    public void An_answer_with_more_says_where_to_go_on()
    {
        static ValueTask<FilterValuesResult> Pages(FilterValuesRequest r) =>
            ValueTask.FromResult(new FilterValuesResult(Enumerable.Range(r.Skip, r.Take).Select(i => new FilterValue($"v{i:000}")).ToList(), true));

        var cut = Static("/orders?fv=name&fv-skip=50", b => b.AddContent(0, Col<string?>(r => r.Name, Pages)));

        var list = cut.Find("ib-filter-list[data-ib-list-key='f-name']");
        Assert.Equal("100", list.GetAttribute("data-ib-next"));
        Assert.Equal("v050", Answered(cut, "f-name").First());
        Assert.NotNull(list.QuerySelector("[data-ib-list-more]"));
        Assert.NotNull(list.QuerySelector("[data-ib-load-next]"));
    }

    [Fact]
    public void A_loader_that_fails_says_so_in_its_answer_and_the_page_still_renders()
    {
        static ValueTask<FilterValuesResult> Down(FilterValuesRequest r) => throw new InvalidOperationException("down");

        var cut = Static("/orders?fv=name", b => b.AddContent(0, Col<string?>(r => r.Name, Down)));

        Assert.True(cut.Find("ib-filter-list[data-ib-list-key='f-name']").HasAttribute("data-ib-load-failed"));
    }

    [Fact]
    public void The_list_is_narrowed_by_the_other_filters_and_not_its_own()
    {
        var cut = Bound(_ => { }, Filter.All(Filter.Where("city", FilterOperator.In, new List<object?> { "Oslo" }), Filter.Where("name", FilterOperator.In, new List<object?> { "Ada" })));

        Toggle(cut, 0, open: true);

        var request = Assert.Single(_asked);
        Assert.Equal("name", request.Field);
        Assert.Equal("city in ('Oslo')", request.OtherFilters?.ToString());
        Assert.Equal(new[] { "Ada" }, request.Ticked);
        Assert.Equal(new[] { "Ada", "Alan" }, Listed(cut, 0));
    }

    [Fact]
    public void Unticking_from_all_ticked_writes_what_is_unticked()
    {
        Filter? reported = null;
        var cut = Bound(f => reported = f);
        Toggle(cut, 0, open: true);

        Tick(cut, 0, "Ada", false);

        cut.WaitForAssertion(() => Assert.Equal(FilterOperator.NotIn, (reported as FieldFilter)?.Operator ?? Assert.IsType<FilterGroup>(reported).Filters.OfType<FieldFilter>().Single().Operator), Timer);
        cut.WaitForAssertion(() => Assert.DoesNotContain("Ada", Names(cut)), Timer);
        Assert.Contains("Filter Name: All but Ada", cut.FindAll(".ib-table-filter-button")[0].GetAttribute("title"));
    }

    [Fact]
    public void Clearing_select_all_takes_in_rows_not_loaded_and_keeps_none()
    {
        var cut = Bound(_ => { });
        Toggle(cut, 0, open: true);

        cut.FindAll("[popover]")[0].QuerySelector("[data-ib-list-all]")!.Change(false);

        cut.WaitForAssertion(() => Assert.Empty(Names(cut)), Timer);
        Assert.Equal("off", cut.FindAll("ib-filter-list")[0].GetAttribute("data-ib-all"));
    }

    [Fact]
    public void The_blank_row_means_null_or_empty_text()
    {
        var cut = Bound(_ => { });
        Toggle(cut, 0, open: true);
        cut.FindAll("[popover]")[0].QuerySelector("[data-ib-list-all]")!.Change(false);
        cut.WaitForAssertion(() => Assert.Empty(Names(cut)), Timer);

        Tick(cut, 0, "(Blank)", true);

        cut.WaitForAssertion(() => Assert.Equal(2, Names(cut).Length), Timer);
    }

    [Fact]
    public void Load_more_adds_the_next_page_after_these_with_no_repeats()
    {
        ValueTask<FilterValuesResult> Pages(FilterValuesRequest r)
        {
            _asked.Add(r);
            var all = Enumerable.Range(0, 120).Select(i => new FilterValue($"v{i:000}")).ToList();
            return ValueTask.FromResult(new FilterValuesResult(all.Skip(r.Skip).Take(r.Take).ToList(), r.Skip + r.Take < all.Count));
        }

        var cut = Bound(_ => { }, columns: b => b.AddContent(0, Col<string?>(r => r.Name, Pages)));
        Toggle(cut, 0, open: true);
        Assert.Equal(50, Listed(cut, 0).Length);

        cut.Find("[data-ib-load-next]").Click();
        Assert.Equal(50, _asked.Last().Skip);
        Assert.Equal(100, Listed(cut, 0).Distinct().Count());

        cut.Find("[data-ib-load-next]").Click();
        Assert.Equal(120, Listed(cut, 0).Length);
        Assert.Empty(cut.FindAll("[data-ib-load-next]"));
    }

    [Fact]
    public void A_long_list_stops_at_its_limit_and_asks_for_more_typing()
    {
        static ValueTask<FilterValuesResult> Many(FilterValuesRequest r) =>
            ValueTask.FromResult(new FilterValuesResult(Enumerable.Range(r.Skip, 300).Select(i => new FilterValue($"v{i:0000}")).ToList(), true));

        var cut = Bound(_ => { }, columns: b => b.AddContent(0, Col<string?>(r => r.Name, Many)));
        Toggle(cut, 0, open: true);

        cut.Find("[data-ib-load-next]").Click();

        Assert.Equal(500, Listed(cut, 0).Length);
        Assert.Empty(cut.FindAll("[data-ib-load-next]"));
        Assert.Equal("Keep typing to see more", cut.Find(".ib-filter-list-status").TextContent);
    }

    [Fact]
    public void A_list_holding_its_limit_disables_what_would_add_one()
    {
        var values = Enumerable.Range(0, ColumnFilter.MaxListValues).Select(i => (object?)$"v{i}").ToList();
        ValueTask<FilterValuesResult> Rows100(FilterValuesRequest r) =>
            ValueTask.FromResult(new FilterValuesResult([new FilterValue("v0"), new FilterValue("extra")]));

        var cut = Bound(_ => { }, new FieldFilter("name", FilterOperator.In, values), b => b.AddContent(0, Col<string?>(r => r.Name, Rows100)));
        Toggle(cut, 0, open: true);

        var boxes = cut.FindAll("ib-filter-list .ib-filter-list-items input");
        Assert.False(boxes.First(b => b.HasAttribute("checked")).HasAttribute("disabled"));
        Assert.True(boxes.First(b => !b.HasAttribute("checked")).HasAttribute("disabled"));
        Assert.Contains("100", cut.Find(".ib-filter-list-status").TextContent);
    }

    [Fact]
    public void A_loader_that_leaves_a_ticked_value_out_still_lists_it()
    {
        static ValueTask<FilterValuesResult> Forgets(FilterValuesRequest r) => ValueTask.FromResult(new FilterValuesResult([new FilterValue("Grace")]));

        var cut = Bound(_ => { }, new FieldFilter("name", FilterOperator.In, new List<object?> { "Ada" }), b => b.AddContent(0, Col<string?>(r => r.Name, Forgets)));
        Toggle(cut, 0, open: true);

        Assert.Equal(new[] { "Ada", "Grace" }, Listed(cut, 0));
        Assert.True(Ticked(cut, 0, "Ada"));
        Assert.False(Ticked(cut, 0, "Grace"));
    }

    [Fact]
    public void A_loader_that_fails_says_so_and_Retry_asks_again()
    {
        var fail = true;
        ValueTask<FilterValuesResult> Flaky(FilterValuesRequest r) =>
            fail ? throw new InvalidOperationException("down") : ValueTask.FromResult(new FilterValuesResult([new FilterValue("Ada")]));

        var cut = Bound(_ => { }, columns: b => b.AddContent(0, Col<string?>(r => r.Name, Flaky)));
        Toggle(cut, 0, open: true);

        Assert.Equal("Couldn't load the values.", cut.Find(".ib-filter-list-status").TextContent);

        fail = false;
        cut.FindAll("ib-filter-list button").First(b => b.TextContent.Contains("Retry")).Click();

        Assert.Equal(new[] { "Ada" }, Listed(cut, 0));
        Assert.Empty(cut.Find(".ib-filter-list-status").TextContent);
    }

    [Theory]
    [InlineData("?f-name-op=in&f-name=Ada", new[] { "1" })]
    [InlineData("?f-name-op=notin&f-name=Ada", new[] { "2", "3", "4", "5" })]
    [InlineData("?f-name-op=in&f-name-blank=1", new[] { "4", "5" })]
    [InlineData("?f-name-op=notin&f-name-blank=1", new[] { "1", "2", "3" })]
    [InlineData("?f-name-none=1", new string[0])]
    public void The_address_means_the_same_with_or_without_the_list(string query, string[] ids)
    {
        void Check(IRenderedComponent<Table<Row>> cut) =>
            Assert.Equal(ids, cut.FindAll("tbody tr:not(.ib-table-empty) td:nth-child(3)").Select(c => c.TextContent.Trim()).ToArray());

        RenderFragment columns = b =>
        {
            b.AddContent(0, Col<string?>(r => r.Name, Counting));
            b.AddContent(1, Col<string>(r => r.City));
            b.AddContent(2, (RenderFragment)(c =>
            {
                c.OpenComponent<PropertyColumn<Row, int>>(0);
                c.AddComponentParameter(1, "Property", (Expression<Func<Row, int>>)(r => r.Id));
                c.CloseComponent();
            }));
        };

        Nav.NavigateTo("/orders" + query);
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        Check(Render<Table<Row>>(p => p.Add(t => t.Items, Rows).Add(t => t.Columns, columns)));

        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        Check(Render<Table<Row>>(p => p.Add(t => t.Items, Rows).Add(t => t.Columns, columns)));
    }

    private static readonly FilterValuesLoader Hundred = r =>
    {
        // As a query does: what's kept comes first and isn't repeated, and a page counts from it.
        var all = Enumerable.Range(0, 120).Select(i => $"v{i:000}").ToList();
        var kept = r.Ticked.Concat(r.Unticked).OfType<string>().ToList();
        var rows = kept.Concat(all.Except(kept)).Skip(r.Skip).Take(r.Take).Select(v => new FilterValue(v)).ToList();
        return ValueTask.FromResult(new FilterValuesResult(rows, r.Skip + r.Take < 120));
    };

    [Fact]
    public void Select_all_on_a_long_list_takes_in_rows_not_loaded()
    {
        Filter? reported = new FieldFilter("x", FilterOperator.IsNull);
        var cut = Bound(f => reported = f, new FieldFilter("name", FilterOperator.In, new List<object?> { "v000" }), b => b.AddContent(0, Col<string?>(r => r.Name, Hundred)));
        Toggle(cut, 0, open: true);

        cut.Find("[data-ib-list-all]").Change(true);

        cut.WaitForAssertion(() => Assert.Null(reported), Timer);
        Assert.Equal("on", cut.Find("ib-filter-list").GetAttribute("data-ib-all"));
    }

    [Fact]
    public void Rows_the_other_filters_narrowed_are_not_every_value()
    {
        var reported = new List<Filter?>();
        var cut = Bound(reported.Add, new FieldFilter("city", FilterOperator.In, new List<object?> { "Oslo" }));
        Toggle(cut, 0, open: true);
        Assert.Equal(new[] { "Ada", "Alan" }, Listed(cut, 0));

        Tick(cut, 0, "Ada", false);
        Tick(cut, 0, "Alan", false);

        cut.WaitForAssertion(() => Assert.Contains("not (name in", reported.LastOrDefault()?.ToString() ?? ""), Timer);
    }

    [Fact]
    public void A_value_with_spaces_means_exactly_that()
    {
        Row[] rows = [new("Ada ", "Oslo", 1), new("Ada", "Rome", 2), new("  ", "Lima", 3)];
        Nav.NavigateTo("/orders?f-name-op=in&f-name=Ada%20&f-name=%20%20");
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        RenderFragment columns = b =>
        {
            b.AddContent(0, Col<string?>(r => r.Name, Counting));
            b.AddContent(1, Col<string>(r => r.City));
        };

        var cut = Render<Table<Row>>(p => p.Add(t => t.Items, rows).Add(t => t.Columns, columns));

        Assert.Equal(new[] { "Oslo", "Lima" }, cut.FindAll("tbody tr td:nth-child(2)").Select(c => c.TextContent.Trim()));
    }

    [Fact]
    public void A_number_list_reads_the_invariant_form_in_any_culture()
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
        try
        {
            Row[] rows = [new("a", "x", 1), new("b", "y", 1234)];
            static ValueTask<FilterValuesResult> Ids(FilterValuesRequest r) => ValueTask.FromResult(new FilterValuesResult([new FilterValue("1"), new FilterValue("1234")]));
            Nav.NavigateTo("/orders?f-id-op=in&f-id=1234");
            SetRendererInfo(new RendererInfo("Static", isInteractive: false));
            RenderFragment columns = b =>
            {
                b.AddContent(0, Col<string>(r => r.City));
                b.AddContent(1, Col<int>(r => r.Id, Ids));
            };

            var cut = Render<Table<Row>>(p => p.Add(t => t.Items, rows).Add(t => t.Columns, columns));

            Assert.Equal(new[] { "y" }, cut.FindAll("tbody tr td:first-child").Select(c => c.TextContent.Trim()));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void Static_panels_carry_what_a_list_wrote_and_their_Clear_removes_it()
    {
        Nav.NavigateTo("/orders?f-name-op=notin&f-name=Ada&f-name-blank=1");
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var cut = Render<Table<Row>>(p => p.Add(t => t.Items, Rows).Add(t => t.Columns, Columns));

        var hidden = cut.FindAll("[popover]")[0].QuerySelectorAll("input[type='hidden']").Select(i => (i.GetAttribute("name"), i.GetAttribute("value"))).ToList();
        Assert.Contains(("f-name-op", "notin"), hidden);
        Assert.Contains(("f-name", "Ada"), hidden);
        Assert.Contains(("f-name-blank", "1"), hidden);
        var clear = cut.FindAll("[popover]")[0].QuerySelector("a[data-ib-filter-clear]")!.GetAttribute("href")!;
        Assert.DoesNotContain("f-name", clear);
    }

    [Fact]
    public void A_bound_filter_round_trips_through_the_list()
    {
        var cut = Bound(_ => { }, new FieldFilter("name", FilterOperator.NotIn, new List<object?> { "Ada", null }));
        Toggle(cut, 0, open: true);

        Assert.Equal(new[] { "Ada" }, _asked[0].Unticked.OfType<string>());
        Assert.Contains(null, _asked[0].Unticked);
        Assert.False(Ticked(cut, 0, "Ada"));
        Assert.False(Ticked(cut, 0, "(Blank)"));
        Assert.True(Ticked(cut, 0, "Grace"));
        Assert.Equal("mixed", cut.Find("ib-filter-list").GetAttribute("data-ib-all"));
    }
}
