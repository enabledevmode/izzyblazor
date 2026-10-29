using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

public class ListViewItemActionsTests : BunitContext
{
    public sealed record Person(string Id, string Name, bool Locked = false);

    private static readonly Person[] People =
    [
        new("1", "Ada"),
        new("2", "Brian", Locked: true),
        new("3", "Cleo"),
    ];

    // Set before rendering, after a test's services: the renderer info locks the service provider.
    private bool _live;

    private IRenderedComponent<ListView<Person>> RenderList(Action<ComponentParameterCollectionBuilder<ListView<Person>>>? more = null)
    {
        SetRendererInfo(_live ? new RendererInfo("Server", isInteractive: true) : new RendererInfo("Static", isInteractive: false));
        return Render<ListView<Person>>(p =>
        {
            p.Add(l => l.Items, People).Add(l => l.ItemKey, x => x.Id).Add(l => l.ItemText, x => x.Name);
            more?.Invoke(p);
        });
    }

    // A Delete item that posts the person's id.
    private static RenderFragment<Person> DeleteMenu(Func<Person, bool>? when = null) => person => b =>
    {
        if (when?.Invoke(person) == false)
        {
            return;
        }

        b.OpenComponent<MenuItem>(0);
        b.AddAttribute(1, nameof(MenuItem.Submit), true);
        b.AddAttribute(2, nameof(MenuItem.Name), "delete");
        b.AddAttribute(3, nameof(MenuItem.Value), person.Id);
        b.AddAttribute(4, nameof(MenuItem.ChildContent), (RenderFragment)(c => c.AddContent(0, "Delete")));
        b.CloseComponent();
    };

    private static RenderFragment<Person> StarAction => person => b =>
    {
        b.OpenElement(0, "button");
        b.AddAttribute(1, "type", "button");
        b.AddAttribute(2, "class", "star");
        b.AddContent(3, $"Star {person.Name}");
        b.CloseElement();
    };

    [Fact]
    public void ItemActions_sit_after_the_content_and_the_menu_button_last()
    {
        var cut = RenderList(p => p.Add(l => l.ItemActions, StarAction).Add(l => l.ItemMenu, DeleteMenu()));

        var first = cut.FindAll("li")[0];
        Assert.Equal(new[] { "ib-listview-content", "ib-listview-trail" }, first.Children.Select(c => c.ClassName));
        var trail = first.QuerySelector(".ib-listview-trail")!;
        Assert.Equal(new[] { "button", "ib-popover" }, trail.Children.Select(c => c.LocalName));
        Assert.Equal("Star Ada", trail.QuerySelector(".star")!.TextContent);
        Assert.Contains("ib-listview-has-trail", first.ClassName);
    }

    [Fact]
    public void The_menu_button_is_named_for_its_item_and_opens_its_menu()
    {
        var cut = RenderList(p => p.Add(l => l.ItemMenu, DeleteMenu()));

        var triggers = cut.FindAll("li button[popovertarget]");
        Assert.Equal(new[] { "Actions for Ada", "Actions for Brian", "Actions for Cleo" }, triggers.Select(t => t.GetAttribute("aria-label")));
        var panel = cut.Find($"#{triggers[0].GetAttribute("popovertarget")}");
        var delete = panel.QuerySelector("button[type=submit]")!;
        Assert.Equal("delete", delete.GetAttribute("name"));
        Assert.Equal("1", delete.GetAttribute("value"));
    }

    [Fact]
    public void An_item_whose_menu_renders_nothing_has_no_menu_button()
    {
        var cut = RenderList(p => p.Add(l => l.ItemMenu, DeleteMenu(x => !x.Locked)));

        Assert.Equal(new[] { 1, 0, 1 }, cut.FindAll("li").Select(li => li.QuerySelectorAll("ib-popover").Length));
    }

    [Fact]
    public void A_disabled_item_keeps_its_actions_and_menu()
    {
        var cut = RenderList(p => p
            .Add(l => l.ItemDisabled, x => x.Locked)
            .Add(l => l.ItemHref, x => $"/people/{x.Id}")
            .Add(l => l.ItemActions, StarAction)
            .Add(l => l.ItemMenu, DeleteMenu()));

        var brian = cut.FindAll("li")[1];
        Assert.Null(brian.QuerySelector("a.ib-listview-action"));
        Assert.NotNull(brian.QuerySelector(".star"));
        Assert.NotNull(brian.QuerySelector("button[popovertarget]"));
    }

    [Fact]
    public void On_a_static_page_a_default_submit_button_comes_ahead_of_the_items_buttons()
    {
        var cut = RenderList(p => p.Add(l => l.ItemMenu, DeleteMenu()));

        var first = cut.Find("button[type=submit]");
        Assert.Contains("ib-listview-default-submit", first.ClassName);
    }

    [Fact]
    public void Without_actions_there_is_no_default_submit_button()
    {
        var cut = RenderList();

        Assert.Empty(cut.FindAll(".ib-listview-default-submit"));
    }

    [Fact]
    public void Live_menus_render_menu_roles_and_still_a_default_submit_button()
    {
        // Submit items render type="submit" on interactive pages too, so Enter would press item 1's.
        _live = true;
        var cut = RenderList(p => p.Add(l => l.ItemMenu, DeleteMenu()));

        Assert.Contains("ib-listview-default-submit", cut.Find("button[type=submit]").ClassName);
        Assert.Equal(3, cut.FindAll("[role=menu]").Count);
    }

    [Fact]
    public void Actions_that_write_nothing_for_an_item_leave_no_trail()
    {
        RenderFragment<Person> sharedBadge = person => b =>
        {
            if (person.Locked)
            {
                b.AddMarkupContent(0, "<span class=\"badge\">Locked</span>");
            }
        };
        var cut = RenderList(p => p.Add(l => l.ItemActions, sharedBadge));

        Assert.Equal(new[] { 0, 1, 0 }, cut.FindAll("li").Select(li => li.QuerySelectorAll(".ib-listview-trail").Length));
        Assert.DoesNotContain("ib-listview-has-trail", cut.FindAll("li")[0].ClassName);
    }

    [Fact]
    public void A_reorderable_list_keeps_the_handle_first_and_the_item_menu_last()
    {
        _live = true;
        var cut = RenderList(p => p.Add(l => l.Reorderable, true).Add(l => l.ItemMenu, DeleteMenu()));

        var first = cut.FindAll("li")[0];
        Assert.NotNull(first.FirstElementChild!.QuerySelector(".ib-sortable-handle"));
        var menu = first.QuerySelector(".ib-listview-trail button[popovertarget]")!;
        Assert.Equal("Actions for Ada", menu.GetAttribute("aria-label"));
        // The move script finds moves by data-ib-move; the item's own menu has none.
        var panel = cut.Find($"#{menu.GetAttribute("popovertarget")}");
        Assert.Empty(panel.QuerySelectorAll("[data-ib-move]"));
    }

    [Fact]
    public void Cards_carry_the_trail_too()
    {
        var cut = RenderList(p => p.Add(l => l.Columns, 3).Add(l => l.ItemMenu, DeleteMenu()));

        Assert.Contains("ib-listview-columns", cut.Find("ib-listview").ClassName);
        Assert.Equal(3, cut.FindAll("li > .ib-listview-trail button[popovertarget]").Count);
    }

    [Fact]
    public void The_menu_button_text_comes_from_IzzyText()
    {
        Services.AddSingleton(new IzzyText { ItemMenu = "Acciones de {0}" });
        var cut = RenderList(p => p.Add(l => l.ItemMenu, DeleteMenu()));

        Assert.Equal("Acciones de Ada", cut.FindAll("button[popovertarget]")[0].GetAttribute("aria-label"));
    }
}
