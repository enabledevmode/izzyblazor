using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// Base class for components that open and close: accordion items, collapsibles, menus,
/// popovers and dialogs.
/// </summary>
/// <remarks>
/// The browser always does the opening and closing, with native <c>details</c>, the Popover
/// API or <c>dialog</c>. <see cref="Open"/> sets the starting state; on interactive pages the
/// <c>ibtoggle</c> event reports changes back, so <c>@bind-Open</c> follows the page and
/// setting <see cref="Open"/> from C# opens or closes it: rendered as <c>data-open</c>, which
/// IzzyBlazor's script applies once C# has caught up with the user's own changes.
/// </remarks>
public abstract class ToggleComponentBase : LayoutItemBase
{
    private string? _generatedId;
    private bool? _lastOpenParameter;

    /// <summary>Whether it is open. Use <c>@bind-Open</c> to follow changes on interactive pages.</summary>
    [Parameter] public bool Open { get; set; }

    /// <summary>Runs when the user opens or closes it on an interactive page.</summary>
    [Parameter, ModeSupport(NoScript = ModeLevel.None, Static = ModeLevel.None)]
    public EventCallback<bool> OpenChanged { get; set; }

    /// <summary>The element id. Generated when not set.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>The prefix for generated ids, such as <c>ib-menu</c>.</summary>
    protected abstract string IdPrefix { get; }

    /// <summary>The id written to the element that opens and closes.</summary>
    protected string ElementId => Id ?? (_generatedId ??= IdGenerator.Next(IdPrefix));

    /// <summary>The current state: the <see cref="Open"/> parameter, updated by the browser's toggle events.</summary>
    protected bool IsOpen { get; set; }

    /// <summary>
    /// The last toggle event handled, by its <see cref="IzzyToggleEventArgs.Seq"/>, rendered on
    /// interactive pages as <c>data-ib-seq</c>. The script applies a render only once it has caught
    /// up with every change the user made: one from before reports a state they've since left.
    /// </summary>
    private protected string? ToggleSeq => IsInteractive ? _seq.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;

    private int _seq;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // Only a new parameter value overrides the state the browser reported.
        if (_lastOpenParameter != Open)
        {
            _lastOpenParameter = Open;
            IsOpen = Open;
        }
    }

    /// <summary>Handles <c>ibtoggle</c> from this component's own element.</summary>
    protected Task HandleToggle(IzzyToggleEventArgs e)
    {
        if (e.Id != ElementId)
        {
            return Task.CompletedTask;
        }

        // Handled, whether or not it changes anything: the render it brings says so.
        _seq = Math.Max(_seq, e.Seq);
        if (e.Open == IsOpen)
        {
            return Task.CompletedTask;
        }

        IsOpen = e.Open;
        return OpenChanged.InvokeAsync(IsOpen);
    }
}
