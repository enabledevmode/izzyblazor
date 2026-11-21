using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;
using Job = IzzyBlazor.Tests.KanbanProbe.Job;

namespace IzzyBlazor.Tests;

public class KanbanTests : BunitContext
{
    private static List<Job> Cards() =>
    [
        new("a", "Ada", "To do"),
        new("b", "Brian", "To do"),
        new("c", "Cleo", "In progress"),
        new("d", "Dan", "Done"),
    ];

    private static IElement Column(IRenderedComponent<KanbanProbe> cut, string title) =>
        cut.FindAll(".ib-kanban-column").Single(c => c.QuerySelector(".ib-kanban-title")!.TextContent == title);

    private static string[] Shown(IRenderedComponent<KanbanProbe> cut, string title) =>
        Column(cut, title).QuerySelectorAll(".ib-kanban-card-text").Select(c => c.TextContent).ToArray();

    private static string Count(IRenderedComponent<KanbanProbe> cut, string title) =>
        Column(cut, title).QuerySelector(".ib-kanban-count-text")!.TextContent;

    [Fact]
    public void A_static_board_shows_the_posted_move_and_counts_it()
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var cards = Cards();
        var cut = Render<KanbanProbe>(p => p.Add(b => b.Cards, cards));

        Assert.Equal(new[] { "Ada", "Brian" }, Shown(cut, "To do"));
        Assert.Equal("Cards: 1 of 1", Count(cut, "In progress"));
        Assert.False(Column(cut, "In progress").HasAttribute("data-ib-off"));

        // What Ada's menu posts for "Move to In progress": the column's value, written as its zone.
        var move = Column(cut, "To do").QuerySelector("button[data-ib-move=to][data-ib-zone='In%20progress']")!.GetAttribute("value");
        var order = new KanbanOrder { Keys = ["a", "b", "c", "d"], Zones = ["To%20do", "To%20do", "In%20progress", "Done"], Move = move };
        cut = Render<KanbanProbe>(p => p.Add(b => b.Cards, cards).Add(b => b.Order, order));

        // The limit is advisory: Ada moves in, and the count says the column is over it.
        Assert.Equal(new[] { "Brian" }, Shown(cut, "To do"));
        Assert.Equal(new[] { "Cleo", "Ada" }, Shown(cut, "In progress"));
        Assert.Equal("Cards: 2 of 1", Count(cut, "In progress"));
        Assert.True(Column(cut, "In progress").HasAttribute("data-ib-off"));

        // What the page saves: each card's column as the data names it.
        var ada = order.Arrange(cards, c => c.Id, c => c.Status).Single(p => p.Item.Id == "a");
        Assert.Equal(("In progress", 1), (ada.Column, ada.Index));
    }

    [Fact]
    public void An_interactive_move_runs_OnMove_with_the_columns_and_counts_again()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var cut = Render<KanbanProbe>(p => p.Add(b => b.Cards, Cards()));

        Column(cut, "To do").QuerySelector("button[data-ib-move=to][data-ib-zone='Done']")!.Click();

        var move = Assert.Single(cut.Instance.Moves);
        Assert.Equal(("Ada", "To do", "Done", 1), (move.Item.Title, move.FromColumn, move.ToColumn, move.ToIndex));
        Assert.Equal(new[] { "Dan", "Ada" }, Shown(cut, "Done"));
        Assert.Equal("Cards: 2", Count(cut, "Done"));
        Assert.Equal("Cards: 1", Count(cut, "To do"));
    }
}
