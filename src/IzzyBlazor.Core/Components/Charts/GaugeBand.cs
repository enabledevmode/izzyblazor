using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// A range on a <see cref="Gauge"/>'s dial or a <see cref="LinearGauge"/>'s scale, such as where a load turns high:
/// <c>&lt;GaugeBand From="0.7" To="0.9" Accent="Accent.From(Color.Warning)" Label="High" /&gt;</c>.
/// It's drawn along the scale (a thin arc on a dial's outer edge), in its <see cref="Accent"/> or the
/// gauge's palette color for its place; a value inside it fills in that color (not in a gray), and
/// its <see cref="Label"/> shows under the value. In a <see cref="BulletChart{TItem}"/>, a range
/// every row shares.
/// </summary>
public sealed class GaugeBand : ComponentBase, IDisposable
{
    [CascadingParameter] private GaugeContext? Context { get; set; }

    /// <summary>Where the band starts, on the gauge's scale.</summary>
    [Parameter, EditorRequired] public double From { get; set; }

    /// <summary>Where the band ends.</summary>
    [Parameter, EditorRequired] public double To { get; set; }

    /// <summary>
    /// The band's color, such as <c>Accent.From(Color.Warning)</c> or <c>Accent.Danger</c>, in place
    /// of the gauge's <see cref="GaugeBase.Palette"/> color for its place.
    /// </summary>
    [Parameter] public Accent? Accent { get; set; }

    /// <summary>
    /// What a value in the band means, such as "High": shown under the value when it's in the
    /// band, and read with it, so the color isn't the only cue.
    /// </summary>
    [Parameter] public string? Label { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Context is null)
        {
            throw new InvalidOperationException("GaugeBand must be inside a Gauge, a LinearGauge or a BulletChart.");
        }

        Context.Bands.Add(this);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (!double.IsFinite(From) || !double.IsFinite(To) || To <= From)
        {
            throw new ArgumentOutOfRangeException(nameof(To), To, "A GaugeBand's To must be more than its From.");
        }

        Context!.Bands.Seen(this);
        Context.Dial?.Refresh();
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Context?.Bands.Remove(this) == true)
        {
            Context.Dial?.Refresh();
        }
    }
}

/// <summary>What a gauge or <see cref="BulletChart{TItem}"/> shares with its bands and pointers.</summary>
internal sealed class GaugeContext
{
    public ChildOrder<GaugeBand> Bands { get; } = new();

    /// <summary>The pointers, on a gauge; null on a BulletChart, which has none.</summary>
    public ChildOrder<GaugePointer>? Pointers { get; init; }

    public ChartPlot? Dial { get; set; }
}

/// <summary>
/// Another value on a <see cref="Gauge"/> or <see cref="LinearGauge"/>, on the same scale, such as
/// the dew point beside the temperature:
/// <c>&lt;GaugePointer Value="12" Label="Dew point" /&gt;</c>. It's drawn as a mark (a notch
/// across a dial, a triangle on a linear scale) in the next of the chart colors, and written under
/// the gauge with its label: "Dew point 12°". The gauge's own <see cref="GaugeBase.Value"/> stays
/// the one that fills.
/// </summary>
/// <remarks>Each pointer is a meter of its own, named by its label, after the gauge's.</remarks>
public sealed class GaugePointer : ComponentBase, IDisposable
{
    [CascadingParameter] private GaugeContext? Context { get; set; }

    /// <summary>The value, on the gauge's scale and in its format.</summary>
    [Parameter, EditorRequired] public double Value { get; set; }

    /// <summary>What the value is, such as "Dew point": written beside it, and its accessible name. Required.</summary>
    [Parameter, EditorRequired] public string Label { get; set; } = "";

    /// <summary>The mark's color, such as <c>Accent.Of("#0d9488")</c>, in place of the next chart color.</summary>
    [Parameter] public Accent? Accent { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Context?.Pointers is null)
        {
            throw new InvalidOperationException("GaugePointer must be inside a Gauge or a LinearGauge.");
        }

        Context.Pointers.Add(this);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (string.IsNullOrWhiteSpace(Label))
        {
            throw new InvalidOperationException("A GaugePointer needs a Label, what its value is, such as \"Dew point\".");
        }

        Context!.Pointers!.Seen(this);
        Context.Dial?.Refresh();
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Context?.Pointers?.Remove(this) == true)
        {
            Context.Dial?.Refresh();
        }
    }
}
