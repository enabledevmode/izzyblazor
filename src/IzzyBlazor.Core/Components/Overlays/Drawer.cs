namespace IzzyBlazor;

/// <summary>
/// A panel that slides in from an edge: a <see cref="Dialog"/> with <see cref="Dialog.Placement"/>
/// <see cref="DialogPlacement.End"/> by default. Everything else is the dialog's: open it with a
/// <see cref="DialogTrigger"/>, a <c>?dialog=id</c> link or <c>@bind-Open</c>; it traps focus and
/// closes on Escape. <see cref="Dialog.Size"/> sets its width (Sm 20rem, Md 24rem, Lg 32rem,
/// Xl 40rem, Full). Use <see cref="DialogPlacement.Bottom"/> for a sheet on phones.
/// </summary>
/// <example>
/// <code>
/// &lt;DialogTrigger For="filters"&gt;Filters&lt;/DialogTrigger&gt;
/// &lt;Drawer Id="filters" Title="Filters"&gt;...&lt;/Drawer&gt;
/// </code>
/// </example>
/// <accessibility>
/// <list type="bullet">
/// <item>It's a native modal <c>dialog</c>: the page behind it is inert, Escape closes it, and focus returns to the <see cref="DialogTrigger"/> that opened it.</item>
/// <item><see cref="Dialog.Title"/> or a <see cref="DialogHeader"/> names it; without either, pass an <c>aria-label</c>.</item>
/// <item>The close button is named "Close" (<see cref="Dialog.CloseLabel"/>).</item>
/// <item>With JavaScript off, a drawer opened from the server isn't modal: the page behind stays usable and focus can leave it. Escape closes it in browsers with the <c>closedby</c> attribute (Chrome and Edge 134, Firefox 141), not in Safari.</item>
/// </list>
/// </accessibility>
/// <seealso cref="Dialog"/>
/// <seealso cref="AppSidebar"/>
/// <seealso cref="Popover"/>
[ModeSupport(Off = ModeLevel.Partial, Notes = "With JavaScript off, a drawer opened from the server isn't modal: focus can leave it, and Escape closes it only in browsers with closedby (not Safari).")]
public class Drawer : Dialog
{
    /// <summary>Creates a drawer on the end side.</summary>
    public Drawer() => Placement = DialogPlacement.End;

    /// <inheritdoc />
    protected override string IdPrefix => "ib-drawer";
}
