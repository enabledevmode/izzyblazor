using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// A barcode of <see cref="Value"/>, rendered on the server as an SVG with its text under the bars:
/// Code 128 for any ASCII text, such as a shipping number, or EAN-13, EAN-8 and UPC-A for products.
/// It works with JavaScript off and scales without blurring. With no value yet, such as while a page
/// loads it, nothing renders.
/// </summary>
/// <example>
/// <code>
/// &lt;Barcode Value="SHIP-2026-0417" /&gt;
/// &lt;Barcode Value="400638133393" Type="BarcodeType.Ean13" /&gt;
/// </code>
/// </example>
/// <accessibility>
/// <list type="bullet">
/// <item>It's an image (<c>role="img"</c>) named "Barcode" and its value (<c>IzzyText.BarcodeLabel</c>), as the value is printed under it anyway; <see cref="Label"/> replaces "Barcode", and the value still follows.</item>
/// <item>The text under the bars is hidden from screen readers, as the name says it.</item>
/// <item>Set <see cref="Decorative"/> when the value is written beside it, so it's skipped.</item>
/// <item>It's dark on light in both color schemes, as scanners need.</item>
/// </list>
/// </accessibility>
/// <seealso cref="QrCode"/>
[ModeSupport]
public class Barcode : LayoutItemBase
{
    /// <summary>
    /// What to encode: any ASCII text for <see cref="BarcodeType.Code128"/>; the digits for EAN and UPC,
    /// with or without the check digit (added when it's left off, checked when it's there). Empty renders
    /// nothing; a value the type can't encode throws an <see cref="ArgumentException"/>.
    /// </summary>
    [Parameter, EditorRequired] public string Value { get; set; } = "";

    /// <summary>The kind of barcode. Defaults to <see cref="BarcodeType.Code128"/>.</summary>
    [Parameter] public BarcodeType Type { get; set; }

    /// <summary>The width of the narrowest bar; the barcode is as wide as its bars need. Defaults to 2px; for print, <c>Length.Of("0.33mm")</c>.</summary>
    [Parameter] public Length? ModuleWidth { get; set; }

    /// <summary>The bars' height, in widths of the narrowest bar, so the barcode scales as one. Defaults to 50; a full-height EAN-13 is about 69.</summary>
    [Parameter] public int BarHeight { get; set; } = 50;

    /// <summary>The value under the bars. On by default.</summary>
    [Parameter] public bool ShowText { get; set; } = true;

    /// <summary>What the barcode is, for screen readers, such as "Tracking number": the name becomes it and the value, "Tracking number, SHIP-2026-0417".</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Hides it from screen readers, for when the value is written beside it.</summary>
    [Parameter] public bool Decorative { get; set; }

    private const int FontSize = 9;

    private BarcodeSymbol? _symbol;
    private (string, BarcodeType)? _encoded;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (BarHeight < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(BarHeight), BarHeight, "Barcode's BarHeight must be at least 1.");
        }

        if (string.IsNullOrEmpty(Value))
        {
            _symbol = null;
            _encoded = null;
        }
        else if (_encoded != (Value, Type))
        {
            _symbol = BarcodeEncoder.Encode(Value, Type);
            _encoded = (Value, Type);
        }
    }

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        classes.Add("ib-barcode");
        base.BuildAttributes(classes, styles);
        styles.AddVar("barcode-modules", (_symbol?.Width ?? 0).ToString(CultureInfo.InvariantCulture));
        if (ModuleWidth is { } width)
        {
            styles.AddVar("barcode-module", width.Css);
        }
    }

    /// <summary>The bars as one path, the guards that run into the text taller by <paramref name="extra"/>.</summary>
    internal static string PathOf(BarcodeSymbol symbol, int height, int extra)
    {
        bool Long(int x) => symbol.Extended.Any(e => x >= e.Start && x < e.Start + e.Length);
        var d = new StringBuilder();
        var modules = symbol.Modules;
        for (var x = 0; x < modules.Length; x++)
        {
            if (!modules[x])
            {
                continue;
            }

            var tall = Long(x);
            var start = x;
            while (x + 1 < modules.Length && modules[x + 1] && Long(x + 1) == tall)
            {
                x++;
            }

            var run = x - start + 1;
            d.Append(CultureInfo.InvariantCulture, $"M{start + symbol.QuietLeft} 0h{run}v{height + (tall ? extra : 0)}h-{run}z");
        }

        return d.ToString();
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        // Nothing to draw yet, such as a value still loading.
        if (_symbol is not { } symbol)
        {
            return;
        }

        var (cls, style) = BuildRootAttributes();
        var textBand = ShowText ? FontSize + 3 : 0;
        var height = BarHeight + textBand;
        string N(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        builder.OpenElement(0, "svg");
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "class", cls);
        builder.AddAttribute(3, "style", style);
        builder.AddAttribute(4, "viewBox", $"0 0 {symbol.Width} {height}");
        if (Decorative)
        {
            builder.AddAttribute(5, "aria-hidden", "true");
        }
        else
        {
            builder.AddAttribute(6, "role", "img");
            var value = string.Concat(symbol.Texts.Select(t => t.Text));
            builder.AddAttribute(7, "aria-label", Label is null ? string.Format(CultureInfo.CurrentCulture, TextDefaults.BarcodeLabel, value) : $"{Label}, {value}");
        }

        builder.OpenElement(8, "rect");
        builder.AddAttribute(9, "class", "ib-scan-light");
        builder.AddAttribute(10, "width", symbol.Width);
        builder.AddAttribute(11, "height", height);
        builder.CloseElement();
        builder.OpenElement(12, "path");
        builder.AddAttribute(13, "class", "ib-scan-dark");
        builder.AddAttribute(14, "shape-rendering", "crispEdges");
        builder.AddAttribute(15, "d", PathOf(symbol, BarHeight, ShowText ? FontSize / 2 + 1 : 0));
        builder.CloseElement();
        if (ShowText)
        {
            builder.OpenElement(16, "g");
            builder.AddAttribute(17, "class", "ib-scan-dark ib-barcode-text");
            builder.AddAttribute(18, "aria-hidden", "true");
            builder.AddAttribute(19, "font-size", FontSize);
            builder.AddAttribute(20, "text-anchor", "middle");
            foreach (var (text, center) in symbol.Texts)
            {
                builder.OpenElement(21, "text");
                builder.AddAttribute(22, "x", N(center));
                builder.AddAttribute(23, "y", N(BarHeight + textBand - 2));
                builder.AddContent(24, text);
                builder.CloseElement();
            }

            builder.CloseElement();
        }

        builder.CloseElement();
    }
}
