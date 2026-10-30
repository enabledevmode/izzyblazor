using System.Linq.Expressions;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

// Table on the data foundations: its columns are the request's fields, the bound filter is the
// tree, and a failed load shows an error with Retry.
public class TableDataTests : BunitContext
{
    public sealed record Row(int Id, string Name, int Qty, Paid Status);

    private static readonly Row[] Rows =
    [
        new(1, "Ada", 2, Paid.Yes),
        new(2, "Grace", 3, Paid.No),
        new(3, "adam", 1, Paid.Refunded),
        new(4, "Alan", 5, Paid.Yes),
    ];

    private BunitNavigationManager Nav => Services.GetRequiredService<BunitNavigationManager>();

    private static RenderFragment Col<TValue>(Expression<Func<Row, TValue>> property, bool filterable = true, Func<FieldFilter, Expression<Func<Row, bool>>?>? filter = null) => builder =>
    {
        builder.OpenComponent<PropertyColumn<Row, TValue>>(0);
        builder.AddComponentParameter(1, "Property", property);
        builder.AddComponentParameter(2, "Filterable", filterable);
        builder.AddComponentParameter(3, "Sortable", true);
        if (filter is not null)
        {
            builder.AddComponentParameter(4, "Filter", filter);
        }

        builder.CloseComponent();
    };

    private static RenderFragment Columns => b =>
    {
        b.AddContent(0, Col<string>(r => r.Name));
        b.AddContent(1, Col<int>(r => r.Qty));
        b.AddContent(2, Col<Paid>(r => r.Status));
    };

    private static string[] Names(IRenderedComponent<Table<Row>> cut) =>
        cut.FindAll("tbody tr:not(.ib-table-empty):not(.ib-table-error)").Select(r => r.Children[0].TextContent).ToArray();

    private void Static(string url = "/rows")
    {
        Nav.NavigateTo(url);
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
    }

    private void Interactive() => SetRendererInfo(new RendererInfo("Server", isInteractive: true));

    // Selecting all that match keeps the filter it was made under; another filter clears it.

    private IRenderedComponent<Table<Row>> SelectAllUnder(Selection selection, Action<Selection> report) => Render<Table<Row>>(p => p
        .Add(t => t.Items, Rows)
        .Add(t => t.ItemKey, r => r.Id.ToString())
        .Add(t => t.SelectionMode, SelectionMode.Multiple)
        .Add(t => t.Selection, selection)
        .Add(t => t.SelectionChanged, report)
        .Add(t => t.ChildContent, Columns));

    [Fact]
    public void All_of_another_filter_is_cleared_when_the_rows_load()
    {
        Static("/rows?f-name=ad");
        Selection? reported = null;
        var cut = SelectAllUnder(new Selection { All = true }, s => reported = s);

        Assert.True(reported!.IsEmpty);
        Assert.Empty(cut.FindAll("tbody .ib-table-select[checked]"));
    }

    [Fact]
    public void All_of_the_shown_filter_is_kept()
    {
        Static("/rows?f-name=ad");
        DataRequest<Row>? seen = null;
        Render<Table<Row>>(p => p
            .Add(t => t.ItemsProvider, r => { seen = r; return ValueTask.FromResult(r.ToResult(Rows.AsQueryable())); })
            .Add(t => t.ChildContent, Columns));
        Selection? reported = null;

        var cut = SelectAllUnder(Selection.AllOf(seen!), s => reported = s);

        Assert.Null(reported);
        Assert.Equal(2, cut.FindAll("tbody .ib-table-select[checked]").Count);
    }

    // The selection bar: the count, Select all N after the page, Clear selection, the page's actions.

    private IRenderedComponent<Table<Row>> Paged(Selection? selection, Action<Selection>? report = null) => Render<Table<Row>>(p => p
        .Add(t => t.Items, Rows)
        .Add(t => t.PageSize, 2)
        .Add(t => t.ItemKey, r => r.Id.ToString())
        .Add(t => t.SelectionMode, SelectionMode.Multiple)
        .Add(t => t.Selection, selection)
        .Add(t => t.SelectionChanged, report ?? (_ => { }))
        .Add(t => t.SelectionActions, s => b => b.AddMarkupContent(0, $"<button class=\"act\">Archive {s.Keys?.Length}</button>"))
        .Add(t => t.ChildContent, Columns));

    [Fact]
    public void The_bar_is_hidden_until_something_is_selected()
    {
        Static();
        var bar = Paged(null).Find(".ib-selection-bar");

        Assert.True(bar.HasAttribute("hidden"));
        Assert.Equal("Selection", bar.GetAttribute("aria-label"));
        Assert.Equal("4", bar.GetAttribute("data-ib-total"));
        Assert.Equal("", bar.QuerySelector(".ib-selection-count")!.TextContent);
    }

    [Fact]
    public void The_bar_counts_the_selection_and_holds_the_actions()
    {
        Static();
        var cut = Paged(Selection.Of("1", "3"));
        var bar = cut.Find(".ib-selection-bar");

        Assert.False(bar.HasAttribute("hidden"));
        Assert.Equal("2 selected", bar.QuerySelector(".ib-selection-count")!.TextContent);
        Assert.Equal("Archive 2", bar.QuerySelector(".ib-selection-actions .act")!.TextContent);
        // Only the first row of this page is selected: no step to all yet.
        Assert.True(bar.QuerySelector(".ib-selection-more")!.HasAttribute("hidden"));
    }

    [Fact]
    public void After_the_page_the_bar_offers_every_matching_row()
    {
        Interactive();
        Selection? reported = null;
        var cut = Paged(Selection.Of("1", "2"), s => reported = s);

        var more = cut.Find(".ib-selection-more");
        Assert.False(more.HasAttribute("hidden"));
        Assert.Contains("All 2 on this page are selected.", more.TextContent);

        cut.Find(".ib-selection-all").Click();
        Assert.True(reported!.All);
        Assert.Equal("All 4 selected", cut.Find(".ib-selection-count").TextContent);
        Assert.True(cut.Find(".ib-selection-more").HasAttribute("hidden"));

        cut.FindAll("tbody .ib-table-select")[0].Change(false);
        Assert.Equal("All 3 selected", cut.Find(".ib-selection-count").TextContent);

        cut.Find(".ib-selection-clear").Click();
        Assert.True(reported!.IsEmpty);
        Assert.True(cut.Find(".ib-selection-bar").HasAttribute("hidden"));
    }

    [Fact]
    public void Single_selection_has_a_bar_but_no_step_to_all()
    {
        Static();
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.Items, Rows)
            .Add(t => t.PageSize, 1)
            .Add(t => t.ItemKey, r => r.Id.ToString())
            .Add(t => t.SelectionMode, SelectionMode.Single)
            .Add(t => t.Selection, Selection.Of("1"))
            .Add(t => t.ChildContent, Columns));

        Assert.Equal("1 selected", cut.Find(".ib-selection-count").TextContent);
        Assert.True(cut.Find(".ib-selection-more").HasAttribute("hidden"));
    }

    [Fact]
    public void No_bar_without_selection()
    {
        Static();
        var cut = Render<Table<Row>>(p => p.Add(t => t.Items, Rows).Add(t => t.ChildContent, Columns));

        Assert.Empty(cut.FindAll(".ib-selection-bar"));
    }

    [Fact]
    public void Columns_are_the_requests_fields()
    {
        Static("/rows?f-who=a&f-name=d&sort=-who");
        DataRequest<Row>? seen = null;
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.PageSize, 10)
            .Add(t => t.ItemsProvider, request =>
            {
                seen = request;
                return ValueTask.FromResult(request.ToResult(Rows.AsQueryable()));
            })
            .Add(t => t.ChildContent, b =>
            {
                b.AddContent(0, Col<string>(r => r.Name));
                b.AddContent(1, Col<int>(r => r.Qty, filterable: false));
                b.OpenComponent<TemplateColumn<Row>>(2);
                b.AddComponentParameter(3, "Title", "Who");
                b.AddComponentParameter(4, "SortKey", "who");
                b.AddComponentParameter(5, "Sortable", true);
                b.AddComponentParameter(6, "SortBy", (Expression<Func<Row, object?>>)(r => r.Qty));
                b.AddComponentParameter(7, "FilterBy", (Expression<Func<Row, string?>>)(r => r.Name));
                b.AddComponentParameter(8, "Filterable", true);
                b.CloseComponent();
                b.OpenComponent<TemplateColumn<Row>>(9);
                b.AddComponentParameter(10, "Title", "Actions");
                b.CloseComponent();
            }));

        var fields = seen!.Fields!;
        Assert.Equal(new[] { "name", "qty", "who" }, fields.Select(f => f.Key));
        Assert.Equal(("Name", true, true, true), (fields[0].Caption, fields[0].Sortable, fields[0].Filterable, fields[0].Searchable));
        Assert.Equal((true, false), (fields[1].Sortable, fields[1].Filterable));
        Assert.Equal(typeof(string), fields[2].Type);

        // The template column sorts by SortBy (Qty) and filters by FilterBy (Name).
        Assert.Equal(new[] { new SortField("who", true) }, seen.Sort);
        Assert.True(seen.NeedsTotal);
    }

    [Fact]
    public void Paging_by_cursor_doesnt_need_the_total()
    {
        Static();
        DataRequest<Row>? seen = null;
        Render<Table<Row>>(p => p
            .Add(t => t.PageSize, 2)
            .Add(t => t.Paging, DataPaging.LoadOnScroll)
            .Add(t => t.ItemsProvider, request =>
            {
                seen = request;
                return ValueTask.FromResult(new DataResult<Row>(Rows.Take(2).ToList()));
            })
            .Add(t => t.ChildContent, Columns));

        Assert.False(seen!.NeedsTotal);
        Assert.Equal((0, 2), (seen.Skip, seen.Take));
    }

    [Fact]
    public void A_column_takes_a_custom_condition()
    {
        // "Qty" filters by ten times the number typed: 0.2..0.3 keeps 2 and 3.
        Static("/rows?f-qty-min=0.2&f-qty-max=0.3");
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.Items, Rows)
            .Add(t => t.ChildContent, Col<int>(r => r.Qty, filter: f => f.Operator == FilterOperator.Between && f.Value is decimal min && f.Value2 is decimal max
                ? r => r.Qty >= min * 10 && r.Qty <= max * 10
                : null)));

        Assert.Equal(new[] { "2", "3" }, cut.FindAll("tbody tr").Select(r => r.Children[0].TextContent));
    }

    [Fact]
    public void A_filter_set_in_code_shows_in_its_columns_and_the_rest_as_tags()
    {
        Interactive();
        Filter? reported = null;
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.Items, Rows)
            .Add(t => t.Filter, Filter.All(
                Filter.Where("status", FilterOperator.Equals, "Yes"),
                Filter.Where("name", FilterOperator.StartsWith, "a")))
            .Add(t => t.FilterChanged, (Filter? f) => reported = f)
            .Add(t => t.ChildContent, Columns));

        // Status's menu shows Yes; the starts-with condition has no panel, so it's a tag.
        Assert.Equal(new[] { "Ada", "Alan" }, Names(cut));
        Assert.Equal("Filter Status: Yes", cut.FindAll(".ib-table-filter-button")[2].GetAttribute("aria-label"));
        var tags = cut.FindAll(".ib-table-filter-tags .ib-tag").Select(t => t.TextContent.Trim()).ToArray();
        Assert.Contains(tags, t => t.Contains("Status: Yes", StringComparison.Ordinal));
        Assert.Contains(tags, t => t.Contains("startswith(name,'a')", StringComparison.Ordinal));

        // Removing that tag reports the filter without it.
        cut.FindAll(".ib-table-filter-tags .ib-tag").Single(t => t.TextContent.Contains("startswith", StringComparison.Ordinal)).QuerySelector("button")!.Click();
        Assert.Equal(new[] { "Ada", "Alan" }, Names(cut));
        Assert.Equal("status eq 'Yes'", reported!.ToString());

        // A new filter from the parent replaces it.
        cut.Render(p => p.Add(t => t.Filter, Filter.Where("qty", FilterOperator.GreaterThanOrEqual, 3)));
        Assert.Equal(new[] { "Grace", "Alan" }, Names(cut));
        Assert.Equal("Filter Qty: 3 or more", cut.FindAll(".ib-table-filter-button")[1].GetAttribute("aria-label"));

        cut.Render(p => p.Add(t => t.Filter, null));
        Assert.Equal(4, Names(cut).Length);
    }

    [Fact]
    public void A_newer_load_cancels_the_older_request()
    {
        Interactive();
        var pending = new List<(DataRequest<Row> Request, TaskCompletionSource<DataResult<Row>> Answer)>();
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.PageSize, 2)
            .Add(t => t.SortChanged, (string? _) => { })
            .Add(t => t.ItemsProvider, request =>
            {
                var answer = new TaskCompletionSource<DataResult<Row>>();
                pending.Add((request, answer));
                return new ValueTask<DataResult<Row>>(answer.Task);
            })
            .Add(t => t.ChildContent, Columns));

        cut.Find("thead button.ib-table-sort").Click();
        Assert.Equal(2, pending.Count);
        Assert.True(pending[0].Request.CancellationToken.IsCancellationRequested);
        Assert.False(pending[1].Request.CancellationToken.IsCancellationRequested);

        // The old request gives up; the table doesn't call that an error.
        cut.InvokeAsync(() => pending[0].Answer.SetCanceled(pending[0].Request.CancellationToken));
        cut.InvokeAsync(() => pending[1].Answer.SetResult(new DataResult<Row>(Rows.Take(2).ToList(), 4)));
        cut.WaitForAssertion(() => Assert.Equal(new[] { "Ada", "Grace" }, Names(cut)));
        Assert.Empty(cut.FindAll(".ib-table-error"));
    }

    // A provider that fails until told not to.
    private sealed class Flaky
    {
        public bool Fail { get; set; } = true;

        public int Calls { get; private set; }

        public ValueTask<DataResult<Row>> Load(DataRequest<Row> request)
        {
            Calls++;
            if (Fail)
            {
                throw new HttpRequestException("The API is down.");
            }

            var page = Rows.Skip(request.Skip).Take(request.Take ?? Rows.Length).ToList();
            return ValueTask.FromResult(new DataResult<Row>(page, Rows.Length)
            {
                Next = request.Skip + page.Count < Rows.Length ? (request.Skip + page.Count).ToString(System.Globalization.CultureInfo.InvariantCulture) : null,
            });
        }
    }

    [Fact]
    public void A_failed_load_on_a_static_page_shows_an_error_with_a_retry_link()
    {
        Static("/rows?sort=name");
        var api = new Flaky();
        var cut = Render<Table<Row>>(p => p.Add(t => t.ItemsProvider, api.Load).Add(t => t.PageSize, 2).Add(t => t.ChildContent, Columns));

        var error = cut.Find("tbody tr.ib-table-error");
        Assert.Equal("Couldn't load the rows.", error.QuerySelector(".ib-data-error-message")!.TextContent.Trim());
        var retry = error.QuerySelector("a")!;
        Assert.Equal("Retry", retry.TextContent.Trim());
        Assert.Equal("http://localhost/rows?sort=name", retry.GetAttribute("href"));
        Assert.Empty(cut.FindAll(".ib-data-footer"));
        Assert.Equal("Couldn't load the rows.", cut.Find("[role=status].ib-data-error-status").TextContent);
        Assert.Equal(3, int.Parse(error.QuerySelector("td")!.GetAttribute("colspan")!, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Error_content_replaces_the_message()
    {
        Static();
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.ItemsProvider, new Flaky().Load)
            .Add(t => t.ErrorContent, b => b.AddContent(0, "The orders service is down."))
            .Add(t => t.ChildContent, Columns));

        Assert.Equal("The orders service is down.", cut.Find(".ib-table-error .ib-data-error-message").TextContent.Trim());
    }

    [Fact]
    public void Retry_on_an_interactive_page_loads_again()
    {
        Interactive();
        var api = new Flaky();
        var cut = Render<Table<Row>>(p => p.Add(t => t.ItemsProvider, api.Load).Add(t => t.PageSize, 2).Add(t => t.ChildContent, Columns));

        var retry = cut.Find(".ib-table-error button");
        Assert.Equal("Retry", retry.TextContent.Trim());
        Assert.Empty(Names(cut));

        api.Fail = false;
        retry.Click();
        cut.WaitForAssertion(() => Assert.Equal(new[] { "Ada", "Grace" }, Names(cut)));
        Assert.Empty(cut.FindAll(".ib-table-error"));
        Assert.Contains("1–2 of 4", cut.Find(".ib-data-summary").TextContent);

        // The Retry button is gone: focus goes to the table's scroll box.
        cut.WaitForAssertion(() => JSInterop.VerifyFocusAsyncInvoke());
    }

    [Fact]
    public void Retry_that_fails_again_keeps_the_error()
    {
        Interactive();
        var api = new Flaky();
        var cut = Render<Table<Row>>(p => p.Add(t => t.ItemsProvider, api.Load).Add(t => t.ChildContent, Columns));

        cut.Find(".ib-table-error button").Click();
        cut.WaitForAssertion(() => Assert.Equal(2, api.Calls));
        Assert.Single(cut.FindAll(".ib-table-error"));
    }

    [Fact]
    public void Load_more_keeps_its_rows_when_the_next_page_fails()
    {
        Interactive();
        var api = new Flaky { Fail = false };
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.ItemsProvider, api.Load)
            .Add(t => t.PageSize, 2)
            .Add(t => t.Paging, DataPaging.LoadOnScroll)
            .Add(t => t.ChildContent, Columns));
        cut.WaitForAssertion(() => Assert.Equal(new[] { "Ada", "Grace" }, Names(cut)));

        api.Fail = true;
        cut.Find(".ib-data-more button").Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".ib-data-more .ib-data-error-message")));

        // The rows stay; the message and Retry are under them, in place of Load more.
        Assert.Equal(new[] { "Ada", "Grace" }, Names(cut));
        Assert.Empty(cut.FindAll("tbody .ib-table-error"));
        Assert.Equal("Couldn't load the rows.", cut.Find(".ib-data-more .ib-data-error-message").TextContent.Trim());
        var retry = cut.Find(".ib-data-more button");
        Assert.Equal("Retry", retry.TextContent.Trim());

        api.Fail = false;
        retry.Click();
        cut.WaitForAssertion(() => Assert.Equal(new[] { "Ada", "Grace", "adam", "Alan" }, Names(cut)));
        Assert.Empty(cut.FindAll(".ib-data-more .ib-data-error-message"));
    }

    [Fact]
    public void Load_on_scroll_stops_asking_after_a_failure()
    {
        Interactive();
        var api = new Flaky { Fail = false };
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.ItemsProvider, api.Load)
            .Add(t => t.PageSize, 2)
            .Add(t => t.Paging, DataPaging.LoadOnScroll)
            .Add(t => t.ChildContent, Columns));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-ib-load-next]")));

        api.Fail = true;
        cut.Find("[data-ib-load-next]").Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".ib-data-more .ib-data-error-message")));
        Assert.Empty(cut.FindAll("[data-ib-load-next]"));
    }

    [Fact]
    public void A_single_choice_column_reads_the_first_of_a_repeated_key()
    {
        Static("/rows?f-status=Yes&f-status=No");
        var cut = Render<Table<Row>>(p => p.Add(t => t.Items, Rows).Add(t => t.ChildContent, Columns));

        Assert.Equal(new[] { "Ada", "Alan" }, Names(cut));
        Assert.Equal("Filter Status: Yes", cut.FindAll(".ib-table-filter-button")[2].GetAttribute("aria-label"));
    }

    [Fact]
    public void Links_follow_the_address_when_another_part_of_it_changes()
    {
        Interactive();
        Nav.NavigateTo("/rows");
        var cut = Render<Table<Row>>(p => p.Add(t => t.Items, Rows).Add(t => t.PageSize, 2).Add(t => t.ChildContent, Columns));

        Nav.NavigateTo("/rows?tab=x");
        cut.WaitForAssertion(() => Assert.Contains("tab=x", cut.Find("thead a.ib-table-sort").GetAttribute("href")));
    }

    [Fact]
    public void A_provider_without_a_total_for_numbered_pages_still_says_so()
    {
        Static();
        var ex = Assert.Throws<InvalidOperationException>(() => Render<Table<Row>>(p => p
            .Add(t => t.PageSize, 2)
            .Add(t => t.ItemsProvider, _ => ValueTask.FromResult(new DataResult<Row>(Rows)))
            .Add(t => t.ChildContent, Columns)));

        Assert.Contains("returned no Total", ex.Message);
    }
}
