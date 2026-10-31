using System.Linq.Expressions;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

// A column's operator: the Select in its panel, what the address and a GET post mean by it, and that
// the export link means the same rows as the screen.
public class ColumnFilterOperatorTests : BunitContext
{
    private static readonly TimeSpan Timer = TimeSpan.FromSeconds(5);

    public sealed record Line(string Name, string? Note, int Qty, int? Extra, Paid Status, DateTime When, DateTime? Maybe, DateOnly Day);

    private static readonly Line[] Lines =
    [
        new("Ada", "first", 1, null, Paid.Yes, new(2026, 1, 1, 9, 30, 0), null, new(2026, 1, 1)),
        new("Grace", "", 2, 20, Paid.No, new(2026, 1, 2, 23, 59, 0), new(2026, 1, 2, 12, 0, 0), new(2026, 1, 2)),
        new("Alan", null, 3, 30, Paid.Refunded, new(2026, 1, 3, 0, 0, 0), new(2026, 1, 3, 12, 0, 0), new(2026, 1, 3)),
        new("Smith, John", "last", 4, 40, Paid.Yes, new(2026, 1, 3, 9, 0, 0), null, new(2026, 1, 3)),
    ];

    private BunitNavigationManager Nav => Services.GetRequiredService<BunitNavigationManager>();

    private static RenderFragment Col<TValue>(Expression<Func<Line, TValue>> property, IReadOnlyList<FilterOperator>? operators = null) => builder =>
    {
        builder.OpenComponent<PropertyColumn<Line, TValue>>(0);
        builder.AddComponentParameter(1, "Property", property);
        builder.AddComponentParameter(2, "Filterable", true);
        builder.AddComponentParameter(3, "FilterOperators", operators);
        builder.CloseComponent();
    };

    private static RenderFragment AllColumns => b =>
    {
        b.AddContent(0, Col(r => r.Name));
        b.AddContent(1, Col(r => r.Note));
        b.AddContent(2, Col(r => r.Qty));
        b.AddContent(3, Col(r => r.Extra));
        b.AddContent(4, Col(r => r.Status));
        b.AddContent(5, Col(r => r.When));
        b.AddContent(6, Col(r => r.Maybe));
        b.AddContent(7, Col(r => r.Day));
    };

    private IRenderedComponent<DataGrid<Line>> Grid(string url = "/lines", RenderFragment? columns = null)
    {
        Nav.NavigateTo(url);
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        return Render<DataGrid<Line>>(p => p.Add(t => t.Items, Lines).Add(t => t.ExportCsvHref, "/export").Add(t => t.ChildContent, columns ?? AllColumns));
    }

    private IRenderedComponent<Table<Line>> Table(string url = "/lines", RenderFragment? columns = null)
    {
        Nav.NavigateTo(url);
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        return Render<Table<Line>>(p => p.Add(t => t.Items, Lines).Add(t => t.ChildContent, columns ?? AllColumns));
    }

    private static string[] Names<T>(IRenderedComponent<T> cut) where T : IComponent =>
        cut.FindAll("tbody tr:not(.ib-table-empty)").Select(r => r.Children[0].TextContent).ToArray();

    private static string[] Options<T>(IRenderedComponent<T> cut, string name) where T : IComponent =>
        cut.FindAll($"select[name='{name}'] option").Select(o => o.TextContent).ToArray();

    private static string? Chosen<T>(IRenderedComponent<T> cut, string name) where T : IComponent =>
        cut.Find($"select[name='{name}'] option[selected]").GetAttribute("value");

    private static string[] Tags<T>(IRenderedComponent<T> cut) where T : IComponent =>
        cut.FindAll(".ib-table-filter-tags .ib-tag").Select(t => t.TextContent.Trim()).ToArray();

    private static readonly string[] TextWords = ["Contains", "Does not contain", "Equals", "Does not equal", "Starts with", "Ends with", "Is empty", "Is not empty"];
    private static readonly string[] NumberWords = ["Equals", "Does not equal", "Greater than", "Greater or equal", "Less than", "Less or equal", "Between"];
    private static readonly string[] DateWords = ["On", "Not on", "After", "On or after", "Before", "On or before", "Between"];

    [Fact]
    public void A_DataGrids_column_chooses_an_operator_by_its_kind()
    {
        var cut = Grid();

        Assert.Equal(TextWords, Options(cut, "f-note-op"));
        Assert.Equal(NumberWords, Options(cut, "f-qty-op"));
        Assert.Equal([.. NumberWords, "Is empty", "Is not empty"], Options(cut, "f-extra-op"));
        Assert.Equal(DateWords, Options(cut, "f-when-op"));
        Assert.Equal(DateWords, Options(cut, "f-day-op"));
        Assert.Equal([.. DateWords, "Is empty", "Is not empty"], Options(cut, "f-maybe-op"));

        // An enum's is still its menu.
        Assert.Empty(cut.FindAll("select[name='f-status-op']"));
        Assert.NotEmpty(cut.FindAll("a.ib-menu-item"));

        // The default is chosen, and said so for the script.
        Assert.Equal("contains", Chosen(cut, "f-note-op"));
        Assert.Equal("eq", Chosen(cut, "f-qty-op"));
        Assert.Equal("eq", Chosen(cut, "f-when-op"));
        Assert.Equal("eq", cut.Find("select[name='f-when-op']").GetAttribute("data-ib-default"));
    }

    [Fact]
    public void A_panels_controls_are_named_and_post_with_the_form()
    {
        var cut = Grid();
        var form = cut.Find("form.ib-table-filter-form").Id;

        var select = cut.Find("select[name='f-qty-op']");
        Assert.Equal(form, select.GetAttribute("form"));
        Assert.Equal("Operator", select.GetAttribute("aria-label"));
        Assert.Equal("Filter Qty", cut.Find($"#{select.Closest("[role=group]")!.GetAttribute("aria-labelledby")}").TextContent);

        // A number's value and second value: named for what they hold with Between, which CSS shows.
        var first = cut.Find("input[name='f-qty']");
        var second = cut.Find("input[name='f-qty-to']");
        Assert.All(new[] { first, second }, i => Assert.Equal(form, i.GetAttribute("form")));
        Assert.Equal("decimal", first.GetAttribute("inputmode"));
        var label = cut.Find($"label[for='{first.Id}']");
        Assert.Equal("Min", label.QuerySelector(".ib-table-filter-if-range")!.TextContent);
        Assert.Equal("Value", label.QuerySelector(".ib-table-filter-if-one")!.TextContent);
        Assert.Equal("Max", cut.Find($"label[for='{second.Id}']").TextContent);

        // A date's: dates, and From and To.
        Assert.Equal("date", cut.Find("input[name='f-when']").GetAttribute("type"));
        Assert.Equal("date", cut.Find("input[name='f-when-to']").GetAttribute("type"));
        Assert.Equal("To", cut.Find($"label[for='{cut.Find("input[name='f-when-to']").Id}']").TextContent);

        // Text has no second value, and its one is named for screen readers.
        Assert.Empty(cut.FindAll("input[name='f-note-to']"));
        Assert.Equal("Value", cut.Find($"label[for='{cut.Find("input[name='f-note']").Id}']").TextContent);
    }

    [Fact]
    public void A_Tables_column_keeps_its_simple_field_until_it_asks()
    {
        var plain = Table();

        Assert.Empty(plain.FindAll("select"));
        Assert.Equal("Min", plain.Find($"label[for='{plain.Find("input[name='f-qty']").Id}']").TextContent);
        Assert.Equal("Filter Note", plain.Find($"label[for='{plain.Find("input[name='f-note']").Id}']").TextContent);
        Assert.Empty(plain.FindAll("input[name='f-when-to']"));

        var asks = Table(columns: b =>
        {
            b.AddContent(0, Col(r => r.Name));
            b.AddContent(1, Col(r => r.Qty, [FilterOperator.GreaterThan, FilterOperator.Between]));
            b.AddContent(2, Col(r => r.Note, [FilterOperator.StartsWith]));
        });
        Assert.Equal(new[] { "Greater than", "Between" }, Options(asks, "f-qty-op"));
        Assert.Equal("gt", Chosen(asks, "f-qty-op"));

        // One operator hides the Select, and is the one used.
        Assert.Empty(asks.FindAll("select[name='f-note-op']"));
        Assert.Empty(asks.FindAll("select[name='f-name-op']"));

        var rows = Table("/lines?f-note=fir", columns: b =>
        {
            b.AddContent(0, Col(r => r.Name));
            b.AddContent(1, Col(r => r.Note, [FilterOperator.StartsWith]));
        });
        Assert.Equal(new[] { "Ada" }, Names(rows));
        Assert.Contains(Tags(rows), t => t.Contains("Note: Starts with fir", StringComparison.Ordinal));
        Assert.Empty(Names(Table("/lines?f-note=irst", columns: b =>
        {
            b.AddContent(0, Col(r => r.Name));
            b.AddContent(1, Col(r => r.Note, [FilterOperator.StartsWith]));
        })));
    }

    // Each operator: the rows, the tag's words, and the export link's text meaning the same rows.
    [Theory]
    [InlineData("f-name=a", "Name: a", new[] { "Ada", "Grace", "Alan" })]
    [InlineData("f-name-op=notcontains&f-name=a", "Name: Does not contain a", new[] { "Smith, John" })]
    [InlineData("f-name-op=eq&f-name=ADA", "Name: Equals ADA", new[] { "Ada" })]
    [InlineData("f-name-op=ne&f-name=ADA", "Name: Does not equal ADA", new[] { "Grace", "Alan", "Smith, John" })]
    [InlineData("f-name-op=startswith&f-name=a", "Name: Starts with a", new[] { "Ada", "Alan" })]
    [InlineData("f-name-op=endswith&f-name=E", "Name: Ends with E", new[] { "Grace" })]
    [InlineData("f-name-op=eq&f-name=Smith%2C%20John", "Name: Equals Smith, John", new[] { "Smith, John" })]
    [InlineData("f-note-op=isempty", "Note: Is empty", new[] { "Grace", "Alan" })]
    [InlineData("f-note-op=isnotempty", "Note: Is not empty", new[] { "Ada", "Smith, John" })]
    [InlineData("f-qty=2", "Qty: 2", new[] { "Grace" })]
    [InlineData("f-qty-op=ne&f-qty=2", "Qty: Does not equal 2", new[] { "Ada", "Alan", "Smith, John" })]
    [InlineData("f-qty-op=gt&f-qty=2", "Qty: Greater than 2", new[] { "Alan", "Smith, John" })]
    [InlineData("f-qty-op=ge&f-qty=2", "Qty: 2 or more", new[] { "Grace", "Alan", "Smith, John" })]
    [InlineData("f-qty-op=lt&f-qty=2", "Qty: Less than 2", new[] { "Ada" })]
    [InlineData("f-qty-op=le&f-qty=2", "Qty: 2 or less", new[] { "Ada", "Grace" })]
    [InlineData("f-qty-op=between&f-qty=2&f-qty-to=3", "Qty: 2–3", new[] { "Grace", "Alan" })]
    [InlineData("f-qty-op=between&f-qty=3", "Qty: 3 or more", new[] { "Alan", "Smith, John" })]
    [InlineData("f-qty-op=between&f-qty=&f-qty-to=1", "Qty: 1 or less", new[] { "Ada" })]
    [InlineData("f-extra-op=isempty", "Extra: Is empty", new[] { "Ada" })]
    [InlineData("f-extra-op=isnotempty", "Extra: Is not empty", new[] { "Grace", "Alan", "Smith, John" })]
    [InlineData("f-when=2026-01-03", "When: 2026-01-03", new[] { "Alan", "Smith, John" })]
    [InlineData("f-when-op=ne&f-when=2026-01-03", "When: Not on 2026-01-03", new[] { "Ada", "Grace" })]
    [InlineData("f-when-op=gt&f-when=2026-01-02", "When: After 2026-01-02", new[] { "Alan", "Smith, John" })]
    [InlineData("f-when-op=ge&f-when=2026-01-02", "When: On or after 2026-01-02", new[] { "Grace", "Alan", "Smith, John" })]
    [InlineData("f-when-op=lt&f-when=2026-01-02", "When: Before 2026-01-02", new[] { "Ada" })]
    [InlineData("f-when-op=le&f-when=2026-01-02", "When: On or before 2026-01-02", new[] { "Ada", "Grace" })]
    [InlineData("f-when-op=between&f-when=2026-01-02&f-when-to=2026-01-03", "When: 2026-01-02–2026-01-03", new[] { "Grace", "Alan", "Smith, John" })]
    [InlineData("f-when-op=between&f-when-to=2026-01-02", "When: On or before 2026-01-02", new[] { "Ada", "Grace" })]
    [InlineData("f-maybe-op=isempty", "Maybe: Is empty", new[] { "Ada", "Smith, John" })]
    [InlineData("f-day-op=le&f-day=2026-01-02", "Day: On or before 2026-01-02", new[] { "Ada", "Grace" })]
    public void An_operator_keeps_its_rows_and_the_export_keeps_the_same(string query, string tag, string[] expected)
    {
        var cut = Grid("/lines?" + query);

        Assert.Equal(expected, Names(cut));
        Assert.Contains(Tags(cut), t => t.Contains(tag, StringComparison.Ordinal));

        var href = cut.Find("a[download]").GetAttribute("href")!;
        var request = DataRequest<Line>.Parse(href);
        Assert.NotNull(request.Filter);
        Assert.Equal(expected, request.ApplyFiltering(Lines.AsQueryable(), DataFields.Of<Line>()).Select(l => l.Name).ToArray());
    }

    [Theory]
    [InlineData("f-name-op=startswith&f-name=a")]
    [InlineData("f-qty-op=gt&f-qty=2")]
    [InlineData("f-qty-op=between&f-qty=2&f-qty-to=3")]
    [InlineData("f-qty=2")]
    [InlineData("f-note-op=isempty")]
    [InlineData("f-when-op=between&f-when=2026-01-02&f-when-to=2026-01-03")]
    public void What_the_writer_writes_the_reader_reads(string query)
    {
        var cut = Grid("/lines?" + query);
        var key = query[..query.IndexOf('=')].Replace("-op", "", StringComparison.Ordinal);
        var column = cut.Instance.Columns.Single(c => $"f-{c.Key}" == key);
        var parsed = QueryString.Parse(query);

        var read = ColumnFilter.Read(column, key, parsed)!;
        var written = ColumnFilter.Write(column, key, read);

        // The same keys and values, and nothing else: the default isn't written, nor an empty end.
        var again = written.Where(k => k.Value is not null).Select(k => $"{k.Key}={(k.Value is string[] many ? many[0] : k.Value)}").Order().ToArray();
        Assert.Equal(query.Split('&').Order(), again);
    }

    // A GET form posts every field: only a value makes a filter.
    [Theory]
    [InlineData("f-name-op=contains&f-name=&f-qty-op=eq&f-qty=&f-qty-to=&f-when-op=eq&f-when=&f-when-to=", "f-qty-op", "eq")]
    [InlineData("f-name-op=startswith&f-name=", "f-name-op", "startswith")]
    [InlineData("f-qty-op=gt&f-qty=&f-qty-to=", "f-qty-op", "gt")]
    [InlineData("f-qty-op=between&f-qty=%20&f-qty-to=", "f-qty-op", "between")]
    [InlineData("f-when-op=lt&f-when=", "f-when-op", "lt")]
    [InlineData("f-qty-op=gt&f-qty=&f-qty-to=9", "f-qty-op", "gt")]
    [InlineData("f-qty-op=bogus&f-qty=", "f-qty-op", "eq")]
    public void A_stray_operator_from_a_get_post_is_no_filter_but_draws_the_select(string query, string select, string chosen)
    {
        var cut = Grid("/lines?" + query);

        Assert.Equal(Lines.Length, Names(cut).Length);
        Assert.Empty(Tags(cut));
        Assert.True(cut.Find(".ib-table-filter-tags").HasAttribute("hidden"));
        Assert.Empty(cut.FindAll(".ib-table-filter-button[data-active]"));
        Assert.DoesNotContain("filter=", cut.Find("a[download]").GetAttribute("href")!, StringComparison.Ordinal);
        Assert.Equal(chosen, Chosen(cut, select));

        // Its own keys don't travel with the rest of the address.
        Assert.DoesNotContain(cut.FindAll("input[type=hidden]"), i => i.GetAttribute("name")!.StartsWith("f-", StringComparison.Ordinal) && i.GetAttribute("name") != "f-status");
    }

    [Theory]
    // A field the operator doesn't use posts all the same, and means nothing.
    [InlineData("f-qty-op=gt&f-qty=2&f-qty-to=2", new[] { "Alan", "Smith, John" }, "Qty: Greater than 2")]
    [InlineData("f-note-op=isempty&f-note=first", new[] { "Grace", "Alan" }, "Note: Is empty")]
    // An unknown word, or one this kind of column doesn't take, reads as the default.
    [InlineData("f-name-op=bogus&f-name=ada", new[] { "Ada" }, "Name: ada")]
    [InlineData("f-qty-op=contains&f-qty=2", new[] { "Grace" }, "Qty: 2")]
    [InlineData("f-qty-op=isempty&f-qty=2", new[] { "Grace" }, "Qty: 2")]
    [InlineData("F-NAME-OP=STARTSWITH&f-name=a", new[] { "Ada", "Alan" }, "Name: Starts with a")]
    public void A_get_post_means_what_the_panel_shows(string query, string[] expected, string tag)
    {
        var cut = Grid("/lines?" + query);

        Assert.Equal(expected, Names(cut));
        Assert.Contains(Tags(cut), t => t.Contains(tag, StringComparison.Ordinal));
    }

    [Fact]
    public void A_Tables_simple_field_ignores_an_operator_in_the_address()
    {
        var cut = Table("/lines?f-qty-op=gt&f-qty=2&f-name-op=startswith&f-name=a");

        // Between, as its Min and Max show, and Contains, as its one field does.
        Assert.Equal(new[] { "Grace", "Alan", "Smith, John" }.Where(n => n.Contains('a', StringComparison.OrdinalIgnoreCase)), Names(cut));
        Assert.Contains(Tags(cut), t => t.Contains("Qty: 2 or more", StringComparison.Ordinal));
        Assert.Contains(Tags(cut), t => t.Contains("Name: a", StringComparison.Ordinal));
    }

    [Fact]
    public void FilterOperators_holds_against_an_address_or_a_bound_filter()
    {
        RenderFragment columns = b =>
        {
            b.AddContent(0, Col(r => r.Name, [FilterOperator.StartsWith, FilterOperator.Equals]));
            b.AddContent(1, Col(r => r.Extra, [FilterOperator.Equals, FilterOperator.IsEmpty]));
            b.AddContent(2, Col(r => r.When, [FilterOperator.OnDay]));
        };

        // In the address: as the default, which isn't Contains.
        var cut = Table("/lines?f-name-op=contains&f-name=a", columns);
        Assert.Equal(new[] { "Ada", "Alan" }, Names(cut));
        Assert.Equal("startswith", Chosen(cut, "f-name-op"));

        // Is empty on a number is a null check, which its field has to take.
        Assert.Equal(new[] { "Ada" }, Names(Table("/lines?f-extra-op=isempty", columns)));

        // OnDay, the same choice as On.
        Assert.Equal(new[] { "Grace" }, Names(Table("/lines?f-when=2026-01-02", columns)));

        // In a filter from code: what no panel offers shows as a tag, and matches nothing.
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var bound = Render<Table<Line>>(p => p
            .Add(t => t.Items, Lines)
            .Add(t => t.Filter, Filter.Where("name", FilterOperator.Contains, "a"))
            .Add(t => t.FilterChanged, (Filter? _) => { })
            .Add(t => t.ChildContent, columns));
        Assert.Empty(Names(bound));
        Assert.Contains(Tags(bound), t => t.Contains("contains(name,'a')", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(FilterOperator.IsEmpty, "first")]
    [InlineData(FilterOperator.GreaterThan, "none")]
    public void FilterOperators_that_cant_work_say_so(FilterOperator first, string words)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Table(columns: b => b.AddContent(0, Col(r => r.Note, [first]))));

        Assert.Contains(words, ex.Message, StringComparison.Ordinal);
        Assert.Contains("FilterOperators", ex.Message, StringComparison.Ordinal);
    }

    // A list written in place is a new one each render, which mustn't look like a change.
    [Fact]
    public void A_list_written_in_place_doesnt_render_the_table_again_and_again()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var builds = 0;
        var cut = Render<Table<Line>>(p => p
            .Add(t => t.Items, Lines)
            .Add(t => t.ChildContent, b =>
            {
                // Bounded: a table that kept rendering would otherwise run for ever.
                if (++builds > 50)
                {
                    throw new InvalidOperationException("The table keeps rendering.");
                }

                b.AddContent(0, Col(r => r.Qty, new List<FilterOperator> { FilterOperator.GreaterThan, FilterOperator.Between }));
            }));

        var settled = builds;
        cut.Render();
        cut.Render();

        Assert.True(builds <= settled + 2, $"{builds - settled} builds for 2 renders.");

        // A real change does show.
        var changed = Render<Table<Line>>(p => p
            .Add(t => t.Items, Lines)
            .Add(t => t.ChildContent, Col(r => r.Qty, [FilterOperator.GreaterThan, FilterOperator.Between])));
        Assert.Equal(new[] { "Greater than", "Between" }, changed.FindAll("select option").Select(o => o.TextContent));
        changed.Render(p => p.Add(t => t.ChildContent, Col(r => r.Qty, [FilterOperator.LessThan, FilterOperator.Between])));
        Assert.Equal(new[] { "Less than", "Between" }, changed.FindAll("select option").Select(o => o.TextContent));
    }

    private IRenderedComponent<DataGrid<Line>> Bound(List<Filter?> reported, Filter? filter = null)
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var cut = Render<DataGrid<Line>>(p => p
            .Add(t => t.Items, Lines)
            .Add(t => t.Filter, filter)
            .Add(t => t.FilterChanged, (Filter? f) => reported.Add(f))
            .Add(t => t.ChildContent, AllColumns));
        cut.Instance.FilterDelay = TimeSpan.FromMilliseconds(1);
        return cut;
    }

    // Typing's pause ends on a timer, whose render can come between finding an element and using it.
    private static void Again<T>(IRenderedComponent<T> cut, Action act) where T : IComponent => cut.WaitForAssertion(act, Timer);

    private static IEnumerable<AngleSharp.Dom.IElement> Panel<T>(IRenderedComponent<T> cut, string title) where T : IComponent =>
        cut.FindAll(".ib-table-filter-panel").Where(p => p.QuerySelector(".ib-table-filter-label")!.TextContent == title);

    [Fact]
    public void A_bound_operator_applies_at_once_with_a_value_and_only_shapes_the_panel_without_one()
    {
        var reported = new List<Filter?>();
        var cut = Bound(reported);
        var url = Nav.Uri;
        var qty = () => Panel(cut, "Filter Qty").Single();

        // No value: nothing is reported, nothing filtered, and it stays chosen through renders.
        Again(cut, () => qty().QuerySelector("select")!.Change("gt"));
        Again(cut, () => qty().QuerySelector("select")!.Change("lt"));
        Assert.Empty(reported);
        Assert.Equal(Lines.Length, Names(cut).Length);
        Assert.Empty(cut.FindAll(".ib-table-filter-button[data-active]"));
        Assert.Equal("lt", qty().QuerySelector("option[selected]")?.GetAttribute("value") ?? qty().QuerySelector("select")!.GetAttribute("value"));

        // A value then uses it.
        Again(cut, () => qty().QuerySelector("input")!.Input("2"));
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(new[] { "Ada" }, Names(cut));
            Assert.Equal(new FieldFilter("qty", FilterOperator.LessThan, 2), reported[^1]!.Find("qty"));
        }, Timer);
        var count = reported.Count;

        // With one, each new operator applies, again and again.
        foreach (var (word, op, rows) in new[]
        {
            ("gt", FilterOperator.GreaterThan, new[] { "Alan", "Smith, John" }),
            ("eq", FilterOperator.Equals, new[] { "Grace" }),
            ("ge", FilterOperator.GreaterThanOrEqual, new[] { "Grace", "Alan", "Smith, John" }),
        })
        {
            Again(cut, () => qty().QuerySelector("select")!.Change(word));
            count++;
            cut.WaitForAssertion(() =>
            {
                Assert.Equal(rows, Names(cut));
                Assert.Equal(new FieldFilter("qty", op, 2), reported[^1]!.Find("qty"));
                Assert.Equal(count, reported.Count);
            }, Timer);
        }

        Assert.Equal(url, Nav.Uri);

        // Removing the tag goes back to the default operator.
        Again(cut, () => cut.FindAll(".ib-table-filter-tags .ib-tag button").Single().Click());
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(Lines.Length, Names(cut).Length);
            Assert.Null(reported[^1]);
            Assert.Equal("", qty().QuerySelector("input")!.GetAttribute("value") ?? "");
            Assert.Equal("eq", qty().QuerySelector("option[selected]")?.GetAttribute("value") ?? "eq");
        }, Timer);
    }

    [Fact]
    public void A_bound_between_holds_both_ends_and_is_empty_needs_none()
    {
        var reported = new List<Filter?>();
        var cut = Bound(reported);
        var qty = () => Panel(cut, "Filter Qty").Single();
        var note = () => Panel(cut, "Filter Note").Single();

        Again(cut, () => qty().QuerySelector("select")!.Change("between"));
        Assert.Empty(reported);
        Again(cut, () => qty().QuerySelectorAll("input")[0].Input("1"));
        Again(cut, () => qty().QuerySelectorAll("input")[1].Input("3."));
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(new[] { "Ada", "Grace", "Alan" }, Names(cut));
            Assert.Equal(new FieldFilter("qty", FilterOperator.Between, 1, 3), reported[^1]!.Find("qty"));
            // What was typed stays in its box.
            Assert.Equal("3.", qty().QuerySelectorAll("input")[1].GetAttribute("value"));
        }, Timer);

        // Applies with no value, and goes when another operator takes its place.
        Again(cut, () => note().QuerySelector("select")!.Change("isempty"));
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(new[] { "Grace", "Alan" }, Names(cut));
            Assert.Equal(new FieldFilter("note", FilterOperator.IsEmpty), reported[^1]!.Find("note"));
        }, Timer);
        Again(cut, () => note().QuerySelector("select")!.Change("contains"));
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(new[] { "Ada", "Grace", "Alan" }, Names(cut));
            Assert.Null(reported[^1]!.Find("note"));
        }, Timer);
    }

    [Theory]
    [InlineData("qty", FilterOperator.GreaterThan, 2, "gt", "Filter Qty: Greater than 2")]
    [InlineData("name", FilterOperator.StartsWith, "a", "startswith", "Filter Name: Starts with a")]
    public void A_condition_set_in_code_shows_in_its_panel(string field, FilterOperator op, object value, string word, string name)
    {
        var cut = Bound([], filter: Filter.Where(field, op, value));

        var panel = cut.FindAll(".ib-table-filter-panel").Single(p => p.QuerySelector("select")?.GetAttribute("value") == word || p.QuerySelector($"option[value='{word}'][selected]") is not null);
        Assert.NotNull(panel);
        Assert.Contains(cut.FindAll(".ib-table-filter-button"), b => b.GetAttribute("aria-label") == name);
        Assert.DoesNotContain(Tags(cut), t => t.Contains('(', StringComparison.Ordinal));
    }

    [Fact]
    public void A_null_check_set_in_code_is_the_panels_is_empty_and_a_time_stays_a_tag()
    {
        var cut = Bound([], filter: Filter.All(
            Filter.Where("extra", FilterOperator.IsNull),
            Filter.Where("when", FilterOperator.GreaterThan, new DateTime(2026, 1, 2, 12, 0, 0))));

        Assert.Contains(cut.FindAll(".ib-table-filter-button"), b => b.GetAttribute("aria-label") == "Filter Extra: Is empty");
        Assert.Contains(Tags(cut), t => t.Contains("when gt 2026-01-02T12:00:00", StringComparison.Ordinal));
        Assert.Empty(Names(cut));
    }
}
