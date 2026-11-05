using System.Text.RegularExpressions;

namespace IzzyBlazor.Tests;

// Theming part of a page, and the calculated colors that follow it (styling API D4, D5, D6).
public class ThemeScopeTests : BunitContext
{
    [Fact]
    public void A_ThemeScope_themes_its_content()
    {
        var part = Render<ThemeScope>(p => p
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

    // The colors made from other colors are worked out where they're used (a fallback in each
    // var()), so an override on part of the page reaches them. A bare var() would fix them to :root.
    [Theory]
    [InlineData("color-focus")]
    [InlineData("color-selected")]
    [InlineData("state-hover")]
    [InlineData("color-segment")]
    [InlineData("color-scrollbar")]
    public void Calculated_colors_always_carry_their_calculation(string token)
    {
        foreach (var file in StyleSheets())
        {
            Assert.DoesNotMatch(new Regex(@"var\(--ib-" + Regex.Escape(token) + @"\)"), File.ReadAllText(file));
            Assert.DoesNotMatch(new Regex(@"^\s*--ib-" + Regex.Escape(token) + @":\s*(var|color-mix|light-dark)", RegexOptions.Multiline), File.ReadAllText(file));
        }
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
