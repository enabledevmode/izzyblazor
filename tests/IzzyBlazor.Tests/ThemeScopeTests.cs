using System.Text.RegularExpressions;

namespace IzzyBlazor.Tests;

// Theming part of a page, and the calculated colors that follow it (styling API D4, D5, D6).
public class ThemeScopeTests : BunitContext
{
    [Fact]
    public void A_nested_IzzyTheme_themes_its_content()
    {
        var part = Render<IzzyTheme>(p => p
            .Add(t => t.Density, ThemeDensity.Compact)
            .Add(t => t.Radius, ThemeRadius.Sm)
            .Add(t => t.Preset, ThemePreset.Editorial)
            .Add(t => t.Brand, "rose")
            .Add(t => t.ColorScheme, ColorScheme.Dark)
            .AddChildContent("<p>Report</p>")).Find("div");

        Assert.Equal("ib-theme ib-scheme-dark", part.ClassName);
        Assert.Equal("compact", part.GetAttribute("data-ib-density"));
        Assert.Equal("sm", part.GetAttribute("data-ib-radius"));
        Assert.Equal("editorial", part.GetAttribute("data-ib-preset"));
        Assert.Equal("rose", part.GetAttribute("data-ib-brand"));
        Assert.Null(part.GetAttribute("data-ib-gray"));
        Assert.Equal("Report", part.QuerySelector("p")!.TextContent);
    }

    [Fact]
    public void A_nested_brand_color_is_inline_and_takes_style_and_class()
    {
        var part = Render<IzzyTheme>(p => p
            .Add(t => t.Brand, "#be123c")
            .Add(t => t.Style, "--ib-card-padding: 1rem")
            .Add(t => t.Class, "report-band")
            .AddUnmatched("id", "report")
            .AddChildContent("x")).Find("div");

        var style = part.GetAttribute("style")!;
        Assert.Contains("--ib-color-primary: light-dark(", style);
        Assert.EndsWith("--ib-card-padding: 1rem;", style);
        Assert.Equal("ib-theme report-band", part.ClassName);
        Assert.Equal("report", part.Id);
    }

    [Theory]
    [InlineData(nameof(IzzyTheme.BodyFont))]
    [InlineData(nameof(IzzyTheme.HeadingFont))]
    [InlineData(nameof(IzzyTheme.MonoFont))]
    public void Fonts_are_app_wide_only(string parameter)
    {
        var e = Assert.ThrowsAny<ArgumentException>(() => Render<IzzyTheme>(p => (parameter switch
        {
            nameof(IzzyTheme.BodyFont) => p.Add(t => t.BodyFont, "Inter, sans-serif"),
            nameof(IzzyTheme.HeadingFont) => p.Add(t => t.HeadingFont, "Inter, sans-serif"),
            _ => p.Add(t => t.MonoFont, "Inter, sans-serif"),
        }).AddChildContent("x")));
        Assert.Contains("app-wide", e.Message);
    }

    [Theory]
    [InlineData(ThemeScrollbars.Themed, "themed")]
    [InlineData(ThemeScrollbars.Native, "native")]
    public void Scrollbars_is_an_app_wide_choice_the_page_reads_from_head(ThemeScrollbars choice, string content)
    {
        Assert.Equal(content, Render<IzzyTheme>(p => p.Add(t => t.Scrollbars, choice)).Find("meta[name=ib-scrollbars]").GetAttribute("content"));
        // Auto is the default: nothing to render.
        Assert.Empty(Render<IzzyTheme>(p => p.Add(t => t.Scrollbars, ThemeScrollbars.Auto)).FindAll("meta[name=ib-scrollbars]"));
        Assert.Contains("app-wide", Assert.ThrowsAny<ArgumentException>(() => Render<IzzyTheme>(p => p.Add(t => t.Scrollbars, choice).AddChildContent("x"))).Message);
    }

    [Fact]
    public void The_app_theme_rejects_attributes_it_would_ignore()
    {
        var e = Assert.ThrowsAny<ArgumentException>(() => Render<IzzyTheme>(p => p.AddUnmatched("Look", "compact")));
        Assert.Contains("Look", e.Message);
    }

    [Fact]
    public void Inverse_is_only_for_part_of_a_page()
    {
        Assert.ThrowsAny<ArgumentException>(() => Render<IzzyTheme>(p => p.Add(t => t.ColorScheme, ColorScheme.Inverse)));
        Assert.Contains("ib-scheme-inverse", Render<IzzyTheme>(p => p.Add(t => t.ColorScheme, ColorScheme.Inverse).AddChildContent("x")).Find("div").ClassName);
    }

    // The colors made from other colors are worked out where they're used (a fallback in each
    // var()), so an override on part of the page reaches them. A bare var() would fix them to :root.
    [Theory]
    [InlineData("color-focus")]
    [InlineData("color-halo")]
    [InlineData("color-selected")]
    [InlineData("state-hover")]
    [InlineData("state-pressed")]
    [InlineData("color-segment")]
    [InlineData("color-segment-edge")]
    [InlineData("color-scrollbar")]
    [InlineData("color-scrollbar-hover")]
    public void Calculated_colors_always_carry_their_calculation(string token)
    {
        foreach (var file in StyleSheets())
        {
            Assert.DoesNotMatch(new Regex(@"var\(--ib-" + Regex.Escape(token) + @"\)"), File.ReadAllText(file));
            Assert.DoesNotMatch(new Regex(@"^\s*--ib-" + Regex.Escape(token) + @":\s*(var|color-mix|light-dark)", RegexOptions.Multiline), File.ReadAllText(file));
        }
    }

    // A more specific choice always wins, whatever order the rules are in: a specific attribute
    // over a specific meta tag over a preset attribute over a preset meta tag.
    [Fact]
    public void Theme_selectors_are_weighted_so_attributes_beat_meta_tags()
    {
        var css = File.ReadAllText(Path.Combine(StylesDir(), "themes.css"));
        Assert.DoesNotMatch(new Regex(@"^\s*\[data-ib-(accent|gray|radius|density)\]\[data-ib-\1=""", RegexOptions.Multiline), css);
        Assert.Matches(new Regex(@"\[data-ib-brand\]\[data-ib-brand\]\[data-ib-brand\]\[data-ib-brand=""rose""\]"), css);
        Assert.Matches(new Regex(@":root:root:root:where\(:has\(> head > meta\[name=""ib-brand""\]\[content=""rose""\]\)\)"), css);
        // A preset's colors only on <html>; its shape anywhere.
        Assert.Matches(new Regex(@":root\[data-ib-preset=""compact""\],\s*\n\s*:root:where\(:has\(> head > meta\[name=""ib-preset""\]\[content=""compact""\]\)\),\s*\n\s*\[data-ib-gray\]"), css.Replace("\r", ""));
        Assert.Matches(new Regex(@"\[data-ib-preset\]\[data-ib-preset=""compact""\],\s*\n\s*:root:where\(:has\(> head > meta\[name=""ib-preset""\]\[content=""compact""\]\)\),\s*\n\s*\[data-ib-radius\]"), css.Replace("\r", ""));
    }

    [Theory]
    [InlineData(ThemeRadius.None, "none")]
    [InlineData(ThemeRadius.Full, "full")]
    public void Radius_none_and_full_render_as_meta_tags_and_attributes(ThemeRadius radius, string name)
    {
        Assert.Equal(name, Render<IzzyTheme>(p => p.Add(t => t.Radius, radius)).Find("meta[name='ib-radius']").GetAttribute("content"));
        Assert.Equal(name, Render<IzzyTheme>(p => p.Add(t => t.Radius, radius).AddChildContent("x")).Find("div").GetAttribute("data-ib-radius"));
    }

    // Full makes controls pills without touching panels: controls read --ib-radius-control first.
    [Fact]
    public void Full_radius_rounds_controls_and_caps_panels()
    {
        var themes = File.ReadAllText(Path.Combine(StylesDir(), "themes.css")).Replace("\r", "");
        var full = Regex.Match(themes, @"\[data-ib-radius=""full""\],[^{]*\{(?<body>[^}]*)\}").Groups["body"].Value;
        Assert.Contains("--ib-radius-control: 9999px;", full);
        Assert.Contains("--ib-radius-xl: 1rem;", full);

        var button = File.ReadAllText(Path.Combine(StylesDir(), "components", "button.css"));
        Assert.Contains("border-radius: var(--ib-btn-radius, var(--ib-radius-control, var(--ib-radius-md)));", button);
        // Every medium radius in the button styles goes through the control radius.
        Assert.Equal(
            Regex.Matches(button, @"var\(--ib-radius-md\)").Count,
            Regex.Matches(button, @"var\(--ib-radius-control, var\(--ib-radius-md\)\)").Count);
    }

    // Public component variables work on :root or any element, so the components only read them
    // (with their default as the fallback) and never declare them on their own class.
    [Theory]
    [InlineData("btn-px")]
    [InlineData("btn-radius")]
    [InlineData("field-radius")]
    [InlineData("table-cell-px")]
    [InlineData("table-cell-py")]
    [InlineData("card-padding")]
    [InlineData("shell-header-h")]
    [InlineData("shell-footer-h")]
    [InlineData("shell-divider")]
    [InlineData("sidebar-w")]
    [InlineData("aside-w")]
    [InlineData("avatar-ring")]
    [InlineData("timeline-bg")]
    [InlineData("radius-control")]
    public void Public_component_variables_are_read_never_declared_by_components(string name)
    {
        foreach (var file in Directory.EnumerateFiles(Path.Combine(StylesDir(), "components"), "*.css"))
        {
            Assert.DoesNotMatch(new Regex(@"^\s*--ib-" + Regex.Escape(name) + @"\s*:", RegexOptions.Multiline), File.ReadAllText(file));
        }
    }

    [Fact]
    public void There_is_no_fallback_for_browsers_without_light_dark()
    {
        Assert.DoesNotContain("@supports not (color: light-dark", File.ReadAllText(Path.Combine(StylesDir(), "tokens.css")));
    }

    private static string StylesDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "izzyblazor.sln")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir!.FullName, "src", "IzzyBlazor.Core", "Styles");
    }

    private static IEnumerable<string> StyleSheets() =>
        Directory.EnumerateFiles(StylesDir(), "*.css", SearchOption.AllDirectories).Where(f => !f.Contains("_generated", StringComparison.Ordinal));
}
