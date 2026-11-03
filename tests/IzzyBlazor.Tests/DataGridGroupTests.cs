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
        cut.FindAll("tbody tr").Select(r => r.ClassList.Contains("ib-grid-group") ? "g:" + r.QuerySelector("th a,th button")!.TextContent.Trim().Split(':')[1].Trim() : "r:" + r.Children[0].TextContent).ToArray();

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
        Assert.Equal("3", group.QuerySelector(".ib-grid-group-count")!.TextContent);
        Assert.Equal("Sum 7", group.Children[^1].TextContent.Trim());
        Assert.Equal("Sum 21", cut.FindAll("tfoot td")[^1].TextContent.Trim());

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
        var cut = Grid("/sales?group=region", p => p.Add(x => x.GroupsOpen, true));
        Assert.Equal(new[] { "g:East/Coast", "r:1", "r:2", "r:3", "g:North", "r:4", "g:West", "r:5", "r:6" }, Rows(cut));

        var shut = cut.FindAll("tr.ib-grid-group")[1].QuerySelector("a")!.GetAttribute("href")!;
        Assert.Contains("shut=North", shut);

        cut = Grid(shut, p => p.Add(x => x.GroupsOpen, true));
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
    public void People_group_and_ungroup_and_open_them_all_by_links()
    {
        var cut = Grid("/sales?page=1", p => p.Add(x => x.PageSize, 2));
        Assert.Empty(cut.FindAll(".ib-grid-group-bar"));

        var byRegion = cut.FindAll("a.ib-menu-item, .ib-col-menu a").First(a => a.TextContent.Contains("Group by") && a.GetAttribute("href")!.Contains("group=region"));
        cut = Grid(byRegion.GetAttribute("href")!, p => p.Add(x => x.PageSize, 2));
        Assert.Equal(new[] { "g:East/Coast", "g:North" }, Rows(cut));
        Assert.DoesNotContain("page=", Nav.Uri);

        var all = cut.FindAll(".ib-grid-group-bar a").First(a => a.TextContent.Contains("Expand all"));
        cut = Grid(all.GetAttribute("href")!, p => p.Add(x => x.PageSize, 2));
        Assert.Equal(new[] { "g:East/Coast", "r:1", "r:2", "r:3", "g:North", "r:4" }, Rows(cut));

        var remove = cut.Find(".ib-grid-group-bar .ib-tag a, .ib-grid-group-bar a[aria-label]");
        cut = Grid(remove.GetAttribute("href")!, p => p.Add(x => x.PageSize, 2));
        Assert.Empty(cut.FindAll("tr.ib-grid-group"));
        Assert.Equal(new[] { "r:1", "r:2" }, Rows(cut));
        Assert.DoesNotContain("open", Nav.Uri);
    }

    [Fact]
    public void Bound_on_an_interactive_page_it_opens_in_state()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        IReadOnlyList<string>? open = null;
        var cut = Grid("/sales", p => p
            .Add(x => x.Group, "region")
            .Add(x => x.GroupChanged, _ => { })
            .Add(x => x.OpenGroupsChanged, v => open = v));

        cut.Find("tr.ib-grid-group button").Click();

        Assert.Equal(["East%2FCoast"], open);
        Assert.Equal("/sales", new Uri(Nav.Uri).AbsolutePath);
        Assert.DoesNotContain("open", Nav.Uri);
    }
}
