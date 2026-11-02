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
}
