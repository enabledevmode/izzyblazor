using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

public class MenuExtrasRenderTests : BunitContext
{
    public MenuExtrasRenderTests() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    private void GoTo(string path) => Services.GetRequiredService<BunitNavigationManager>().NavigateTo(path);

    private IRenderedComponent<Menu> MenuWith(RenderFragment content) =>
        Render<Menu>(p => p.Add(m => m.Id, "m").Add(m => m.Label, "Menu").Add(m => m.ChildContent, content));

    private static RenderFragment Item(string text, params (string Name, object? Value)[] parameters) => builder =>
    {
        builder.OpenComponent<MenuItem>(0);
        builder.AddComponentParameter(1, nameof(MenuItem.ChildContent), (RenderFragment)(b => b.AddContent(0, text)));
        foreach (var (name, value) in parameters)
        {
            builder.AddComponentParameter(2, name, value);
        }

        builder.CloseComponent();
    };

    private static RenderFragment Radios(params (string Name, object? Value)[] group) => builder =>
    {
        builder.OpenComponent<MenuRadioGroup>(0);
        builder.AddComponentParameter(1, nameof(MenuRadioGroup.Label), "Sort by");
        foreach (var (name, value) in group)
        {
            builder.AddComponentParameter(2, name, value);
        }

        builder.AddComponentParameter(3, nameof(MenuRadioGroup.ChildContent), (RenderFragment)(b =>
        {
            b.AddContent(0, Item("Name", (nameof(MenuItem.Value), "name")));
            b.AddContent(1, Item("Date", (nameof(MenuItem.Value), "date")));
        }));
        builder.CloseComponent();
    };

    [Fact]
    public void Checkbox_item_has_the_role_state_and_a_check_slot()
    {
        var cut = MenuWith(b =>
        {
            b.AddContent(0, Item("On", (nameof(MenuItem.Checked), true)));
            b.AddContent(1, Item("Off", (nameof(MenuItem.Checked), false)));
            b.AddContent(2, Item("Plain"));
        });
        var items = cut.FindAll("[popover] > button");

        Assert.Equal(new[] { "menuitemcheckbox", "menuitemcheckbox", "menuitem" }, items.Select(i => i.GetAttribute("data-ib-role")).ToArray());
        Assert.Equal(new string?[] { "true", "false", null }, items.Select(i => i.GetAttribute("data-ib-checked")).ToArray());
        // With JavaScript off they're plain buttons, the state carried by aria-pressed.
        Assert.All(items, i => Assert.Null(i.GetAttribute("role")));
        Assert.Equal(new string?[] { "true", "false", null }, items.Select(i => i.GetAttribute("aria-pressed")).ToArray());
        Assert.NotNull(items[0].QuerySelector(".ib-menu-check svg"));
        Assert.Null(items[1].QuerySelector(".ib-menu-check svg"));
        Assert.NotNull(items[1].QuerySelector(".ib-menu-check"));
        Assert.Null(items[2].QuerySelector(".ib-menu-check"));
    }

    [Fact]
    public void Checkbox_item_toggles_through_CheckedChanged()
    {
        bool? changed = null;
        var cut = MenuWith(Item("Wrap", (nameof(MenuItem.Checked), true), (nameof(MenuItem.CheckedChanged), EventCallback.Factory.Create<bool>(this, v => changed = v))));

        cut.Find("[data-ib-role=menuitemcheckbox]").Click();

        Assert.False(changed);
    }

    [Fact]
    public void Radio_group_with_QueryKey_links_options_and_checks_from_the_url()
    {
        GoTo("files?sort=date&page=2");
        var cut = MenuWith(Radios((nameof(MenuRadioGroup.QueryKey), "sort")));
        var options = cut.FindAll("a[data-ib-role=menuitemradio]");

        Assert.Equal(new[] { "false", "true" }, options.Select(o => o.GetAttribute("data-ib-checked")).ToArray());
        Assert.Equal(new string?[] { null, "true" }, options.Select(o => o.GetAttribute("aria-current")).ToArray());
        Assert.Contains("sort=name", options[0].GetAttribute("href"));
        Assert.Contains("page=2", options[0].GetAttribute("href"));
        Assert.NotNull(options[1].QuerySelector(".ib-menu-dot"));
        Assert.Equal("Sort by", cut.Find("[role=group] .ib-menu-heading").TextContent);
    }

    [Fact]
    public void Radio_group_with_Name_posts_each_option()
    {
        var cut = MenuWith(Radios((nameof(MenuRadioGroup.Name), "sort"), (nameof(MenuRadioGroup.Value), "name")));
        var options = cut.FindAll("button[data-ib-role=menuitemradio]");

        Assert.All(options, o => Assert.Equal("submit", o.GetAttribute("type")));
        Assert.All(options, o => Assert.Equal("sort", o.GetAttribute("name")));
        Assert.Equal(new[] { "name", "date" }, options.Select(o => o.GetAttribute("value")).ToArray());
        Assert.Equal("true", options[0].GetAttribute("data-ib-checked"));
    }

    [Fact]
    public void Radio_group_binds_its_value()
    {
        string? chosen = null;
        var cut = MenuWith(Radios((nameof(MenuRadioGroup.Value), "name"), (nameof(MenuRadioGroup.ValueChanged), EventCallback.Factory.Create<string>(this, v => chosen = v))));

        cut.FindAll("button[data-ib-role=menuitemradio]")[1].Click();

        Assert.Equal("date", chosen);
    }

    [Fact]
    public void Submenu_is_a_nested_menu_opened_by_its_item()
    {
        var cut = MenuWith(b =>
        {
            b.OpenComponent<MenuSub>(0);
            b.AddComponentParameter(1, nameof(MenuSub.Label), "Move to");
            b.AddComponentParameter(2, nameof(MenuSub.ChildContent), Item("Inbox"));
            b.CloseComponent();
        });

        var trigger = cut.Find(".ib-menu-sub-trigger");
        var sub = cut.Find("[popover] [popover]");

        Assert.Equal("menuitem", trigger.GetAttribute("data-ib-role"));
        Assert.Equal("menu", trigger.GetAttribute("data-ib-haspopup"));
        Assert.Equal(sub.Id, trigger.GetAttribute("popovertarget"));
        Assert.Equal("menu", sub.GetAttribute("data-ib-role"));
        Assert.Equal(trigger.Id, sub.GetAttribute("aria-labelledby"));
        Assert.Equal("end-start", cut.Find(".ib-menu-sub-root").GetAttribute("data-placement"));

        // Items in the submenu close the whole menu, natively.
        var item = sub.QuerySelector("button[data-ib-role=menuitem]")!;
        Assert.Equal("m", item.GetAttribute("popovertarget"));
        Assert.Equal("hide", item.GetAttribute("popovertargetaction"));
    }

    [Fact]
    public void Interactive_menus_render_their_roles_directly()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var cut = MenuWith(b =>
        {
            b.AddContent(0, Item("Wrap", (nameof(MenuItem.Checked), true)));
            b.AddContent(1, Item("Plain"));
        });

        Assert.Equal("menu", cut.Find("[popover]").GetAttribute("role"));
        Assert.Equal("menu", cut.Find("button[popovertarget]").GetAttribute("aria-haspopup"));
        var items = cut.FindAll("[popover] > button");
        Assert.Equal(new[] { "menuitemcheckbox", "menuitem" }, items.Select(i => i.GetAttribute("role")).ToArray());
        Assert.Equal("true", items[0].GetAttribute("aria-checked"));
        Assert.Null(items[0].GetAttribute("aria-pressed"));
        Assert.Empty(cut.FindAll("[data-ib-role], [data-ib-checked]"));
    }
}
