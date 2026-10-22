namespace IzzyBlazor;

/// <summary>
/// An on/off switch: a checkbox with <c>role="switch"</c> and a track-and-thumb look. Use it for
/// settings that apply immediately or read as on/off; use <see cref="Checkbox"/> for agreeing or choosing.
/// </summary>
/// <example><code>&lt;Switch @bind-Value="Model.EmailNotifications" Label="Email notifications" /&gt;</code></example>
/// <accessibility>
/// <list type="bullet">
/// <item>It renders a native checkbox with <c>role="switch"</c> inside its <c>label</c>, so Space toggles it and screen readers hear "on" or "off".</item>
/// <item>The label text names it; the hint and errors are linked with <c>aria-describedby</c>.</item>
/// <item>In Windows high-contrast mode it shows as the browser's own checkbox.</item>
/// <item>A read-only switch arrives disabled; the script enables it, so it can be focused and is announced as read-only, and stops it from changing.</item>
/// </list>
/// </accessibility>
/// <seealso cref="Checkbox"/>
/// <seealso cref="SegmentedControl{TValue}"/>
/// <seealso cref="CheckboxGroup{TValue, TItem}"/>
[ModeSupport]
public class Switch : Checkbox
{
    /// <inheritdoc />
    protected override bool AsSwitch => true;
}
