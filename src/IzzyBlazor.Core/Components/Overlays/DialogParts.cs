using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// Heading content for a <see cref="Dialog"/> in place of its <c>Title</c>, such as a title with
/// an icon or a badge. It names the dialog for screen readers. Write it directly in the dialog; the
/// dialog draws it in its header row, beside the close button, so a form or cascading value it's
/// written inside doesn't reach it.
/// </summary>
/// <example><code>&lt;DialogHeader&gt;&lt;Row Gap="2"&gt;&lt;Icon Name="Lucide.Users" /&gt;Invite people&lt;/Row&gt;&lt;/DialogHeader&gt;</code></example>
public sealed class DialogHeader : ComponentPart
{
    internal override Type HostType => typeof(Dialog);
}

/// <summary>
/// The row of buttons at the bottom of a <see cref="Dialog"/> or <see cref="Drawer"/>, usually a
/// <see cref="DialogClose"/> and the main action. Write it last: last in the dialog, or last in a
/// form that's last in the dialog, so its buttons stay inside the form and submit it. It reaches the
/// dialog's edges (and follows <c>ContentPadding</c>) and stays in view when the body scrolls. In a
/// side drawer it follows the content, so with little content it sits partway down.
/// A row like <see cref="Row"/>, with <c>Gap</c>, <c>Align</c> and <c>Justify</c> (the buttons go at
/// the end by default). Also for content shown by <see cref="DialogService"/>.
/// </summary>
/// <example>
/// <code>
/// &lt;Dialog Id="rename" Title="Rename"&gt;
///     &lt;Form Model="model" OnValidSubmit="Save"&gt;
///         &lt;TextField @bind-Value="model.Name" Label="Name" /&gt;
///         &lt;DialogFooter&gt;
///             &lt;DialogClose&gt;Cancel&lt;/DialogClose&gt;
///             &lt;Button Submit&gt;Save&lt;/Button&gt;
///         &lt;/DialogFooter&gt;
///     &lt;/Form&gt;
/// &lt;/Dialog&gt;
/// </code>
/// </example>
public class DialogFooter : FlexBase
{
    /// <inheritdoc />
    protected override Responsive<Direction> LayoutDirection => IzzyBlazor.Direction.Row;

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        base.BuildAttributes(classes, styles);
        classes.Add("ib-dialog-footer");
    }
}

/// <summary>
/// A <see cref="Dialog"/>'s body with layout props: a column with <c>Gap</c>, <c>Align</c>,
/// <c>Justify</c> and <c>Direction</c>, like <see cref="Stack"/>. Optional: content written straight
/// in the dialog is its body too. It fills the dialog's height, so a <see cref="DialogFooter"/>
/// after it sits at the bottom of a side drawer whatever the content's length; put it and the
/// footer in a form to keep the footer's buttons in the form. <see cref="Flush"/> runs it to the
/// dialog's edges, for a table or a list.
/// </summary>
/// <example>
/// <code>
/// &lt;Drawer Id="details" Title="Project details"&gt;
///     &lt;DialogBody Gap="4"&gt;...&lt;/DialogBody&gt;
///     &lt;DialogFooter&gt;&lt;DialogClose&gt;Close&lt;/DialogClose&gt;&lt;/DialogFooter&gt;
/// &lt;/Drawer&gt;
/// </code>
/// </example>
public class DialogBody : FlexBase
{
    /// <summary>Layout direction. Defaults to a column.</summary>
    [Parameter] public Responsive<Direction> Direction { get; set; }

    /// <summary>Runs the content to the dialog's sides, without its side padding: for a table, a divided list or an image.</summary>
    [Parameter] public bool Flush { get; set; }

    /// <inheritdoc />
    protected override Responsive<Direction> LayoutDirection => Direction;

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        base.BuildAttributes(classes, styles);
        classes.Add("ib-dialog-content");
        classes.Add("ib-dialog-flush", Flush);
    }
}
