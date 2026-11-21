using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms.Mapping;
using Microsoft.Extensions.DependencyInjection;
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
    public void A_static_board_posts_a_move_to_OnMove_and_counts_it()
    {
        // The board's form mapping scope needs a mapper; the test fills the post itself.
        Services.AddSingleton<IFormValueMapper>(new NoPosts());
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var cut = Render<KanbanProbe>(p => p.Add(b => b.Cards, Cards()));

        Assert.Equal(new[] { "Ada", "Brian" }, Shown(cut, "To do"));
        Assert.Equal("Cards: 1 of 1", Count(cut, "In progress"));
        Assert.False(Column(cut, "In progress").HasAttribute("data-ib-off"));

        // What the board's own form posts when Ada's menu says "Move to In progress".
        var post = cut.FindComponent<Kanban<Job>>().Instance.Post;
        post.Keys = cut.FindAll("input[name='ib-kanban.Keys']").Select(i => i.GetAttribute("value")!).ToArray();
        post.Zones = cut.FindAll("input[name='ib-kanban.Zones']").Select(i => i.GetAttribute("value")!).ToArray();
        post.Move = Column(cut, "To do").QuerySelector("button[data-ib-move=to][data-ib-zone='In%20progress']")!.GetAttribute("value");
        cut.Render();
        cut.Find("form.ib-kanban-form").Submit();

        // OnMove gets the columns as the data names them, and the limit is advisory: Ada moves in.
        var move = Assert.Single(cut.Instance.Moves);
        Assert.Equal(("Ada", "To do", "In progress", 0, 1), (move.Item.Title, move.FromColumn, move.ToColumn, move.FromIndex, move.ToIndex));
        Assert.Equal(new[] { "Brian" }, Shown(cut, "To do"));
        Assert.Equal(new[] { "Cleo", "Ada" }, Shown(cut, "In progress"));
        Assert.Equal("Cards: 2 of 1", Count(cut, "In progress"));
        Assert.True(Column(cut, "In progress").HasAttribute("data-ib-off"));
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

    private sealed class NoPosts : IFormValueMapper
    {
        public bool CanMap(Type valueType, string scopeName, string? formName) => false;

        public void Map(FormValueMappingContext context)
        {
        }
    }
}
