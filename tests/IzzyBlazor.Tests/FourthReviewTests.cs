using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

// Fixes from the fourth review (Oct 14): one test or more per finding.
public class FourthReviewTests : BunitContext
{
    [Fact]
    public async Task A_bad_dialog_brand_fails_the_caller_not_the_host()
    {
        Services.AddIzzyBlazorCore();
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var dialogs = Services.GetRequiredService<DialogService>();
        var host = Render<DialogHost>();

        await Assert.ThrowsAsync<ArgumentException>(() => dialogs.OpenAsync<Badge>("x", null, new DialogOptions { Brand = "purple" }));
        host.Render();
        Assert.Empty(host.FindAll("dialog"));
    }

    [Fact]
    public void A_side_set_only_at_a_breakpoint_keeps_the_all_sides_value_below_it()
    {
        var box = Render<Box>(p => p.Add(b => b.Border, 1).Add(b => b.BorderTop, new Responsive<int> { Md = 0 })).Find("div");
        var style = box.GetAttribute("style")!;

        Assert.Contains("--ib-bt:1px", style);
        Assert.Contains("--ib-bt-md:0", style);
        Assert.Contains("--ib-bb:1px", style);
    }

    [Fact]
    public void Padding_on_an_axis_at_a_breakpoint_keeps_the_all_sides_padding_below_it()
    {
        var box = Render<Box>(p => p.Add(b => b.Padding, 4).Add(b => b.PaddingX, new Responsive<int> { Md = 2 })).Find("div");
        var style = box.GetAttribute("style")!;

        Assert.Contains("ib-px", box.ClassName);
        Assert.Contains("ib-px-md", box.ClassName);
        Assert.Matches(@"--ib-px:var\(--ib-space-4\)|--ib-px:1rem", style);
    }

    [Fact]
    public void A_side_set_at_base_holds_where_the_axis_changes_at_a_breakpoint()
    {
        var box = Render<Box>(p => p.Add(b => b.BorderX, new Responsive<int> { Base = 1, Lg = 3 }).Add(b => b.BorderStart, 2)).Find("div");
        var style = box.GetAttribute("style")!;

        Assert.Contains("--ib-bs:2px", style);
        Assert.Contains("--ib-bs-lg:2px", style);
        Assert.Contains("--ib-be-lg:3px", style);
    }

    [Fact]
    public void BorderColor_at_a_breakpoint_survives_a_width_set_at_a_larger_one()
    {
        var css = Read("Styles", "_generated", "style-props.css");

        Assert.Contains(".ib-bc-md { --ib-bc-now: var(--ib-bc-md); border-color: var(--ib-bc-md); }", css);
        Assert.Contains(".ib-bt-lg { border-block-start: var(--ib-bt-lg) var(--ib-bst-now, solid) var(--ib-bc-now, var(--ib-color-border)); }", css);
        Assert.DoesNotContain("var(--ib-bc, var(--ib-color-border))", css);
        Assert.Contains("@property --ib-bc-now { syntax: \"*\"; inherits: false; }", Read("Styles", "_generated", "properties.css"));
    }

    [Fact]
    public void A_null_accent_falls_back_to_the_default()
    {
        var button = Render<Button>(p => p.Add(b => b.Accent, (Accent)null!).AddChildContent("Save")).Find("button");
        Assert.Contains("ib-btn-primary", button.ClassName);
    }

    [Fact]
    public void A_custom_menu_item_color_is_a_class_and_variable_so_disabled_still_wins()
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var item = Render<MenuItem>(p => p.Add(m => m.Accent, Accent.Of("#0d9488")).AddChildContent("Share"));
        var root = item.Find(".ib-menu-item");

        Assert.Contains("ib-menu-item-custom", root.ClassName);
        Assert.Contains("--ib-menu-item-color:", root.GetAttribute("style"));
        Assert.DoesNotContain("color:#", root.GetAttribute("style")!.Replace("--ib-menu-item-color:", ""));

        var css = Read("Styles", "components", "overlay.css");
        Assert.Contains(".ib-menu-item-custom { color: var(--ib-menu-item-color); }", css);
        Assert.Matches(@"\.ib-menu-item-custom \.ib-icon \{\s*color: inherit;", css);
    }

    [Fact]
    public void A_circle_skeleton_sized_by_height_stays_round()
    {
        var circle = Render<Skeleton>(p => p.Add(s => s.Circle, true).Add(s => s.Height, Length.Rem(4))).Find(".ib-skeleton");

        Assert.Contains("ib-skeleton-circle-h", circle.ClassName);
        Assert.Matches(@"\.ib-skeleton-circle-h \{\s*width: auto;", Read("Styles", "components", "feedback.css"));
    }

    [Theory]
    [InlineData("Inter (")]
    [InlineData("Inter, [x")]
    [InlineData("url(x)")]
    public void Font_lists_with_brackets_are_refused(string font) =>
        Assert.Throws<ArgumentException>(() => Render<IzzyTheme>(p => p.Add(t => t.BodyFont, font)));

    [Theory]
    [InlineData("# 12345")]
    [InlineData("# ab")]
    [InlineData("#12 345")]
    public void Hex_colors_with_spaces_inside_are_refused(string hex)
    {
        Assert.Throws<ArgumentException>(() => BrandPalette.From(hex));
        Assert.Throws<ArgumentException>(() => GrayPalette.From(hex));
    }

    [Fact]
    public void Derived_brand_colors_meet_their_ratios_as_written_on_every_built_in_gray()
    {
        string[] lightHigh = ["#dfdfe3", "#dce2ea", "#e0ddd5"];
        string[] darkSurface = ["#141416", "#161a21", "#1d1f22"];
        var random = new Random(7);
        for (var i = 0; i < 1500; i++)
        {
            var hex = $"#{random.Next(0x1000000):x6}";
            var p = BrandPalette.From(hex);

            Assert.True(Ratio(p.OnFill, p.Fill) >= 4.5, $"{hex}: on-fill");
            Assert.True(Ratio(p.DarkOnFill, p.DarkFill) >= 4.5, $"{hex}: dark on-fill");
            Assert.True(Ratio(p.Text, p.Soft) >= 4.5, $"{hex}: text on soft");
            foreach (var gray in lightHigh)
            {
                Assert.True(Ratio(p.Text, gray) >= 4.5, $"{hex}: text on {gray}");
            }

            foreach (var surface in darkSurface)
            {
                Assert.True(Ratio(p.DarkFill, surface) >= 3, $"{hex}: dark fill on {surface}");
            }
        }
    }

    [Theory]
    [InlineData("#f4efe6")]
    [InlineData("#e8e0f0")]
    [InlineData("#e6e6e6")]
    [InlineData("#d9e4dd")]
    [InlineData("#fdf6e3")]
    public void A_hex_gray_keeps_its_fills_as_light_as_the_built_in_sets(string hex)
    {
        var colors = GrayPalette.From(hex).Colors.ToDictionary(c => c.Token, c => c.Light);
        var floors = new Dictionary<string, string[]>
        {
            ["surface-container"] = ["#e9e9ec", "#e7ebf1", "#eae8e2"],
            ["surface-container-high"] = ["#dfdfe3", "#dce2ea", "#e0ddd5"],
        };

        foreach (var (token, builtIn) in floors)
        {
            Assert.True(Luminance(colors[token]) >= builtIn.Min(Luminance) - 0.0005, $"{hex}: {token} {colors[token]}");
        }

        // A color that doesn't follow the grays still reads on the strongest fill.
        Assert.True(Ratio("#5f6670", colors["surface-container"]) >= 4.5, $"{hex}: code comment");
    }

    [Fact]
    public void The_scheme_attribute_beats_the_meta_tag_on_html()
    {
        var tokens = Read("Styles", "tokens.css");
        Assert.Contains("[data-ib-color-scheme][data-ib-color-scheme=\"dark\"],", tokens);
        Assert.Contains("[data-ib-color-scheme][data-ib-color-scheme=\"light\"],", tokens);
    }

    [Fact]
    public void A_preset_on_a_part_resets_depth_and_leaves_fonts_to_the_app()
    {
        var themes = Read("Styles", "themes.css").Replace("\r", "");

        Assert.Matches(@"\[data-ib-preset\]\[data-ib-preset=""standard""\],\s*\[data-ib-preset\]\[data-ib-preset=""editorial""\] \{\s*--ib-shadow-xs:", themes);
        Assert.DoesNotMatch(@"\[data-ib-preset\]\[data-ib-preset=""editorial""\],\s*:root:where\(:has\(> head > meta\[name=""ib-preset""\]\[content=""editorial""\]\)\) \{\s*--ib-font-body", themes);
        Assert.Matches(@":root\[data-ib-preset=""editorial""\],\s*:root:where\(:has\(> head > meta\[name=""ib-preset""\]\[content=""editorial""\]\)\) \{\s*--ib-font-body", themes);
    }

    private static string Read(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "izzyblazor.sln")))
        {
            dir = dir.Parent;
        }

        return File.ReadAllText(Path.Combine([dir!.FullName, "src", "IzzyBlazor.Core", .. parts])).Replace("\r", "");
    }

    private static double Luminance(string hex)
    {
        var v = Convert.ToInt32(hex[1..], 16);
        static double Lin(int c) => c / 255.0 <= 0.04045 ? c / 255.0 / 12.92 : Math.Pow((c / 255.0 + 0.055) / 1.055, 2.4);
        return 0.2126 * Lin((v >> 16) & 0xFF) + 0.7152 * Lin((v >> 8) & 0xFF) + 0.0722 * Lin(v & 0xFF);
    }

    private static double Ratio(string a, string b)
    {
        var (la, lb) = (Luminance(a), Luminance(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }
}
