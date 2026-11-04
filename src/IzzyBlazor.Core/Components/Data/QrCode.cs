using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// A QR code of <see cref="Value"/>, drawn on the server as an SVG: a link, an authenticator app's
/// setup key, a ticket number. It needs no script and scales without blurring.
/// </summary>
/// <example>
/// <code>
/// &lt;QrCode Value="https://example.com/tickets/4417" Label="QR code for ticket 4417" /&gt;
/// &lt;QrCode Value="@AuthenticatorUri" Label="QR code for your authenticator app" Size="12.Rem" /&gt;
/// </code>
/// </example>
/// <accessibility>
/// <list type="bullet">
/// <item>It's an image (<c>role="img"</c>) named by <see cref="Label"/>, which is required; say what scanning it does, not what it holds.</item>
/// <item>The value is never read out: a link read letter by letter helps no one, and an authenticator's holds its secret. Put what someone without a camera needs, such as the link or the setup key, in text beside it.</item>
/// <item>Set <see cref="Decorative"/> when the text beside it already says what it is, so it's skipped.</item>
/// <item>It's dark on light in both color schemes, since not every scanner reads light on dark.</item>
/// </list>
/// </accessibility>
/// <seealso cref="Image"/>
[ModeSupport]
public class QrCode : LayoutItemBase
{
    /// <summary>The text to encode, such as a URL. Longer text makes a denser code.</summary>
    [Parameter, EditorRequired] public string Value { get; set; } = "";

    /// <summary>What scanning it does, for screen readers, such as "QR code for your authenticator app".</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Hides it from screen readers, for when the text beside it says what it is.</summary>
    [Parameter] public bool Decorative { get; set; }

    /// <summary>
    /// How much of the code can be damaged and still scan. Defaults to <see cref="QrErrorCorrection.Medium"/>;
    /// a higher level is used when it fits in the same size.
    /// </summary>
    [Parameter] public QrErrorCorrection ErrorCorrection { get; set; } = QrErrorCorrection.Medium;

    /// <summary>The width and height, quiet zone included. Defaults to 10rem.</summary>
    [Parameter] public Length? Size { get; set; }

    /// <summary>The light border around the code, in modules. Defaults to 4, which scanners expect.</summary>
    [Parameter] public int QuietZone { get; set; } = 4;

    private QrMatrix? _matrix;
    private string? _encoded;
    private QrErrorCorrection _encodedLevel;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (!Decorative && string.IsNullOrWhiteSpace(Label))
        {
            throw new InvalidOperationException("QrCode needs a Label, its accessible name, such as \"QR code for your authenticator app\"; or Decorative when the text beside it says what it is.");
        }

        if (QuietZone < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(QuietZone), QuietZone, "QrCode's QuietZone can't be negative.");
        }

        if (_matrix is null || _encoded != Value || _encodedLevel != ErrorCorrection)
        {
            _matrix = QrEncoder.Encode(Value ?? "", ErrorCorrection);
            _encoded = Value;
            _encodedLevel = ErrorCorrection;
        }
    }

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        classes.Add("ib-qr");
        base.BuildAttributes(classes, styles);
        if (Size is { } size)
        {
            styles.AddVar("qr-size", size.Css);
        }
    }

    /// <summary>The dark modules as one path, a rectangle per run of dark modules in a row.</summary>
    internal static string PathOf(QrMatrix matrix, int margin)
    {
        var d = new StringBuilder();
        for (var y = 0; y < matrix.Size; y++)
        {
            for (var x = 0; x < matrix.Size; x++)
            {
                if (!matrix[x, y])
                {
                    continue;
                }

                var start = x;
                while (x + 1 < matrix.Size && matrix[x + 1, y])
                {
                    x++;
                }

                var run = x - start + 1;
                d.Append(CultureInfo.InvariantCulture, $"M{start + margin} {y + margin}h{run}v1h-{run}z");
            }
        }

        return d.ToString();
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var (cls, style) = BuildRootAttributes();
        var matrix = _matrix!;
        var side = (matrix.Size + QuietZone * 2).ToString(CultureInfo.InvariantCulture);

        builder.OpenElement(0, "svg");
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "class", cls);
        builder.AddAttribute(3, "style", style);
        builder.AddAttribute(4, "viewBox", $"0 0 {side} {side}");
        builder.AddAttribute(5, "shape-rendering", "crispEdges");
        if (Decorative)
        {
            builder.AddAttribute(6, "aria-hidden", "true");
        }
        else
        {
            builder.AddAttribute(7, "role", "img");
            builder.AddAttribute(8, "aria-label", Label);
        }

        builder.OpenElement(9, "rect");
        builder.AddAttribute(10, "class", "ib-scan-light");
        builder.AddAttribute(11, "width", side);
        builder.AddAttribute(12, "height", side);
        builder.CloseElement();
        builder.OpenElement(13, "path");
        builder.AddAttribute(14, "class", "ib-scan-dark");
        builder.AddAttribute(15, "d", PathOf(matrix, QuietZone));
        builder.CloseElement();
        builder.CloseElement();
    }
}
