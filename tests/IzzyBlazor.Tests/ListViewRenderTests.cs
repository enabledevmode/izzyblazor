using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

public class ListViewRenderTests : BunitContext
{
    public sealed record Person(string Id, string Name, string Email, bool Away = false);

    private static readonly Person[] People =
    [
        new("1", "Ada", "ada@example.com"),
        new("2", "Brian", "brian@example.com", Away: true),
        new("3", "Cleo", "cleo@example.com"),
    ];

    public ListViewRenderTests() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    private void Live() => SetRendererInfo(new RendererInfo("Server", isInteractive: true));

    private void GoTo(string path) => Services.GetRequiredService<BunitNavigationManager>().NavigateTo(path);

    private IRenderedComponent<ListView<Person>> RenderList(Action<ComponentParameterCollectionBuilder<ListView<Person>>>? more = null) =>
        Render<ListView<Person>>(p =>
        {
            p.Add(l => l.Items, People).Add(l => l.ItemKey, x => x.Id).Add(l => l.ItemText, x => x.Name);
            more?.Invoke(p);
        });

    [Fact]
    public void OnItemClick_makes_each_item_a_button_on_interactive_pages()
    {
        Live();
        Person? clicked = null;
        var cut = RenderList(p => p.Add(l => l.OnItemClick, x => clicked = x));

        var buttons = cut.FindAll("button.ib-listview-action");
        Assert.Equal(3, buttons.Count);
        Assert.Equal("button", buttons[1].GetAttribute("type"));
        buttons[1].Click();
        Assert.Equal("Brian", clicked?.Name);
    }

    [Fact]
    public void PageSize_pages_through_the_url_with_the_shared_footer()
    {
        GoTo("/people?page=2");
        var cut = RenderList(p => p.Add(l => l.PageSize, 2));

        Assert.Equal(new[] { "Cleo" }, cut.FindAll(".ib-listview-title").Select(t => t.TextContent));
        Assert.Equal("3–3 of 3", cut.Find(".ib-data-summary").TextContent);
        Assert.Equal("List pages", cut.Find("nav.ib-pagination").GetAttribute("aria-label"));
    }

    [Fact]
    public void Load_more_on_a_static_page_links_to_the_list_with_one_more_page()
    {
        GoTo("/people");
        var cut = RenderList(p => p.Add(l => l.PageSize, 2).Add(l => l.Paging, DataPaging.LoadMore));

        Assert.Equal(2, cut.FindAll("li").Count);
        Assert.Equal("http://localhost/people?page=2", cut.Find(".ib-data-more > a.ib-btn").GetAttribute("href"));
    }

    [Fact]
    public void A_failed_provider_shows_the_list_message_with_retry()
    {
        var cut = Render<ListView<Person>>(p => p
            .Add(l => l.ItemsProvider, _ => throw new InvalidOperationException("down")));

        Assert.Contains("Couldn't load the items.", cut.Find(".ib-listview-state").TextContent);
        Assert.Equal("Couldn't load the items.", cut.Find(".ib-data-error-status").TextContent);
        Assert.NotNull(cut.Find(".ib-listview-state a.ib-btn"));
    }

    [Fact]
    public void Selectable_items_post_their_keys_with_checkboxes_named_by_label()
    {
        var cut = RenderList(p => p
            .Add(l => l.SelectionMode, SelectionMode.Multiple)
            .Add(l => l.Selection, Selection.Of("3"))
            .Add(l => l.ItemDisabled, x => x.Away));

        var boxes = cut.FindAll("li > input.ib-listview-select");
        Assert.Equal(new[] { "1", "2", "3" }, boxes.Select(b => b.GetAttribute("value")));
        Assert.All(boxes, b => Assert.Equal("selected.Keys", b.GetAttribute("name")));
        Assert.Equal("Select Ada", boxes[0].GetAttribute("aria-label"));
        Assert.True(boxes[2].HasAttribute("checked"));
        Assert.True(boxes[1].HasAttribute("disabled"));
        Assert.NotNull(cut.Find(".ib-listview-select-all"));
    }

    [Fact]
    public void Selecting_on_an_interactive_page_reports_the_keys()
    {
        Live();
        Selection? selection = null;
        var cut = RenderList(p => p
            .Add(l => l.SelectionMode, SelectionMode.Multiple)
            .Add(l => l.SelectionChanged, s => selection = s));

        cut.FindAll("input.ib-listview-select")[1].Change(true);

        Assert.Equal(new[] { "2" }, selection!.Keys);
    }

    [Fact]
    public void A_selected_disabled_item_still_posts_its_key()
    {
        var cut = RenderList(p => p
            .Add(l => l.SelectionMode, SelectionMode.Multiple)
            .Add(l => l.Selection, Selection.Of("2"))
            .Add(l => l.ItemDisabled, x => x.Away));

        var hidden = cut.FindAll("li input[type=hidden]");
        Assert.Equal("2", Assert.Single(hidden).GetAttribute("value"));
        Assert.Equal("selected.Keys", hidden[0].GetAttribute("name"));
    }
}
