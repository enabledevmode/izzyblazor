using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;

namespace IzzyBlazor;

/// <summary>What a Scheduler's toolbar parts draw, from the Scheduler they're in: null when a part is off.</summary>
internal interface ISchedulerToolbar
{
    RenderFragment TodayPart { get; }

    RenderFragment ArrowsPart { get; }

    RenderFragment PeriodPart { get; }

    RenderFragment? CategoriesPart { get; }

    RenderFragment? ViewsPart { get; }

    RenderFragment? NewPart { get; }
}

/// <summary>
/// One of a <c>Scheduler</c>'s own toolbar parts, written in its <c>ToolbarContent</c> to put it
/// among your items, in the order they're written: <see cref="SchedulerToday"/>,
/// <see cref="SchedulerArrows"/>, <see cref="SchedulerPeriod"/>, <see cref="SchedulerCategories"/>,
/// <see cref="SchedulerViews"/>, <see cref="SchedulerNew"/>. A part you don't write keeps
/// its usual place. The Scheduler still decides whether a part shows (Categories with categories,
/// the view menu with more than one view, New with OnSave or an EditDialogForm). Write them directly in
/// <c>ToolbarContent</c>: inside a component of your own the Scheduler can't see them, and draws
/// its own as well.
/// </summary>
public abstract class SchedulerToolbarItem : ComponentBase, IToolbarItem, IDisposable
{
    private readonly ToolbarSlot _slot;

    private protected SchedulerToolbarItem() => _slot = new ToolbarSlot(this, GetType().Name);

    /// <summary>Where it sits, among the items of that position in the order they're written. The navigation's parts default to the start, the rest to the end.</summary>
    [Parameter] public ToolbarPosition? Position { get; set; }

    [CascadingParameter] private ToolbarContext? Toolbar { get; set; }

    [CascadingParameter] private ISchedulerToolbar? SchedulerValue { get; set; }

    private ISchedulerToolbar Scheduler => SchedulerValue ?? throw new InvalidOperationException($"{GetType().Name} must be in a Scheduler's ToolbarContent.");

    private protected abstract ToolbarPosition DefaultPosition { get; }

    private protected abstract RenderFragment? Part(ISchedulerToolbar scheduler);

    ToolbarPosition IToolbarItem.Position => Position ?? DefaultPosition;

    int IToolbarItem.Priority => 0;

    ToolbarOverflow IToolbarItem.Overflow => ToolbarOverflow.Never;

    bool IToolbarItem.Separator => false;

    // Off (no categories yet, one view, no OnSave), it keeps its place, so turned on later it
    // comes back where it was.
    bool IToolbarItem.Hidden => Part(Scheduler) is null;

    RenderFragment IToolbarItem.Bar => Part(Scheduler) ?? (_ => { });

    RenderFragment? IToolbarItem.Menu => null;

    double? IToolbarItem.Width => null;

    /// <inheritdoc />
    protected override void OnParametersSet() => _slot.Join(Toolbar);

    /// <inheritdoc />
    protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder) => _slot.Rendered();

    /// <inheritdoc />
    public void Dispose() => _slot.Dispose();

    // Which parts a Scheduler's ToolbarContent writes, so the rest go in their usual places.
    internal static HashSet<Type> Placed(RenderFragment? content)
    {
        var placed = new HashSet<Type>();
        if (content is null)
        {
            return placed;
        }

#pragma warning disable BL0006
        using var builder = new Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder();
        content(builder);
        var frames = builder.GetFrames();
        for (var i = 0; i < frames.Count; i++)
        {
            ref var frame = ref frames.Array[i];
            if (frame.FrameType == RenderTreeFrameType.Component && frame.ComponentType.IsAssignableTo(typeof(SchedulerToolbarItem)))
            {
                placed.Add(frame.ComponentType);
            }
        }
#pragma warning restore BL0006
        return placed;
    }
}

/// <summary>The Scheduler's Today link, placed in its <c>ToolbarContent</c>.</summary>
public sealed class SchedulerToday : SchedulerToolbarItem
{
    private protected override ToolbarPosition DefaultPosition => ToolbarPosition.Start;

    private protected override RenderFragment? Part(ISchedulerToolbar scheduler) => scheduler.TodayPart;
}

/// <summary>The Scheduler's previous and next arrows, placed in its <c>ToolbarContent</c>.</summary>
public sealed class SchedulerArrows : SchedulerToolbarItem
{
    private protected override ToolbarPosition DefaultPosition => ToolbarPosition.Start;

    private protected override RenderFragment? Part(ISchedulerToolbar scheduler) => scheduler.ArrowsPart;
}

/// <summary>The Scheduler's period ("November 2026"), which opens a month to go to any date, placed in its <c>ToolbarContent</c>.</summary>
public sealed class SchedulerPeriod : SchedulerToolbarItem
{
    private protected override ToolbarPosition DefaultPosition => ToolbarPosition.Start;

    private protected override RenderFragment? Part(ISchedulerToolbar scheduler) => scheduler.PeriodPart;
}

/// <summary>The Scheduler's Categories menu, placed in its <c>ToolbarContent</c>; it shows when the Scheduler has categories.</summary>
public sealed class SchedulerCategories : SchedulerToolbarItem
{
    private protected override ToolbarPosition DefaultPosition => ToolbarPosition.End;

    private protected override RenderFragment? Part(ISchedulerToolbar scheduler) => scheduler.CategoriesPart;
}

/// <summary>The Scheduler's view menu, placed in its <c>ToolbarContent</c>; it shows when there's more than one view.</summary>
public sealed class SchedulerViews : SchedulerToolbarItem
{
    private protected override ToolbarPosition DefaultPosition => ToolbarPosition.End;

    private protected override RenderFragment? Part(ISchedulerToolbar scheduler) => scheduler.ViewsPart;
}

/// <summary>The Scheduler's New button, placed in its <c>ToolbarContent</c>; it shows with OnSave or an EditDialogForm.</summary>
public sealed class SchedulerNew : SchedulerToolbarItem
{
    private protected override ToolbarPosition DefaultPosition => ToolbarPosition.End;

    private protected override RenderFragment? Part(ISchedulerToolbar scheduler) => scheduler.NewPart;
}
