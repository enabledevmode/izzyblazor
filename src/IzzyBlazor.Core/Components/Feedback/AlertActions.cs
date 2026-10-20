using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// Buttons or links below an <see cref="Alert"/>'s message, such as "Retry" or "Details". Write it
/// last in the alert; it's drawn where it is. A row like <see cref="Row"/>, with <c>Gap</c>,
/// <c>Align</c>, <c>Justify</c> and <c>Wrap</c> (it wraps by default).
/// </summary>
/// <example>
/// <code>
/// &lt;Alert Status="Status.Danger" Title="Couldn't save"&gt;
///     The server didn't answer.
///     &lt;AlertActions&gt;&lt;Button Size="ControlSize.Sm"&gt;Retry&lt;/Button&gt;&lt;/AlertActions&gt;
/// &lt;/Alert&gt;
/// </code>
/// </example>
public class AlertActions : FlexBase
{
    /// <inheritdoc />
    protected override Responsive<Direction> LayoutDirection => IzzyBlazor.Direction.Row;

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        base.BuildAttributes(classes, styles);
        classes.Add("ib-alert-actions");
    }
}
