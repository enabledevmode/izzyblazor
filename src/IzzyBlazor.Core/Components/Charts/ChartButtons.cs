using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// What a <see cref="Tile"/> or <see cref="Card"/> tells the chart in it: which of its buttons
/// (<c>reset</c>, <c>zoom</c>, <c>pause</c>) are in its header, as ChartResetZoom, ChartZoom and
/// ChartPause with no <c>For</c>. The chart hides its own of those (they stay, as what a press is
/// passed to) and, when none of its own are left to show, its row of buttons.
/// </summary>
internal sealed class ChartButtonHost
{
    private readonly HashSet<string> _moved = [];

    /// <summary>
    /// The id of the tile's or card's title, which the buttons add to their names ("Download as
    /// PNG, Revenue"), so two charts' buttons on a page are told apart. Null without a title.
    /// </summary>
    public string? TitleId { get; set; }
    private readonly List<Action> _charts = [];

    public bool Moved(string kind) => _moved.Contains(kind);

    public void Add(string kind)
    {
        if (_moved.Add(kind))
        {
            // A button drawn after the chart (in a Card's actions, or a later render): it draws again.
            foreach (var render in _charts.ToArray())
            {
                render();
            }
        }
    }

    public void Listen(Action render) => _charts.Add(render);

    public void Unlisten(Action render) => _charts.Remove(render);
}

/// <summary>
/// A chart's button placed outside the chart, such as in a <see cref="Tile"/>'s <c>HeaderEnd</c>
/// or a <see cref="Card"/>'s <see cref="CardActions"/>. It acts on the chart named by
/// <see cref="For"/>, or else the one chart in its tile or card; there, the chart's own row of
/// buttons is left out. It needs script: with JavaScript off it isn't shown.
/// </summary>
public abstract class ChartButtonBase : IzzyComponentBase
{
    private protected ChartButtonBase()
    {
    }

    /// <summary>
    /// The <c>id</c> of the chart it acts on. Leave it out in a tile or card with one chart: it
    /// acts on that one.
    /// </summary>
    [Parameter] public string? For { get; set; }

    /// <summary>What it does, for the script: <c>download</c>, <c>reset</c>, <c>zoom-out</c>, <c>zoom-in</c> or <c>pause</c>.</summary>
    private protected abstract string Act { get; }

    /// <summary>Which of the chart's own buttons it stands in for: <c>reset</c>, <c>zoom</c> or <c>pause</c>; null for Download.</summary>
    private protected virtual string? Replaces => null;

    [CascadingParameter] private ChartButtonHost? Host { get; set; }

    private readonly string _id = IdGenerator.Next("ib-chart-button");

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (For is null && Replaces is { } kind)
        {
            Host?.Add(kind);
        }
    }

    /// <summary>Opens a button of the chart tools' look, with what the script needs to find its chart.</summary>
    private protected void Open(RenderTreeBuilder builder, string act, string label, bool iconOnly, string? extraClass = null)
    {
        var (cls, _) = BuildRootAttributes();
        builder.OpenElement(0, "button");
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "type", "button");
        builder.AddAttribute(3, "class", new ClassBuilder()
            .Add("ib-btn ib-btn-neutral ib-btn-outline ib-btn-sm")
            .Add("ib-btn-icon-only", iconOnly)
            .Add(extraClass)
            .Add(cls)
            .ToString());
        builder.AddAttribute(4, "style", Style);
        builder.AddAttribute(5, "data-ib-script", true);
        builder.AddAttribute(6, "data-ib-chart-act", act);
        builder.AddAttribute(7, "data-ib-chart-for", For);
        if (iconOnly)
        {
            builder.AddAttribute(8, "aria-label", label);
            builder.AddAttribute(9, "title", label);
        }

        // Its name, then its tile's or card's title: "Download as PNG, Revenue by channel".
        if (For is null && Host?.TitleId is { } title)
        {
            var id = $"{_id}-{act}";
            builder.AddAttribute(25, "id", id);
            builder.AddAttribute(26, "aria-labelledby", $"{id} {title}");
        }
    }
}

/// <summary>Saves a chart as a PNG, from outside the chart: a tile's or card's header.</summary>
/// <example>
/// <code>
/// &lt;Tile Title="Revenue" TitleId="revenue-title"&gt;
///     &lt;HeaderEnd&gt;
///         &lt;ChartDownload /&gt;
///     &lt;/HeaderEnd&gt;
///     &lt;ChildContent&gt;
///         &lt;Chart TitledBy="revenue-title" ... /&gt;
///     &lt;/ChildContent&gt;
/// &lt;/Tile&gt;
/// </code>
/// </example>
[ModeSupport(NoScript = ModeLevel.None)]
public sealed class ChartDownload : ChartButtonBase
{
    private protected override string Act => "download";

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        Open(builder, Act, TextDefaults.ChartDownload, iconOnly: true);
        // Chart's own download handler (chart.ts) takes it: the chart's id, or empty to find it.
        builder.AddAttribute(10, "data-ib-chart-download", For ?? "");
        builder.OpenComponent<Icon>(11);
        builder.AddComponentParameter(12, nameof(Icon.Purpose), IconPurpose.Export);
        builder.CloseComponent();
        builder.CloseElement();
    }
}

/// <summary>
/// Resets a zoomed chart, from outside the chart. It shows only while the chart is zoomed in;
/// the chart needs zooming on.
/// </summary>
/// <example>
/// <code>
/// &lt;HeaderEnd&gt;
///     &lt;ChartResetZoom /&gt;
///     &lt;ChartZoom /&gt;
/// &lt;/HeaderEnd&gt;
/// </code>
/// </example>
[ModeSupport(NoScript = ModeLevel.None)]
public sealed class ChartResetZoom : ChartButtonBase
{
    private protected override string Act => "reset";

    private protected override string? Replaces => "reset";

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        Open(builder, Act, TextDefaults.ChartResetZoom, iconOnly: false);
        builder.AddAttribute(10, "hidden", true);
        builder.AddContent(11, TextDefaults.ChartResetZoom);
        builder.CloseElement();
    }
}

/// <summary>Zooms a chart out and in, from outside the chart: two buttons. The chart needs zooming on.</summary>
/// <example>
/// <code>
/// &lt;HeaderEnd&gt;
///     &lt;ChartZoom /&gt;
/// &lt;/HeaderEnd&gt;
/// </code>
/// </example>
[ModeSupport(NoScript = ModeLevel.None)]
public sealed class ChartZoom : ChartButtonBase
{
    private protected override string Act => "zoom-out";

    private protected override string? Replaces => "zoom";

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        Open(builder, "zoom-out", TextDefaults.ChartZoomOut, iconOnly: true);
        builder.AddAttribute(10, "disabled", true);
        builder.AddAttribute(11, "aria-keyshortcuts", "-");
        builder.OpenComponent<Icon>(12);
        builder.AddComponentParameter(13, nameof(Icon.Name), Lucide.ZoomOut);
        builder.CloseComponent();
        builder.CloseElement();

        Open(builder, "zoom-in", TextDefaults.ChartZoomIn, iconOnly: true);
        builder.AddAttribute(20, "aria-keyshortcuts", "+");
        builder.OpenComponent<Icon>(21);
        builder.AddComponentParameter(22, nameof(Icon.Name), Lucide.ZoomIn);
        builder.CloseComponent();
        builder.CloseElement();
    }
}

/// <summary>
/// Pauses and resumes a live chart's updates, from outside the chart. Pressed, the chart keeps
/// what it shows. The chart needs <c>Refresh</c> (or updates on an interactive page).
/// </summary>
/// <example>
/// <code>
/// &lt;HeaderEnd&gt;
///     &lt;ChartPause /&gt;
/// &lt;/HeaderEnd&gt;
/// </code>
/// </example>
[ModeSupport(NoScript = ModeLevel.None)]
public sealed class ChartPause : ChartButtonBase
{
    private protected override string Act => "pause";

    private protected override string? Replaces => "pause";

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        Open(builder, Act, TextDefaults.ChartPauseUpdates, iconOnly: true, extraClass: "ib-chart-pause");
        builder.AddAttribute(10, "aria-pressed", "false");
        builder.OpenComponent<Icon>(11);
        builder.AddComponentParameter(12, nameof(Icon.Purpose), IconPurpose.Pause);
        builder.AddComponentParameter(13, nameof(Icon.Class), "ib-chart-pause-icon");
        builder.CloseComponent();
        builder.OpenComponent<Icon>(14);
        builder.AddComponentParameter(15, nameof(Icon.Purpose), IconPurpose.Play);
        builder.AddComponentParameter(16, nameof(Icon.Class), "ib-chart-play-icon");
        builder.CloseComponent();
        builder.CloseElement();
    }
}
