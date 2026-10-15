using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// A vertical list of events, such as a commit log, an order's history or a changelog: each
/// <see cref="TimelineItem"/> has a marker on a rail, a title and time, and any content under it
/// (a link, a description, actions).
/// </summary>
/// <example>
/// <code>
/// &lt;Timeline&gt;
///     &lt;TimelineItem Title="Add Section" Time="Oct 13" Current&gt;
///         &lt;Anchor Href="..."&gt;93c85d4&lt;/Anchor&gt;
///     &lt;/TimelineItem&gt;
/// &lt;/Timeline&gt;
/// </code>
/// </example>
public class Timeline : Box
{
    /// <summary>Creates a timeline, rendered as an ordered list.</summary>
    public Timeline() => As = "ol";

    /// <inheritdoc />
    protected override string BaseClass => "ib-timeline";
}

/// <summary>One event in a <see cref="Timeline"/>.</summary>
public class TimelineItem : IzzyComponentBase
{
    /// <summary>What happened.</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>Makes the title a link.</summary>
    [Parameter] public string? Href { get; set; }

    /// <summary>When, as shown: "Oct 13", "2 hours ago".</summary>
    [Parameter] public string? Time { get; set; }

    /// <summary>
    /// The time in machine-readable form for the <c>time</c> element's <c>datetime</c>, such as
    /// <c>2026-10-13</c> or <c>2026-10-13T21:58Z</c>.
    /// </summary>
    [Parameter] public string? DateTime { get; set; }

    /// <summary>An icon in the marker in place of the dot, such as <c>Tabler.GitCommit</c>.</summary>
    [Parameter] public IconData? Icon { get; set; }

    /// <summary>Colors the marker, such as <see cref="Status.Success"/> for a passed build.</summary>
    [Parameter] public Status Status { get; set; } = Status.Neutral;

    /// <summary>
    /// Marks this as where things stand now (the deployed commit, the current step): the marker
    /// lights in the accent and the item is announced as current.
    /// </summary>
    [Parameter] public bool Current { get; set; }

    /// <summary>Content under the title: a description, a link, actions.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        classes.Add("ib-timeline-item");
        classes.Add("ib-timeline-current", Current);
        classes.Add("ib-timeline-has-icon", Icon is not null);
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
        if (Icon is { } icon)
        {
            builder.OpenComponent<Icon>(8);
            builder.AddComponentParameter(9, nameof(IzzyBlazor.Icon.Name), icon);
            builder.CloseComponent();
        }

        builder.CloseElement();

        builder.OpenElement(10, "div");
        builder.AddAttribute(11, "class", "ib-timeline-body");
        if (Title is not null || Time is not null)
        {
            builder.OpenElement(12, "div");
            builder.AddAttribute(13, "class", "ib-timeline-head");
            if (Title is not null)
            {
                builder.OpenElement(14, Href is null ? "span" : "a");
                builder.AddAttribute(15, "class", "ib-timeline-title");
                if (Href is not null)
                {
                    builder.AddAttribute(16, "href", Href);
                }

                builder.AddContent(17, Title);
                builder.CloseElement();
            }

            if (Time is not null)
            {
                builder.OpenElement(18, DateTime is null ? "span" : "time");
                builder.AddAttribute(19, "class", "ib-timeline-time");
                if (DateTime is not null)
                {
                    builder.AddAttribute(20, "datetime", DateTime);
                }

                builder.AddContent(21, Time);
                builder.CloseElement();
            }

            builder.CloseElement();
        }

        if (ChildContent is not null)
        {
            builder.OpenElement(22, "div");
            builder.AddAttribute(23, "class", "ib-timeline-content");
            builder.AddContent(24, ChildContent);
            builder.CloseElement();
        }

        builder.CloseElement();
        builder.CloseElement();
    }
}
