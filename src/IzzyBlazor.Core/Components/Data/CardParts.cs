using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>Where a <see cref="CardPart"/> is drawn.</summary>
internal enum CardPartKind
{
    Media,
    Header,
    Actions,
}

/// <summary>What a <see cref="Card"/> cascades to its parts.</summary>
internal sealed class CardContext(Card card)
{
    public Card Card { get; } = card;
}

/// <summary>
/// A part of a <see cref="Card"/> that the card draws in its own place, wherever it's written
/// directly in the card. It's drawn at the card's level, so a form or cascading value it's written
/// inside doesn't reach its content.
/// </summary>
public abstract class CardPart : ComponentBase, IDisposable
{
    private bool _added;

    private protected CardPart()
    {
    }

    /// <summary>The part's content.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    [CascadingParameter] private CardContext? Context { get; set; }

    internal abstract CardPartKind Kind { get; }

    /// <inheritdoc />
    protected sealed override void OnParametersSet()
    {
        if (Context is null)
        {
            throw new InvalidOperationException($"{GetType().Name} must be inside a Card.");
        }

        if (_added)
        {
            Context.Card.PartChanged(this);
        }
        else
        {
            _added = true;
            Context.Card.AddPart(this);
        }
    }

    /// <inheritdoc />
    protected sealed override void BuildRenderTree(RenderTreeBuilder builder)
    {
        // Drawn by the card, not here.
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_added)
        {
            Context?.Card.RemovePart(this);
        }

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// An image or video at the top of a <see cref="Card"/>, edge to edge. Write it directly in the card.
/// </summary>
/// <example><code>&lt;CardMedia&gt;&lt;img src="cover.jpg" alt="" /&gt;&lt;/CardMedia&gt;</code></example>
public sealed class CardMedia : CardPart
{
    internal override CardPartKind Kind => CardPartKind.Media;
}

/// <summary>
/// Custom heading content in a <see cref="Card"/>, above its <c>Title</c>, such as an avatar row,
/// beside any <see cref="CardActions"/>. Write it directly in the card.
/// </summary>
/// <example><code>&lt;CardHeader&gt;&lt;Avatar Name="Maya Reyes" /&gt;&lt;/CardHeader&gt;</code></example>
public sealed class CardHeader : CardPart
{
    internal override CardPartKind Kind => CardPartKind.Header;
}

/// <summary>
/// Buttons at the end of a <see cref="Card"/>'s heading row, such as a menu. They stay clickable on
/// a linked card. Write it directly in the card.
/// </summary>
/// <example><code>&lt;CardActions&gt;&lt;IconButton Icon="Lucide.Ellipsis" Label="More" /&gt;&lt;/CardActions&gt;</code></example>
public sealed class CardActions : CardPart
{
    internal override CardPartKind Kind => CardPartKind.Actions;
}

/// <summary>Draws a <see cref="CardPart"/>'s content in the card.</summary>
internal sealed class CardPartOutlet : ComponentBase
{
    [Parameter] public CardPart? Part { get; set; }

    public void Refresh() => StateHasChanged();

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, Part?.ChildContent);
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
