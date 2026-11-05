using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>When a slider shows its value in a tooltip over the thumb.</summary>
public enum SliderTooltip
{
    /// <summary>Never: the thumb's position says it.</summary>
    None,

    /// <summary>While the thumb is hovered, focused from the keyboard or held.</summary>
    Active,

    /// <summary>All the time, over each thumb.</summary>
    Always,
}

/// <summary>A point marked on a slider's track, with a label under it.</summary>
/// <param name="Value">Where on the scale the mark goes.</param>
/// <param name="Label">The text under the mark, such as "37°C", or null for a dot alone.</param>
public sealed record SliderMark(double Value, string? Label = null);

/// <summary>The scale arithmetic Slider and RangeSlider share.</summary>
internal static class SliderScale
{
    /// <summary>A value's place on the scale, 0 to 100, as CSS reads it.</summary>
    public static string Percent(double value, double min, double max)
    {
        var span = max - min;
        var percent = span <= 0 ? 0 : Math.Clamp((value - min) / span * 100, 0, 100);
        return Math.Round(percent, 3).ToString("R", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The marks to draw, in order: each of <paramref name="marks"/> inside the scale, and with
    /// <paramref name="everyStep"/> a dot at each step that isn't already marked.
    /// </summary>
    public static IReadOnlyList<SliderMark> Marks(IReadOnlyList<SliderMark>? marks, bool everyStep, double min, double max, double? step)
    {
        var shown = (marks ?? []).Where(m => m.Value >= min && m.Value <= max).ToList();
        if (everyStep && max > min)
        {
            var size = step is > 0 ? step.Value : 1;
            var count = (int)Math.Min(Math.Floor((max - min) / size + 1e-9), 1000);
            for (var i = 0; i <= count; i++)
            {
                var value = Math.Round(min + i * size, 10);
                if (!shown.Any(m => Math.Abs(m.Value - value) < size / 1000))
                {
                    shown.Add(new SliderMark(value));
                }
            }
        }

        return shown.OrderBy(m => m.Value).ToList();
    }

    /// <summary>
    /// Draws the marks and, with <paramref name="ends"/>, the ends' labels (marks with no dot) where
    /// no labelled mark already sits, under the track's thumbs. Hidden from screen readers, which
    /// hear the slider's own scale.
    /// </summary>
    public static RenderFragment? Draw(IReadOnlyList<SliderMark> marks, bool ends, double min, double max, Func<double, string> said)
    {
        var points = marks.Select(m => (m.Value, m.Label, End: false)).ToList();
        if (ends)
        {
            foreach (var end in new[] { min, max })
            {
                if (!marks.Any(m => m.Label is not null && Math.Abs(m.Value - end) < 1e-9))
                {
                    points.Add((end, said(end), End: true));
                }
            }
        }

        if (points.Count == 0)
        {
            return null;
        }

        return builder =>
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", "ib-slider-marks");
            builder.AddAttribute(2, "aria-hidden", "true");
            foreach (var (value, label, end) in points)
            {
                builder.OpenElement(3, "span");
                // A label at either end lines up with the track's edge rather than spilling past it.
                var edge = Math.Abs(value - min) < 1e-9 ? " ib-slider-mark-min" : Math.Abs(value - max) < 1e-9 ? " ib-slider-mark-max" : "";
                builder.AddAttribute(4, "class", (end ? "ib-slider-mark ib-slider-mark-end" : "ib-slider-mark") + edge);
                builder.AddAttribute(5, "style", "--ib-at: " + Percent(value, min, max));
                if (label is not null)
                {
                    builder.OpenElement(6, "span");
                    builder.AddAttribute(7, "class", "ib-slider-mark-label");
                    builder.AddContent(8, label);
                    builder.CloseElement();
                }

                builder.CloseElement();
            }

            builder.CloseElement();
        };
    }

    /// <summary>Whether anything under the track has a label, which takes room below it.</summary>
    public static bool HasLabels(IReadOnlyList<SliderMark> marks, bool ends) => ends || marks.Any(m => m.Label is not null);
}
