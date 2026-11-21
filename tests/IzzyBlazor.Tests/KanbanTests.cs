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

    [Fact]
    public void A_card_opens_the_form_which_saves_its_title_and_column()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var cut = Render<KanbanProbe>(p => p.Add(b => b.Cards, Cards()));

        // With OnSave a card is a link to its form, and each header's add button to a new card's.
        var link = Column(cut, "To do").QuerySelector("a.ib-listview-action")!;
        Assert.Contains("card=a", link.GetAttribute("href"));
        Assert.Contains("card=new&column=Done", Column(cut, "Done").QuerySelector("a.ib-kanban-add")!.GetAttribute("href"));

        Services.GetRequiredService<NavigationManager>().NavigateTo(link.GetAttribute("href")!);
        cut.Render();
        Assert.Equal("Ada", cut.Find("input[name='ib-kanban-edit.Title']").GetAttribute("value"));

        cut.Find("input[name='ib-kanban-edit.Title']").Change("Ada Lovelace");
        cut.Find("select[name='ib-kanban-edit.Column']").Change("Done");
        cut.Find("form.ib-form").Submit();

        var save = cut.Instance.Save;
        Assert.NotNull(save);
        Assert.Equal((false, "Ada", "Ada Lovelace", "Done"), (save!.IsNew, save.Item!.Title, save.Title, save.Column));
        Assert.Empty(cut.FindAll("dialog[open]"));
    }

    [Fact]
    public void A_board_with_lanes_shows_a_cell_per_lane_and_column_and_moves_between_lanes()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var moves = new List<KanbanMoveEventArgs<Job>>();
        var jobs = new List<Job> { new("a", "Ada", "To do") { Team = "Design" }, new("b", "Brian", "To do") { Team = "Build" }, new("c", "Cleo", "Done") { Team = "Build" } };
        var cut = Render<Kanban<Job>>(p => p
            .Add(k => k.Items, jobs)
            .Add(k => k.ItemKey, j => j.Id)
            .Add(k => k.ColumnOf, j => j.Status)
            .Add(k => k.LaneOf, j => j.Team)
            .Add(k => k.ItemText, j => j.Title)
            .Add(k => k.OnMove, (KanbanMoveEventArgs<Job> m) => { moves.Add(m); (m.Item.Status, m.Item.Team) = (m.ToColumn, m.ToLane!); })
            .Add(k => k.Columns, b =>
            {
                b.OpenComponent<KanbanColumn<Job>>(0);
                b.AddAttribute(1, nameof(KanbanColumn<Job>.Value), "To do");
                b.CloseComponent();
                b.OpenComponent<KanbanColumn<Job>>(2);
                b.AddAttribute(3, nameof(KanbanColumn<Job>.Value), "Done");
                b.CloseComponent();
            })
            .Add(k => k.Lanes, b =>
            {
                b.OpenComponent<KanbanLane>(0);
                b.AddAttribute(1, nameof(KanbanLane.Value), "Design");
                b.CloseComponent();
                b.OpenComponent<KanbanLane>(2);
                b.AddAttribute(3, nameof(KanbanLane.Value), "Build");
                b.CloseComponent();
            }));

        string[] Cell(string lane, string column) => cut.FindAll(".ib-kanban-lane").Single(l => l.QuerySelector(".ib-kanban-lane-title")!.TextContent == lane)
            .QuerySelectorAll($".ib-kanban-cell[data-ib-column='{column}'] .ib-kanban-card-text").Select(c => c.TextContent).ToArray();
        string Head(string column) => cut.FindAll(".ib-kanban-head").Single(h => h.QuerySelector(".ib-kanban-title")!.TextContent == column)
            .QuerySelector(".ib-kanban-count-text")!.TextContent;

        Assert.Equal(new[] { "Ada" }, Cell("Design", "To do"));
        Assert.Equal(new[] { "Brian" }, Cell("Build", "To do"));
        // A column's header counts its cards in every lane.
        Assert.Equal("Cards: 2", Head("To do"));

        // Ada's menu: to Done in the Build lane.
        cut.FindAll(".ib-kanban-lane")[0].QuerySelector("button[data-ib-move=to][data-ib-zone='Build/Done']")!.Click();

        var move = Assert.Single(moves);
        Assert.Equal(("To do", "Design", "Done", "Build"), (move.FromColumn, move.FromLane, move.ToColumn, move.ToLane));
        Assert.Equal(new[] { "Cleo", "Ada" }, Cell("Build", "Done"));
        Assert.Equal("Cards: 1", Head("To do"));
    }

    private sealed class NoPosts : IFormValueMapper
    {
        public bool CanMap(Type valueType, string scopeName, string? formName) => false;

        public void Map(FormValueMappingContext context)
        {
        }
    }
}
