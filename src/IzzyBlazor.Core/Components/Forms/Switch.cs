namespace IzzyBlazor;

/// <summary>
/// An on/off switch: a checkbox with <c>role="switch"</c> and a track-and-thumb look. Use it for
/// settings that apply immediately or read as on/off; use <see cref="Checkbox"/> for agreeing or choosing.
/// </summary>
/// <example><code>&lt;Switch @bind-Value="Model.EmailNotifications" Label="Email notifications" /&gt;</code></example>
[ModeSupport]
public class Switch : Checkbox
{
    /// <inheritdoc />
    protected override bool AsSwitch => true;
}
