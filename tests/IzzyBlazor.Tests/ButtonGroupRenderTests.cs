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

    [Fact]
    public void Buttons_in_a_dialog_dont_take_the_groups_style()
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var cut = Render<ButtonGroup>(p => p
            .Add(g => g.Accent, Accent.Neutral)
            .Add(g => g.Variant, ButtonVariant.Ghost)
            .Add(g => g.Size, ControlSize.Sm)
            .Add(g => g.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<ConfirmButton>(0);
                b.AddAttribute(1, nameof(ConfirmButton.Title), "Delete?");
                b.AddAttribute(2, nameof(ConfirmButton.Accent), Accent.Danger);
                b.AddAttribute(3, nameof(ConfirmButton.ChildContent), (RenderFragment)(c => c.AddContent(0, "Delete")));
                b.CloseComponent();
            })));

        // The trigger is in the group: ghost and small, but its own accent.
        var trigger = cut.Find(".ib-btn-group > button");
        Assert.Contains("ib-btn-ghost", trigger.ClassName);
        Assert.Contains("ib-btn-danger", trigger.ClassName);

        // The dialog's buttons are not.
        var inDialog = cut.FindAll("dialog .ib-dialog-footer .ib-btn");
        Assert.NotEmpty(inDialog);
        Assert.All(inDialog, b => Assert.DoesNotContain("ib-btn-ghost", b.ClassName));
        Assert.All(inDialog, b => Assert.DoesNotContain("ib-btn-sm", b.ClassName));
    }
}
