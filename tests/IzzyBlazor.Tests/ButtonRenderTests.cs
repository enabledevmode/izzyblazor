using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace IzzyBlazor.Tests;

public class ButtonRenderTests : BunitContext
{
    [Fact]
    public void A_toggle_button_is_a_checkbox_drawn_as_a_button()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var on = false;
        var cut = Render<ToggleButton>(p => p
            .Add(b => b.Value, on)
            .Add(b => b.ValueChanged, (bool value) => on = value)
            .Add(b => b.ValueExpression, () => on)
            .Add(b => b.Icon, Lucide.Bold)
            .Add(b => b.Label, "Bold")
            .Add(b => b.IconOnly, true));

        var label = cut.Find("label.ib-btn.ib-btn-toggle");
        var box = label.QuerySelector("input[type=checkbox]")!;
        Assert.Equal("Bold", label.GetAttribute("title"));
        Assert.Equal("Bold", label.QuerySelector(".ib-visually-hidden:not(input)")!.TextContent);

        box.Change(true);
        Assert.True(on);
    }

    [Fact]
    public void Submit_button_posts_its_name_and_value()
    {
        var button = Render<Button>(p => p
            .Add(b => b.Submit, true)
            .Add(b => b.Name, "intent")
            .Add(b => b.Value, "publish")
            .Add(b => b.FormAction, "/drafts")).Find("button");

        Assert.Equal("submit", button.GetAttribute("type"));
        Assert.Equal("intent", button.GetAttribute("name"));
        Assert.Equal("publish", button.GetAttribute("value"));
        Assert.Equal("/drafts", button.GetAttribute("formaction"));
    }

    [Fact]
    public void Icon_submit_button_posts_its_name_and_value()
    {
        var button = Render<IconButton>(p => p
            .Add(b => b.Icon, Lucide.Trash)
            .Add(b => b.Label, "Delete invoice 42")
            .Add(b => b.Submit, true)
            .Add(b => b.Name, "delete")
            .Add(b => b.Value, "42")
            .Add(b => b.Form, "invoices")).Find("button");

        Assert.Equal("submit", button.GetAttribute("type"));
        Assert.Equal("delete", button.GetAttribute("name"));
        Assert.Equal("42", button.GetAttribute("value"));
        Assert.Equal("invoices", button.GetAttribute("form"));
        Assert.Throws<InvalidOperationException>(() => Render<IconButton>(p => p
            .Add(b => b.Icon, Lucide.Trash)
            .Add(b => b.Label, "Delete")
            .Add(b => b.Href, "/x")
            .Add(b => b.Name, "delete")));
    }

    [Theory]
    [InlineData(false, false, "button")]
    [InlineData(true, false, "submit")]
    [InlineData(false, true, "reset")]
    public void Submit_and_Reset_set_the_type(bool submit, bool reset, string? expected)
    {
        if (expected is null)
        {
            Assert.Throws<InvalidOperationException>(() => Render<Button>(p => p.Add(b => b.Submit, submit).Add(b => b.Reset, reset)));
            return;
        }

        var type = Render<Button>(p => p.Add(b => b.Submit, submit).Add(b => b.Reset, reset)).Find("button").GetAttribute("type");
        Assert.Equal(expected, type);
    }

    [Fact]
    public void OnClick_runs_when_clicked()
    {
        var clicks = 0;
        var cut = Render<Button>(p => p.Add(b => b.OnClick, (MouseEventArgs _) => clicks++));

        cut.Find("button").Click();

        Assert.Equal(1, clicks);
    }

    [Fact]
    public void IconButton_requires_a_label()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Render<IconButton>(p => p.Add(b => b.Icon, Lucide.Pencil)));
    }

    [Fact]
    public void Accent_rejects_css_that_escapes_the_declaration()
    {
        Assert.Throws<FormatException>(() => Accent.Of("red; } body { color: red"));
    }
}
