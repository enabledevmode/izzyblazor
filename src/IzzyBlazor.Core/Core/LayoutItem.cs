namespace IzzyBlazor;

/// <summary>
/// The item props: how a component sits in its parent (margins, flex sizing, widths, alignment).
/// <see cref="LayoutItemBase"/> and <see cref="FieldBase{TValue}"/> both declare them, and both
/// write them with <see cref="LayoutItemProps"/>, so the two sets can't drift apart.
/// </summary>
internal interface ILayoutItem
{
    Responsive<int> Margin { get; }
    Responsive<int> MarginX { get; }
    Responsive<int> MarginY { get; }
    Responsive<int> MarginTop { get; }
    Responsive<int> MarginBottom { get; }
    Responsive<int> MarginStart { get; }
    Responsive<int> MarginEnd { get; }
    Responsive<Length> Width { get; }
    Responsive<Length> MinWidth { get; }
    Responsive<Length> MaxWidth { get; }
    bool Grow { get; }
    bool Shrink { get; }
    Responsive<Length> Basis { get; }
    Responsive<Align> AlignSelf { get; }
    Responsive<int> ColSpan { get; }
}

/// <summary>Special values for <c>ColSpan</c>.</summary>
public static class GridSpan
{
    /// <summary>Spans the whole row: <c>ColSpan="GridSpan.Full"</c>.</summary>
    public const int Full = -1;
}

/// <summary>Writes style props as modifier classes and custom properties.</summary>
internal static class LayoutItemProps
{
    /// <summary>All item props, in the order <see cref="LayoutItemBase"/> writes them.</summary>
    public static void Apply(ClassBuilder classes, StyleBuilder styles, ILayoutItem item)
    {
        ApplyMargins(classes, styles, item);
        ApplyWidths(classes, styles, item);
        ApplyFlex(classes, styles, item);
        ApplyGrid(classes, styles, item);
    }

    public static void ApplyGrid(ClassBuilder classes, StyleBuilder styles, ILayoutItem item) =>
        Write(classes, styles, item.ColSpan, "ib-colspan", "colspan", StyleValues.ColumnSpan);

    public static void ApplyMargins(ClassBuilder classes, StyleBuilder styles, ILayoutItem item)
    {
        ApplyAxis(classes, styles, Either(item.MarginX, item.Margin), item.MarginStart, item.MarginEnd, "mx", "ms", "me", StyleValues.Margin);
        ApplyAxis(classes, styles, Either(item.MarginY, item.Margin), item.MarginTop, item.MarginBottom, "my", "mt", "mb", StyleValues.Margin);
    }

    public static void ApplyWidths(ClassBuilder classes, StyleBuilder styles, ILayoutItem item)
    {
        Write(classes, styles, item.Width, "ib-w", "w", StyleValues.Length);
        Write(classes, styles, item.MaxWidth, "ib-maxw", "maxw", StyleValues.Length);
        Write(classes, styles, item.MinWidth, "ib-minw", "minw", StyleValues.Length);
    }

    public static void ApplyFlex(ClassBuilder classes, StyleBuilder styles, ILayoutItem item)
    {
        // On/off switches are plain bools (so <Box Grow> works); only a change from the default adds CSS.
        Write(classes, styles, item.Grow ? true : default(Responsive<bool>), "ib-grow", "grow", StyleValues.Factor);
        Write(classes, styles, item.Shrink ? default(Responsive<bool>) : false, "ib-shrink", "shrink", StyleValues.Factor);
        Write(classes, styles, item.Basis, "ib-basis", "basis", StyleValues.Length);
        Write(classes, styles, item.AlignSelf, "ib-self", "self", StyleValues.Align);
    }

    /// <summary>
    /// For each value in a responsive parameter, adds the modifier class (<c>ib-px</c>,
    /// <c>ib-px-md</c>...) and its custom property (<c>--ib-px</c>, <c>--ib-px-md</c>...).
    /// </summary>
    public static void Write<T>(ClassBuilder classes, StyleBuilder styles, Responsive<T> value, string cssClass, string varName, Func<T, string> map)
    {
        foreach (var (breakpoint, _) in value.Entries)
        {
            classes.Add(breakpoint is { } bp ? $"{cssClass}-{bp.Name()}" : cssClass);
        }

        styles.AddVars(varName, value, map);
    }

    /// <summary>The <see cref="IzzyComponentBase.HiddenFrom"/> and <see cref="IzzyComponentBase.VisibleFrom"/> classes, or null.</summary>
    public static string? VisibilityClasses(Breakpoint? hiddenFrom, Breakpoint? visibleFrom)
    {
        var hidden = hiddenFrom is { } h ? $"ib-hidden-from-{h.Name()}" : null;
        var visible = visibleFrom is { } v ? $"ib-visible-from-{v.Name()}" : null;
        return hidden is null ? visible : visible is null ? hidden : $"{hidden} {visible}";
    }

    // With no side set, one axis class (ib-px). With either side set, a class per side, each from
    // its own value or the axis value: an axis class at a larger breakpoint would otherwise
    // override a side set at a smaller one.
    public static void ApplyAxis(ClassBuilder classes, StyleBuilder styles, Responsive<int> axis, Responsive<int> start, Responsive<int> end,
        string axisName, string startName, string endName, Func<int, string> format)
    {
        if (start.IsEmpty && end.IsEmpty)
        {
            Write(classes, styles, axis, "ib-" + axisName, axisName, format);
            return;
        }

        Write(classes, styles, Either(start, axis), "ib-" + startName, startName, format);
        Write(classes, styles, Either(end, axis), "ib-" + endName, endName, format);
    }

    // The specific value (a side, or an axis over the all-sides value) where it applies, the
    // general one elsewhere. Mobile first: once the specific value starts at a breakpoint it holds
    // at every larger one, so it's repeated wherever the general value would take over again.
    // PaddingTop {Md=0} with Padding 4 is 4 below md and 0 from md up.
    public static Responsive<int> Either(Responsive<int> specific, Responsive<int> general)
    {
        if (specific.IsEmpty || general.IsEmpty)
        {
            return specific.IsEmpty ? general : specific;
        }

        var own = specific.Entries.ToDictionary(e => e.Breakpoint ?? (Breakpoint)(-1), e => e.Value);
        var fallback = general.Entries.ToDictionary(e => e.Breakpoint ?? (Breakpoint)(-1), e => e.Value);
        var result = default(Responsive<int>);
        int? current = null;
        foreach (var bp in new[] { (Breakpoint)(-1), Breakpoint.Sm, Breakpoint.Md, Breakpoint.Lg, Breakpoint.Xl })
        {
            int? value = own.TryGetValue(bp, out var o) ? current = o
                : current is { } held ? (fallback.ContainsKey(bp) ? held : null)
                : fallback.TryGetValue(bp, out var f) ? f : null;
            if (value is { } v)
            {
                result = bp switch
                {
                    Breakpoint.Sm => result with { Sm = v },
                    Breakpoint.Md => result with { Md = v },
                    Breakpoint.Lg => result with { Lg = v },
                    Breakpoint.Xl => result with { Xl = v },
                    _ => result with { Base = v },
                };
            }
        }

        return result;
    }
}
