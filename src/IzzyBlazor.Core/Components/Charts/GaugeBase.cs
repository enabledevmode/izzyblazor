using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// What <see cref="Gauge"/> and <see cref="LinearGauge"/> share: a value on a scale from
/// <see cref="Min"/> to <see cref="Max"/>, named by <see cref="Label"/>, with
/// <see cref="GaugeBand"/>s, a <see cref="Target"/> and <see cref="GaugePointer"/>s.
/// </summary>
public abstract class GaugeBase : LayoutItemBase, IDisposable
{
    /// <summary>
    /// How often the gauge reads its data again, at least a second, such as
    /// <c>Refresh="TimeSpan.FromSeconds(30)"</c>. On a static page the page (or
    /// <see cref="RefreshFrom"/>) is fetched again and the gauge drawn from it, found by its
    /// <c>id</c>, which it then needs; on an interactive page <see cref="OnRefresh"/> is raised, for
    /// you to load new data. Not while the tab is hidden or the gauge is off-screen, nor (static)
    /// while the pointer or focus is in it. Unlike Chart's, it has no Pause button.
    /// </summary>
    [Parameter, ModeSupport(NoScript = ModeLevel.None)]
    public TimeSpan? Refresh { get; set; }

    /// <summary>On a static page with <see cref="Refresh"/>: a lighter page to fetch than this one, which renders the same gauge with the same <c>id</c>.</summary>
    [Parameter, ModeSupport(NoScript = ModeLevel.None)]
    public string? RefreshFrom { get; set; }

    /// <summary>On an interactive page with <see cref="Refresh"/>: raised at each interval, to load new data.</summary>
    [Parameter, ModeSupport(Static = ModeLevel.None, NoScript = ModeLevel.None)]
    public EventCallback OnRefresh { get; set; }

    /// <summary>The value. One outside <see cref="Min"/> and <see cref="Max"/> fills none or all of the scale, and still shows as it is.</summary>
    [Parameter, EditorRequired] public double Value { get; set; }

    /// <summary>The scale's start. Defaults to 0.</summary>
    [Parameter] public double Min { get; set; }

    /// <summary>The scale's end. Defaults to 1, for a share shown with <c>ValueFormat="P0"</c>.</summary>
    [Parameter] public double Max { get; set; } = 1;

    /// <summary>What it measures, such as "Disk used": written with the value, and its accessible name. Required.</summary>
    [Parameter, EditorRequired] public string Label { get; set; } = "";

    /// <summary>Hides the label, which still names the gauge for screen readers.</summary>
    [Parameter] public bool HideLabel { get; set; }

    /// <summary>A .NET format string for the value, the scale's numbers, the target and the pointers, such as <c>P0</c>, <c>N0</c> or <c>0 mb</c>.</summary>
    [Parameter] public string? ValueFormat { get; set; }

    /// <summary>A goal to read the value against, such as <c>0.8</c>: a tick across the track, read with the value.</summary>
    [Parameter] public double? Target { get; set; }

    /// <summary>
    /// The value's color, such as <c>Accent.Of("#0d9488")</c>. Defaults to the color of the
    /// <see cref="GaugeBand"/> the value is in, or the primary color.
    /// </summary>
    [Parameter] public Accent? Accent { get; set; }

    /// <summary>
    /// The bands' colors, in the order they're written, such as
    /// <c>ChartPalette.Of(Color.Warning, Color.Danger)</c>, or <c>ChartPalette.Tints(Color.Warning)</c>
    /// for a light tint up to the full color (for a thick track's bands).
    /// Defaults to grays, darkest first, which mark the ranges without saying good or bad; a value
    /// in a gray band keeps <see cref="Accent"/> or the primary color. A band's own
    /// <see cref="GaugeBand.Accent"/> takes the place of its palette color.
    /// </summary>
    [Parameter] public ChartPalette? Palette { get; set; }

    /// <summary>
    /// The <see cref="GaugeBand"/>s, ranges along the scale such as where a load turns high, and
    /// <see cref="GaugePointer"/>s, more values on the same scale.
    /// </summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    private readonly ChartRefresh _refresh = new();

    internal GaugeContext Context { get; } = new() { Pointers = new() };

    /// <summary>The value, a finite number.</summary>
    private protected double Shown { get; private set; }

    /// <summary>The value, formatted.</summary>
    private protected string ShownText { get; private set; } = "";

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        base.OnAfterRender(firstRender);
        _refresh.AfterRender(Refresh, RendererInfo, OnRefresh, InvokeAsync, DispatchExceptionAsync);
    }

    /// <inheritdoc />
    public void Dispose() => _refresh.Dispose();

    /// <summary>Refresh on a static page: the element whose script fetches the gauge again (chart-live.ts).</summary>
    private protected RenderFragment RefreshScript => builder =>
    {
        if (_refresh.Every is null) return;
        builder.OpenElement(0, "ib-chart");
        builder.AddAttribute(1, "data-ib-refresh", _refresh.Every);
        builder.AddAttribute(2, "data-ib-refresh-from", _refresh.From);
        builder.AddAttribute(3, "style", "display: none");
        builder.CloseElement();
    };

    /// <summary>The plot, rendered after the bands and pointers inside the gauge have said they're there.</summary>
    private protected RenderFragment Plot(RenderFragment content) => builder =>
    {
        builder.OpenComponent<ChartPlot>(0);
        builder.AddComponentParameter(1, nameof(ChartPlot.Content), content);
        builder.AddComponentParameter(2, nameof(ChartPlot.Attach), (Action<ChartPlot>)(plot => Context.Dial = plot));
        builder.CloseComponent();
    };

    private protected static string Num(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);

    private protected string Text(double value) => value.ToString(ValueFormat ?? "#,##0.##", CultureInfo.CurrentCulture);

    /// <summary>Where a value sits on the scale, 0 to 1.</summary>
    private protected double Share(double value) => Math.Clamp((value - Min) / (Max - Min), 0, 1);

    /// <summary>A band's color: its own Accent, else the palette's color for its place, else a gray.</summary>
    private protected string BandColor(GaugeBand band, IReadOnlyList<GaugeBand> bands)
    {
        if (band.Accent is { } accent) return StyleValues.Color(accent.ToColor());
        var i = 0;
        while (i < bands.Count && !ReferenceEquals(bands[i], band)) i++;
        return Palette is { } palette ? palette.Color(i < bands.Count ? i : 0, bands.Count) : ChartPalette.Gray(i < bands.Count ? i : 0, bands.Count);
    }

    /// <summary>The band a value is in: the last that holds it, so a later band wins where two meet.</summary>
    private protected static GaugeBand? BandOf(double value, IReadOnlyList<GaugeBand> bands) =>
        bands.LastOrDefault(b => value >= b.From && value <= b.To);

    /// <summary>
    /// The value's color when its band colors it (a palette or an accent of its own), and the gauge
    /// has no Accent; else null, for the gauge's color.
    /// </summary>
    private protected string? ValueColor(GaugeBand? band, IReadOnlyList<GaugeBand> bands) =>
        Accent is null && band is not null && (band.Accent is not null || Palette is not null) ? BandColor(band, bands) : null;

    /// <summary>The value in words: "72%, High, target 80%".</summary>
    private protected string Words(GaugeBand? band)
    {
        var target = Target is { } t ? string.Format(CultureInfo.CurrentCulture, TextDefaults.GaugeTarget, Text(t)) : null;
        return string.Join(", ", new[] { ShownText, band?.Label, target }.Where(w => !string.IsNullOrEmpty(w)));
    }

    /// <summary>A pointer's color: its own Accent, else the chart colors after the first (the value's).</summary>
    private protected static string PointerColor(GaugePointer pointer, int i) =>
        pointer.Accent is { } accent ? StyleValues.Color(accent.ToColor()) : $"var(--ib-chart-{(i + 1) % 8 + 1})";

    /// <summary>The pointers' readouts: each a meter of its own, "Dew point 12°" with its mark's color.</summary>
    private protected RenderFragment Readouts(IReadOnlyList<GaugePointer> pointers) => builder =>
    {
        if (pointers.Count == 0) return;
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "ib-gauge-readouts");
        for (var i = 0; i < pointers.Count; i++)
        {
            var p = pointers[i];
            var value = double.IsFinite(p.Value) ? p.Value : Min;
            var text = Text(value);
            builder.OpenElement(2, "span");
            builder.SetKey(p);
            builder.AddAttribute(3, "class", "ib-gauge-readout");
            builder.AddAttribute(4, "style", $"--ib-gauge-pointer:{PointerColor(p, i)}");
            builder.AddAttribute(5, "role", "meter");
            builder.AddAttribute(6, "aria-label", p.Label);
            builder.AddAttribute(7, "aria-valuenow", Num(Math.Clamp(value, Min, Max)));
            builder.AddAttribute(8, "aria-valuemin", Num(Min));
            builder.AddAttribute(9, "aria-valuemax", Num(Max));
            builder.AddAttribute(10, "aria-valuetext", text);
            builder.AddContent(11, p.Label);
            builder.OpenElement(12, "b");
            builder.AddContent(13, text);
            builder.CloseElement();
            builder.CloseElement();
        }

        builder.CloseElement();
    };

    /// <summary>A pointer's value, a finite number.</summary>
    private protected double PointerValue(GaugePointer pointer) => double.IsFinite(pointer.Value) ? pointer.Value : Min;

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        if (Accent is { } accent)
        {
            styles.AddVar("gauge-color", StyleValues.Color(accent.ToColor()));
        }

        base.BuildAttributes(classes, styles);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        var name = GetType().Name;
        _refresh.Check(name, Refresh, RefreshFrom, RendererInfo, AssignedRenderMode, AdditionalAttributes, OnRefresh.HasDelegate);
        if (string.IsNullOrWhiteSpace(Label))
        {
            throw new InvalidOperationException($"{name} needs a Label, what it measures, such as \"Disk used\".");
        }

        if (!(Max > Min))
        {
            throw new ArgumentOutOfRangeException(nameof(Max), Max, $"A {name}'s Max must be more than its Min.");
        }

        Shown = double.IsFinite(Value) ? Value : Min;
        ShownText = Text(Shown);
        Context.Bands.BeginPass();
        Context.Pointers!.BeginPass();
        base.OnParametersSet();
    }
}
