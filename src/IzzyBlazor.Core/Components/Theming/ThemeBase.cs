using System.ComponentModel;
using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// What <see cref="IzzyTheme"/> (the app's theme) and <see cref="ThemeScope"/> (part of a page)
/// share: the preset, colors, radius, density and color scheme. Not a component of its own.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class ThemeBase : ComponentBase
{
    private protected ThemeBase()
    {
    }

    /// <summary>
    /// The starting set of defaults: density, shape and type, and a default accent and gray, which
    /// the other settings override. Unset: Standard (or, in a ThemeScope, what's around it).
    /// </summary>
    [Parameter] public ThemePreset? Preset { get; set; }

    /// <summary>
    /// The brand color, over the preset's: a hex color (<c>"#0a7d5a"</c>), from which the fill, the text
    /// on it, the accent as text and the soft tint are derived in light and dark, each pair at AA
    /// (see <see cref="BrandPalette"/>); or a built-in, hand-tuned one: <c>blue</c> (the default),
    /// <c>ink</c> (monochrome), <c>indigo</c>, <c>violet</c>, <c>teal</c>, <c>green</c>, <c>rose</c>.
    /// <see cref="Accent.Primary"/> controls are painted with it.
    /// </summary>
    [Parameter] public string? Brand { get; set; }

    /// <summary>
    /// The grays (page, surfaces, fills, borders and text), over the preset's: a built-in set,
    /// <c>neutral</c> (the default), <c>slate</c> (cool) or <c>stone</c> (warm paper). The app's
    /// <see cref="IzzyTheme"/> also takes a hex color for your light page background
    /// (<c>"#f4efe6"</c>), from which the rest is derived with contrast checked (see <see cref="GrayPalette"/>).
    /// </summary>
    [Parameter] public string? Gray { get; set; }

    /// <summary>The radius scale, over the preset's.</summary>
    [Parameter] public ThemeRadius? Radius { get; set; }

    /// <summary>Control heights, table rows and card padding, over the preset's.</summary>
    [Parameter] public ThemeDensity? Density { get; set; }

    /// <summary>
    /// The colors charts, gauges and the scheduler use, unless a chart sets its own
    /// <see cref="IzzyBlazor.ChartPalette"/>. Unset: <see cref="ThemeChartPalette.Brand"/>, which starts at
    /// the brand color (in a ThemeScope, what's around it, or its own <see cref="Brand"/>'s).
    /// </summary>
    [Parameter] public ThemeChartPalette? ChartPalette { get; set; }

    // The fixed palettes are chosen in CSS, by name. Brand's colors are worked out for the brand and written
    // out (BrandChartCss), so it has no name there.
    private protected string? ChartPaletteName => ChartPalette is { } palette and not ThemeChartPalette.Brand ? ChartPaletteNames.Name(palette) : null;

    /// <summary>
    /// The Brand chart palette's declarations for this theme, or null when there are none to write.
    /// <paramref name="app"/>: the app's theme, where a preset brings its brand. It always writes its brand's
    /// palette (<c>--ib-chart-brand-*</c>), and points the charts at it unless it chose a fixed one. Part of a
    /// page with a brand of its own writes that brand's; one that asks for Brand by name takes the app's.
    /// A brand with no color (ink, a gray) has no palette of its own: its charts are Contrast's.
    /// </summary>
    private protected string? BrandChartCss(bool app)
    {
        var brand = ChartPalette is null or ThemeChartPalette.Brand;
        var hex = BrandColor.Hex is { } value ? BrandPalette.From(value) : null;
        var name = BrandColor.Name ?? (app ? BrandChartPalette.BrandOf(Preset) : null);
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
        return ChartPalette is null ? null : BrandChartPalette.Use(orContrast: true);
    }

    /// <summary>The brand color as parsed: a built-in name or a hex color.</summary>
    private protected (string? Name, string? Hex) BrandColor { get; private set; }

    /// <summary>The grays as parsed: a built-in name or a hex color.</summary>
    private protected (string? Name, string? Hex) GrayColor { get; private set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        BrandColor = string.IsNullOrWhiteSpace(Brand) ? default : ThemeColors.Parse(Brand, ThemeColors.Brands, nameof(Brand));
        GrayColor = string.IsNullOrWhiteSpace(Gray) ? default : ThemeColors.Parse(Gray, ThemeColors.Grays, nameof(Gray));
    }

    private protected static string Name<T>(T value) where T : struct, Enum => value.ToString().ToLowerInvariant();

    private protected static string? NameOrNull<T>(T? value) where T : struct, Enum => value is { } v ? Name(v) : null;
}
