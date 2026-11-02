using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;
using Task = IzzyBlazor.Tests.DropZoneProbe.BoardTask;

namespace IzzyBlazor.Tests;

public class DropZoneGroupTests : BunitContext
{
    // Doing is full (two at most); Done takes only reviewed tasks.
    private static List<Task> Board() =>
    [
        new("a", "Ada", "todo", reviewed: true),
        new("b", "Brian", "todo"),
        new("c", "Cleo", "doing"),
        new("d", "Dan", "doing"),
        new("e", "Eve", "done", reviewed: true),
    ];

    private void Static() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    private void Live() => SetRendererInfo(new RendererInfo("Server", isInteractive: true));

    private IRenderedComponent<DropZoneProbe> RenderBoard(
        List<Task>? tasks = null, string? name = "board", ListOrder? order = null, bool saveZone = true, bool cancel = false) =>
        Render<DropZoneProbe>(p => p
            .Add(b => b.Tasks, tasks ?? Board())
            .Add(b => b.Name, name)
            .Add(b => b.Order, order)
            .Add(b => b.SaveZone, saveZone)
            .Add(b => b.Cancel, cancel));

    private static IElement Zone(IRenderedComponent<DropZoneProbe> cut, string zone) =>
        cut.Find($"ib-sortable[data-ib-zone={zone}]");

    private static string[] Shown(IRenderedComponent<DropZoneProbe> cut, string zone) =>
        Zone(cut, zone).QuerySelectorAll(".ib-listview-content").Select(c => c.TextContent).ToArray();

    private static IElement[] MoveTo(IElement zone, int item) =>
        zone.QuerySelectorAll("li")[item].QuerySelectorAll("button[data-ib-move=to]").ToArray();

    [Fact]
    public void Each_zone_shows_the_group_items_in_it()
    {
        Static();
        var cut = RenderBoard();

        Assert.Equal(new[] { "Ada", "Brian" }, Shown(cut, "todo"));
        Assert.Equal(new[] { "Cleo", "Dan" }, Shown(cut, "doing"));
        Assert.Equal(new[] { "Eve" }, Shown(cut, "done"));
        Assert.Equal("To do", Zone(cut, "todo").QuerySelector("ul")!.GetAttribute("aria-label"));
        // Every zone of a group shares its id, for the script's drag between them.
        Assert.Single(cut.FindAll("ib-sortable").Select(z => z.GetAttribute("data-ib-group")).Distinct());
    }

    [Fact]
    public void Items_post_their_keys_and_zones_in_page_order()
    {
        Static();
        var cut = RenderBoard();

        var keys = cut.FindAll("input[name='board.Keys']").Select(i => i.GetAttribute("value"));
        var zones = cut.FindAll("input[name='board.Zones']").Select(i => i.GetAttribute("value"));
        Assert.Equal(new[] { "a", "b", "c", "d", "e" }, keys);
        Assert.Equal(new[] { "todo", "todo", "doing", "doing", "done" }, zones);
    }

    [Fact]
    public void The_menu_moves_to_each_zone_that_takes_the_item()
    {
        Static();
        var cut = RenderBoard();
        var todo = Zone(cut, "todo");

        // Ada (reviewed): every zone; her own hidden, Doing disabled while it's full.
        var ada = MoveTo(todo, 0);
        Assert.Equal(new[] { "todo", "doing", "done" }, ada.Select(b => b.GetAttribute("data-ib-zone")));
        Assert.True(ada[0].HasAttribute("hidden"));
        Assert.True(ada[1].HasAttribute("disabled"));
        Assert.False(ada[2].HasAttribute("disabled"));
        Assert.Equal("to:done:a", ada[2].GetAttribute("value"));
        Assert.Equal("board.Move", ada[2].GetAttribute("name"));
        Assert.Equal("Done", ada[2].TextContent.Trim());
        Assert.Equal("Move to", todo.QuerySelectorAll("li")[0].QuerySelector(".ib-menu-group .ib-menu-heading")!.TextContent);

        // Brian (not reviewed): Done doesn't take him.
        Assert.Equal(new[] { "todo", "doing" }, MoveTo(todo, 1).Select(b => b.GetAttribute("data-ib-zone")));
        Assert.Equal("todo doing", todo.QuerySelectorAll("li")[1].GetAttribute("data-ib-to"));
        Assert.Equal("2", Zone(cut, "doing").GetAttribute("data-ib-max"));
    }

    [Fact]
    public void A_post_shows_the_posted_zones_and_applies_a_move_to_a_zone()
    {
        Static();
        var order = new ListOrder
        {
            // The script moved Brian to the top of Doing... which was full, and Ada to Done.
            Keys = ["b", "c", "d", "e", "a"],
            Zones = ["todo", "doing", "doing", "done", "done"],
            Move = "to:doing:a",
        };
        var cut = RenderBoard(order: order);

        // The move into a full zone is ignored; Ada stays in Done, where she was posted.
        Assert.Equal(new[] { "Brian" }, Shown(cut, "todo"));
        Assert.Equal(new[] { "Cleo", "Dan" }, Shown(cut, "doing"));
        Assert.Equal(new[] { "Eve", "Ada" }, Shown(cut, "done"));
    }

    [Fact]
    public void A_posted_zone_that_refuses_the_item_keeps_its_own()
    {
        Static();
        var order = new ListOrder { Keys = ["a", "b", "c", "d", "e"], Zones = ["todo", "done", "doing", "doing", "nowhere"] };
        var cut = RenderBoard(order: order);

        // Done takes only reviewed tasks; "nowhere" is no zone.
        Assert.Equal(new[] { "Ada", "Brian" }, Shown(cut, "todo"));
        Assert.Equal(new[] { "Eve" }, Shown(cut, "done"));
    }

    [Fact]
    public void A_posted_move_to_a_zone_moves_the_item_and_focuses_its_handle()
    {
        Static();
        var cut = RenderBoard(order: new ListOrder { Keys = ["a", "b", "c", "d", "e"], Move = "to:done:a" });

        Assert.Equal(new[] { "Brian" }, Shown(cut, "todo"));
        Assert.Equal(new[] { "Eve", "Ada" }, Shown(cut, "done"));
        var handle = Zone(cut, "done").QuerySelectorAll("li")[1].QuerySelector(".ib-sortable-handle")!;
        Assert.True(handle.HasAttribute("autofocus"));
    }

    [Fact]
    public void A_live_drop_in_another_zone_runs_OnMove_and_moves_it()
    {
        Live();
        var cut = RenderBoard(name: null);

        Zone(cut, "todo").TriggerEvent("onibmove", new IzzyMoveEventArgs { Key = "a", ToZone = "done", ToIndex = 0, Seq = 1 });

        var move = Assert.Single(cut.Instance.Moves);
        Assert.Equal(("todo", 0, "done", 0), (move.FromZone, move.FromIndex, move.ToZone, move.ToIndex));
        Assert.Equal(new[] { "Brian" }, Shown(cut, "todo"));
        Assert.Equal(new[] { "Ada", "Eve" }, Shown(cut, "done"));
        Assert.Equal(new[] { "b", "c", "d", "a", "e" }, cut.Instance.Changes!.Select(t => t.Id));
        Assert.Equal("Moved Ada to Done, position 1 of 2", Zone(cut, "done").QuerySelector("[role=status]")!.TextContent);
        // Every zone renders the count: the drop's list waits for it.
        Assert.All(cut.FindAll("ib-sortable"), z => Assert.Equal("1", z.GetAttribute("data-ib-seq")));
    }

    [Fact]
    public void A_cancelled_live_move_stays_and_says_so()
    {
        Live();
        var cut = RenderBoard(name: null, cancel: true);

        Zone(cut, "todo").TriggerEvent("onibmove", new IzzyMoveEventArgs { Key = "a", ToZone = "done", ToIndex = 0, Seq = 1 });

        Assert.Equal(new[] { "Ada", "Brian" }, Shown(cut, "todo"));
        Assert.Equal("Ada wasn't moved", Zone(cut, "todo").QuerySelector("[role=status]")!.TextContent);
        Assert.All(cut.FindAll("ib-sortable"), z => Assert.Equal("1", z.GetAttribute("data-ib-seq")));
    }

    [Fact]
    public void A_live_drop_the_zone_refuses_is_ignored()
    {
        Live();
        var cut = RenderBoard(name: null);

        // Brian isn't reviewed; Doing is full.
        Zone(cut, "todo").TriggerEvent("onibmove", new IzzyMoveEventArgs { Key = "b", ToZone = "done", ToIndex = 0, Seq = 1 });
        Zone(cut, "todo").TriggerEvent("onibmove", new IzzyMoveEventArgs { Key = "a", ToZone = "doing", ToIndex = 0, Seq = 2 });

        Assert.Empty(cut.Instance.Moves);
        Assert.Equal(new[] { "Ada", "Brian" }, Shown(cut, "todo"));
        Assert.All(cut.FindAll("ib-sortable"), z => Assert.Equal("2", z.GetAttribute("data-ib-seq")));
    }

    [Fact]
    public void The_live_menu_moves_to_a_zone_and_within_one()
    {
        Live();
        var cut = RenderBoard(name: null);

        MoveTo(Zone(cut, "todo"), 0).Single(b => b.GetAttribute("data-ib-zone") == "done").Click();
        Assert.Equal(new[] { "Eve", "Ada" }, Shown(cut, "done"));

        Zone(cut, "done").QuerySelectorAll("li")[1].QuerySelectorAll("button[data-ib-move=up]").Single().Click();
        Assert.Equal(new[] { "Ada", "Eve" }, Shown(cut, "done"));
        Assert.Equal("Moved Ada to position 1 of 2", Zone(cut, "done").QuerySelector("[role=status]")!.TextContent);
    }

    [Fact]
    public void ListOrder_arranges_zones_for_saving()
    {
        var order = new ListOrder { Keys = ["b", "a", "c"], Zones = ["doing", "todo", "doing"], Move = "to:todo:c" };
        var tasks = Board().Take(3).ToList();

        var places = order.Arrange(tasks, t => t.Id, t => t.Status);

        Assert.Equal(new[] { ("b", "doing", 0), ("a", "todo", 0), ("c", "todo", 1) }, places.Select(p => (p.Item.Id, p.Zone, p.Index)));
        Assert.Equal("todo", order.ZoneOf("c"));
        Assert.Equal("doing", order.ZoneOf("b"));
        Assert.Null(order.ZoneOf("zzz"));
    }
}
