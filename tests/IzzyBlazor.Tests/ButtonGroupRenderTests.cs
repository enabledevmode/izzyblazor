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
}
