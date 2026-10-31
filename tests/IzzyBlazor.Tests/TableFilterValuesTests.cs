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
            .Add(t => t.ChildContent, columns ?? Columns));
        cut.Instance.FilterDelay = TimeSpan.FromMilliseconds(30);
        return cut;
    }

    private IRenderedComponent<Table<Row>> Unbound(string url = "/orders")
    {
        Nav.NavigateTo(url);
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var cut = Render<Table<Row>>(p => p.Add(t => t.Items, Rows).Add(t => t.ChildContent, Columns));
        cut.Instance.FilterDelay = TimeSpan.FromMilliseconds(30);
        return cut;
    }

    private static string[] Names(IRenderedComponent<Table<Row>> cut) =>
        cut.FindAll("tbody tr:not(.ib-table-empty) td:first-child").Select(c => c.TextContent.Trim()).ToArray();

    private static void Toggle(IRenderedComponent<Table<Row>> cut, int column, bool open)
    {
        var panel = cut.FindAll("[popover]")[column];
        panel.TriggerEvent("onibtoggle", new IzzyToggleEventArgs { Id = panel.Id!, Open = open, Seq = open ? 1 : 2 });
    }

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

    [Fact]
    public void Opening_asks_each_time_and_a_tick_asks_nothing()
    {
        var cut = Bound(_ => { });

        Toggle(cut, 0, open: true);
        Assert.Single(_asked);
        Assert.Equal(new[] { "(Blank)", "Ada", "Alan", "Grace" }, Listed(cut, 0));

        Tick(cut, 0, "Ada", false);
        Assert.Single(_asked);

        Toggle(cut, 0, open: false);
        Toggle(cut, 0, open: true);
        Assert.Equal(2, _asked.Count);
    }

    [Fact]
    public void Static_pages_show_the_usual_panel_and_ask_nothing()
    {
        Nav.NavigateTo("/orders");
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var cut = Render<Table<Row>>(p => p.Add(t => t.Items, Rows).Add(t => t.ChildContent, Columns));

        Assert.Empty(_asked);
        Assert.Empty(cut.FindAll("ib-filter-list"));
        Assert.NotNull(cut.Find("input[name='f-name']"));
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
        Assert.Contains("Filter Name: All but Ada", cut.FindAll(".ib-table-filter-button")[0].GetAttribute("aria-label"));
    }

    [Fact]
    public void Clearing_select_all_takes_in_rows_not_loaded_and_keeps_none()
    {
        var cut = Bound(_ => { });
        Toggle(cut, 0, open: true);

        cut.FindAll("[popover]")[0].QuerySelector("[data-ib-list-all]")!.Change(false);

        cut.WaitForAssertion(() => Assert.Empty(Names(cut)), Timer);
        Assert.Equal("off", cut.FindAll("ib-filter-list")[0].GetAttribute("data-ib-all"));

        Tick(cut, 0, "Grace", true);
        cut.WaitForAssertion(() => Assert.Equal(new[] { "Grace" }, Names(cut)), Timer);
        Assert.Equal("mixed", cut.FindAll("ib-filter-list")[0].GetAttribute("data-ib-all"));
    }

    [Fact]
    public void With_every_row_known_all_ticked_is_no_filter()
    {
        Filter? reported = new FieldFilter("x", FilterOperator.IsNull);
        var cut = Bound(f => reported = f, new FieldFilter("city", FilterOperator.In, new List<object?> { "Oslo" }));
        Toggle(cut, 1, open: true);

        Tick(cut, 1, "Lima", true);
        Tick(cut, 1, "Rome", true);

        cut.WaitForAssertion(() => Assert.Null(reported), Timer);
        Assert.Equal(5, Names(cut).Length);
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
    public void The_search_asks_again_after_the_pause_and_select_all_says_how_many()
    {
        var cut = Bound(_ => { });
        Toggle(cut, 0, open: true);

        cut.FindAll("[popover]")[0].QuerySelector("[data-ib-bound-search]")!.Input("a");

        cut.WaitForAssertion(() => Assert.Equal("a", _asked.Last().Search), Timer);
        cut.WaitForAssertion(() => Assert.Equal(new[] { "Ada", "Alan", "Grace" }, Listed(cut, 0)), Timer);
        Assert.Equal("Select all 3 results", cut.FindAll("[popover]")[0].QuerySelector("[data-ib-list-results]")!.TextContent);

        Toggle(cut, 0, open: false);
        Toggle(cut, 0, open: true);
        Assert.Null(_asked.Last().Search);
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

    [Fact]
    public void A_superseded_request_is_canceled_and_its_answer_dropped()
    {
        var answers = new List<TaskCompletionSource<FilterValuesResult>>();
        var tokens = new List<CancellationToken>();
        ValueTask<FilterValuesResult> Slow(FilterValuesRequest r)
        {
            tokens.Add(r.CancellationToken);
            answers.Add(new());
            return new(answers[^1].Task);
        }

        var cut = Bound(_ => { }, columns: b => b.AddContent(0, Col<string?>(r => r.Name, Slow)));
        Toggle(cut, 0, open: true);
        Toggle(cut, 0, open: false);
        Assert.True(tokens[0].IsCancellationRequested);

        Toggle(cut, 0, open: true);
        cut.InvokeAsync(() => answers[0].SetResult(new([new FilterValue("old")])));
        cut.InvokeAsync(() => answers[1].SetResult(new([new FilterValue("new")])));

        cut.WaitForAssertion(() => Assert.Equal(new[] { "new" }, Listed(cut, 0)), Timer);
    }

    [Theory]
    [InlineData("?f-name-op=in&f-name=Ada", new[] { "1" })]
    [InlineData("?f-name-op=in&f-name=Ada&f-name=Grace", new[] { "1", "2" })]
    [InlineData("?f-name-op=notin&f-name=Ada", new[] { "2", "3", "4", "5" })]
    [InlineData("?f-name-op=in&f-name-blank=1", new[] { "4", "5" })]
    [InlineData("?f-name-op=notin&f-name-blank=1", new[] { "1", "2", "3" })]
    [InlineData("?f-name-none=1", new string[0])]
    [InlineData("?f-name=a", new[] { "1", "2", "3" })]
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
        Check(Render<Table<Row>>(p => p.Add(t => t.Items, Rows).Add(t => t.ChildContent, columns)));

        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        Check(Render<Table<Row>>(p => p.Add(t => t.Items, Rows).Add(t => t.ChildContent, columns)));
    }

    [Fact]
    public void Without_a_bound_filter_ticks_reach_the_address_after_the_pause()
    {
        var cut = Unbound();
        Toggle(cut, 0, open: true);

        Tick(cut, 0, "Ada", false);
        Tick(cut, 0, "Grace", false);
        Assert.False(Ticked(cut, 0, "Ada"));

        cut.WaitForAssertion(() => Assert.Contains("f-name-op=notin", Nav.Uri), Timer);
        Assert.Contains("f-name=Ada", Nav.Uri);
        Assert.Contains("f-name=Grace", Nav.Uri);
        cut.WaitForAssertion(() => Assert.DoesNotContain("Ada", Names(cut)), Timer);
        Assert.False(Ticked(cut, 0, "Ada"));
    }

    [Fact]
    public void A_sort_during_the_pause_keeps_the_ticks_waiting()
    {
        var cut = Unbound();
        cut.Instance.FilterDelay = TimeSpan.FromMilliseconds(100);
        Toggle(cut, 0, open: true);

        Tick(cut, 0, "Ada", false);
        Nav.NavigateTo("/orders?sort=name");

        cut.WaitForAssertion(() => Assert.Contains("f-name-op=notin", Nav.Uri), Timer);
        Assert.Contains("sort=name", Nav.Uri);
    }

    [Fact]
    public void A_move_that_changes_the_column_drops_the_ticks_waiting()
    {
        var cut = Unbound();
        cut.Instance.FilterDelay = TimeSpan.FromMilliseconds(100);
        Toggle(cut, 0, open: true);

        Tick(cut, 0, "Ada", false);
        Nav.NavigateTo("/orders?f-name=x");

        Thread.Sleep(350);
        Assert.Contains("f-name=x", Nav.Uri);
        Assert.DoesNotContain("notin", Nav.Uri);
    }

    [Fact]
    public void Ticks_that_come_back_to_where_they_started_are_no_move()
    {
        var cut = Unbound();
        var moves = 0;
        Nav.LocationChanged += (_, _) => moves++;
        Toggle(cut, 0, open: true);

        Tick(cut, 0, "Ada", false);
        Tick(cut, 0, "Ada", true);
        cut.WaitForAssertion(() => Assert.True(Ticked(cut, 0, "Ada")), Timer);
        Thread.Sleep(150);

        Assert.Equal(0, moves);
    }

    [Fact]
    public void A_tick_leaves_the_section_out_of_the_address()
    {
        var cut = Unbound("/orders#list");
        Toggle(cut, 0, open: true);

        Tick(cut, 0, "Ada", false);

        cut.WaitForAssertion(() => Assert.Contains("f-name-op=notin", Nav.Uri), Timer);
        Assert.DoesNotContain("#", Nav.Uri);
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
    public void A_later_page_counts_from_what_was_kept_when_the_first_was_asked()
    {
        var cut = Bound(_ => { }, new FieldFilter("name", FilterOperator.NotIn, new List<object?> { "v100" }), b => b.AddContent(0, Col<string?>(r => r.Name, Hundred)));
        Toggle(cut, 0, open: true);
        Assert.Equal("v100", Listed(cut, 0)[0]);

        // Ticked again, it no longer has to stay first, but this list's pages still count it there.
        Tick(cut, 0, "v100", true);
        cut.WaitForAssertion(() => Assert.True(Ticked(cut, 0, "v100")), Timer);
        cut.Find("[data-ib-load-next]").Click();

        Assert.Contains("v049", Listed(cut, 0));
        Assert.Contains("v050", Listed(cut, 0));
        Assert.Equal(100, Listed(cut, 0).Distinct().Count());
    }

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
    public void Unticking_on_a_long_list_stays_a_list()
    {
        var cut = Bound(_ => { }, new FieldFilter("name", FilterOperator.NotIn, new List<object?> { "v000" }), b => b.AddContent(0, Col<string?>(r => r.Name, Hundred)));
        Toggle(cut, 0, open: true);

        Tick(cut, 0, "v001", false);

        cut.WaitForAssertion(() => Assert.Contains("All but 2", cut.Find(".ib-table-filter-button").GetAttribute("aria-label")), Timer);
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
    public void Select_all_waits_for_rows_that_match_the_box_and_a_search_that_worked()
    {
        var fail = false;
        var answer = new TaskCompletionSource<FilterValuesResult>();
        ValueTask<FilterValuesResult> Slow(FilterValuesRequest r) =>
            r.Search is null ? ValueTask.FromResult(new FilterValuesResult([new FilterValue("Ada")]))
            : fail ? throw new InvalidOperationException("down")
            : new(answer.Task);

        var cut = Bound(_ => { }, columns: b => b.AddContent(0, Col<string?>(r => r.Name, Slow)));
        Toggle(cut, 0, open: true);
        Assert.False(cut.Find("[data-ib-list-all]").HasAttribute("disabled"));

        cut.Find("[data-ib-bound-search]").Input("a");
        Assert.True(cut.Find("[data-ib-list-all]").HasAttribute("disabled"));

        fail = true;
        cut.Find("[data-ib-bound-search]").Input("ab");
        cut.WaitForAssertion(() => Assert.Equal("Couldn't load the values.", cut.Find(".ib-filter-list-status").TextContent), Timer);
        Assert.True(cut.Find("[data-ib-list-all]").HasAttribute("disabled"));
    }

    [Fact]
    public void A_search_with_nothing_found_says_so()
    {
        var cut = Bound(_ => { });
        Toggle(cut, 0, open: true);

        cut.FindAll("[popover]")[0].QuerySelector("[data-ib-bound-search]")!.Input("zzz");

        cut.WaitForAssertion(() => Assert.Equal("No values found", cut.FindAll(".ib-filter-list-status")[0].TextContent), Timer);
    }

    [Fact]
    public void Clearing_the_usual_panel_leaves_it_in_place_until_it_closes()
    {
        Filter? reported = new FieldFilter("x", FilterOperator.IsNull);
        var cut = Bound(f => reported = f, new FieldFilter("name", FilterOperator.Contains, "ad"));
        Toggle(cut, 0, open: true);
        Assert.Empty(_asked);

        cut.FindAll("[popover]")[0].QuerySelector("input")!.Input("");
        cut.WaitForAssertion(() => Assert.Null(reported), Timer);
        Assert.Empty(cut.FindAll("[popover]")[0].QuerySelectorAll("ib-filter-list"));

        Toggle(cut, 0, open: false);
        Toggle(cut, 0, open: true);
        Assert.Single(_asked);
        Assert.NotEmpty(Listed(cut, 0));
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

        var cut = Render<Table<Row>>(p => p.Add(t => t.Items, rows).Add(t => t.ChildContent, columns));

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

            var cut = Render<Table<Row>>(p => p.Add(t => t.Items, rows).Add(t => t.ChildContent, columns));

            Assert.Equal(new[] { "y" }, cut.FindAll("tbody tr td:first-child").Select(c => c.TextContent.Trim()));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void A_condition_the_list_cannot_show_has_the_usual_panel_and_asks_nothing()
    {
        var cut = Unbound("/orders?f-name=ad");
        Toggle(cut, 0, open: true);

        Assert.Empty(_asked);
        Assert.Equal("ad", cut.Find("input[name='f-name']").GetAttribute("value"));
    }

    [Fact]
    public void Static_panels_carry_what_a_list_wrote_and_their_Clear_removes_it()
    {
        Nav.NavigateTo("/orders?f-name-op=notin&f-name=Ada&f-name-blank=1");
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var cut = Render<Table<Row>>(p => p.Add(t => t.Items, Rows).Add(t => t.ChildContent, Columns));

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

    [Fact]
    public void A_DataGrid_passes_its_search_on()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        Nav.NavigateTo("/orders?search=ada");
        var cut = Render<DataGrid<Row>>(p => p.Add(t => t.Items, Rows).Add(t => t.Searchable, true).Add(t => t.ChildContent, Columns));

        var panel = cut.FindAll("[popover]").First(p => p.QuerySelector("ib-filter-list") is not null);
        panel.TriggerEvent("onibtoggle", new IzzyToggleEventArgs { Id = panel.Id!, Open = true, Seq = 1 });

        Assert.Equal("ada", Assert.Single(_asked).GridSearch);
    }

    [Theory]
    [InlineData(true, false, "its type has a list")]
    [InlineData(false, true, "reads text")]
    public void A_column_that_cannot_list_values_says_why(bool onEnum, bool onNumber, string message)
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        RenderFragment columns = b =>
        {
            if (onEnum)
            {
                b.OpenComponent<PropertyColumn<Row, FilterCase>>(0);
                b.AddComponentParameter(1, "Property", (Expression<Func<Row, FilterCase>>)(r => FilterCase.Database));
                b.AddComponentParameter(2, "SortKey", "x");
                b.AddComponentParameter(3, "Filterable", true);
                b.AddComponentParameter(4, "FilterList", true);
                b.CloseComponent();
            }
            else if (onNumber)
            {
                b.AddContent(0, Col<int>(r => r.Id, list: true));
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() => Render<Table<Row>>(p => p.Add(t => t.Items, Rows).Add(t => t.ChildContent, columns)));
        Assert.Contains(message, ex.Message);
    }

    [Fact]
    public void FilterList_with_a_provider_names_FilterValues()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        RenderFragment columns = b => b.AddContent(0, Col<string>(r => r.City, list: true));

        var ex = Assert.Throws<InvalidOperationException>(() => Render<Table<Row>>(p => p
            .Add(t => t.ItemsProvider, r => ValueTask.FromResult(new DataResult<Row>([], 0)))
            .Add(t => t.ChildContent, columns)));

        Assert.Contains("FilterValues", ex.Message);
    }
}
