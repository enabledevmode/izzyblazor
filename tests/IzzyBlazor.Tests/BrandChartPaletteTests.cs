using Bunit;
using System.Text.RegularExpressions;
using Xunit.Abstractions;

namespace IzzyBlazor.Tests;

// The Brand chart palette: the brand first, then Contrast's colors, for each built-in brand. Checked the way
// the fixed palettes are (ChartPaletteSeparationTests): neighbors of the eight apart, and every pair of a set.
public class BrandChartPaletteTests(ITestOutputHelper output) : BunitContext
{
    public static TheoryData<string> Brands => [.. BrandChartPalette.Brands.Keys];

    [Theory]
    [MemberData(nameof(Brands))]
    public void Each_built_in_brand_keeps_its_chart_colors_apart(string brand)
    {
        var (light, dark) = BrandChartPalette.Brands[brand];
        var (eight, sets) = BrandChartPalette.Pick(light, dark);
        output.WriteLine($"{brand}: {string.Join(" ", eight.Select(t => t ?? "brand"))} | {string.Join(" | ", sets.Select(s => string.Join(" ", s.Select(t => t ?? "brand"))))}");
        var colors = BrandChartPalette.Eight.Concat(BrandChartPalette.Sets.SelectMany(s => s)).ToDictionary(c => c.Token);
        BrandChartPalette.Named Color(string? token) => token is null ? new("brand", light, dark) : colors[token];

        Assert.Equal(8, eight.Distinct().Count());
        for (var i = 0; i + 1 < eight.Length; i++) Apart(Color(eight[i]), Color(eight[i + 1]));
        foreach (var set in sets)
        {
            // A brand that leaves no set with every pair apart (teal and green, of four) takes the first of its eight.
            if (set.SequenceEqual(eight.Take(set.Length))) continue;
            for (var i = 0; i < set.Length; i++)
            {
                for (var j = i + 1; j < set.Length; j++) Apart(Color(set[i]), Color(set[j]));
            }
        }

        void Apart(BrandChartPalette.Named a, BrandChartPalette.Named b)
        {
            var (cvd, normal) = BrandChartPalette.Apart(a, b);
            Assert.True(cvd >= BrandChartPalette.CvdTarget, $"{brand}: {a.Token} and {b.Token} are {cvd:F1} apart for color-blind readers");
            Assert.True(normal >= BrandChartPalette.NormalFloor, $"{brand}: {a.Token} and {b.Token} are {normal:F1} apart");
        }
    }

    // Charts start at the brand: the app's theme writes a brand's colors (tokens.css has the default brand's), a
    // part of the page with its own brand writes its own, and a fixed palette leaves both alone.
    [Fact]
    public void A_theme_gives_its_charts_its_brand()
    {
        string? Style(Action<ComponentParameterCollectionBuilder<IzzyTheme>> set) => Render(set).FindAll("style").FirstOrDefault()?.TextContent;

        var rose = Style(p => p.Add(t => t.Brand, "rose"));
        Assert.Contains("--ib-chart-1: var(--ib-color-primary); --ib-chart-2: var(--ib-chart-contrast-2); --ib-chart-3: var(--ib-chart-contrast-1);", rose);
        Assert.Contains("--ib-chart-", Style(p => p.Add(t => t.Preset, ThemePreset.Soft)));
        Assert.DoesNotContain("--ib-chart-", Style(p => p.Add(t => t.Preset, ThemePreset.Compact)) ?? "");
        Assert.DoesNotContain("--ib-chart-", Style(p => p.Add(t => t.Brand, "rose").Add(t => t.ChartPalette, ThemeChartPalette.Calm)) ?? "");
        // A brand too light to show on white is drawn in its text color there.
        Assert.Matches(@"--ib-chart-1: light-dark\(#[0-9a-f]{6}, #[0-9a-f]{6}\);", Style(p => p.Add(t => t.Brand, "#fde047")));

        var part = Render<ThemeScope>(p => p.Add(t => t.Brand, "indigo").AddChildContent("<p>Chart</p>")).Find("div");
        Assert.Contains("--ib-chart-1: var(--ib-color-primary);", part.GetAttribute("style"));
    }

    // The colors the palette is worked out from are the stylesheets' own, and tokens.css holds the default brand's.
    [Fact]
    public void Its_colors_and_its_default_are_the_stylesheets()
    {
        var styles = Path.Combine(RepoRoot(), "src", "IzzyBlazor.Core", "Styles");
        var tokens = File.ReadAllText(Path.Combine(styles, "tokens.css"));
        foreach (var color in BrandChartPalette.Eight.Concat(BrandChartPalette.Sets.SelectMany(s => s)))
        {
            Assert.Contains($"--ib-chart-{color.Token}: light-dark({color.Light}, {color.Dark});", tokens);
        }

        var themes = File.ReadAllText(Path.Combine(styles, "themes.css"));
        foreach (var (name, (light, dark)) in BrandChartPalette.Brands.Where(b => b.Key != "ink"))
        {
            Assert.Matches($@"data-ib-brand=""{name}""[^}}]*--ib-color-primary: light-dark\({light}, {dark}\);", themes);
        }

        var declared = Regex.Matches(tokens, @"--ib-chart-(?:\d|of\d-\d): [^;]+;").Select(m => m.Value);
        Assert.Equal(BrandChartPalette.Default, string.Join(" ", declared));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "izzyblazor.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("izzyblazor.sln not found.");
    }
}
