using System.Globalization;

namespace IzzyBlazor;

/// <summary>
/// What a theme writes into the page: the meta tags the theme CSS matches, and the style block for what a
/// stylesheet can't work out (a hex brand's or gray's palette, the charts' brand colors, fonts). IzzyTheme
/// writes these into <c>&lt;head&gt;</c>; izzy theme vars gives the same, for a preview with no app running
/// (the izzy CLI compiles this file and the palettes it uses).
/// </summary>
internal static class ThemeCss
{
    /// <summary>The app theme's meta tags, by name (<c>ib-preset</c>, <c>ib-brand</c>, …), in the order written.</summary>
    public static List<(string Name, string Content)> Meta(
        ThemePreset? preset, string? brandName, string? grayName, ThemeRadius? radius, ThemeDensity? density,
        ThemeChartPalette? chartPalette, ThemeScrollbars? scrollbars, ColorScheme? scheme)
    {
        var meta = new List<(string, string)>();
        if (preset is { } p) meta.Add(("ib-preset", Name(p)));
        if (brandName is not null) meta.Add(("ib-brand", brandName));
        if (grayName is not null) meta.Add(("ib-gray", grayName));
        if (radius is { } r) meta.Add(("ib-radius", Name(r)));
        if (density is { } d) meta.Add(("ib-density", Name(d)));
        // The fixed palettes are chosen in CSS, by name. Brand's colors are worked out and written out.
        if (chartPalette is { } chart and not ThemeChartPalette.Brand) meta.Add(("ib-chart-palette", ChartPaletteNames.Name(chart)));
        if (scrollbars is { } s and not ThemeScrollbars.Auto) meta.Add(("ib-scrollbars", Name(s)));
        if (scheme is ColorScheme.Light or ColorScheme.Dark) meta.Add(("ib-color-scheme", ColorSchemes.Name(scheme.Value)));
        return meta;
    }

    /// <summary>The app theme's style block, or null when there is nothing to write.</summary>
    public static string? App(
        ThemePreset? preset, (string? Name, string? Hex) brand, (string? Name, string? Hex) gray, ThemeChartPalette? chartPalette,
        string? bodyFont, string? headingFont, string? monoFont, int? headingWeight)
    {
        var declarations = new List<string>();
        if (brand.Hex is { } brandHex) declarations.Add(BrandPalette.From(brandHex).ToCss());
        if (gray.Hex is { } grayHex) declarations.Add(GrayPalette.From(grayHex).ToCss());

        // The charts' colors for this brand: a stylesheet can't work them out, so tokens.css's own are Contrast.
        if (BrandCharts(brand, preset, chartPalette, app: true) is { } charts) declarations.Add(charts);

        if (!string.IsNullOrWhiteSpace(bodyFont)) declarations.Add($"--ib-font-body: {FontList(bodyFont, "BodyFont")};");
        if (!string.IsNullOrWhiteSpace(headingFont)) declarations.Add($"--ib-font-heading: {FontList(headingFont, "HeadingFont")};");
        if (!string.IsNullOrWhiteSpace(monoFont)) declarations.Add($"--ib-font-mono: {FontList(monoFont, "MonoFont")};");
        if (headingWeight is { } weight)
        {
            if (weight is < 100 or > 900)
            {
                throw new ArgumentOutOfRangeException("HeadingWeight", weight, "HeadingWeight is a CSS font weight, 100 to 900.");
            }

            declarations.Add(string.Create(CultureInfo.InvariantCulture, $"--ib-weight-heading: {weight};"));
        }

        // In the tokens layer like the library's own values, and written as :root:root:root so it
        // beats a preset's or an accent's values on <html> whatever order the stylesheets load in.
        return declarations.Count == 0 ? null : "@layer izzy.tokens { :root:root:root { " + string.Join(" ", declarations) + " } }";
    }

    /// <summary>
    /// The Brand chart palette's declarations for a theme, or null when there are none to write.
    /// <paramref name="app"/>: the app's theme, where a preset brings its brand. It always writes its brand's
    /// palette (<c>--ib-chart-brand-*</c>), and points the charts at it unless it chose a fixed one. Part of a
    /// page with a brand of its own writes that brand's; one that asks for Brand by name takes the app's.
    /// A brand with no color (ink, a gray) has no palette of its own: its charts are Contrast's.
    /// </summary>
    public static string? BrandCharts((string? Name, string? Hex) brandColor, ThemePreset? preset, ThemeChartPalette? chartPalette, bool app)
    {
        var brand = chartPalette is null or ThemeChartPalette.Brand;
        var hex = brandColor.Hex is { } value ? BrandPalette.From(value) : null;
        var name = brandColor.Name ?? (app ? BrandChartPalette.BrandOf(preset) : null);
        var colorless = hex is not null ? BrandChartPalette.Colorless(hex) : name is not null && BrandChartPalette.Colorless(name);
        if (app)
        {
            // tokens.css's own are Contrast.
            if (colorless) return null;
            var palette = hex is not null ? BrandChartPalette.Css(hex, app: true) : BrandChartPalette.Css(name!, app: true);
            return brand ? $"{palette} {BrandChartPalette.Use(orContrast: false)}" : palette;
        }

        if (!brand) return null;
        if (colorless) return BrandChartPalette.Contrast;
        if (hex is not null) return BrandChartPalette.Css(hex);
        if (name is not null) return BrandChartPalette.Css(name);
        return chartPalette is null ? null : BrandChartPalette.Use(orContrast: true);
    }

    public static string Name<T>(T value) where T : struct, Enum => value.ToString().ToLowerInvariant();

    // A font-family list goes into a style block as is, so it may not close the declaration,
    // the rule or the element.
    private static string FontList(string value, string parameter)
    {
        // No way out of the declaration or the style element, and no open string or block: an
        // unclosed quote, bracket or parenthesis (or a newline, which ends a CSS string) would
        // swallow every declaration after it. A font-family list needs none of ( ) [ ].
        var list = value.Trim();
        if (list.IndexOfAny([';', '{', '}', '<', '>', '\\', '(', ')', '[', ']']) >= 0 || list.Contains("/*", StringComparison.Ordinal)
            || list.Any(char.IsControl) || !QuotesClose(list))
        {
            throw new ArgumentException($"{parameter} is a CSS font-family list, such as \"Inter, system-ui, sans-serif\".", parameter);
        }

        return list;
    }

    // Every quoted family name ends with the quote it started with.
    private static bool QuotesClose(string list)
    {
        char? open = null;
        foreach (var c in list)
        {
            if (open is null && c is '\'' or '"')
            {
                open = c;
            }
            else if (c == open)
            {
                open = null;
            }
        }

        return open is null;
    }
}
