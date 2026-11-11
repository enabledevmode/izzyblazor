using System.Linq.Expressions;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

// Rows that form a tree: open by the address, sorted among siblings, filtered with their ancestors, paged by top rows.
public class TreeListTests : BunitContext
{
    public sealed record Person(int Id, int? ManagerId, string Name, Person[]? Reports = null);

    private static readonly Person[] People =
    [
        new(1, null, "Maya"),
        new(2, 1, "Sofia"),
        new(3, 2, "Mei"),
        new(4, 2, "James"),
        new(5, 1, "Daniel"),
        new(6, 5, "Lena"),
        new(7, null, "Ravi"),
    ];

    public TreeListTests() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    private BunitNavigationManager Nav => Services.GetRequiredService<BunitNavigationManager>();

    private static RenderFragment Columns => builder =>
    {
        builder.OpenComponent<PropertyColumn<Person, string>>(0);
        builder.AddComponentParameter(1, "Property", (Expression<Func<Person, string>>)(p => p.Name));
        builder.AddComponentParameter(2, "Sortable", true);
        builder.AddComponentParameter(3, "Filterable", true);
        builder.CloseComponent();
    };

    private IRenderedComponent<TreeList<Person>> Tree(string address, Action<ComponentParameterCollectionBuilder<TreeList<Person>>>? more = null)
    {
        Nav.NavigateTo(address);
        return Render<TreeList<Person>>(p =>
        {
            p.Add(x => x.Items, People);
            p.Add(x => x.ItemKey, x => x.Id.ToString());
            p.Add(x => x.ItemParentKey, x => x.ManagerId?.ToString());
            p.Add(x => x.Columns, Columns);
            more?.Invoke(p);
        });
    }

    private static string[] Rows(IRenderedComponent<TreeList<Person>> cut) =>
        cut.FindAll("tbody > tr:not(.ib-treelist-more)").Select(r => $"{r.GetAttribute("aria-level")}:{r.QuerySelector(".ib-treelist-value")!.TextContent.Trim()}").ToArray();

    [Fact]
    public void A_link_in_the_address_opens_a_row_under_its_parent()
    {
        var cut = Tree("/people");
        Assert.Equal(["1:Maya", "1:Ravi"], Rows(cut));
        var toggle = cut.Find("tbody > tr .ib-treelist-toggle");
        Assert.Equal("Expand Maya", toggle.GetAttribute("aria-label"));
        Assert.Equal("false", cut.Find("tbody > tr").GetAttribute("aria-expanded"));

        cut = Tree(toggle.GetAttribute("href")!);
        Assert.Equal(["1:Maya", "2:Sofia", "2:Daniel", "1:Ravi"], Rows(cut));
        var daniel = cut.FindAll("tbody > tr")[2];
        Assert.Equal("2", daniel.GetAttribute("aria-posinset"));
        Assert.Equal("2", daniel.GetAttribute("aria-setsize"));
        Assert.Null(cut.FindAll("tbody > tr")[3].GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Sorting_orders_each_rows_children_among_themselves()
    {
        var cut = Tree("/people?sort=name&open=*");
        Assert.Equal(["1:Maya", "2:Daniel", "3:Lena", "2:Sofia", "3:James", "3:Mei", "1:Ravi"], Rows(cut));
    }

    [Fact]
    public void Nested_rows_open_and_sort_the_same_way()
    {
        Nav.NavigateTo("/people?sort=name&open=1");
        var cut = Render<TreeList<Person>>(p =>
        {
            p.Add(x => x.Items, [new(7, null, "Ravi"), new(1, null, "Maya", [new(2, 1, "Sofia"), new(5, 1, "Daniel")])]);
            p.Add(x => x.ItemKey, x => x.Id.ToString());
            p.Add(x => x.ItemChildren, x => x.Reports);
            p.Add(x => x.Columns, Columns);
        });

        Assert.Equal(["1:Maya", "2:Daniel", "2:Sofia", "1:Ravi"], Rows(cut));
    }

    [Fact]
    public void Filtering_keeps_the_rows_above_a_match_open_and_muted()
    {
        var cut = Tree("/people?f-name=lena");
        Assert.Equal(["1:Maya", "2:Daniel", "3:Lena"], Rows(cut));
        var rows = cut.FindAll("tbody > tr");
        Assert.Contains("ib-treelist-context", rows[0].ClassList);
        Assert.DoesNotContain("ib-treelist-context", rows[2].ClassList);

        cut = Tree("/people?f-name=daniel", p => p.Add(x => x.FilterBranch, true));
        Assert.Equal(["1:Maya", "2:Daniel", "3:Lena"], Rows(cut));
    }

    [Fact]
    public void A_provider_loads_each_open_rows_children_and_keeps_the_rows_above_a_match()
    {
        var parents = new List<string?>();
        IRenderedComponent<TreeList<Person>> Load(string address)
        {
            Nav.NavigateTo(address);
            return Render<TreeList<Person>>(p =>
            {
                p.Add(x => x.ItemsProvider, request =>
                {
                    parents.Add(request.Parent);
                    return ValueTask.FromResult(request.ToTreeResult(People.AsQueryable(), x => x.Id, x => x.ManagerId));
                });
                p.Add(x => x.ItemKey, x => x.Id.ToString());
                p.Add(x => x.ItemHasChildren, x => People.Any(c => c.ManagerId == x.Id));
                p.Add(x => x.Columns, Columns);
            });
        }

        var cut = Load("/people?open=1&sort=name");
        Assert.Equal(["1:Maya", "2:Daniel", "2:Sofia", "1:Ravi"], Rows(cut));
        Assert.Equal([null, "1"], parents);
        Assert.Null(cut.FindAll("tbody > tr")[3].GetAttribute("aria-expanded"));

        cut = Load("/people?f-name=lena");
        Assert.Equal(["1:Maya", "2:Daniel", "3:Lena"], Rows(cut));
        Assert.Contains("ib-treelist-context", cut.FindAll("tbody > tr")[1].ClassList);
    }

    [Fact]
    public void A_row_with_more_children_than_the_limit_shows_more_by_a_link()
    {
        var cut = Tree("/people?open=1", p => p.Add(x => x.ChildRowLimit, 1));
        Assert.Equal(["1:Maya", "2:Sofia", "1:Ravi"], Rows(cut));
        var more = cut.Find(".ib-treelist-more a");
        Assert.Contains("Showing 1 of 2", cut.Find(".ib-treelist-more").TextContent);

        cut = Tree(more.GetAttribute("href")!, p => p.Add(x => x.ChildRowLimit, 1));
        Assert.Equal(["1:Maya", "2:Sofia", "2:Daniel", "1:Ravi"], Rows(cut));
    }

    [Fact]
    public void A_page_counts_top_rows_and_keeps_each_branch_whole()
    {
        var cut = Tree("/people?open=*", p => p.Add(x => x.PageSize, 1));
        Assert.Equal(["1:Maya", "2:Sofia", "3:Mei", "3:James", "2:Daniel", "3:Lena"], Rows(cut));
        Assert.Contains("1–1 of 2", cut.Markup);
    }
}
