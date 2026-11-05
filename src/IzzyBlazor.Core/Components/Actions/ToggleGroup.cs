namespace IzzyBlazor;

/// <summary>
/// Toggles that belong together, such as bold, italic and underline: any of them can be on. It
/// names the set and joins the buttons into one control; Accent, Variant and Size here are the
/// defaults for the <see cref="ToggleButton"/>s inside. Each toggle keeps its own value. For one
/// choice of several, such as text alignment (a single-selection toggle group), use
/// <see cref="SegmentedControl{TValue}"/>.
/// </summary>
/// <example>
/// <code>
/// &lt;ToggleGroup Label="Text style"&gt;
///     &lt;ToggleButton @bind-Value="_bold" Icon="Lucide.Bold" Label="Bold" IconOnly /&gt;
///     &lt;ToggleButton @bind-Value="_italic" Icon="Lucide.Italic" Label="Italic" IconOnly /&gt;
/// &lt;/ToggleGroup&gt;
/// </code>
/// </example>
/// <accessibility>
/// <list type="bullet">
/// <item>It renders a <c>div</c> with <c>role="group"</c>, named by <c>Label</c>, such as "Text style".</item>
/// <item>Each toggle is its own checkbox and tab stop, so Tab moves between them and Space toggles one.</item>
/// <item>For one choice among several, such as text alignment, use <see cref="SegmentedControl{TValue}"/>, which is a radio group.</item>
/// </list>
/// </accessibility>
/// <seealso cref="ToggleButton"/>
/// <seealso cref="SegmentedControl{TValue}"/>
/// <seealso cref="CheckboxGroup{TValue, TItem}"/>
[ModeSupport]
public sealed class ToggleGroup : ButtonGroup
{
}
