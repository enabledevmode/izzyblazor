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
    public void Items_render_as_a_list_with_the_template()
    {
        var cut = RenderList(p => p.Add(l => l.ChildContent, x => b => b.AddContent(0, $"[{x.Name}]")));

        var list = cut.Find("ul.ib-listview-list");
        Assert.Equal("list", list.GetAttribute("role"));
        Assert.Equal(new[] { "[Ada]", "[Brian]", "[Cleo]" }, cut.FindAll("li").Select(li => li.TextContent));
        Assert.Empty(cut.FindAll("a, button"));
    }

    [Fact]
    public void The_default_row_shows_the_text_description_and_icon()
    {
        var cut = RenderList(p => p
            .Add(l => l.ItemDescription, x => x.Email)
            .Add(l => l.ItemIcon, _ => Lucide.User));

        var first = cut.FindAll("li")[0];
        Assert.Equal("Ada", first.QuerySelector(".ib-listview-title")!.TextContent);
        Assert.Equal("ada@example.com", first.QuerySelector(".ib-listview-description")!.TextContent);
        Assert.NotNull(first.QuerySelector("svg.ib-listview-icon"));
    }

    [Fact]
    public void ItemHref_makes_one_named_link_per_item_and_marks_the_current_one()
    {
        var cut = RenderList(p => p
            .Add(l => l.ItemHref, x => $"/people/{x.Id}")
            .Add(l => l.ItemCurrent, x => x.Id == "3")
            .Add(l => l.ItemDisabled, x => x.Away));

        var links = cut.FindAll("a.ib-listview-action");
        Assert.Equal(new[] { "/people/1", "/people/3" }, links.Select(a => a.GetAttribute("href")));
        Assert.Equal(new[] { "Ada", "Cleo" }, links.Select(a => a.TextContent));
        Assert.Equal("true", links[1].GetAttribute("aria-current"));
        Assert.Null(cut.FindAll("li")[2].GetAttribute("aria-current"));

        var away = cut.FindAll("li")[1];
        Assert.Contains("ib-listview-disabled", away.ClassName);
        Assert.Null(away.QuerySelector("a"));
    }

    [Fact]
    public void Without_a_link_the_current_item_is_marked_itself()
    {
        var cut = RenderList(p => p.Add(l => l.ItemCurrent, x => x.Id == "2"));

        Assert.Equal("true", cut.FindAll("li")[1].GetAttribute("aria-current"));
        Assert.Contains("ib-listview-current", cut.FindAll("li")[1].ClassName);
    }

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
    public void An_empty_list_says_so()
    {
        var cut = Render<ListView<Person>>(p => p.Add(l => l.Items, Array.Empty<Person>()).Add(l => l.EmptyText, "Nobody here"));

        Assert.Empty(cut.FindAll("li"));
        Assert.Equal("Nobody here", cut.Find(".ib-listview-state").TextContent.Trim());
    }

    [Fact]
    public void PageSize_pages_through_the_url_with_the_shared_footer()
    {
        GoTo("/people?page=2");
        var cut = RenderList(p => p.Add(l => l.PageSize, 2));

        Assert.Equal(new[] { "Cleo" }, cut.FindAll(".ib-listview-title").Select(t => t.TextContent));
        Assert.Equal("3–3 of 3", cut.Find(".ib-table-summary").TextContent);
        Assert.Equal("List pages", cut.Find("nav.ib-pagination").GetAttribute("aria-label"));
    }

    [Fact]
    public void Load_more_on_a_static_page_links_to_the_list_with_one_more_page()
    {
        GoTo("/people");
        var cut = RenderList(p => p.Add(l => l.PageSize, 2).Add(l => l.Paging, DataPaging.LoadMore));

        Assert.Equal(2, cut.FindAll("li").Count);
        Assert.Equal("http://localhost/people?page=2", cut.Find(".ib-table-more > a.ib-btn").GetAttribute("href"));
    }

    [Fact]
    public void Load_more_sits_after_the_items_inside_the_scroll_box_with_no_footer()
    {
        GoTo("/people");
        var cut = RenderList(p => p
            .Add(l => l.PageSize, 2)
            .Add(l => l.Paging, DataPaging.LoadOnScroll)
            .Add(l => l.MaxHeight, Length.Rem(10))
            .Add(l => l.AriaLabel, "People"));

        var body = cut.Find("ib-listview > .ib-listview-body");
        Assert.Contains("ib-listview-scroll", body.ClassName);
        Assert.Equal("region", body.GetAttribute("role"));
        Assert.Equal("People", body.GetAttribute("aria-label"));
        Assert.Equal("0", body.GetAttribute("tabindex"));
        Assert.Contains("max-height:10rem", body.GetAttribute("style"));
        Assert.NotNull(cut.Find("ib-listview > .ib-listview-body > .ib-listview-list + .ib-table-summary + .ib-table-more"));
        Assert.Empty(cut.FindAll(".ib-table-footer"));
        Assert.Equal("1–2 of 3", cut.Find(".ib-table-summary").TextContent);
    }

    [Fact]
    public void A_failed_provider_shows_the_list_message_with_retry()
    {
        var cut = Render<ListView<Person>>(p => p
            .Add(l => l.ItemsProvider, _ => throw new InvalidOperationException("down")));

        Assert.Contains("Couldn't load the items.", cut.Find(".ib-listview-state").TextContent);
        Assert.Equal("Couldn't load the items.", cut.Find(".ib-table-error-status").TextContent);
        Assert.NotNull(cut.Find(".ib-listview-state a.ib-btn"));
    }

    [Fact]
    public void Selectable_items_post_their_keys_with_checkboxes_named_by_label()
    {
        var cut = RenderList(p => p
            .Add(l => l.Selectable, true)
            .Add(l => l.SelectedKeys, new[] { "3" })
            .Add(l => l.ItemDisabled, x => x.Away));

        var boxes = cut.FindAll("li > input.ib-listview-select");
        Assert.Equal(new[] { "1", "2", "3" }, boxes.Select(b => b.GetAttribute("value")));
        Assert.All(boxes, b => Assert.Equal("selected", b.GetAttribute("name")));
        Assert.Equal("Select Ada", boxes[0].GetAttribute("aria-label"));
        Assert.True(boxes[2].HasAttribute("checked"));
        Assert.True(boxes[1].HasAttribute("disabled"));
        Assert.NotNull(cut.Find(".ib-listview-select-all"));
    }

    [Fact]
    public void Selecting_on_an_interactive_page_reports_the_keys()
    {
        Live();
        IReadOnlyCollection<string>? keys = null;
        var cut = RenderList(p => p
            .Add(l => l.Selectable, true)
            .Add(l => l.SelectedKeysChanged, k => keys = k));

        cut.FindAll("input.ib-listview-select")[1].Change(true);

        Assert.Equal(new[] { "2" }, keys);
    }

    [Fact]
    public void Select_all_is_there_before_any_items_so_the_script_finds_it_when_they_come()
    {
        var cut = Render<ListView<Person>>(p => p.Add(l => l.Items, Array.Empty<Person>()).Add(l => l.Selectable, true));

        Assert.NotNull(cut.Find(".ib-listview-select-all"));
        // Focus after Retry goes to the root, which is always there.
        Assert.Equal("-1", cut.Find("ib-listview").GetAttribute("tabindex"));
    }

    [Fact]
    public void A_selected_disabled_item_still_posts_its_key()
    {
        var cut = RenderList(p => p
            .Add(l => l.Selectable, true)
            .Add(l => l.SelectedKeys, new[] { "2" })
            .Add(l => l.ItemDisabled, x => x.Away));

        var hidden = cut.FindAll("li input[type=hidden]");
        Assert.Equal("2", Assert.Single(hidden).GetAttribute("value"));
        Assert.Equal("selected", hidden[0].GetAttribute("name"));
    }

    [Fact]
    public void Columns_lay_the_items_out_as_cards()
    {
        var cut = RenderList(p => p.Add(l => l.Columns, 3));

        var root = cut.Find("ib-listview");
        Assert.Contains("ib-listview-columns", root.ClassName);
        Assert.DoesNotContain("ib-listview-dividers", root.ClassName);
        Assert.Contains("--ib-listview-columns:3", root.GetAttribute("style"));
    }
}
