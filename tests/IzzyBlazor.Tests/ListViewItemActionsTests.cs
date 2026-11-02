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

    private IRenderedComponent<ListView<Person>> RenderList(Action<ComponentParameterCollectionBuilder<ListView<Person>>>? more = null)
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
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
    public void On_a_static_page_a_default_submit_button_comes_ahead_of_the_items_buttons()
    {
        var cut = RenderList(p => p.Add(l => l.ItemMenu, DeleteMenu()));

        var first = cut.Find("button[type=submit]");
        Assert.Contains("ib-data-default-submit", first.ClassName);
    }
}
