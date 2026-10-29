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
    public void A_posting_list_puts_a_default_submit_button_ahead_of_its_move_buttons()
    {
        Static();
        var cut = RenderList();

        // The first submit button in the form is the one Enter presses.
        var first = cut.FindAll("button[type=submit]")[0];
        Assert.Contains("ib-listview-default-submit", first.ClassName);
        Assert.Null(first.GetAttribute("name"));
    }

    [Fact]
    public void Moves_toward_an_end_are_disabled_at_that_end()
    {
        Static();
        var cut = RenderList();

        Assert.True(MenuItem(cut, 0, "Move up").HasAttribute("disabled"));
        Assert.True(MenuItem(cut, 0, "Move to top").HasAttribute("disabled"));
        Assert.False(MenuItem(cut, 0, "Move down").HasAttribute("disabled"));
        Assert.True(MenuItem(cut, 2, "Move down").HasAttribute("disabled"));
        Assert.True(MenuItem(cut, 2, "Move to bottom").HasAttribute("disabled"));
        Assert.False(MenuItem(cut, 2, "Move up").HasAttribute("disabled"));
    }

    [Fact]
    public void A_static_list_without_a_Name_shows_no_handles_and_posts_nothing()
    {
        Static();
        var cut = RenderList(name: null);

        Assert.Equal(new[] { "Ada", "Brian", "Cleo" }, Shown(cut));
        Assert.Empty(cut.FindAll(".ib-sortable-handle"));
        Assert.Empty(cut.FindAll("input"));
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
    public void A_posted_order_without_a_move_focuses_nothing()
    {
        Static();
        var cut = RenderList(order: new ListOrder { Keys = ["b", "a", "c"] });

        Assert.Equal(new[] { "Brian", "Ada", "Cleo" }, Shown(cut));
        Assert.Empty(cut.FindAll("[autofocus]"));
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
    public void A_horizontal_list_shows_left_and_right_shortcuts()
    {
        Static();
        var cut = RenderList(more: p => p.Add(l => l.Orientation, Orientation.Horizontal));

        Assert.Contains("ib-sortable-horizontal", cut.Find("ib-sortable").ClassName);
        Assert.Contains("Alt+←", MenuItem(cut, 1, "Move up").TextContent);
        Assert.Contains("Alt+→", MenuItem(cut, 1, "Move down").TextContent);
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

    [Fact]
    public void A_drop_is_counted_only_once_an_awaited_OnMove_is_done_and_moves_meanwhile_are_ignored()
    {
        // The script settles on the count: a render while OnMove saves must not carry it.
        Live();
        var save = new TaskCompletionSource();
        var calls = 0;
        var cut = RenderList(name: null, more: p => p.Add(l => l.OnMove, async m => { calls++; await save.Task; }));

        cut.Find("ib-sortable").TriggerEvent("onibmove", new IzzyMoveEventArgs { Key = "a", ToIndex = 2, Seq = 1 });
        Assert.Equal("0", cut.Find("ib-sortable").GetAttribute("data-ib-seq"));

        // A second drag while the first saves: ignored, but counted so the script puts it back.
        cut.Find("ib-sortable").TriggerEvent("onibmove", new IzzyMoveEventArgs { Key = "c", ToIndex = 0, Seq = 2 });
        Assert.Equal(1, calls);
        Assert.Equal("2", cut.Find("ib-sortable").GetAttribute("data-ib-seq"));

        save.SetResult();
        cut.WaitForAssertion(() => Assert.Equal(new[] { "Brian", "Cleo", "Ada" }, Shown(cut)));
        Assert.Equal("2", cut.Find("ib-sortable").GetAttribute("data-ib-seq"));
    }

    [Fact]
    public void A_list_prerendered_for_an_interactive_page_renders_no_count_so_nothing_drags_yet()
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var cut = RenderList(name: null, more: p => p.AddCascadingValue(new Microsoft.AspNetCore.Components.Web.InteractiveServerRenderMode(prerender: true)));

        Assert.False(cut.Find("ib-sortable").HasAttribute("data-ib-seq"));
    }

    [Fact]
    public void A_static_list_renders_no_drop_count()
    {
        Static();
        var cut = RenderList();

        Assert.False(cut.Find("ib-sortable").HasAttribute("data-ib-seq"));
    }

    [Fact]
    public void The_script_finds_each_move_label_and_mode_in_the_markup()
    {
        Static();
        var cut = RenderList(order: new ListOrder { Move = "down:a" }, more: p => p.Add(l => l.SubmitOnMove, true));

        var root = cut.Find("ib-sortable");
        Assert.False(root.HasAttribute("data-ib-interactive"));
        Assert.True(root.HasAttribute("data-ib-submit"));
        Assert.Equal("Moved {0} to position {1} of {2}", root.GetAttribute("data-ib-moved"));
        Assert.Equal(new[] { "Brian", "Ada", "Cleo" }, cut.FindAll("li").Select(l => l.GetAttribute("data-ib-label")));
        Assert.Equal(new[] { "up", "down", "top", "bottom" },
            cut.FindAll("li")[0].QuerySelectorAll("button[data-ib-move]").Select(b => b.GetAttribute("data-ib-move")));
        // Native autofocus for full page loads; the marker for enhanced posts (focus.ts).
        Assert.Equal("Move Ada", cut.Find("[data-ib-autofocus]").GetAttribute("aria-label"));
    }

    [Fact]
    public void A_live_list_is_marked_interactive_and_never_submits()
    {
        Live();
        var cut = RenderList(name: null, more: p => p.Add(l => l.SubmitOnMove, true));

        var root = cut.Find("ib-sortable");
        Assert.True(root.HasAttribute("data-ib-interactive"));
        Assert.False(root.HasAttribute("data-ib-submit"));
        Assert.Equal(4, cut.FindAll("li")[0].QuerySelectorAll("button[data-ib-move]").Length);
    }

    [Fact]
    public void A_reorderable_list_refuses_paging_and_a_provider()
    {
        Static();
        var paged = Assert.Throws<InvalidOperationException>(() => RenderList(more: p => p.Add(l => l.PageSize, 2)));
        Assert.Contains("shows every item", paged.Message);
        Assert.Throws<InvalidOperationException>(() => RenderList(more: p => p
            .Add(l => l.ItemsProvider, _ => ValueTask.FromResult(new DataResult<Step>([], 0)))));
    }

    [Fact]
    public void Reordering_works_with_selection_and_links()
    {
        Static();
        var cut = RenderList(more: p => p.Add(l => l.Selectable, true).Add(l => l.ItemHref, s => $"/steps/{s.Id}"));

        var first = cut.FindAll("li")[0];
        // Handle, then the checkbox, then the content; the link is stretched over the item.
        Assert.NotNull(first.QuerySelector(".ib-sortable-handle"));
        Assert.Equal("a", first.QuerySelector("input.ib-listview-select")!.GetAttribute("value"));
        Assert.Equal("/steps/a", first.QuerySelector("a.ib-listview-action")!.GetAttribute("href"));
        Assert.Equal("a", first.GetAttribute("data-ib-key"));
        // What the script finds the items and the list by.
        Assert.Contains("ib-sortable-item", first.ClassName);
        Assert.Contains("ib-sortable-list", cut.Find("ib-sortable > ul").ClassName);
    }

    [Fact]
    public void The_handle_and_menu_text_come_from_IzzyText()
    {
        Services.AddSingleton(new IzzyText { MoveItem = "{0} verschieben", MoveUp = "Nach oben" });
        Static();
        var cut = RenderList();

        Assert.Equal("Ada verschieben", cut.Find(".ib-sortable-handle").GetAttribute("aria-label"));
        Assert.NotNull(MenuItem(cut, 1, "Nach oben"));
    }

    [Fact]
    public void DragWholeItem_marks_the_list_for_the_script_and_keeps_the_handles()
    {
        Static();
        var cut = RenderList(more: p => p.Add(l => l.DragWholeItem, true));

        Assert.True(cut.Find("ib-sortable").HasAttribute("data-ib-whole"));
        Assert.Equal(3, cut.FindAll(".ib-sortable-handle").Count);

        var plain = RenderList();
        Assert.False(plain.Find("ib-sortable").HasAttribute("data-ib-whole"));
    }
}
