using System.Linq.Expressions;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

// Table paging by cursor: NextPrevious, LoadMore and LoadOnScroll, with a provider and with Items.
public class TableCursorRenderTests : BunitContext
{
    public sealed record Row(string Name);

    private static readonly Row[] Rows = Enumerable.Range(1, 7).Select(i => new Row($"r{i}")).ToArray();

    // A cursor API over Rows: "c{offset}" cursors, two rows a page, no total unless asked.
    private sealed class Api(Row[] data, bool total = false, bool previous = false)
    {
        public List<DataRequest<Row>> Requests { get; } = [];

        public Row[] Data { get; set; } = data;

        public ValueTask<DataResult<Row>> Load(DataRequest<Row> request)
        {
            Requests.Add(request);
            var at = request.Cursor is null ? 0 : int.Parse(request.Cursor[1..], System.Globalization.CultureInfo.InvariantCulture);
            var size = request.Take ?? Data.Length;
            return ValueTask.FromResult(new DataResult<Row>(Data.Skip(at).Take(size).ToList(), total ? Data.Length : null)
            {
                Next = at + size < Data.Length ? $"c{at + size}" : null,
                Previous = previous && at > 0 ? $"c{Math.Max(0, at - size)}" : null,
            });
        }
    }

    private BunitNavigationManager Nav => Services.GetRequiredService<BunitNavigationManager>();

    private static RenderFragment Columns => builder =>
    {
        builder.OpenComponent<PropertyColumn<Row, string>>(0);
        builder.AddComponentParameter(1, "Property", (Expression<Func<Row, string>>)(r => r.Name));
        builder.AddComponentParameter(2, "Sortable", true);
        builder.CloseComponent();
    };

    private static string[] Names(IRenderedComponent<Table<Row>> cut) =>
        cut.FindAll("tbody tr").Select(r => r.TextContent.Trim()).ToArray();

    private static string Summary(IRenderedComponent<Table<Row>> cut) => cut.Find(".ib-table-summary").TextContent.Trim();

    private static string? Href(IRenderedComponent<Table<Row>> cut, string control) =>
        cut.Find($".ib-table-footer .ib-pagination-{control}").GetAttribute("href");

    private static bool Unavailable(IRenderedComponent<Table<Row>> cut, string control) =>
        cut.Find($".ib-table-footer .ib-pagination-{control}").GetAttribute("aria-disabled") == "true";

    private IRenderedComponent<Table<Row>> RenderProvider(Api api, DataPaging paging, Action<ComponentParameterCollectionBuilder<Table<Row>>>? more = null) =>
        Render<Table<Row>>(p =>
        {
            p.Add(t => t.ItemsProvider, api.Load)
             .Add(t => t.PageSize, 2)
             .Add(t => t.Paging, paging)
             .Add(t => t.ItemKey, r => r.Name)
             .Add(t => t.Caption, "Rows")
             .Add(t => t.ChildContent, Columns);
            more?.Invoke(p);
        });

    private void Static() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    private void Interactive() => SetRendererInfo(new RendererInfo("Server", isInteractive: true));

    [Fact]
    public void A_table_refuses_load_more_and_loads_on_scroll_inside_its_scroll_box()
    {
        Static();
        var refused = Assert.Throws<InvalidOperationException>(() => RenderProvider(new Api(Rows), DataPaging.LoadMore));
        Assert.Contains("ListView", refused.Message);

        var cut = RenderProvider(new Api(Rows), DataPaging.LoadOnScroll);
        Assert.NotNull(cut.Find("ib-table > .ib-table-scroll > .ib-table-more"));
        Assert.Empty(cut.FindAll(".ib-table-footer"));
    }

    [Fact]
    public void Next_previous_reads_the_cursor_and_position_from_the_url_and_links_the_next_page()
    {
        Static();
        Nav.NavigateTo("/rows?cursor=c2&page=2");
        var api = new Api(Rows);
        var cut = RenderProvider(api, DataPaging.NextPrevious);

        var request = Assert.Single(api.Requests);
        Assert.Equal("c2", request.Cursor);
        Assert.Equal(2, request.Page);
        Assert.Equal(new[] { "r3", "r4" }, Names(cut));
        Assert.Equal("3–4", Summary(cut));
        Assert.Contains("cursor=c4", Href(cut, "next"));
        Assert.Contains("page=3", Href(cut, "next"));
        // The second page's previous is the first, with no cursor; First too.
        Assert.DoesNotContain("cursor=", Href(cut, "prev"));
        Assert.DoesNotContain("cursor=", Href(cut, "first"));
        Assert.Empty(cut.FindAll(".ib-table-footer .ib-pagination-item:not(.ib-btn-icon-only)"));
    }

    [Fact]
    public void The_first_page_ignores_a_page_number_without_a_cursor_and_disables_first_and_previous()
    {
        Static();
        Nav.NavigateTo("/rows?page=3");
        var api = new Api(Rows, total: true);
        var cut = RenderProvider(api, DataPaging.NextPrevious);

        Assert.Equal(1, api.Requests[0].Page);
        Assert.Null(api.Requests[0].Cursor);
        Assert.Equal("1–2 of 7", Summary(cut));
        Assert.True(Unavailable(cut, "first"));
        Assert.True(Unavailable(cut, "prev"));
        Assert.False(Unavailable(cut, "next"));
    }

    [Fact]
    public void The_last_page_has_no_next()
    {
        Static();
        Nav.NavigateTo("/rows?cursor=c6&page=4");
        var cut = RenderProvider(new Api(Rows), DataPaging.NextPrevious);

        Assert.Equal(new[] { "r7" }, Names(cut));
        Assert.Equal("7–7", Summary(cut));
        Assert.True(Unavailable(cut, "next"));
    }

    [Fact]
    public void Previous_comes_from_the_provider_and_is_unavailable_on_static_pages_without_it()
    {
        Static();
        Nav.NavigateTo("/rows?cursor=c4&page=3");
        var without = RenderProvider(new Api(Rows), DataPaging.NextPrevious);
        Assert.True(Unavailable(without, "prev"));
        Assert.False(Unavailable(without, "first"));

        var with = RenderProvider(new Api(Rows, previous: true), DataPaging.NextPrevious);
        Assert.Contains("cursor=c2", Href(with, "prev"));
        Assert.Contains("page=2", Href(with, "prev"));
    }

    [Fact]
    public void Interactive_tables_remember_where_a_cursor_came_from()
    {
        Interactive();
        Nav.NavigateTo("/rows?cursor=c2&page=2");
        var cut = RenderProvider(new Api(Rows), DataPaging.NextPrevious);

        Nav.NavigateTo(Href(cut, "next")!);
        cut.WaitForAssertion(() => Assert.Equal(new[] { "r5", "r6" }, Names(cut)));

        Assert.Contains("cursor=c2", Href(cut, "prev"));
        Assert.Contains("page=2", Href(cut, "prev"));
    }

    [Fact]
    public void Sort_filters_and_first_page_links_drop_the_cursor()
    {
        Static();
        Nav.NavigateTo("/rows?cursor=c2&page=2");
        var cut = RenderProvider(new Api(Rows), DataPaging.NextPrevious);

        var sort = cut.Find("thead a.ib-table-sort").GetAttribute("href")!;
        Assert.DoesNotContain("cursor=", sort);
        Assert.DoesNotContain("page=", sort);
    }

    [Fact]
    public void Numbered_pages_need_a_total()
    {
        Static();
        var error = Assert.Throws<InvalidOperationException>(() => RenderProvider(new Api(Rows), DataPaging.Pages));

        Assert.Contains(nameof(Table<Row>.Paging), error.Message);
    }

    [Fact]
    public void Bound_cursor_pages_with_buttons_and_reports_cursor_and_page()
    {
        Interactive();
        var cursors = new List<string?>();
        var pages = new List<int>();
        var cut = RenderProvider(new Api(Rows), DataPaging.NextPrevious, p => p
            .Add(t => t.CursorChanged, (string? c) => cursors.Add(c))
            .Add(t => t.PageChanged, (int page) => pages.Add(page)));
        var urlBefore = Nav.Uri;

        cut.Find(".ib-table-footer button.ib-pagination-next").Click();
        Assert.Equal(new[] { "r3", "r4" }, Names(cut));
        cut.Find(".ib-table-footer button.ib-pagination-next").Click();
        Assert.Equal(new[] { "r5", "r6" }, Names(cut));
        // Previous from what the table followed.
        cut.Find(".ib-table-footer button.ib-pagination-prev").Click();
        Assert.Equal(new[] { "r3", "r4" }, Names(cut));

        Assert.Equal(new string?[] { "c2", "c4", "c2" }, cursors);
        Assert.Equal(new[] { 2, 3, 2 }, pages);
        Assert.Equal(urlBefore, Nav.Uri);

        // A new sort starts over, and says so.
        cut.Find("thead button.ib-table-sort").Click();
        Assert.Null(cursors[^1]);
        Assert.Equal(1, pages[^1]);
        Assert.Equal(new[] { "r1", "r2" }, Names(cut));
    }

    [Fact]
    public void Load_more_adds_the_next_page_and_moves_focus_to_its_first_row()
    {
        Interactive();
        var api = new Api(Rows);
        var cut = RenderProvider(api, DataPaging.LoadOnScroll, p => p.Add(t => t.LoadMoreText, "Load earlier"));

        var button = cut.Find(".ib-table-more button");
        Assert.Equal("Load earlier", button.TextContent.Trim());
        button.Click();

        Assert.Equal(new[] { "r1", "r2", "r3", "r4" }, Names(cut));
        Assert.Equal("1–4", Summary(cut));
        Assert.Equal("c2", api.Requests[^1].Cursor);
        Assert.Equal(2, api.Requests[^1].Page);
        Assert.Equal("-1", cut.FindAll("tbody tr")[2].GetAttribute("tabindex"));
        Assert.Null(cut.FindAll("tbody tr")[0].GetAttribute("tabindex"));
        cut.WaitForAssertion(() => JSInterop.VerifyFocusAsyncInvoke());

        cut.Find(".ib-table-more button").Click();
        cut.Find(".ib-table-more button").Click();
        Assert.Equal(7, Names(cut).Length);
        Assert.Empty(cut.FindAll(".ib-table-more button"));
    }

    [Fact]
    public void Load_more_skips_rows_already_shown()
    {
        Interactive();
        var api = new Api(Rows);
        var cut = RenderProvider(api, DataPaging.LoadOnScroll);

        // A row added at the top shifts the offset: r2 would come again.
        api.Data = [new Row("r0"), .. Rows];
        cut.Find(".ib-table-more button").Click();

        Assert.Equal(new[] { "r1", "r2", "r3" }, Names(cut));
    }

    [Fact]
    public async Task Load_more_keeps_its_pages_when_the_parent_renders_and_starts_over_on_reload()
    {
        Interactive();
        var api = new Api(Rows);
        var cut = RenderProvider(api, DataPaging.LoadOnScroll);
        cut.Find(".ib-table-more button").Click();
        var requests = api.Requests.Count;

        cut.Render();
        Assert.Equal(4, Names(cut).Length);
        Assert.Equal(requests, api.Requests.Count);

        await cut.InvokeAsync(() => cut.Instance.ReloadAsync());
        Assert.Equal(new[] { "r1", "r2" }, Names(cut));
    }

    // A cursor API whose answers wait until the test releases them.
    private sealed class SlowApi
    {
        public List<(DataRequest<Row> Request, TaskCompletionSource<DataResult<Row>> Answer)> Pending { get; } = [];

        public ValueTask<DataResult<Row>> Load(DataRequest<Row> request)
        {
            var answer = new TaskCompletionSource<DataResult<Row>>();
            Pending.Add((request, answer));
            return new ValueTask<DataResult<Row>>(answer.Task);
        }

        public void Answer(int index)
        {
            var (request, answer) = Pending[index];
            var at = request.Cursor is null ? 0 : int.Parse(request.Cursor[1..], System.Globalization.CultureInfo.InvariantCulture);
            var rows = request.Sort is [{ Descending: true }] ? Rows.Reverse().ToArray() : Rows;
            answer.SetResult(new DataResult<Row>(rows.Skip(at).Take(2).ToList()) { Next = at + 2 < rows.Length ? $"c{at + 2}" : null });
        }
    }

    [Fact]
    public void Load_more_waits_while_the_list_reloads()
    {
        Interactive();
        var api = new SlowApi();
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.ItemsProvider, api.Load)
            .Add(t => t.PageSize, 2)
            .Add(t => t.Paging, DataPaging.LoadOnScroll)
            .Add(t => t.SortChanged, (string? _) => { })
            .Add(t => t.ChildContent, Columns));
        cut.InvokeAsync(() => api.Answer(0));
        cut.WaitForAssertion(() => Assert.Equal(new[] { "r1", "r2" }, Names(cut)));

        // A new sort starts a reload; the old rows stay on view meanwhile, but no Load more starts from them.
        cut.Find("thead button.ib-table-sort").Click();
        Assert.Empty(cut.FindAll("[data-ib-load-next]"));
        cut.Find(".ib-table-more button").Click();
        Assert.Equal(new string?[] { null, null }, api.Pending.Select(p => p.Request.Cursor));

        cut.InvokeAsync(() => api.Answer(1));
        cut.WaitForAssertion(() => Assert.Equal(new[] { "r1", "r2" }, Names(cut)));
        Assert.Single(cut.FindAll("[data-ib-load-next]"));
    }

    [Fact]
    public void A_parent_render_keeps_a_load_more_that_is_under_way()
    {
        Interactive();
        var api = new SlowApi();
        var cut = RenderProvider(new Api(Rows), DataPaging.LoadOnScroll);
        cut.Render(p => p.Add(t => t.ItemsProvider, api.Load));
        cut.InvokeAsync(() => api.Answer(0));
        cut.WaitForAssertion(() => Assert.Equal(2, Names(cut).Length));

        cut.Find(".ib-table-more button").Click();
        cut.Render();
        // The first page and the next one asked for; no reload of the first page.
        Assert.Equal(new[] { null, "c2" }, api.Pending.Select(p => p.Request.Cursor));

        cut.InvokeAsync(() => api.Answer(1));
        cut.WaitForAssertion(() => Assert.Equal(new[] { "r1", "r2", "r3", "r4" }, Names(cut)));
    }

    [Fact]
    public void Load_more_on_a_static_page_grows_the_list_by_a_page()
    {
        Static();
        Nav.NavigateTo("/rows?page=2");
        var cut = RenderProvider(new Api(Rows), DataPaging.LoadOnScroll);

        // ?page=2 is the first two pages, loaded as one; Load more asks for three.
        Assert.Equal(4, Names(cut).Length);
        var link = cut.Find(".ib-table-more a.ib-btn");
        Assert.Equal("Load more", link.TextContent.Trim());
        Assert.Contains("page=3", link.GetAttribute("href"));
        Assert.DoesNotContain("cursor=", link.GetAttribute("href"));
        // No way back: the list starts at the top.
        Assert.Empty(cut.FindAll(".ib-pagination-first"));
        Assert.Equal("1–4", Summary(cut));
        // Focus goes to the first row the last page added.
        var rows = cut.FindAll("tbody tr");
        Assert.Equal(new[] { 2 }, Enumerable.Range(0, rows.Count).Where(i => rows[i].HasAttribute("data-ib-autofocus")));
        Assert.Equal("-1", rows[2].GetAttribute("tabindex"));
    }

    [Fact]
    public void Load_on_scroll_has_a_trigger_that_loads_without_moving_focus()
    {
        Interactive();
        var cut = RenderProvider(new Api(Rows), DataPaging.LoadOnScroll);

        Assert.NotNull(cut.Find("ib-table").GetAttribute("data-ib-load-on-scroll"));
        cut.Find("[data-ib-load-next]").Click();

        Assert.Equal(4, Names(cut).Length);
        Assert.Empty(JSInterop.Invocations);
        // The button stays for the keyboard, out of sight until it has focus.
        Assert.Contains("ib-table-more-on-focus", cut.Find(".ib-table-more button:not([hidden])").ClassName);
    }

    [Fact]
    public void Load_on_scroll_on_a_static_page_loads_by_its_link()
    {
        Static();
        var cut = RenderProvider(new Api(Rows), DataPaging.LoadOnScroll);

        // The script follows the link as the last row comes into view; it's out of sight until
        // focused (CSS shows it with no script, where it's the only way on).
        var link = cut.Find(".ib-table-more a.ib-btn");
        Assert.True(link.HasAttribute("data-ib-load-next"));
        Assert.Contains("ib-table-more-on-focus", link.ClassName);
        Assert.Contains("page=2", link.GetAttribute("href"));
    }

    [Fact]
    public void Items_page_by_offset()
    {
        Interactive();
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.Items, Rows)
            .Add(t => t.PageSize, 3)
            .Add(t => t.Paging, DataPaging.LoadOnScroll)
            .Add(t => t.ChildContent, Columns));

        cut.Find(".ib-table-more button").Click();
        cut.Find(".ib-table-more button").Click();

        Assert.Equal(Rows.Select(r => r.Name), Names(cut));
        Assert.Equal("1–7 of 7", Summary(cut));
        Assert.Empty(cut.FindAll(".ib-table-more button"));
    }

    [Fact]
    public void Items_next_previous_links_use_offsets()
    {
        Static();
        Nav.NavigateTo("/rows?cursor=6&page=3");
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.Items, Rows)
            .Add(t => t.PageSize, 3)
            .Add(t => t.Paging, DataPaging.NextPrevious)
            .Add(t => t.ChildContent, Columns));

        Assert.Equal(new[] { "r7" }, Names(cut));
        Assert.Equal("7–7 of 7", Summary(cut));
        Assert.Contains("cursor=3", Href(cut, "prev"));
        Assert.True(Unavailable(cut, "next"));
    }

    [Fact]
    public void A_cursor_past_the_end_shows_the_empty_text_and_first()
    {
        Static();
        Nav.NavigateTo("/rows?cursor=c40&page=21");
        var cut = RenderProvider(new Api(Rows), DataPaging.NextPrevious);

        Assert.Single(cut.FindAll(".ib-table-empty"));
        Assert.False(Unavailable(cut, "first"));
        Assert.Equal("", Summary(cut));
    }
}
