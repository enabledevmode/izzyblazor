using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// A <see cref="Scheduler{TItem}"/>'s edit form: what it posts, on a static page too. Days and
/// times are apart, as the form shows them; an all-day appointment's times are left out.
/// </summary>
public sealed class SchedulerEditFields
{
    /// <summary>The title. Required.</summary>
    [Required]
    public string? Title { get; set; }

    /// <summary>The first day.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>The time it starts, unless it's all day.</summary>
    public TimeOnly StartTime { get; set; }

    /// <summary>The last day.</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>The time it ends, unless it's all day.</summary>
    public TimeOnly EndTime { get; set; }

    /// <summary>All day: the days only.</summary>
    public bool AllDay { get; set; }

    /// <summary>The description, when the Scheduler has one.</summary>
    public string? Description { get; set; }

    /// <summary>The category, when the Scheduler has categories: a <c>SchedulerCategory</c>'s <c>Value</c>.</summary>
    public string? Category { get; set; }

    /// <summary>Set by the form's Delete button.</summary>
    public bool Remove { get; set; }

    /// <summary>When it starts: the first day at its time, or at midnight all day.</summary>
    public DateTime Start => StartDate.ToDateTime(AllDay ? TimeOnly.MinValue : StartTime);

    /// <summary>When it ends: the last day at its time, or all day the midnight after it.</summary>
    public DateTime End => AllDay ? EndDate.AddDays(1).ToDateTime(TimeOnly.MinValue) : EndDate.ToDateTime(EndTime);
}

/// <summary>
/// A save from a <see cref="Scheduler{TItem}"/>, for its <c>OnSave</c>: the form's values, and the
/// appointment they're for (none for a new one). With no errors added, the form closes; with
/// errors, it stays open and shows them.
/// </summary>
/// <typeparam name="TItem">The appointments' type.</typeparam>
public sealed class SchedulerSave<TItem>
{
    private readonly Action<string> _error;
    private readonly Action<TItem> _copy;

    internal SchedulerSave(TItem? item, SchedulerEditFields fields, Action<TItem> copy, Action<string> error)
    {
        (Item, Fields, _copy, _error) = (item, fields, copy, error);
    }

    /// <summary>The appointment being edited, or the default for a new one.</summary>
    public TItem? Item { get; }

    /// <summary>Whether it's a new appointment.</summary>
    public bool IsNew => Item is null;

    /// <summary>The form's values.</summary>
    public SchedulerEditFields Fields { get; }

    /// <summary>The title.</summary>
    public string Title => Fields.Title ?? "";

    /// <summary>When it starts.</summary>
    public DateTime Start => Fields.Start;

    /// <summary>When it ends (all day: the midnight after its last day).</summary>
    public DateTime End => Fields.End;

    /// <summary>Whether it's all day.</summary>
    public bool AllDay => Fields.AllDay;

    /// <summary>The description, when the Scheduler has one.</summary>
    public string? Description => Fields.Description;

    /// <summary>The category, when the Scheduler has categories.</summary>
    public string? Category => Fields.Category;

    internal bool HasErrors { get; private set; }

    /// <summary>
    /// Sets the Scheduler's <c>Start</c>, <c>End</c>, <c>Title</c>, <c>AllDay</c>,
    /// <c>Description</c> and <c>Category</c> properties on <paramref name="target"/> from the form, such as a new
    /// appointment or one just read from the database, and leaves the rest of it alone.
    /// </summary>
    public void CopyTo(TItem target) => _copy(target);

    /// <summary>Reports a problem with the save, shown at the top of the form, which stays open.</summary>
    public void AddError(string message)
    {
        HasErrors = true;
        _error(message);
    }
}

/// <summary>A delete from a <see cref="Scheduler{TItem}"/>, for its <c>OnDelete</c>. With an error added, the form stays open and shows it.</summary>
/// <typeparam name="TItem">The appointments' type.</typeparam>
public sealed class SchedulerDelete<TItem>
{
    private readonly Action<string> _error;

    internal SchedulerDelete(TItem item, Action<string> error) => (Item, _error) = (item, error);

    /// <summary>The appointment to delete.</summary>
    public TItem Item { get; }

    internal bool HasErrors { get; private set; }

    /// <summary>Reports why it can't be deleted, shown at the top of the form, which stays open.</summary>
    public void AddError(string message)
    {
        HasErrors = true;
        _error(message);
    }
}

/// <summary>Gets a static post's values. A part of its own for <see cref="FormMappingScope"/>, which keeps each Scheduler's post to itself.</summary>
internal sealed class SchedulerEditBinding : ComponentBase
{
    /// <summary>The form's name within its Scheduler's scope.</summary>
    internal const string FormName = "edit";

    /// <summary>What the fields' names start with.</summary>
    internal const string Prefix = "ib-sched";

    [SupplyParameterFromForm(Name = Prefix)]
    public SchedulerEditFields? Posted { get; set; }

    [Parameter] public RenderFragment<SchedulerEditFields?>? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, ChildContent, Posted);
}

/// <summary>A drag's move, which the Scheduler's script posts: the appointment and its new times.</summary>
internal sealed class SchedulerMoveFields
{
    public string? Key { get; set; }

    public string? Start { get; set; }

    public string? End { get; set; }
}

/// <summary>Gets a static post of a drag's move. A part of its own for <see cref="FormMappingScope"/>.</summary>
internal sealed class SchedulerMoveBinding : ComponentBase
{
    internal const string FormName = "move";

    internal const string Prefix = "ib-sched-move";

    [SupplyParameterFromForm(Name = Prefix)]
    public SchedulerMoveFields? Posted { get; set; }

    [Parameter] public RenderFragment<SchedulerMoveFields?>? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, ChildContent, Posted);
}

/// <summary>Sets a property a lambda reads, such as <c>b =&gt; b.Start</c>, on an item.</summary>
internal static class SchedulerMembers
{
    public static void Set(LambdaExpression? read, object target, object? value)
    {
        if (read is null) return;
        var body = read.Body;
        while (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } convert) body = convert.Operand;
        if (body is not MemberExpression { Expression: ParameterExpression, Member: PropertyInfo { CanWrite: true } property }) return;
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        property.SetValue(target, value is null || type.IsInstanceOfType(value) ? value : Convert.ChangeType(value, type, System.Globalization.CultureInfo.InvariantCulture));
    }
}
