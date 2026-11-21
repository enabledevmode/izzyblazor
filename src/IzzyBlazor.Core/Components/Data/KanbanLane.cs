using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// A lane of a <see cref="Kanban{TItem}"/>: a row across the board's columns holding the cards
/// whose <c>LaneOf</c> is its <see cref="Value"/>, such as a team's or a priority's, under a
/// header with its title and how many cards it holds. Put the lanes in the board's <c>Lanes</c>.
/// </summary>
/// <seealso cref="Kanban{TItem}"/>
[ModeSupport]
public sealed class KanbanLane : ComponentBase, IDisposable
{
    private bool _collapsed;
    private bool _collapsedParam;
    private bool _added;
    private KanbanRow? _row;
    private string? _titleShown;

    /// <summary>The lane's value: it shows the cards whose <c>LaneOf</c> is this, and a card moved here takes it.</summary>
    [Parameter, EditorRequired] public string? Value { get; set; }

    /// <summary>The lane's title. Defaults to <see cref="Value"/>.</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>With the board's <c>Collapsible</c>, whether the lane is collapsed to its header. Bind it on an interactive page to keep it.</summary>
    [Parameter] public bool Collapsed { get; set; }

    /// <summary>Runs when the user collapses or expands the lane, on interactive pages; use <c>@bind-Collapsed</c>.</summary>
    [Parameter, ModeSupport(NoScript = ModeLevel.None, Static = ModeLevel.None)]
    public EventCallback<bool> CollapsedChanged { get; set; }

    [CascadingParameter] private IKanbanLanes? Board { get; set; }

    internal string TitleId { get; } = IdGenerator.Next("ib-kanban-lane");

    internal string TitleText => Title ?? Value ?? "";

    internal bool IsCollapsed => _collapsed;

    /// <summary>Where the board draws this lane's cells.</summary>
    internal KanbanRow Row => _row ??= new(this);

    internal Task ToggleAsync()
    {
        _collapsed = !_collapsed;
        return CollapsedChanged.InvokeAsync(_collapsed);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Board is null)
        {
            throw new InvalidOperationException("A KanbanLane goes in a Kanban's Lanes.");
        }

        _collapsed = _collapsedParam = Collapsed;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (string.IsNullOrEmpty(Value))
        {
            throw new InvalidOperationException("A KanbanLane needs a Value, such as Value=\"Design\": the LaneOf of the cards it shows.");
        }

        // Told only of what it shows, so the board's own render (which sets this lane's
        // parameters again) doesn't ask for another.
        var changed = TitleText != _titleShown;
        _titleShown = TitleText;
        if (Collapsed != _collapsedParam)
        {
            _collapsed = _collapsedParam = Collapsed;
            changed = true;
        }

        if (!_added)
        {
            _added = true;
            Board!.AddLane(this);
        }
        else if (changed)
        {
            Board!.LaneChanged();
        }
    }

    /// <inheritdoc />
    public void Dispose() => Board?.RemoveLane(this);
}
