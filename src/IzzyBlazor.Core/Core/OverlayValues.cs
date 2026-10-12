namespace IzzyBlazor;

/// <summary>How an <see cref="Accordion"/> draws its items.</summary>
public enum AccordionVariant
{
    /// <summary>Each item is its own bordered card, with space between (the default).</summary>
    Separated,

    /// <summary>One bordered box with dividers between items.</summary>
    Bordered,

    /// <summary>Dividers only, no outer border or padding at the sides. For sidebars and dense pages.</summary>
    Flush,
}

/// <summary>
/// Where a floating element sits next to the thing that opened it. <c>Start</c> and <c>End</c>
/// follow the reading direction, so they flip in right-to-left languages.
/// </summary>
public enum Placement
{
    /// <summary>Below, lined up with the start edge (the default for menus).</summary>
    BottomStart,

    /// <summary>Below, centered.</summary>
    Bottom,

    /// <summary>Below, lined up with the end edge.</summary>
    BottomEnd,

    /// <summary>Above, lined up with the start edge.</summary>
    TopStart,

    /// <summary>Above, centered (the default for tooltips).</summary>
    Top,

    /// <summary>Above, lined up with the end edge.</summary>
    TopEnd,

    /// <summary>Beside, on the start side (the left in left-to-right languages).</summary>
    Start,

    /// <summary>Beside, on the end side (the right in left-to-right languages).</summary>
    End,
}

/// <summary>How a <see cref="Popover"/> closes.</summary>
public enum PopoverMode
{
    /// <summary>Closes on Escape, on a click outside, or when another popover opens (the default).</summary>
    Auto,

    /// <summary>Stays open until its trigger or a close button closes it.</summary>
    Manual,
}

/// <summary>The width of a <see cref="Dialog"/>.</summary>
public enum DialogSize
{
    /// <summary>24rem. Confirmations.</summary>
    Sm,

    /// <summary>32rem (the default).</summary>
    Md,

    /// <summary>48rem. Forms with two columns.</summary>
    Lg,

    /// <summary>64rem. Tables and previews.</summary>
    Xl,

    /// <summary>The whole viewport.</summary>
    Full,
}

/// <summary>Where a <see cref="Dialog"/> appears.</summary>
public enum DialogPlacement
{
    /// <summary>In the middle of the screen (the default).</summary>
    Center,

    /// <summary>A full-height drawer on the start side.</summary>
    Start,

    /// <summary>A full-height drawer on the end side.</summary>
    End,
}

/// <summary>CSS names for the overlay enums.</summary>
internal static class OverlayNames
{
    public static string Name(this Placement placement) => placement switch
    {
        Placement.BottomStart => "bottom-start",
        Placement.Bottom => "bottom",
        Placement.BottomEnd => "bottom-end",
        Placement.TopStart => "top-start",
        Placement.Top => "top",
        Placement.TopEnd => "top-end",
        Placement.Start => "start",
        Placement.End => "end",
        _ => throw new ArgumentOutOfRangeException(nameof(placement)),
    };

    public static string Name(this DialogSize size) => size.ToString().ToLowerInvariant();

    public static string Name(this DialogPlacement placement) => placement.ToString().ToLowerInvariant();

    public static string Name(this AccordionVariant variant) => variant.ToString().ToLowerInvariant();
}
