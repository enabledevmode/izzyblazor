using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// A kind of appointment in a <see cref="Scheduler{TItem}"/>, such as Work or Family: its color,
/// and an item in the toolbar's Categories menu that shows or hides its appointments. An
/// appointment's category is the Scheduler's <c>Category</c> value that matches
/// <see cref="Value"/>.
/// <para><c>&lt;SchedulerCategory Value="work" Title="Work" Color="var(--ib-chart-7)" /&gt;</c></para>
/// </summary>
[ModeSupport]
public sealed class SchedulerCategory : ComponentBase, IDisposable
{
    [CascadingParameter] private SchedulerCategories? Host { get; set; }

    /// <summary>What an appointment's <c>Category</c> gives for it, such as <c>"work"</c>: also its key in the page's address when hidden.</summary>
    [Parameter, EditorRequired] public string Value { get; set; } = "";

    /// <summary>Its name in the Categories menu. Defaults to <see cref="Value"/>.</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>Its appointments' color, a CSS color such as <c>var(--ib-chart-3)</c>, unless an appointment has its own. Defaults to the palette's next.</summary>
    [Parameter] public string? Color { get; set; }

    private string? _seen;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Host is null) throw new InvalidOperationException("SchedulerCategory goes inside a Scheduler.");
        Host.Register(this);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (string.IsNullOrWhiteSpace(Value)) throw new InvalidOperationException("SchedulerCategory needs a Value, what an appointment's Category gives for it, such as \"work\".");
        var seen = $"{Value}|{Title}|{Color}";
        if (seen == _seen) return;
        var first = _seen is null;
        _seen = seen;
        if (!first) Host!.Changed();
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
    }

    /// <inheritdoc />
    public void Dispose() => Host?.Unregister(this);
}

/// <summary>A Scheduler's categories, in order; it draws again when one comes, goes or changes.</summary>
internal sealed class SchedulerCategories(Action changed)
{
    private readonly List<SchedulerCategory> _items = [];

    public IReadOnlyList<SchedulerCategory> Items => _items;

    public void Register(SchedulerCategory category)
    {
        _items.Add(category);
        changed();
    }

    public void Changed() => changed();

    public void Unregister(SchedulerCategory category)
    {
        if (_items.Remove(category)) changed();
    }

    /// <summary>A category's color: its own, or the palette's in the order they're declared.</summary>
    public string ColorOf(SchedulerCategory category) => category.Color ?? $"var(--ib-chart-{_items.IndexOf(category) % 8 + 1})";
}
