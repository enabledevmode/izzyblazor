using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// A vertical list of events, such as a commit log, an order's history or a changelog: each
/// <see cref="TimelineItem"/> has a marker on a rail, a time, a title and any content under it (a
/// description, a link, actions). For steps in an order, such as a delivery or a build, mark the
/// finished ones <see cref="TimelineItem.Done"/>: their markers fill and the rail runs solid through
/// them, and the steps still to come stay dim.
/// </summary>
/// <accessibility>
/// <list type="bullet">
/// <item>It renders an ordered list: an <c>ol</c> with <c>role="list"</c> (which Safari otherwise drops from a list with no markers), with each <see cref="TimelineItem"/> an <c>li</c>, in the order written.</item>
/// <item><see cref="TimelineItem.Current"/> sets <c>aria-current="true"</c> on its item; <see cref="TimelineItem.Done"/> adds hidden text after the title ("(completed)", <see cref="IzzyText.Completed"/>).</item>
/// <item>The marker and its icon or avatar are hidden, and its <see cref="TimelineItem.Status"/> color isn't read: put a status that matters in the title, the badge or the content.</item>
/// <item>With <see cref="TimelineItem.Href"/> the title is a link; with <see cref="TimelineItem.DateTime"/> or <see cref="TimelineItem.Value"/> the time is a <c>time</c> element. The time is read before the title, wherever it's placed.</item>
/// </list>
/// </accessibility>
/// <seealso cref="Stepper"/>
/// <seealso cref="List"/>
[ModeSupport]
public class Timeline : Box
{
    /// <summary>Creates a timeline, rendered as an ordered list.</summary>
    public Timeline() => As = "ol";

    /// <summary>
    /// Where each item's time goes: <see cref="TimelinePlacement.Above"/> the title (the default),
    /// <see cref="TimelinePlacement.Below"/> the content, in a column at the
    /// <see cref="TimelinePlacement.Start"/>, or at the <see cref="TimelinePlacement.End"/> of the title's line.
    /// </summary>
    [Parameter] public TimelinePlacement TimePlacement { get; set; }

    /// <summary>
    /// The plain markers: a small <see cref="TimelineMarker.Dot"/> (the default) or a larger
    /// <see cref="TimelineMarker.Ring"/>, which shows a check once its item is done. Items with an
    /// <see cref="TimelineItem.Icon"/> or <see cref="TimelineItem.Avatar"/> keep theirs.
    /// </summary>
    [Parameter] public TimelineMarker Marker { get; set; }

    /// <summary>
    /// <see cref="ControlSize.Sm"/> for a compact list (smaller markers and text, closer items, the
    /// rail in short segments between the markers), such as recent activity in a sidebar. Defaults to
    /// <see cref="ControlSize.Md"/>.
    /// </summary>
    [Parameter] public ControlSize Size { get; set; } = ControlSize.Md;

    /// <inheritdoc />
    protected override string BaseClass => "ib-timeline";

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        base.BuildAttributes(classes, styles);
        classes.Add(TimePlacement switch
        {
            TimelinePlacement.Below => "ib-timeline-time-below",
            TimelinePlacement.Start => "ib-timeline-time-start",
            TimelinePlacement.End => "ib-timeline-time-end",
            _ => null,
        });
        classes.Add("ib-timeline-ring", Marker == TimelineMarker.Ring);
        classes.Add("ib-timeline-sm", Size == ControlSize.Sm);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        // Its list has no markers, and Safari drops a list's role without them (as List does).
        if (AdditionalAttributes?.ContainsKey("role") != true)
        {
            AdditionalAttributes = new Dictionary<string, object>(AdditionalAttributes ?? new Dictionary<string, object>()) { ["role"] = "list" };
        }
    }
}

/// <summary>Where a <see cref="Timeline"/> puts each item's time.</summary>
public enum TimelinePlacement
{
    /// <summary>On its own line above the title: a dated history or a roadmap. The default.</summary>
    Above,

    /// <summary>Under the title and content: an activity feed, where what happened comes first.</summary>
    Below,

    /// <summary>In a column at the start, before the markers: a roadmap read down its dates.</summary>
    Start,

    /// <summary>At the end of the title's line: a log with short titles.</summary>
    End,
}

/// <summary>The plain markers on a <see cref="Timeline"/>.</summary>
public enum TimelineMarker
{
    /// <summary>A small dot. The default.</summary>
    Dot,

    /// <summary>A larger ring, which fills with a check once its item is done.</summary>
    Ring,
}

/// <summary>One event in a <see cref="Timeline"/>.</summary>
public class TimelineItem : IzzyComponentBase
{
    /// <summary>What happened.</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>
    /// The title as markup, in place of <see cref="Title"/>, when part of it should stand out:
    /// "<b>Ada Lindqvist</b> updated this invoice", "Completed <b>Beta program</b>". It's in the
    /// regular weight, so bold the part that matters.
    /// </summary>
    [Parameter] public RenderFragment? TitleContent { get; set; }

    /// <summary>Makes the title a link.</summary>
    [Parameter] public string? Href { get; set; }

    /// <summary>A short badge after the title, such as "Running" or "12s".</summary>
    [Parameter] public string? Badge { get; set; }

    /// <summary>The badge's color, such as <see cref="Status.Success"/>. Defaults to neutral.</summary>
    [Parameter] public Status BadgeStatus { get; set; } = Status.Neutral;

    /// <summary>When, as written: "Oct 13", "Q3". For a time shown as how long ago, set <see cref="Value"/>.</summary>
    [Parameter] public string? Time { get; set; }

    /// <summary>
    /// The time in machine-readable form for the <c>time</c> element's <c>datetime</c>, such as
    /// <c>2026-10-13</c> or <c>2026-10-13T21:58Z</c>.
    /// </summary>
    [Parameter] public string? DateTime { get; set; }

    /// <summary>
    /// When, as a time: shown as how long ago ("2 hours ago", "yesterday"), kept current as the page
    /// stays open, and as the date past 30 days; see <see cref="RelativeTime"/>. <see cref="Time"/>
    /// wins when both are set, for wording such as "Q3".
    /// </summary>
    [Parameter] public DateTimeOffset? Value { get; set; }

    /// <summary>An icon in the marker in place of the dot, such as <c>Lucide.GitCommitHorizontal</c>.</summary>
    [Parameter] public IconData? Icon { get; set; }

    /// <summary>
    /// A person's name, whose initials are the marker in place of the dot: who did it, in an activity
    /// feed. With <see cref="AvatarSrc"/>, their picture.
    /// </summary>
    [Parameter] public string? Avatar { get; set; }

    /// <summary>A picture for the avatar marker, such as the <see cref="Avatar"/> person's photo.</summary>
    [Parameter] public string? AvatarSrc { get; set; }

    /// <summary>Colors the marker, such as <see cref="Status.Success"/> for a passed build.</summary>
    [Parameter] public Status Status { get; set; } = Status.Neutral;

    /// <summary>
    /// A finished step: its marker fills (a ring shows a check) and the rail runs solid from it to the
    /// next item. Once any item in the timeline is done, the ones that aren't are drawn as still to come.
    /// </summary>
    [Parameter] public bool Done { get; set; }

    /// <summary>
    /// Marks this as where things stand now (the deployed commit, the running step): the marker
    /// lights in the accent and the item is announced as current.
    /// </summary>
    [Parameter] public bool Current { get; set; }

    /// <summary>Content under the title: a description, a link, actions.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    private bool HasAvatar => Avatar is not null || AvatarSrc is not null;

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        classes.Add("ib-timeline-item");
        classes.Add("ib-timeline-current", Current);
        classes.Add("ib-timeline-done", Done);
        classes.Add("ib-timeline-has-icon", Icon is not null && !HasAvatar);
        classes.Add("ib-timeline-has-avatar", HasAvatar);
        classes.Add(Status == Status.Neutral ? null : $"ib-timeline-{Status.ToString().ToLowerInvariant()}");
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var (cssClass, style) = BuildRootAttributes();
        builder.OpenElement(0, "li");
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "class", cssClass);
        builder.AddAttribute(3, "style", style);
        if (Current)
        {
            builder.AddAttribute(4, "aria-current", "true");
        }

        builder.OpenElement(5, "span");
        builder.AddAttribute(6, "class", "ib-timeline-marker");
        builder.AddAttribute(7, "aria-hidden", "true");
        if (HasAvatar)
        {
            builder.OpenComponent<Avatar>(8);
            builder.AddComponentParameter(9, nameof(IzzyBlazor.Avatar.Name), Avatar);
            builder.AddComponentParameter(10, nameof(IzzyBlazor.Avatar.Src), AvatarSrc);
            builder.AddComponentParameter(11, nameof(IzzyBlazor.Avatar.Decorative), true);
            builder.CloseComponent();
        }
        else if ((Icon ?? (Done ? Lucide.Check : null)) is { } icon)
        {
            // A done step's check shows on a ring; a done dot just fills (timeline CSS).
            builder.OpenComponent<Icon>(12);
            builder.AddComponentParameter(13, nameof(IzzyBlazor.Icon.Name), icon);
            builder.CloseComponent();
        }

        builder.CloseElement();

        builder.OpenElement(14, "div");
        builder.AddAttribute(15, "class", "ib-timeline-body");
        AddTime(builder);

        if (Title is not null || TitleContent is not null || Badge is not null)
        {
            builder.OpenElement(30, "div");
            builder.AddAttribute(31, "class", "ib-timeline-head");
            if (Title is not null || TitleContent is not null)
            {
                builder.OpenElement(32, Href is null ? "span" : "a");
                builder.AddAttribute(33, "class", TitleContent is null ? "ib-timeline-title" : "ib-timeline-title ib-timeline-title-rich");
                if (Href is not null)
                {
                    builder.AddAttribute(34, "href", Href);
                }

                if (TitleContent is not null)
                {
                    builder.AddContent(35, TitleContent);
                }
                else
                {
                    builder.AddContent(36, Title);
                }

                if (Done)
                {
                    builder.OpenElement(37, "span");
                    builder.AddAttribute(38, "class", "ib-visually-hidden");
                    builder.AddContent(39, " " + TextDefaults.Completed);
                    builder.CloseElement();
                }

                builder.CloseElement();
            }

            if (Badge is not null)
            {
                builder.OpenComponent<Badge>(40);
                builder.AddComponentParameter(41, nameof(IzzyBlazor.Badge.Size), BadgeSize.Sm);
                builder.AddComponentParameter(42, nameof(IzzyBlazor.Badge.Status), BadgeStatus);
                builder.AddComponentParameter(43, nameof(IzzyBlazor.Badge.ChildContent), (RenderFragment)(b => b.AddContent(0, Badge)));
                builder.CloseComponent();
            }

            builder.CloseElement();
        }

        if (ChildContent is not null)
        {
            builder.OpenElement(50, "div");
            builder.AddAttribute(51, "class", "ib-timeline-content");
            builder.AddContent(52, ChildContent);
            builder.CloseElement();
        }

        builder.CloseElement();
        builder.CloseElement();
    }

    // The time comes first in the body, so it's read first; the timeline's CSS moves it below, to the
    // start or to the end of the title's line.
    private void AddTime(RenderTreeBuilder builder)
    {
        if (Time is not null)
        {
            builder.OpenElement(16, DateTime is null ? "span" : "time");
            builder.AddAttribute(17, "class", "ib-timeline-time");
            if (DateTime is not null)
            {
                builder.AddAttribute(18, "datetime", DateTime);
            }

            builder.AddContent(19, Time);
            builder.CloseElement();
        }
        else if (Value is { } value)
        {
            var threshold = TimeSpan.FromDays(30);
            builder.OpenElement(20, "time");
            builder.AddAttribute(21, "class", "ib-timeline-time");
            RelativeTime.AddTimeAttributes(builder, 22, value, threshold, System.Globalization.CultureInfo.CurrentCulture);
            builder.AddContent(25, RelativeTime.Text(value, DateTimeOffset.UtcNow, threshold, TextDefaults, System.Globalization.CultureInfo.CurrentCulture));
            builder.CloseElement();
        }
    }
}
