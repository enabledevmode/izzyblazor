namespace IzzyBlazor;

/// <summary>The direction a <see cref="Stack"/> lays out its children.</summary>
public enum Direction
{
    /// <summary>Left to right (in left-to-right languages).</summary>
    Row,

    /// <summary>Top to bottom.</summary>
    Column,

    /// <summary>Right to left.</summary>
    RowReverse,

    /// <summary>Bottom to top.</summary>
    ColumnReverse,
}

/// <summary>Cross-axis alignment of a <see cref="Stack"/>'s children.</summary>
public enum Align
{
    /// <summary>Children stretch to fill the cross axis.</summary>
    Stretch,

    /// <summary>Aligned to the start.</summary>
    Start,

    /// <summary>Centered.</summary>
    Center,

    /// <summary>Aligned to the end.</summary>
    End,

    /// <summary>Aligned on their text baselines.</summary>
    Baseline,
}

/// <summary>Main-axis distribution of a <see cref="Stack"/>'s children.</summary>
public enum Justify
{
    /// <summary>Packed at the start.</summary>
    Start,

    /// <summary>Packed in the center.</summary>
    Center,

    /// <summary>Packed at the end.</summary>
    End,

    /// <summary>First and last at the edges, equal space between.</summary>
    Between,

    /// <summary>Equal space around each child.</summary>
    Around,

    /// <summary>Equal space between and at the edges.</summary>
    Evenly,
}

/// <summary>Corner radius tokens.</summary>
public enum Radius
{
    /// <summary>Square corners.</summary>
    None,

    /// <summary>4px. Small parts: checkboxes, menu items, tooltips.</summary>
    Sm,

    /// <summary>6px. Controls: buttons, inputs, nav items.</summary>
    Md,

    /// <summary>8px. Surfaces: cards, menus, dialogs.</summary>
    Lg,

    /// <summary>12px. Large panels and hero cards.</summary>
    Xl,

    /// <summary>Fully rounded (pills and circles).</summary>
    Full,
}

/// <summary>Elevation tokens.</summary>
public enum Shadow
{
    /// <summary>No shadow.</summary>
    None,

    /// <summary>A subtle lift.</summary>
    Sm,

    /// <summary>Cards and popovers.</summary>
    Md,

    /// <summary>Dialogs and floating panels.</summary>
    Lg,
}

/// <summary>Whether items run across or down.</summary>
public enum Orientation
{
    /// <summary>Top to bottom.</summary>
    Vertical,

    /// <summary>Side by side, wrapping when space runs out.</summary>
    Horizontal,
}

/// <summary>Which side of the page a sidebar sits on, following the reading direction.</summary>
public enum SidebarPosition
{
    /// <summary>The start side: left in left-to-right languages, right in right-to-left ones.</summary>
    Start,

    /// <summary>The end side: right in left-to-right languages, left in right-to-left ones.</summary>
    End,
}

/// <summary>Top or bottom, such as where AppSidebar's collapse button sits.</summary>
public enum VerticalPosition
{
    /// <summary>At the top.</summary>
    Top,

    /// <summary>At the bottom.</summary>
    Bottom,
}

/// <summary>Whether an AppShell header or footer scrolls with the page.</summary>
public enum BarPosition
{
    /// <summary>Scrolls away with the page.</summary>
    Static,

    /// <summary>Stays in view: the header at the top, the footer at the bottom.</summary>
    Sticky,
}

/// <summary>What scrolls in an <see cref="AppShell"/>.</summary>
public enum ShellScroll
{
    /// <summary>
    /// The whole page scrolls (the default). Sticky bars, the sidebar and the aside stay in
    /// view. Best for content pages: browser scroll restoration and find-in-page just work.
    /// </summary>
    Page,

    /// <summary>
    /// The shell fills the window and only the main area scrolls, like a desktop app; the
    /// header, footer, sidebar and aside always stay put and scroll on their own.
    /// </summary>
    Main,
}
