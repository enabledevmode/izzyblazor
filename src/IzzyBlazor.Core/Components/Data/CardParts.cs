using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// An image or video at the top of a <see cref="Card"/>, edge to edge. Write it directly in the card.
/// </summary>
/// <example><code>&lt;CardMedia&gt;&lt;img src="cover.jpg" alt="" /&gt;&lt;/CardMedia&gt;</code></example>
public sealed class CardMedia : ComponentPart
{
    internal override Type HostType => typeof(Card);
}

/// <summary>
/// Custom heading content in a <see cref="Card"/>, above its <c>Title</c>, such as an avatar row,
/// beside any <see cref="CardActions"/>. Write it directly in the card.
/// </summary>
/// <example><code>&lt;CardHeader&gt;&lt;Avatar Name="Maya Reyes" /&gt;&lt;/CardHeader&gt;</code></example>
public sealed class CardHeader : ComponentPart
{
    internal override Type HostType => typeof(Card);
}

/// <summary>
/// Buttons at the end of a <see cref="Card"/>'s heading row, such as a menu. They stay clickable on
/// a linked card. Write it directly in the card.
/// </summary>
/// <example><code>&lt;CardActions&gt;&lt;IconButton Icon="Lucide.Ellipsis" Label="More" /&gt;&lt;/CardActions&gt;</code></example>
public sealed class CardActions : ComponentPart
{
    internal override Type HostType => typeof(Card);
}

/// <summary>
/// A <see cref="Card"/>'s body with layout props: a column with <c>Gap</c>, <c>Align</c>,
/// <c>Justify</c> and <c>Direction</c>, like <see cref="Stack"/>. Optional: content written
/// straight in the card is its body too. <see cref="Flush"/> runs it edge to edge, for a table
/// or a list.
/// </summary>
/// <example><code>&lt;CardBody Gap="2"&gt;...&lt;/CardBody&gt;</code></example>
public class CardBody : FlexBase
{
    /// <summary>Layout direction. Defaults to a column.</summary>
    [Parameter] public Responsive<Direction> Direction { get; set; }

    /// <summary>Runs the content to the card's edges, without side padding: for a table, a divided list or a map.</summary>
    [Parameter] public bool Flush { get; set; }

    /// <inheritdoc />
    protected override Responsive<Direction> LayoutDirection => Direction;

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        base.BuildAttributes(classes, styles);
        classes.Add("ib-card-content");
        classes.Add("ib-card-flush", Flush);
    }
}

/// <summary>
/// The bottom row of a <see cref="Card"/>, such as buttons or metadata. A row like <see cref="Row"/>,
/// with <c>Gap</c>, <c>Align</c>, <c>Justify</c> and <c>Wrap</c>. Write it directly in the card; it's
/// drawn last. It spans the card's width and bottom edge, so a <c>Bg</c> or <c>BorderTop</c> makes it a bar.
/// </summary>
/// <example><code>&lt;CardFooter Justify="Justify.End"&gt;&lt;Button&gt;Cancel&lt;/Button&gt;&lt;/CardFooter&gt;</code></example>
public class CardFooter : FlexBase
{
    /// <inheritdoc />
    protected override Responsive<Direction> LayoutDirection => IzzyBlazor.Direction.Row;

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        base.BuildAttributes(classes, styles);
        classes.Add("ib-card-footer");
    }
}
