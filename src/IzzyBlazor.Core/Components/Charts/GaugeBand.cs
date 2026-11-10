using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// A range on a <see cref="Gauge"/>'s dial, such as where a load turns high:
/// <c>&lt;GaugeBand From="0.7" To="0.9" Accent="Accent.From(Color.Warning)" Label="High" /&gt;</c>.
/// It's drawn as a thin arc along the dial's outer edge, in its <see cref="Accent"/> or the
/// gauge's palette color for its place; a value inside it fills in that color (not in a gray), and
/// its <see cref="Label"/> shows under the value.
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
    /// of the gauge's <see cref="Gauge.Palette"/> color for its place.
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
            throw new InvalidOperationException("GaugeBand must be inside a Gauge.");
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

/// <summary>What a <see cref="Gauge"/> shares with its bands.</summary>
internal sealed class GaugeContext
{
    public ChildOrder<GaugeBand> Bands { get; } = new();

    public ChartPlot? Dial { get; set; }
}
