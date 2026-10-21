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
[ModeSupport(Off = ModeLevel.Partial, Notes = "With JavaScript off, a drawer opened from the server isn't modal: Escape doesn't close it and focus can leave it.")]
public class Drawer : Dialog
{
    /// <summary>Creates a drawer on the end side.</summary>
    public Drawer() => Placement = DialogPlacement.End;

    /// <inheritdoc />
    protected override string IdPrefix => "ib-drawer";
}
