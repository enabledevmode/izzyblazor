using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

public class ListViewReorderTests : BunitContext
{
    public sealed record Step(string Id, string Title, bool Locked = false);

    private static readonly Step[] Steps = [new("a", "Ada"), new("b", "Brian"), new("c", "Cleo")];

    private void Static() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    private void Live() => SetRendererInfo(new RendererInfo("Server", isInteractive: true));

    private IRenderedComponent<ListView<Step>> RenderList(
        IReadOnlyList<Step>? items = null, string? name = "order", ListOrder? order = null,
        Action<ComponentParameterCollectionBuilder<ListView<Step>>>? more = null) =>
        Render<ListView<Step>>(p =>
        {
            p.Add(l => l.Items, items ?? Steps)
                .Add(l => l.Reorderable, true)
                .Add(l => l.ItemKey, s => s.Id)
                .Add(l => l.ItemLabel, s => s.Title)
                .Add(l => l.ItemDisabled, s => s.Locked)
                .Add(l => l.Name, name)
                .Add(l => l.Order, order)
                .Add(l => l.ChildContent, s => b => b.AddContent(0, s.Title));
            more?.Invoke(p);
        });

    private static string[] Shown(IRenderedComponent<ListView<Step>> cut) =>
        cut.FindAll(".ib-listview-content").Select(c => c.TextContent).ToArray();

    private static IElement MenuItem(IRenderedComponent<ListView<Step>> cut, int item, string text) =>
        cut.FindAll("li")[item].QuerySelectorAll("button.ib-menu-item").Single(b => b.TextContent.Contains(text));

    [Fact]
    public void A_static_list_posts_its_keys_in_order_and_its_menu_posts_moves()
    {
        Static();
        var cut = RenderList();

        Assert.Equal("list", cut.Find("ul.ib-listview-list").GetAttribute("role"));
        Assert.Equal(new[] { "a", "b", "c" }, cut.FindAll("input[type=hidden]").Select(i => i.GetAttribute("value")));
        Assert.All(cut.FindAll("input[type=hidden]"), i => Assert.Equal("order.Keys", i.GetAttribute("name")));

        var handles = cut.FindAll(".ib-sortable-handle");
        Assert.Equal(new[] { "Move Ada", "Move Brian", "Move Cleo" }, handles.Select(h => h.GetAttribute("aria-label")));

        var up = MenuItem(cut, 1, "Move up");
        Assert.Equal("submit", up.GetAttribute("type"));
        Assert.Equal("order.Move", up.GetAttribute("name"));
        Assert.Equal("up:b", up.GetAttribute("value"));
        Assert.Equal("bottom:b", MenuItem(cut, 1, "Move to bottom").GetAttribute("value"));
    }

    [Fact]
    public void A_posted_order_and_move_show_and_focus_the_moved_handle()
    {
        Static();
        var cut = RenderList(order: new ListOrder { Keys = ["c", "a", "b"], Move = "up:b" });

        Assert.Equal(new[] { "Cleo", "Brian", "Ada" }, Shown(cut));
        Assert.Equal(new[] { "c", "b", "a" }, cut.FindAll("input[type=hidden]").Select(i => i.GetAttribute("value")));
        var focused = cut.FindAll(".ib-sortable-handle[autofocus]");
        Assert.Equal("Move Brian", Assert.Single(focused).GetAttribute("aria-label"));
    }

    [Fact]
    public void A_locked_item_has_no_handle_and_a_posted_move_of_it_is_ignored()
    {
        Static();
        Step[] items = [new("a", "Ada", Locked: true), new("b", "Brian"), new("c", "Cleo")];
        var cut = RenderList(items, order: new ListOrder { Move = "bottom:a" });

        Assert.Equal(new[] { "Ada", "Brian", "Cleo" }, Shown(cut));
        var first = cut.FindAll("li")[0];
        Assert.Contains("ib-listview-disabled", first.ClassName);
        Assert.Null(first.QuerySelector(".ib-sortable-handle"));
        Assert.NotNull(first.QuerySelector(".ib-sortable-spacer"));
        Assert.Equal(2, cut.FindAll(".ib-sortable-handle").Count);
        Assert.Equal(3, cut.FindAll("input[type=hidden]").Count);

        // The others move past it.
        Assert.Equal("up:b", MenuItem(cut, 1, "Move up").GetAttribute("value"));
        Assert.False(MenuItem(cut, 1, "Move up").HasAttribute("disabled"));
    }

    [Fact]
    public void A_live_menu_move_raises_OnMove_then_ItemsChanged_and_announces_it()
    {
        Live();
        var calls = new List<string>();
        IReadOnlyList<Step>? changed = null;
        ListMoveEventArgs<Step>? move = null;
        var cut = RenderList(name: null, more: p => p
            .Add(l => l.OnMove, m => { move = m; calls.Add("move"); })
            .Add(l => l.ItemsChanged, items => { changed = items; calls.Add("changed"); }));

        Assert.Empty(cut.FindAll("input"));
        var down = MenuItem(cut, 0, "Move down");
        Assert.Equal("button", down.GetAttribute("type"));
        down.Click();

        Assert.Equal(new[] { "move", "changed" }, calls);
        Assert.Equal("a", move!.Key);
        Assert.Equal(0, move.FromIndex);
        Assert.Equal(1, move.ToIndex);
        Assert.Null(move.ToZone);
        Assert.Equal(new[] { "b", "a", "c" }, changed!.Select(s => s.Id));
        Assert.Equal(new[] { "Brian", "Ada", "Cleo" }, Shown(cut));
        Assert.Equal("Moved Ada to position 2 of 3", cut.Find("ib-sortable > [role=status]").TextContent);
    }

    [Fact]
    public void A_cancelled_live_move_keeps_the_order()
    {
        Live();
        var changed = false;
        var cut = RenderList(name: null, more: p => p
            .Add(l => l.OnMove, m => m.Cancel = true)
            .Add(l => l.ItemsChanged, _ => changed = true));

        MenuItem(cut, 2, "Move to top").Click();

        Assert.False(changed);
        Assert.Equal(new[] { "Ada", "Brian", "Cleo" }, Shown(cut));
        Assert.Equal("Cleo wasn't moved", cut.Find("ib-sortable > [role=status]").TextContent);
    }

    [Fact]
    public void A_live_drop_moves_the_item_through_OnMove_and_renders_its_count()
    {
        Live();
        ListMoveEventArgs<Step>? move = null;
        IReadOnlyList<Step>? changed = null;
        var cut = RenderList(name: null, more: p => p
            .Add(l => l.OnMove, m => move = m)
            .Add(l => l.ItemsChanged, items => changed = items));

        Assert.Equal("0", cut.Find("ib-sortable").GetAttribute("data-ib-seq"));
        cut.Find("ib-sortable").TriggerEvent("onibmove", new IzzyMoveEventArgs { Key = "a", ToIndex = 2, Seq = 1 });

        Assert.Equal(0, move!.FromIndex);
        Assert.Equal(2, move.ToIndex);
        Assert.Equal(new[] { "b", "c", "a" }, changed!.Select(s => s.Id));
        Assert.Equal(new[] { "Brian", "Cleo", "Ada" }, Shown(cut));
        Assert.Equal("1", cut.Find("ib-sortable").GetAttribute("data-ib-seq"));
        Assert.Equal("Moved Ada to position 3 of 3", cut.Find("ib-sortable > [role=status]").TextContent);
    }

    [Fact]
    public void A_cancelled_or_locked_drop_keeps_the_order_and_still_renders_its_count()
    {
        // The script holds the dragged item until the render with its count: it must come either way.
        Live();
        var cut = RenderList(items: [new("a", "Ada"), new("b", "Brian", Locked: true), new("c", "Cleo")], name: null,
            more: p => p.Add(l => l.OnMove, m => m.Cancel = m.Key == "c"));

        cut.Find("ib-sortable").TriggerEvent("onibmove", new IzzyMoveEventArgs { Key = "c", ToIndex = 0, Seq = 1 });
        Assert.Equal(new[] { "Ada", "Brian", "Cleo" }, Shown(cut));
        Assert.Equal("1", cut.Find("ib-sortable").GetAttribute("data-ib-seq"));

        cut.Find("ib-sortable").TriggerEvent("onibmove", new IzzyMoveEventArgs { Key = "b", ToIndex = 2, Seq = 2 });
        Assert.Equal(new[] { "Ada", "Brian", "Cleo" }, Shown(cut));
        Assert.Equal("2", cut.Find("ib-sortable").GetAttribute("data-ib-seq"));
    }

    // An Open item for every step, or only for those `when` allows.
    private static RenderFragment<Step> OpenMenu(Func<Step, bool>? when = null) => step => b =>
    {
        if (when?.Invoke(step) == false)
        {
            return;
        }

        b.OpenComponent<IzzyBlazor.MenuItem>(0);
        b.AddAttribute(1, nameof(IzzyBlazor.MenuItem.Href), $"/steps/{step.Id}");
        b.AddAttribute(2, nameof(IzzyBlazor.MenuItem.ChildContent), (RenderFragment)(c => c.AddContent(0, "Open")));
        b.CloseComponent();
    };

    private static string[] MenuTexts(IElement item) =>
        item.QuerySelectorAll(".ib-menu-item .ib-menu-label").Select(l => l.TextContent.Trim()).ToArray();
}
