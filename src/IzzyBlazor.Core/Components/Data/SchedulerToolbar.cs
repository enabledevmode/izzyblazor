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

    /// <summary>An id for the Scheduler's element, which its parts outside it carry so its script finds them.</summary>
    string PartsId { get; }
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
/// <remarks>
/// A part can also go outside the Scheduler, such as in a <c>Tile</c>'s <c>HeaderEnd</c>: it acts on
/// the Scheduler whose <c>id</c> is <see cref="For"/>, or else the one Scheduler in its <c>Tile</c> or
/// <c>Card</c>, and the Scheduler leaves its own out. With every part outside and no
/// <c>ToolbarContent</c>, the Scheduler has no toolbar.
/// </remarks>
public abstract class SchedulerToolbarItem : ComponentBase, IToolbarItem, IDisposable
{
    private readonly ToolbarSlot _slot;

    private protected SchedulerToolbarItem() => _slot = new ToolbarSlot(this, GetType().Name);

    /// <summary>Where it sits, among the items of that position in the order they're written. The navigation's parts default to the start, the rest to the end.</summary>
    [Parameter] public ToolbarPosition? Position { get; set; }

    [CascadingParameter] private ToolbarContext? Toolbar { get; set; }

    /// <summary>
    /// The <c>id</c> of the Scheduler it acts on, when it's outside the Scheduler. Leave it out in the
    /// Scheduler's <c>ToolbarContent</c>, or in a <c>Tile</c> or <c>Card</c> with one Scheduler: it
    /// acts on that one.
    /// </summary>
    [Parameter] public string? For { get; set; }

    [CascadingParameter] private ISchedulerToolbar? SchedulerValue { get; set; }

    [CascadingParameter] private HeaderHost? Host { get; set; }

    [Inject] private IServiceProvider Services { get; set; } = default!;

    private PartLink? _link;
    private Action? _redraw;
    private bool _joined;

    // Outside the Scheduler, the one it draws from once that one has drawn.
    private ISchedulerToolbar? Target => _link is not null ? _link.Target as ISchedulerToolbar : SchedulerValue;

    private protected abstract ToolbarPosition DefaultPosition { get; }

    private protected abstract RenderFragment? Part(ISchedulerToolbar scheduler);

    ToolbarPosition IToolbarItem.Position => Position ?? DefaultPosition;

    int IToolbarItem.Priority => 0;

    ToolbarOverflow IToolbarItem.Overflow => ToolbarOverflow.Never;

    bool IToolbarItem.Separator => false;

    // Off (no categories yet, one view, no OnSave), it keeps its place, so turned on later it
    // comes back where it was.
    bool IToolbarItem.Hidden => Target is null || Part(Target) is null;

    RenderFragment IToolbarItem.Bar => Target is { } target && Part(target) is { } part ? OutsideParts.Wrap(_link is null ? null : target.PartsId, "data-ib-scheduler", null, part) : _ => { };

    RenderFragment? IToolbarItem.Menu => null;

    double? IToolbarItem.Width => null;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (For is null && SchedulerValue is not null)
        {
            Leave();
            _joined = true;
            _slot.Join(Toolbar);
            return;
        }

        var link = For is { Length: > 0 } id ? PartLinks.For(Services, typeof(ISchedulerToolbar), id, GetType().Name)
            : Host?.Parts(typeof(ISchedulerToolbar))
            ?? throw new InvalidOperationException($"{GetType().Name} must be in a Scheduler's ToolbarContent, in a Tile or Card with a Scheduler, or name its Scheduler with For.");
        if (!ReferenceEquals(link, _link))
        {
            Leave();
            _link = link;
            link.Place(GetType(), _redraw ??= StateHasChanged);
        }

        // In a toolbar of the page's, it's one of its items.
        _joined = Toolbar is not null && !ReferenceEquals(Toolbar, ToolbarContext.Outside);
        if (_joined)
        {
            _slot.Join(Toolbar);
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
    {
        if (_joined)
        {
            _slot.Rendered();
        }
        else if (Target is { } target && Part(target) is { } part)
        {
            // As in the Scheduler's own toolbar.
            builder.AddContent(0, OutsideParts.Wrap(target.PartsId, "data-ib-scheduler", ButtonGroup.Defaults(Accent.Neutral, ButtonVariant.Ghost, ControlSize.Md), part));
        }
    }

    private void Leave()
    {
        if (_link is not null)
        {
            _link.Unplace(GetType(), _redraw!);
            _link = null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Leave();
        _slot.Dispose();
    }

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
