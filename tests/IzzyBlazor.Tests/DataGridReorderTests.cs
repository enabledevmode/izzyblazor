using System.Linq.Expressions;
using AngleSharp.Dom;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

// Rows moved by a handle at their start (ReorderableRows).
public class DataGridReorderTests : BunitContext
{
    public sealed record Task(string Id, string Title);

    private static readonly Task[] Tasks = [new("a", "Ada"), new("b", "Brian"), new("c", "Cleo")];

    private BunitNavigationManager Nav => Services.GetRequiredService<BunitNavigationManager>();

    private void Static() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    private void Live() => SetRendererInfo(new RendererInfo("Server", isInteractive: true));

    private static RenderFragment Columns => builder =>
    {
        builder.OpenComponent<PropertyColumn<Task, string>>(0);
        builder.AddComponentParameter(1, "Property", (Expression<Func<Task, string>>)(t => t.Title));
        builder.AddComponentParameter(2, "Sortable", true);
        builder.CloseComponent();
    };

    private IRenderedComponent<DataGrid<Task>> Grid(string address = "/tasks", Action<ComponentParameterCollectionBuilder<DataGrid<Task>>>? more = null)
    {
        Nav.NavigateTo(address);
        return Render<DataGrid<Task>>(p =>
        {
            p.Add(x => x.Items, Tasks)
                .Add(x => x.ItemKey, t => t.Id)
                .Add(x => x.RowLabel, t => t.Title)
                .Add(x => x.ReorderableRows, true)
                .Add(x => x.ChildContent, Columns);
            more?.Invoke(p);
        });
    }

    private static string[] Shown(IRenderedComponent<DataGrid<Task>> cut) =>
        cut.FindAll("tbody > tr").Select(r => r.QuerySelectorAll("td").Last().TextContent.Trim()).ToArray();

    private static IElement MenuItem(IRenderedComponent<DataGrid<Task>> cut, int row, string text) =>
        cut.FindAll("tbody > tr")[row].QuerySelectorAll("button.ib-menu-item").Single(b => b.TextContent.Contains(text));

    [Fact]
    public void A_static_grid_posts_its_keys_and_moves_and_shows_a_posted_move_with_the_handle_focused()
    {
        Static();
        var cut = Grid(more: p => p.Add(x => x.OrderName, "order"));

        Assert.Equal(new[] { "a", "b", "c" }, cut.FindAll("input[name='order.Keys']").Select(i => i.GetAttribute("value")));
        Assert.Equal(new[] { "Move Ada", "Move Brian", "Move Cleo" }, cut.FindAll(".ib-sortable-handle").Select(h => h.GetAttribute("aria-label")));
        var up = MenuItem(cut, 1, "Move up");
        Assert.Equal("order.Move", up.GetAttribute("name"));
        Assert.Equal("up:b", up.GetAttribute("value"));
        Assert.True(MenuItem(cut, 0, "Move up").HasAttribute("disabled"));

        // Every row spans as many columns as the header, handle column included.
        var header = cut.Find("thead > tr").QuerySelectorAll(":scope > th").Length;
        Assert.All(cut.FindAll("tbody > tr"), r => Assert.Equal(header, r.QuerySelectorAll(":scope > td").Length));

        cut = Grid(more: p => p.Add(x => x.OrderName, "order").Add(x => x.Order, new ListOrder { Keys = ["c", "a", "b"], Move = "up:b" }));
        Assert.Equal(new[] { "Cleo", "Brian", "Ada" }, Shown(cut));
        Assert.Equal("Move Brian", Assert.Single(cut.FindAll(".ib-sortable-handle[autofocus]")).GetAttribute("aria-label"));
    }

    [Fact]
    public void A_sorted_grid_disables_its_handles_and_posts_no_order()
    {
        Static();
        var cut = Grid("/tasks?sort=-title", p => p.Add(x => x.OrderName, "order"));

        Assert.Equal(new[] { "Cleo", "Brian", "Ada" }, Shown(cut));
        Assert.All(cut.FindAll(".ib-sortable-handle"), h => Assert.True(h.HasAttribute("disabled")));
        Assert.Empty(cut.FindAll("input[name='order.Keys']"));
        Assert.Equal("Clear the sort, filters and grouping to move rows", cut.Find(".ib-grid-grip-cell span[title]").GetAttribute("title"));
    }

    [Fact]
    public void A_live_menu_move_raises_OnMove_then_ItemsChanged_and_announces_it()
    {
        Live();
        var calls = new List<string>();
        IReadOnlyList<Task>? changed = null;
        ListMoveEventArgs<Task>? move = null;
        var cut = Grid(more: p => p
            .Add(x => x.OnMove, m => { move = m; calls.Add("move"); })
            .Add(x => x.ItemsChanged, items => { changed = items; calls.Add("changed"); }));

        MenuItem(cut, 0, "Move down").Click();

        Assert.Equal(new[] { "move", "changed" }, calls);
        Assert.Equal(("a", 0, 1), (move!.Key, move.FromIndex, move.ToIndex));
        Assert.Equal(new[] { "b", "a", "c" }, changed!.Select(t => t.Id));
        Assert.Equal(new[] { "Brian", "Ada", "Cleo" }, Shown(cut));
        Assert.Equal("Moved Ada to position 2 of 3", cut.Find("ib-sortable > [role=status]").TextContent);
    }

    [Fact]
    public void Rows_that_aren_t_all_shown_throw()
    {
        Live();
        var ex = Assert.Throws<InvalidOperationException>(() => Grid(more: p => p.Add(x => x.PageSize, 2)));
        Assert.Contains("shows every row", ex.Message);
    }
}
