using System.Linq.Expressions;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

// Rows that form a tree: open by the address, sorted among siblings, filtered with their ancestors, paged by top rows.
public class TreeListTests : BunitContext
{
    public sealed record Person(int Id, int? ManagerId, string Name, Person[]? Reports = null, int Hours = 0);

    private static readonly Person[] People =
    [
        new(1, null, "Maya"),
        new(2, 1, "Sofia"),
        new(3, 2, "Mei", Hours: 30),
        new(4, 2, "James", Hours: 10),
        new(5, 1, "Daniel"),
        new(6, 5, "Lena", Hours: 5),
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

    private IRenderedComponent<TreeList<Person>> Tree(string address, Action<ComponentParameterCollectionBuilder<TreeList<Person>>>? more = null, RenderFragment? columns = null)
    {
        Nav.NavigateTo(address);
        return Render<TreeList<Person>>(p =>
        {
            p.Add(x => x.Items, People);
            p.Add(x => x.ItemKey, x => x.Id.ToString());
            p.Add(x => x.ItemParentKey, x => x.ManagerId?.ToString());
            p.Add(x => x.Columns, columns ?? Columns);
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
    public void A_checked_row_stands_for_its_branch_and_the_rows_above_a_match_have_no_box()
    {
        // Daniel's branch (Lena under him) and Mei on her own: Sofia isn't checked, so James isn't.
        var rows = Selection.Of("5", "3").ApplyTree(People.AsQueryable(), p => p.Id, p => p.ManagerId).Select(p => p.Name).Order();
        Assert.Equal(["Daniel", "Lena", "Mei"], rows);

        var cut = Tree("/people", p => p.Add(x => x.SelectionMode, SelectionMode.Multiple));
        Assert.Equal("up", cut.Find("ib-table").GetAttribute("data-ib-cascade"));

        cut = Tree("/people?f-name=lena", p => p.Add(x => x.SelectionMode, SelectionMode.Multiple));
        Assert.Equal("down", cut.Find("ib-table").GetAttribute("data-ib-cascade"));
        Assert.Equal([false, false, true], cut.FindAll("tbody > tr").Select(r => r.QuerySelector(".ib-table-select") is not null));
    }

    [Fact]
    public void A_row_rolls_up_the_rows_at_the_ends_of_its_branch()
    {
        Nav.NavigateTo("/people?open=*");
        var cut = Render<TreeList<Person>>(p =>
        {
            p.Add(x => x.Items, People);
            p.Add(x => x.ItemKey, x => x.Id.ToString());
            p.Add(x => x.ItemParentKey, x => x.ManagerId?.ToString());
            p.Add(x => x.Columns, builder =>
            {
                builder.AddContent(0, Columns);
                builder.OpenComponent<PropertyColumn<Person, int>>(1);
                builder.AddComponentParameter(2, "Property", (Expression<Func<Person, int>>)(x => x.Hours));
                builder.AddComponentParameter(3, "RollUp", (DataAggregate?)DataAggregate.Sum);
                builder.CloseComponent();
            });
        });

        // Maya: Mei, James and Lena; Sofia: Mei and James. A leaf shows its own.
        var hours = cut.FindAll("tbody > tr").Select(r => r.Children[1].TextContent.Trim()).ToArray();
        Assert.Equal(["45", "40", "30", "10", "5", "5", "0"], hours);
    }

    [Fact]
    public async Task An_export_keeps_the_trees_order_and_levels_and_the_rows_above_a_match()
    {
        var fields = DataFields.Of<Person>().Where(f => f.Key.Equals("name", StringComparison.OrdinalIgnoreCase)).ToList();
        var tree = DataExportTree<Person>.Of(p => p.Id, p => p.ManagerId);
        using var stream = new MemoryStream();
        await DataExport.WriteCsvAsync(stream, People.AsQueryable(), "sort=name&filter=" + Uri.EscapeDataString("name eq 'Lena'"), fields, tree: tree);
        var lines = System.Text.Encoding.UTF8.GetString(stream.ToArray()).TrimStart('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        // Quoted, so a reader keeps the spaces.
        Assert.Equal(["Name", "Maya", "\"  Daniel\"", "\"    Lena\""], lines);
    }

    [Fact]
    public void Adding_under_a_row_opens_it_and_puts_the_new_row_first_under_it()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Tree("/people", p => p
            .Add(x => x.EditMode, DataGridEditMode.Row)
            .Add(x => x.OnSave, (DataGridSave<Person> _) => { })
            .Add(x => x.NewChild, parent => new Person(0, parent.Id, "")), builder =>
            {
                builder.AddContent(0, Columns);
                builder.OpenComponent<ActionColumn<Person>>(1);
                builder.AddComponentParameter(2, nameof(ActionColumn<Person>.ChildContent), (RenderFragment<Person>)(_ => b =>
                {
                    b.OpenComponent<AddChildAction>(0);
                    b.CloseComponent();
                }));
                builder.CloseComponent();
            });

        Nav.NavigateTo(cut.Find("a[aria-label='Add under Maya']").GetAttribute("href")!);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("tr[data-ib-editing]")));
        var rows = cut.FindAll("tbody > tr");
        Assert.Equal("Maya", rows[0].QuerySelector(".ib-treelist-value")!.TextContent.Trim());
        Assert.Equal("2", rows[1].GetAttribute("aria-level"));
        Assert.NotNull(rows[1].GetAttribute("data-ib-editing"));
        Assert.Equal("Sofia", rows[2].QuerySelector(".ib-treelist-value")!.TextContent.Trim());
    }

    [Fact]
    public void The_move_dialog_offers_every_row_but_the_moving_one_and_its_branch()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Tree("/people?move=2", p => p.Add(x => x.OnReparent, (TreeListMove<Person> _) => { }));

        // Sofia (2) is moving: she, Mei and James aren't offered; the top level and the others are.
        var offered = cut.FindAll("dialog li[data-ib-key]").Select(e => e.GetAttribute("data-ib-key") ?? "?").ToArray();
        Assert.Equal(["", "1", "5", "6", "7"], offered);
        Assert.DoesNotContain("2", offered);
        Assert.DoesNotContain("3", offered);
        Assert.Contains("Move Sofia", cut.Find("dialog").TextContent);
    }

    [Fact]
    public async Task A_drop_moves_a_row_but_never_into_its_own_branch()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        JSInterop.Mode = JSRuntimeMode.Loose;
        var moves = new List<string>();
        var cut = Tree("/people?open=*", p => p
            .Add(x => x.MoveByDrag, true)
            .Add(x => x.OnReparent, (TreeListMove<Person> m) => moves.Add($"{m.Item.Name} > {m.Parent?.Name ?? "top"} @{m.Index}")));
        var sofia = cut.Find(".ib-treelist-grip[data-ib-key='2']");

        // Under Mei, her own report: refused. Before Daniel's Lena: Daniel's first.
        await cut.InvokeAsync(() => sofia.TriggerEventAsync("onibmove", new IzzyMoveEventArgs { Key = "2", ToZone = "3", ToIndex = -1 }));
        await cut.InvokeAsync(() => cut.Find(".ib-treelist-grip[data-ib-key='2']").TriggerEventAsync("onibmove", new IzzyMoveEventArgs { Key = "2", ToZone = "5", ToIndex = 0 }));
        Assert.Equal(["Sofia > Daniel @0"], moves);
    }

    [Fact]
    public void A_page_counts_top_rows_and_keeps_each_branch_whole()
    {
        var cut = Tree("/people?open=*", p => p.Add(x => x.PageSize, 1));
        Assert.Equal(["1:Maya", "2:Sofia", "3:Mei", "3:James", "2:Daniel", "3:Lena"], Rows(cut));
        Assert.Contains("1–1 of 2", cut.Markup);
    }
}
