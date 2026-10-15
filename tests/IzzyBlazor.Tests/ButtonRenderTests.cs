using Microsoft.AspNetCore.Components.Web;

namespace IzzyBlazor.Tests;

public class ButtonRenderTests : BunitContext
{
    [Fact]
    public void Button_defaults_to_a_primary_solid_plain_button()
    {
        var button = Render<Button>(p => p.AddChildContent("Save")).Find("button");

        Assert.Equal("button", button.GetAttribute("type"));
        Assert.Equal("ib-btn ib-btn-primary ib-btn-solid", button.ClassName);
        Assert.Equal("Save", button.QuerySelector(".ib-btn-label")!.TextContent);
    }

    [Fact]
    public void Accent_variant_and_size_add_classes()
    {
        var button = Render<Button>(p => p
            .Add(b => b.Accent, Accent.Danger)
            .Add(b => b.Variant, ButtonVariant.Outline)
            .Add(b => b.Size, ControlSize.Lg)
            .Add(b => b.FullWidth, true)).Find("button");

        Assert.Equal("ib-btn ib-btn-danger ib-btn-outline ib-btn-lg ib-btn-full", button.ClassName);
    }

    [Fact]
    public void Submit_button_posts_its_name_and_value()
    {
        var button = Render<Button>(p => p
            .Add(b => b.Type, ButtonType.Submit)
            .Add(b => b.Name, "action")
            .Add(b => b.Value, "publish")
            .Add(b => b.FormAction, "/drafts")).Find("button");

        Assert.Equal("submit", button.GetAttribute("type"));
        Assert.Equal("action", button.GetAttribute("name"));
        Assert.Equal("publish", button.GetAttribute("value"));
        Assert.Equal("/drafts", button.GetAttribute("formaction"));
    }

    [Fact]
    public void Icons_render_around_the_label()
    {
        var button = Render<Button>(p => p
            .Add(b => b.StartIcon, Tabler.Plus)
            .Add(b => b.EndIcon, Tabler.ArrowRight)
            .AddChildContent("Add")).Find("button");

        var children = button.Children;
        Assert.Equal(3, children.Length);
        Assert.Equal("svg", children[0].TagName, ignoreCase: true);
        Assert.Equal("ib-btn-label", children[1].ClassName);
        Assert.Equal("svg", children[2].TagName, ignoreCase: true);
    }

    [Fact]
    public void Loading_shows_a_spinner_and_stays_focusable()
    {
        var button = Render<Button>(p => p
            .Add(b => b.Loading, true)
            .Add(b => b.StartIcon, Tabler.DeviceFloppy)
            .AddChildContent("Saving")).Find("button");

        Assert.False(button.HasAttribute("disabled"));
        Assert.Equal("true", button.GetAttribute("aria-busy"));
        Assert.Equal("true", button.GetAttribute("aria-disabled"));
        Assert.Contains("ib-icon-spin", button.QuerySelector("svg")!.ClassName);
        Assert.Single(button.QuerySelectorAll("svg"));
    }

    [Fact]
    public void Disabled_uses_the_native_attribute()
    {
        var button = Render<Button>(p => p.Add(b => b.Disabled, true)).Find("button");

        Assert.True(button.HasAttribute("disabled"));
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
    public void LinkButton_renders_a_link()
    {
        var a = Render<LinkButton>(p => p
            .Add(b => b.Href, "/start")
            .Add(b => b.Variant, ButtonVariant.Soft)).Find("a");

        Assert.Equal("/start", a.GetAttribute("href"));
        Assert.Equal("ib-btn ib-btn-primary ib-btn-soft", a.ClassName);
    }

    [Fact]
    public void External_LinkButton_opens_a_new_tab_and_says_so()
    {
        var a = Render<LinkButton>(p => p
            .Add(b => b.Href, "https://example.com")
            .Add(b => b.External, true)
            .AddChildContent("Docs")).Find("a");

        Assert.Equal("_blank", a.GetAttribute("target"));
        Assert.Equal("noopener noreferrer", a.GetAttribute("rel"));
        Assert.Contains("(opens in a new tab)", a.QuerySelector(".ib-visually-hidden")!.TextContent);
    }

    [Fact]
    public void Disabled_LinkButton_has_no_href()
    {
        var a = Render<LinkButton>(p => p
            .Add(b => b.Href, "/start")
            .Add(b => b.Disabled, true)).Find("a");

        Assert.False(a.HasAttribute("href"));
        Assert.Equal("true", a.GetAttribute("aria-disabled"));
    }

    [Fact]
    public void IconButton_is_labelled_and_square()
    {
        var button = Render<IconButton>(p => p
            .Add(b => b.Icon, Tabler.Pencil)
            .Add(b => b.Label, "Edit")).Find("button");

        Assert.Equal("Edit", button.GetAttribute("aria-label"));
        Assert.Equal("Edit", button.GetAttribute("title"));
        Assert.Contains("ib-btn-icon-only", button.ClassName);
        Assert.Equal("true", button.QuerySelector("svg")!.GetAttribute("aria-hidden"));
    }

    [Fact]
    public void IconButton_requires_a_label()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Render<IconButton>(p => p.Add(b => b.Icon, Tabler.Pencil)));
    }

    [Fact]
    public void IconButton_with_Href_renders_a_link()
    {
        var a = Render<IconButton>(p => p
            .Add(b => b.Icon, Tabler.ExternalLink)
            .Add(b => b.Label, "Docs")
            .Add(b => b.Href, "/docs")).Find("a");

        Assert.Equal("/docs", a.GetAttribute("href"));
    }

    [Fact]
    public void Custom_hex_color_picks_readable_text()
    {
        var amber = Render<Button>(p => p.Add(b => b.Accent, Accent.Of("#fbbf24")).AddChildContent("Upgrade")).Find("button");
        Assert.Contains("--ib-accent:#fbbf24;", amber.GetAttribute("style"));
        Assert.Contains("--ib-on-accent:#0f172a;", amber.GetAttribute("style"));

        var teal = Render<Button>(p => p.Add(b => b.Accent, Accent.Of("#0f766e")).AddChildContent("Approve")).Find("button");
        Assert.Contains("--ib-on-accent:#ffffff;", teal.GetAttribute("style"));
    }

    [Fact]
    public void Custom_token_color_uses_its_pairs()
    {
        var style = Render<Button>(p => p.Add(b => b.Accent, Accent.Token("brand")).AddChildContent("Go")).Find("button").GetAttribute("style");

        Assert.Contains("--ib-accent:var(--ib-color-brand);", style);
        Assert.Contains("--ib-on-accent:var(--ib-color-on-brand);", style);
    }

    [Fact]
    public void Actions_have_three_accent_roles()
    {
        Assert.Equal(["primary", "neutral", "danger"], new[] { Accent.Primary, Accent.Neutral, Accent.Danger }.Select(a => a.ToString()));
        Assert.Equal(Accent.Of("#0f766e"), Accent.Of("#0f766e"));
        Assert.NotEqual(Accent.Primary, Accent.Token("primary"));
    }

    [Fact]
    public void A_custom_accent_paints_over_the_primary_role()
    {
        var button = Render<Button>(p => p.Add(b => b.Accent, Accent.Of("#0f766e")).AddChildContent("Go")).Find("button");

        Assert.Contains("ib-btn-primary", button.ClassName);
        Assert.Contains("--ib-accent:#0f766e;", button.GetAttribute("style"));
    }

    [Fact]
    public void Accent_rejects_css_that_escapes_the_declaration()
    {
        Assert.Throws<FormatException>(() => Accent.Of("red; } body { color: red"));
    }
}
