using System.Linq.Expressions;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

// A page of groups: in the address, whole, with each group's rows opened by a link.
public class DataGridGroupTests : BunitContext
{
    public sealed record Sale(int Id, string Region, string? Rep, int Units);

    private static readonly Sale[] Sales =
    [
        new(1, "East/Coast", "Ada", 2),
        new(2, "East/Coast", "Ada", 4),
        new(3, "East/Coast", null, 1),
        new(4, "North", "Cy", 6),
        new(5, "West", "Bo", 3),
        new(6, "West", "Bo", 5),
    ];

    public DataGridGroupTests() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    private BunitNavigationManager Nav => Services.GetRequiredService<BunitNavigationManager>();

    private static RenderFragment Columns => builder =>
    {
        builder.OpenComponent<PropertyColumn<Sale, int>>(0);
        builder.AddComponentParameter(1, "Property", (Expression<Func<Sale, int>>)(s => s.Id));
        builder.CloseComponent();
        builder.OpenComponent<PropertyColumn<Sale, string>>(2);
        builder.AddComponentParameter(3, "Property", (Expression<Func<Sale, string>>)(s => s.Region));
        builder.CloseComponent();
        builder.OpenComponent<PropertyColumn<Sale, string?>>(4);
        builder.AddComponentParameter(5, "Property", (Expression<Func<Sale, string?>>)(s => s.Rep));
        builder.CloseComponent();
        builder.OpenComponent<PropertyColumn<Sale, int>>(6);
        builder.AddComponentParameter(7, "Property", (Expression<Func<Sale, int>>)(s => s.Units));
        builder.AddComponentParameter(8, "Aggregate", (DataAggregate?)DataAggregate.Sum);
        builder.AddComponentParameter(9, "GroupAggregate", (DataAggregate?)DataAggregate.Sum);
        builder.CloseComponent();
    };

    private IRenderedComponent<DataGrid<Sale>> Grid(string address, Action<ComponentParameterCollectionBuilder<DataGrid<Sale>>>? more = null, RenderFragment? columns = null)
    {
        Nav.NavigateTo(address);
        return Render<DataGrid<Sale>>(p =>
        {
            p.Add(x => x.Items, Sales);
            p.Add(x => x.Groupable, true);
            p.Add(x => x.ChildContent, columns ?? Columns);
            more?.Invoke(p);
        });
    }

    private static string[] Rows(IRenderedComponent<DataGrid<Sale>> cut) =>
        cut.FindAll("tbody tr").Select(r => r.ClassList.Contains("ib-grid-group")
            ? "g:" + System.Text.RegularExpressions.Regex.Replace(r.QuerySelector("th a,th button")!.TextContent.Trim().Split(':', 2)[1].Trim(), @"\s*\(\d+\)$", "")
            : "r:" + r.QuerySelectorAll("td").First(c => !c.ClassList.Contains("ib-grid-lead") && !c.ClassList.Contains("ib-table-select-cell")).TextContent).ToArray();

    [Fact]
    public void A_page_counts_top_level_groups_and_a_grouped_column_leaves()
    {
        var cut = Grid("/sales?group=region&page=2", p => p.Add(x => x.PageSize, 1));

        Assert.Equal(new[] { "g:North" }, Rows(cut));
        Assert.Equal("2–2 of 3 groups", cut.Find(".ib-data-summary").TextContent);
        Assert.DoesNotContain(cut.FindAll("thead th"), th => th.TextContent.Contains("Region"));
        var row = cut.Find("tr.ib-grid-group");
        Assert.Equal("false", row.GetAttribute("aria-expanded"));
        Assert.Equal("1", row.GetAttribute("aria-level"));
        Assert.Equal("2", row.GetAttribute("aria-posinset"));
        Assert.Equal("3", row.GetAttribute("aria-setsize"));
    }

    [Fact]
    public void A_link_opens_a_group_with_all_its_rows_and_its_whole_count_and_aggregate()
    {
        var cut = Grid("/sales?group=region", p => p.Add(x => x.PageSize, 2));
        var href = cut.Find("tr.ib-grid-group a").GetAttribute("href")!;

        cut = Grid(href, p => p.Add(x => x.PageSize, 2));

        Assert.Equal(new[] { "g:East/Coast", "r:1", "r:2", "r:3", "g:North" }, Rows(cut));
        var group = cut.Find("tr.ib-grid-group");
        Assert.Equal("true", group.GetAttribute("aria-expanded"));
        Assert.EndsWith("(3)", group.QuerySelector("th a")!.TextContent.Trim());
        Assert.Equal("Sum: 7", group.Children[^1].TextContent.Trim());
        Assert.Equal("Sum: 21", cut.FindAll("tfoot td")[^1].TextContent.Trim());

        // And the same link closes it.
        cut = Grid(cut.Find("tr.ib-grid-group a").GetAttribute("href")!, p => p.Add(x => x.PageSize, 2));
        Assert.Equal(new[] { "g:East/Coast", "g:North" }, Rows(cut));
    }

    [Fact]
    public void Levels_nest_and_a_null_value_has_its_own_group()
    {
        var cut = Grid("/sales?group=region,-rep&open=East%252FCoast");
        Assert.Equal(new[] { "g:East/Coast", "g:Ada", "g:(None)", "g:North", "g:West" }, Rows(cut));
        Assert.Equal("2", cut.FindAll("tr.ib-grid-group")[1].GetAttribute("aria-level"));

        var none = cut.FindAll("tr.ib-grid-group")[2].QuerySelector("a")!.GetAttribute("href")!;
        cut = Grid(none);

        Assert.Equal(new[] { "g:East/Coast", "g:Ada", "g:(None)", "r:3", "g:North", "g:West" }, Rows(cut));
    }

    [Fact]
    public void All_open_with_exceptions()
    {
        var cut = Grid("/sales?group=region", p => p.Add(x => x.ExpandAllGroups, true));
        Assert.Equal(new[] { "g:East/Coast", "r:1", "r:2", "r:3", "g:North", "r:4", "g:West", "r:5", "r:6" }, Rows(cut));

        var shut = cut.FindAll("tr.ib-grid-group")[1].QuerySelector("a")!.GetAttribute("href")!;
        Assert.Contains("shut=North", shut);

        cut = Grid(shut, p => p.Add(x => x.ExpandAllGroups, true));
        Assert.Equal(new[] { "g:East/Coast", "r:1", "r:2", "r:3", "g:North", "g:West", "r:5", "r:6" }, Rows(cut));
    }

    [Fact]
    public void A_provider_gets_each_open_groups_rows_as_an_ordinary_filtered_request()
    {
        var rowRequests = new List<DataRequest<Sale>>();
        Nav.NavigateTo("/sales?group=region&open=West");
        var cut = Render<DataGrid<Sale>>(p => p
            .Add(x => x.Groupable, true)
            .Add(x => x.ChildContent, Columns)
            .Add(x => x.GroupsProvider, r => ValueTask.FromResult(r.ToResult(Sales.AsQueryable(), DataFields.Of<Sale>())))
            .Add(x => x.ItemsProvider, r =>
            {
                rowRequests.Add(r);
                return ValueTask.FromResult(r.ToResult(Sales.AsQueryable(), DataFields.Of<Sale>()));
            }));

        Assert.Equal("West", Assert.Single(rowRequests).Filter!.Find("region")!.Value);
        Assert.Equal(new[] { "g:East/Coast", "g:North", "g:West", "r:5", "r:6" }, Rows(cut));
    }

    [Fact]
    public void A_group_past_the_limit_links_to_the_ordinary_grid_with_only_its_rows()
    {
        RenderFragment filterable = builder =>
        {
            builder.OpenComponent<PropertyColumn<Sale, int>>(0);
            builder.AddComponentParameter(1, "Property", (Expression<Func<Sale, int>>)(s => s.Id));
            builder.CloseComponent();
            builder.OpenComponent<PropertyColumn<Sale, string>>(2);
            builder.AddComponentParameter(3, "Property", (Expression<Func<Sale, string>>)(s => s.Region));
            builder.AddComponentParameter(4, "Filterable", true);
            builder.CloseComponent();
        };

        var cut = Grid("/sales?group=region&open=West", p => p.Add(x => x.GroupRowLimit, 1), filterable);
        Assert.Contains("Showing 1 of 2.", cut.Find(".ib-grid-group-more").TextContent);

        var href = cut.Find(".ib-grid-group-more a").GetAttribute("href")!;
        cut = Grid(href, p => p.Add(x => x.GroupRowLimit, 1), filterable);

        Assert.Empty(cut.FindAll("tr.ib-grid-group"));
        Assert.Equal(new[] { "r:5", "r:6" }, Rows(cut));
    }

    [Fact]
    public void A_template_column_groups_by_a_computed_key_with_aggregates_under_the_rows()
    {
        RenderFragment columns = builder =>
        {
            builder.OpenComponent<PropertyColumn<Sale, int>>(0);
            builder.AddComponentParameter(1, "Property", (Expression<Func<Sale, int>>)(s => s.Id));
            builder.CloseComponent();
            builder.OpenComponent<TemplateColumn<Sale>>(2);
            builder.AddComponentParameter(3, "Title", "Size");
            builder.AddComponentParameter(4, "GroupBy", (Expression<Func<Sale, object?>>)(s => s.Units / 3));
            builder.AddComponentParameter(5, "GroupFormat", "0.0");
            builder.AddComponentParameter(6, "ChildContent", (RenderFragment<Sale>)(s => b => b.AddContent(0, s.Units)));
            builder.CloseComponent();
            builder.OpenComponent<PropertyColumn<Sale, int>>(7);
            builder.AddComponentParameter(8, "Property", (Expression<Func<Sale, int>>)(s => s.Units));
            builder.AddComponentParameter(9, "GroupFooterAggregate", (DataAggregate?)DataAggregate.Max);
            builder.CloseComponent();
        };

        var cut = Grid("/sales?group=Size&open=1", null, columns);

        Assert.Equal(new[] { "g:0.0", "g:1.0", "r:2", "r:5", "r:6", "g:2.0" }, Rows(cut).Where(r => r != "r:").ToArray());
        Assert.Equal("Max: 5", cut.Find("tr.ib-grid-group-foot").Children[^1].TextContent.Trim());
    }

    [Fact]
    public void Text_from_GroupBy_groups_and_an_aggregate_in_the_first_column_still_shows()
    {
        RenderFragment columns = builder =>
        {
            builder.OpenComponent<PropertyColumn<Sale, int>>(0);
            builder.AddComponentParameter(1, "Property", (Expression<Func<Sale, int>>)(s => s.Units));
            builder.AddComponentParameter(2, "GroupAggregate", (DataAggregate?)DataAggregate.Sum);
            builder.CloseComponent();
            builder.OpenComponent<TemplateColumn<Sale>>(3);
            builder.AddComponentParameter(4, "Title", "Who");
            builder.AddComponentParameter(5, "GroupBy", (Expression<Func<Sale, object?>>)(s => s.Region));
            builder.AddComponentParameter(6, "ChildContent", (RenderFragment<Sale>)(s => b => b.AddContent(0, s.Region)));
            builder.CloseComponent();
        };

        var cut = Grid("/sales?group=Who", null, columns);

        Assert.Equal(new[] { "g:East/Coast", "g:North", "g:West" }, Rows(cut));
        Assert.Contains("Sum: 7", cut.Find("tr.ib-grid-group").TextContent);
    }

    [Fact]
    public void An_address_cant_group_by_a_column_people_cant_see()
    {
        RenderFragment columns = builder =>
        {
            builder.OpenComponent<PropertyColumn<Sale, int>>(0);
            builder.AddComponentParameter(1, "Property", (Expression<Func<Sale, int>>)(s => s.Id));
            builder.CloseComponent();
            builder.OpenComponent<PropertyColumn<Sale, string>>(2);
            builder.AddComponentParameter(3, "Property", (Expression<Func<Sale, string>>)(s => s.Region));
            builder.AddComponentParameter(4, "Visible", false);
            builder.AddComponentParameter(5, "Pickable", false);
            builder.CloseComponent();
        };

        Assert.Empty(Grid("/sales?group=region", null, columns).FindAll("tr.ib-grid-group"));
    }

    [Fact]
    public void A_provider_without_groups_ignores_an_address_and_offers_no_menu_item()
    {
        Nav.NavigateTo("/sales?group=region");
        var cut = Render<DataGrid<Sale>>(p => p
            .Add(x => x.Groupable, true)
            .Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<PropertyColumn<Sale, string>>(0);
                b.AddComponentParameter(1, "Property", (Expression<Func<Sale, string>>)(s => s.Region));
                b.CloseComponent();
            })
            .Add(x => x.ItemsProvider, r => ValueTask.FromResult(new DataResult<Sale>(Sales, Sales.Length))));

        Assert.Empty(cut.FindAll("tr.ib-grid-group"));
        Assert.DoesNotContain("Group by", cut.Markup);
    }

    [Fact]
    public void Show_only_writes_a_date_the_page_reads_back_in_any_culture()
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new("en-GB");
        try
        {
            RenderFragment columns = builder =>
            {
                builder.OpenComponent<PropertyColumn<Dated, DateOnly>>(0);
                builder.AddComponentParameter(1, "Property", (Expression<Func<Dated, DateOnly>>)(d => d.Day));
                builder.AddComponentParameter(2, "Filterable", true);
                builder.CloseComponent();
                builder.OpenComponent<PropertyColumn<Dated, int>>(3);
                builder.AddComponentParameter(4, "Property", (Expression<Func<Dated, int>>)(d => d.Id));
                builder.CloseComponent();
            };
            Dated[] days = [new(1, new(2026, 11, 2)), new(2, new(2026, 11, 2)), new(3, new(2026, 2, 11))];
            IRenderedComponent<DataGrid<Dated>> Show(string address)
            {
                Nav.NavigateTo(address);
                return Render<DataGrid<Dated>>(p => p.Add(x => x.Items, days).Add(x => x.Groupable, true).Add(x => x.GroupRowLimit, 1).Add(x => x.ChildContent, columns));
            }

            var cut = Show("/days?group=day&open=2026-11-02");
            var href = cut.Find(".ib-grid-group-more a").GetAttribute("href")!;
            var rows = Show(href).FindAll("tbody tr").Select(r => r.Children[^1].TextContent).ToArray();

            Assert.Equal(new[] { "1", "2" }, rows);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = culture;
        }
    }

    public sealed record Dated(int Id, DateOnly Day);

    [Fact]
    public void People_group_and_ungroup_and_open_them_all_by_links()
    {
        var cut = Grid("/sales?page=1", p => p.Add(x => x.PageSize, 2));
        Assert.Empty(cut.FindAll(".ib-grid-group-bar"));

        var byRegion = cut.FindAll("a.ib-menu-item, .ib-col-menu a").First(a => a.TextContent.Contains("Group by") && a.GetAttribute("href")!.Contains("group=region"));
        cut = Grid(byRegion.GetAttribute("href")!, p => p.Add(x => x.PageSize, 2));
        Assert.Equal(new[] { "g:East/Coast", "g:North" }, Rows(cut));
        Assert.DoesNotContain("page=", Nav.Uri);

        var all = cut.Find("thead .ib-grid-expand");
        Assert.Equal("Expand all groups", all.GetAttribute("aria-label"));
        cut = Grid(all.GetAttribute("href")!, p => p.Add(x => x.PageSize, 2));
        Assert.Equal(new[] { "g:East/Coast", "r:1", "r:2", "r:3", "g:North", "r:4" }, Rows(cut));

        var remove = cut.Find(".ib-grid-group-bar .ib-tag a, .ib-grid-group-bar a[aria-label]");
        cut = Grid(remove.GetAttribute("href")!, p => p.Add(x => x.PageSize, 2));
        Assert.Empty(cut.FindAll("tr.ib-grid-group"));
        Assert.Equal(new[] { "r:1", "r:2" }, Rows(cut));
        Assert.DoesNotContain("open", Nav.Uri);
    }

    [Fact]
    public void In_state_a_group_closes_and_collapse_all_stays_collapsed()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var cut = Grid("/sales", p => p
            .Add(x => x.Group, "region")
            .Add(x => x.GroupChanged, _ => { })
            .Add(x => x.ExpandAllGroups, true));
        Assert.Contains("r:1", Rows(cut));

        cut.Find("thead button.ib-grid-expand").Click();
        Assert.DoesNotContain("r:1", Rows(cut));

        cut.Find("tr.ib-grid-group button").Click();
        Assert.Contains("r:1", Rows(cut));

        cut.Find("tr.ib-grid-group button").Click();
        Assert.DoesNotContain("r:1", Rows(cut));
        Assert.DoesNotContain("r:4", Rows(cut));
    }

    [Fact]
    public void GroupPanel_waits_for_a_header_and_a_drop_on_a_bound_page_sets_the_grouping()
    {
        var cut = Grid("/sales", p => p.Add(x => x.GroupPanel, true));
        Assert.Contains("Drag a column header here", cut.Find(".ib-grid-group-bar").TextContent);
        Assert.NotEmpty(cut.FindAll("thead th[data-ib-group='region']"));

        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        string? group = null;
        cut = Grid("/sales", p => p.Add(x => x.Group, "region").Add(x => x.GroupChanged, v => group = v));
        // Grouped already, its header isn't offered again.
        Assert.Empty(cut.FindAll("thead th[data-ib-group='region']"));

        // What the script writes when a header or chip is dropped.
        cut.Find(".ib-grid-group-bar input.ib-grid-group-set").Change("rep,region");

        Assert.Equal("rep,region", group);
    }

    [Fact]
    public void Bound_on_an_interactive_page_it_opens_in_state()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        IReadOnlyList<string>? open = null;
        var cut = Grid("/sales", p => p
            .Add(x => x.Group, "region")
            .Add(x => x.GroupChanged, _ => { })
            .Add(x => x.ExpandedGroupsChanged, v => open = v));

        cut.Find("tr.ib-grid-group button").Click();

        Assert.Equal(["East%2FCoast"], open);
        Assert.Equal("/sales", new Uri(Nav.Uri).AbsolutePath);
        Assert.DoesNotContain("open", Nav.Uri);
    }
}
