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

    /// <summary>4px by default (the look sets it). Small parts: checkboxes, menu items, tooltips.</summary>
    Sm,

    /// <summary>6px by default (the look sets it). Controls: buttons, inputs, nav items.</summary>
    Md,

    /// <summary>10px by default (the look sets it). Popovers, menus and dialogs.</summary>
    Lg,

    /// <summary>12px by default (the look sets it). Cards and large panels.</summary>
    Xl,

    /// <summary>Fully rounded (pills and circles).</summary>
    Full,
}

/// <summary>What happens to content that doesn't fit.</summary>
public enum Overflow
{
    /// <summary>It shows outside the box (the CSS default).</summary>
    Visible,

    /// <summary>It's cut off, with no scrollbars.</summary>
    Hidden,

    /// <summary>The box scrolls when it needs to.</summary>
    Auto,

    /// <summary>The box always has scrollbars.</summary>
    Scroll,

    /// <summary>It's cut off, and the box can't be scrolled even from script.</summary>
    Clip,
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

/// <summary>
/// How an <see cref="AppShell"/> layers its parts. Content is the brightest layer by default and
/// the frame recedes into the page tint (<c>--ib-color-background</c>). A part's own <c>Bg</c>
/// always wins.
/// </summary>
public enum ShellVariant
{
    /// <summary>
    /// The sidebar takes the page tint; header, main, aside and footer are surfaces (the default).
    /// The content stands out and the navigation recedes, like Linear, Notion or VS Code.
    /// </summary>
    Tinted,

    /// <summary>
    /// The whole frame takes the page tint and the main area is a raised panel inset from the
    /// window edge, with rounded corners. The frame has no lines; the panel has an edge.
    /// </summary>
    Inset,

    /// <summary>Every part is a surface; lines alone separate them.</summary>
    Flat,

    /// <summary>
    /// The main area takes the page tint and the frame is surfaces: for dashboards whose content
    /// is all cards. Put content in <see cref="Card"/>s; fills placed straight on the tint read
    /// as holes.
    /// </summary>
    Canvas,
}
