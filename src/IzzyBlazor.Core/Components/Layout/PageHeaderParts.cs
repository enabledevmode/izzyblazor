using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// A picture before a <see cref="PageHeader"/>'s title on a record's page, such as a contact's
/// <see cref="Avatar"/> or a company's logo. Write it directly in the header; it's centered on the
/// title and description, and stays beside them on a phone.
/// </summary>
/// <example>
/// <code>
/// &lt;PageHeaderMedia&gt;
///     &lt;Avatar Name="Olivia Chen" Size="AvatarSize.Lg" Decorative /&gt;
/// &lt;/PageHeaderMedia&gt;
/// </code>
/// </example>
public sealed class PageHeaderMedia : ComponentPart
{
    internal override Type HostType => typeof(PageHeader);
}

/// <summary>
/// Buttons, links or a status at the end of a <see cref="PageHeader"/>, such as "New invoice" or a
/// badge. Write it directly in the header; it's drawn after the title and wraps under it on
/// narrow screens.
/// </summary>
/// <example>
/// <code>
/// &lt;PageHeaderActions&gt;
///     &lt;Button Href="/invoices/new"&gt;New invoice&lt;/Button&gt;
/// &lt;/PageHeaderActions&gt;
/// </code>
/// </example>
[ImportantParameters]
public sealed class PageHeaderActions : ComponentPart
{
    internal override Type HostType => typeof(PageHeader);

    /// <summary>
    /// The speed dial's name when more than one of its buttons floats on a small screen
    /// (<see cref="Button.FloatBelow"/>), such as "Create". Defaults to "Actions".
    /// </summary>
    [Parameter] public string? FloatLabel { get; set; }
}

/// <summary>
/// The buttons of a <see cref="PageHeader"/>'s actions that float below a width (<see cref="Button.FloatBelow"/>,
/// or a grid's <see cref="DataGridAdd"/>), so several share one speed dial.
/// </summary>
internal sealed class PageFloats
{
    private readonly List<(object Owner, Breakpoint Below, ToolbarFloat Float)> _items = [];

    /// <summary>Raised when a button starts or stops floating, or what it does changes.</summary>
    public event Action? Changed;

    public IEnumerable<(Breakpoint Below, ToolbarFloat Float)> Items => _items.Select(i => (i.Below, i.Float));

    public void Set(object owner, Breakpoint below, ToolbarFloat action)
    {
        var at = _items.FindIndex(i => ReferenceEquals(i.Owner, owner));
        if (at < 0)
        {
            _items.Add((owner, below, action));
        }
        else if (_items[at].Below == below && _items[at].Float == action)
        {
            return;
        }
        else
        {
            _items[at] = (owner, below, action);
        }

        Changed?.Invoke();
    }

    public void Remove(object owner)
    {
        if (_items.RemoveAll(i => ReferenceEquals(i.Owner, owner)) > 0)
        {
            Changed?.Invoke();
        }
    }
}

/// <summary>Draws a <see cref="PageHeader"/>'s floating buttons, and again as its actions' buttons change.</summary>
internal sealed class PageHeaderFloats : ComponentBase, IDisposable
{
    [Parameter, EditorRequired] public PageFloats Floats { get; set; } = null!;

    [Parameter, EditorRequired] public string Label { get; set; } = "";

    /// <inheritdoc />
    protected override void OnInitialized() => Floats.Changed += StateHasChanged;

    /// <inheritdoc />
    protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder) =>
        // A header's main action is its last button; in a speed dial it's the first, nearest the thumb.
        FloatActions.Render(builder, "ib-page-header-floats", Label, Floats.Items.Reverse());

    /// <inheritdoc />
    public void Dispose() => Floats.Changed -= StateHasChanged;
}
