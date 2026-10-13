using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor.Tests;

public class ButtonGroupRenderTests : BunitContext
{
    private static RenderFragment Buttons(params (string Text, Accent? Accent)[] buttons) => builder =>
    {
        foreach (var (text, accent) in buttons)
        {
            builder.OpenComponent<Button>(0);
            if (accent is { } a) builder.AddAttribute(1, nameof(Button.Accent), a);
            builder.AddAttribute(2, nameof(Button.ChildContent), (RenderFragment)(b => b.AddContent(0, text)));
            builder.CloseComponent();
        }
    };

    [Fact]
    public void Renders_a_named_attached_group()
    {
        var group = Render<ButtonGroup>(p => p.Add(g => g.Label, "Calendar view").Add(g => g.ChildContent, Buttons(("Day", null), ("Week", null)))).Find("div");

        Assert.Equal("group", group.GetAttribute("role"));
        Assert.Equal("Calendar view", group.GetAttribute("aria-label"));
        Assert.Equal("ib-btn-group ib-btn-group-attached", group.ClassName);
        Assert.Equal(2, group.QuerySelectorAll("button.ib-btn").Length);
    }

    [Fact]
    public void Group_settings_become_the_buttons_defaults()
    {
        var cut = Render<ButtonGroup>(p => p
            .Add(g => g.Accent, Accent.Neutral)
            .Add(g => g.Variant, ButtonVariant.Outline)
            .Add(g => g.Size, ControlSize.Sm)
            .Add(g => g.ChildContent, Buttons(("Keep", null), ("Delete", Accent.Danger))));

        var buttons = cut.FindAll("button");

        Assert.Equal("ib-btn ib-btn-neutral ib-btn-outline ib-btn-sm", buttons[0].ClassName);
        Assert.Equal("ib-btn ib-btn-danger ib-btn-outline ib-btn-sm", buttons[1].ClassName);
    }

    [Fact]
    public void Buttons_outside_a_group_keep_their_defaults()
    {
        Assert.Equal("ib-btn ib-btn-primary ib-btn-solid", Render<Button>(p => p.AddChildContent("Save")).Find("button").ClassName);
    }

    [Fact]
    public void Spaced_vertical_and_full_width_options()
    {
        var group = Render<ButtonGroup>(p => p
            .Add(g => g.Attached, false)
            .Add(g => g.Orientation, Orientation.Vertical)
            .Add(g => g.FullWidth, true)).Find("div");

        Assert.Equal("ib-btn-group ib-btn-group-vertical ib-btn-group-full", group.ClassName);
    }
}
