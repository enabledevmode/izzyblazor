using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;

namespace IzzyBlazor;

/// <summary>
/// A delete from a <c>DataGrid</c>, for its <c>OnDelete</c>. With no error added, the dialog closes and
/// the rows load again.
/// </summary>
/// <typeparam name="TItem">The type of the rows.</typeparam>
public sealed class DataGridDelete<TItem>
{
    private readonly Action<string> _addError;

    internal DataGridDelete(TItem item, Action<string> addError) => (Item, _addError) = (item, addError);

    /// <summary>The row to delete.</summary>
    public TItem Item { get; }

    internal bool HasErrors { get; private set; }

    /// <summary>Reports why it can't be deleted, shown in the dialog, which stays open.</summary>
    public void AddError(string message)
    {
        _addError(message);
        HasErrors = true;
    }
}

/// <summary>Where a row's action shows in a <c>DataGrid</c>'s <see cref="ActionColumn{TItem}"/>.</summary>
public enum RowActionOverflow
{
    /// <summary>
    /// A button in the row, until the grid hides columns to fit (<c>DataGridSmallScreen.HideColumns</c>);
    /// then in the row's ⋮ menu. The default.
    /// </summary>
    Auto,

    /// <summary>A button in the row, always.</summary>
    Never,

    /// <summary>In the row's ⋮ menu.</summary>
    Always,
}

/// <summary>
/// The column that holds a row's actions, at the end of a <c>DataGrid</c>. A grid with editing has one
/// by itself, with Edit and Delete; write one to set their order, or to add your own actions.
/// </summary>
/// <example>
/// <code>
/// &lt;ActionColumn Context="o"&gt;
///     &lt;EditAction /&gt;
///     &lt;RowAction Icon="Lucide.Copy" Label="Duplicate" Href="@($"/orders/new?from={o.Id}")" /&gt;
///     &lt;DeleteAction /&gt;
/// &lt;/ActionColumn&gt;
/// </code>
/// </example>
public sealed class ActionColumn<TItem> : ComponentBase
{
    /// <summary>The row's actions: <see cref="EditAction"/>, <see cref="DeleteAction"/> and <see cref="RowAction"/>. <c>context</c> is the row.</summary>
    [Parameter] public RenderFragment<TItem>? ChildContent { get; set; }

    /// <summary>
    /// Where its actions show, unless one says otherwise. <see cref="RowActionOverflow.Always"/> makes it
    /// a ⋮ menu. The default, <see cref="RowActionOverflow.Auto"/>, is buttons, which move into the menu
    /// once the grid hides columns to fit (<c>DataGridSmallScreen.HideColumns</c>).
    /// </summary>
    [Parameter] public RowActionOverflow Overflow { get; set; }

    [CascadingParameter] internal TableViewBase<TItem>? Table { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Table is not GridViewBase<TItem> grid)
        {
            throw new InvalidOperationException("An ActionColumn must be inside a DataGrid or a TreeList.");
        }

        grid.ActionColumn = this;
    }

    /// <inheritdoc />
    protected override bool ShouldRender() => false;
}

/// <summary>What a row's actions draw: as buttons, or in its menu.</summary>
internal sealed record RowActionScope(bool InMenu, RowActionOverflow Overflow, bool Folds, Func<bool, string?, RenderFragment?> Edit, Func<bool, string?, RenderFragment?> Delete, Func<bool, RenderFragment?> AddUnder);

/// <summary>An action in a <c>DataGrid</c>'s <see cref="ActionColumn{TItem}"/>: drawn once as a button or a menu item, by where it shows.</summary>
public abstract class RowActionBase : ComponentBase
{
    /// <summary>Whether this row has it.</summary>
    [Parameter] public bool Visible { get; set; } = true;

    /// <summary>Where it shows. Defaults to the column's <c>Overflow</c>.</summary>
    [Parameter] public RowActionOverflow? Overflow { get; set; }

    [CascadingParameter] private RowActionScope? Scope { get; set; }

    private protected abstract void Draw(RenderTreeBuilder builder, RowActionScope scope);

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (Scope is not { } scope)
        {
            throw new InvalidOperationException($"{GetType().Name} must be inside an ActionColumn.");
        }

        if (!Visible)
        {
            return;
        }

        var overflow = Overflow ?? scope.Overflow;
        if (overflow == RowActionOverflow.Auto && scope.Folds)
        {
            // Drawn in both places; the grid's style shows the button while every column shows,
            // and the menu's item once one is hidden.
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", scope.InMenu ? "ib-fold-off" : "ib-fold-on");
            Draw(builder, scope);
            builder.CloseElement();
        }
        else if ((overflow == RowActionOverflow.Always) == scope.InMenu)
        {
            Draw(builder, scope);
        }
    }

    // What a row's actions come to, read without drawing them, as a toolbar's items are.
    // Folding: the grid hides columns, so Auto ones move into the menu then (Auto counts them).
    internal static (int Buttons, bool Menu, int Auto) Count(RenderFragment content, RowActionOverflow fallback, bool canEdit, bool canDelete, bool folds)
    {
#pragma warning disable BL0006
        using var builder = new RenderTreeBuilder();
        content(builder);
        var frames = builder.GetFrames();
        var (buttons, menu, auto) = (0, false, 0);
        for (var i = 0; i < frames.Count; i++)
        {
            ref var frame = ref frames.Array[i];
            if (frame.FrameType != RenderTreeFrameType.Component
                || !frame.ComponentType.IsAssignableTo(typeof(RowActionBase)))
            {
                continue;
            }

            var (visible, overflow, reason) = (true, fallback, false);
            for (var a = i + 1; a < frames.Count && frames.Array[a].FrameType == RenderTreeFrameType.Attribute; a++)
            {
                ref var attribute = ref frames.Array[a];
                if (attribute.AttributeName == nameof(Visible) && attribute.AttributeValue is bool shown)
                {
                    visible = shown;
                }
                else if (attribute.AttributeName == nameof(Overflow) && attribute.AttributeValue is RowActionOverflow set)
                {
                    overflow = set;
                }
                else if (attribute.AttributeName == nameof(EditAction.DisabledReason) && attribute.AttributeValue is string)
                {
                    reason = true;
                }
            }

            // One the row can't use shows only if it says why.
            if ((frame.ComponentType == typeof(EditAction) && !canEdit && !reason) || (frame.ComponentType == typeof(DeleteAction) && !canDelete && !reason))
            {
                continue;
            }

            if (visible && overflow == RowActionOverflow.Always)
            {
                menu = true;
            }
            else if (visible && overflow == RowActionOverflow.Auto && folds)
            {
                auto++;
            }
            else if (visible)
            {
                buttons++;
            }
        }
#pragma warning restore BL0006
        return (buttons, menu, auto);
    }
}

/// <summary>The row's edit action: a pencil, or an item in its menu. Drawn only where the row can be edited.</summary>
public sealed class EditAction : RowActionBase
{
    /// <summary>
    /// Where <c>ItemEditable</c> says no, the action stays, disabled, with this as its tooltip, in place
    /// of leaving a gap. The reason can depend on the row.
    /// </summary>
    [Parameter] public string? DisabledReason { get; set; }

    private protected override void Draw(RenderTreeBuilder builder, RowActionScope scope) =>
        builder.AddContent(0, scope.Edit(scope.InMenu, DisabledReason));
}

/// <summary>The row's delete action, which asks first: a trash can, or an item in its menu. Drawn only where the row can be deleted.</summary>
public sealed class DeleteAction : RowActionBase
{
    /// <summary>
    /// Where <c>ItemDeletable</c> says no, the action stays, disabled, with this as its tooltip, in place
    /// of leaving a gap. The reason can depend on the row.
    /// </summary>
    [Parameter] public string? DisabledReason { get; set; }

    private protected override void Draw(RenderTreeBuilder builder, RowActionScope scope) =>
        builder.AddContent(0, scope.Delete(scope.InMenu, DisabledReason));
}

/// <summary>
/// A TreeList row's Add child action: adds a row under it, from the tree list's <c>NewChild</c>, in
/// the edit dialog or in place under it, and opens it so the new row shows.
/// </summary>
public sealed class AddChildAction : RowActionBase
{
    private protected override void Draw(RenderTreeBuilder builder, RowActionScope scope) =>
        builder.AddContent(0, scope.AddUnder(scope.InMenu));
}

/// <summary>A row's field in the editor, for a column's <c>EditTemplate</c>.</summary>
/// <typeparam name="TItem">The type of the rows.</typeparam>
public sealed class DataGridEditField<TItem>
{
    internal DataGridEditField(TItem item, string name, IReadOnlyDictionary<string, object> attributes) => (Item, Name, Attributes) = (item, name, attributes);

    /// <summary>The copy of the row being edited, to bind the field to.</summary>
    public TItem Item { get; }

    /// <summary>The name the field posts under.</summary>
    public string Name { get; }

    /// <summary>What a library field needs to fit in: its name, label, hint and span. Pass them with <c>@attributes</c>.</summary>
    public IReadOnlyDictionary<string, object> Attributes { get; }
}

/// <summary>What a <c>DataGrid</c>'s <c>EditDialogForm</c> gets.</summary>
/// <typeparam name="TItem">The type of the rows.</typeparam>
public sealed class DataGridEditFormContext<TItem>
{
    private readonly Func<Task> _close;

    internal DataGridEditFormContext(TItem? item, bool isNew, RenderFragment cancel, Func<Task> close) => (Item, IsNew, Cancel, _close) = (item, isNew, cancel, close);

    /// <summary>The row as loaded, which isn't a copy, or the default for a new one.</summary>
    public TItem? Item { get; }

    /// <summary>Whether it's a new row.</summary>
    public bool IsNew { get; }

    /// <summary>The Cancel button, for your <c>DialogFooter</c>.</summary>
    public RenderFragment Cancel { get; }

    /// <summary>Closes the dialog and loads the rows again, once your form has saved.</summary>
    public Task CloseAsync() => _close();
}

/// <summary>Finds a column's field for an <see cref="EditField"/>.</summary>
internal sealed record DataGridEditFields(Func<EditField, RenderFragment?> Field);

/// <summary>A column's generated field, placed in a <c>DataGrid</c>'s <c>EditDialog</c>.</summary>
public sealed class EditField : ComponentBase
{
    /// <summary>The column's key: its <c>SortKey</c>, or its property's name in lower case, as in the address.</summary>
    [Parameter, EditorRequired] public string Key { get; set; } = "";

    /// <summary>The field's label, if not the column's title.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>The field's hint.</summary>
    [Parameter] public string? Hint { get; set; }

    /// <summary>The columns of the form the field spans, such as 2 or <c>GridSpan.Full</c>.</summary>
    [Parameter] public Responsive<int> ColSpan { get; set; }

    [CascadingParameter] private DataGridEditFields? Fields { get; set; }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (Fields is null)
        {
            throw new InvalidOperationException("An EditField must be inside a DataGrid's EditDialog.");
        }

        builder.AddContent(0, Fields.Field(this) ?? throw new InvalidOperationException($"An EditField names \"{Key}\", which isn't an editable column of this DataGrid."));
    }

#pragma warning disable BL0006
    // The attribute's owner: the nearest frame before it that isn't an attribute.
    private static bool IsEditField(ArrayRange<RenderTreeFrame> frames, int attribute)
    {
        var i = attribute - 1;
        while (i >= 0 && frames.Array[i].FrameType == RenderTreeFrameType.Attribute)
        {
            i--;
        }

        return i >= 0 && frames.Array[i].FrameType == RenderTreeFrameType.Component && frames.Array[i].ComponentType == typeof(EditField);
    }
#pragma warning restore BL0006

    // The keys a layout places, in order, read without drawing it.
    internal static List<string> KeysIn(RenderFragment? layout)
    {
        var keys = new List<string>();
        if (layout is null)
        {
            return keys;
        }

#pragma warning disable BL0006
        using var builder = new RenderTreeBuilder();
        layout(builder);
        var frames = builder.GetFrames();
        for (var i = 0; i < frames.Count; i++)
        {
            if (frames.Array[i].FrameType != RenderTreeFrameType.Attribute)
            {
                continue;
            }

            // In order, and into what's written inside other components, such as a Fieldset. Inside a
            // component of your own that draws them itself, the grid can't see them.
            if (frames.Array[i].AttributeValue is RenderFragment inner)
            {
                keys.AddRange(KeysIn(inner));
            }
            else if (frames.Array[i].AttributeName == nameof(Key) && frames.Array[i].AttributeValue is string key && IsEditField(frames, i))
            {
                keys.Add(key);
            }
        }
#pragma warning restore BL0006
        return keys;
    }
}

/// <summary>An action of your own on a row: an icon button with a tooltip, or an item in its menu.</summary>
/// <example><code>&lt;RowAction Icon="Lucide.Copy" Label="@($"Duplicate {o.Id}")" Href="@($"/orders/new?from={o.Id}")" /&gt;</code></example>
public sealed class RowAction : RowActionBase
{
    /// <summary>Its icon.</summary>
    [Parameter] public IconData? Icon { get; set; }

    /// <summary>What it does: the button's name and tooltip, or the item's text. Say which row, if it isn't clear from a list of them.</summary>
    [Parameter, EditorRequired] public string Label { get; set; } = "";

    /// <summary>Makes it a link.</summary>
    [Parameter] public string? Href { get; set; }

    /// <summary>The form field name the button posts under, with <see cref="Value"/>, and <see cref="Form"/> as the table can't hold a form of its own.</summary>
    [Parameter] public string? Name { get; set; }

    /// <summary>The value the button posts.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>The id of the form it submits. Setting it makes it a submit button.</summary>
    [Parameter] public string? Form { get; set; }

    /// <summary>Whether it can't be used.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Runs when it's used, on interactive pages.</summary>
    [Parameter, ModeSupport(NoScript = ModeLevel.None, Static = ModeLevel.None)]
    public EventCallback OnClick { get; set; }

    private protected override void Draw(RenderTreeBuilder builder, RowActionScope scope)
    {
        if (scope.InMenu)
        {
            builder.OpenComponent<MenuItem>(0);
            builder.AddComponentParameter(1, nameof(MenuItem.Icon), Icon);
            builder.AddComponentParameter(2, nameof(MenuItem.ChildContent), (RenderFragment)(b => b.AddContent(0, Label)));
        }
        else
        {
            builder.OpenComponent<IconButton>(0);
            builder.AddComponentParameter(1, nameof(IconButton.Icon), Icon);
            builder.AddComponentParameter(2, nameof(IconButton.Label), Label);
            builder.AddComponentParameter(3, nameof(IconButton.Variant), ButtonVariant.Ghost);
            builder.AddComponentParameter(4, nameof(IconButton.Accent), Accent.Neutral);
            builder.AddComponentParameter(5, nameof(IconButton.Size), ControlSize.Sm);
        }

        builder.AddComponentParameter(6, "Href", Href);
        builder.AddComponentParameter(7, "Disabled", Disabled);
        if (Href is null)
        {
            builder.AddComponentParameter(8, "Name", Name);
            builder.AddComponentParameter(9, "Value", Value);
            builder.AddComponentParameter(10, "Form", Form);
            builder.AddComponentParameter(11, "Submit", Form is not null);
            if (OnClick.HasDelegate)
            {
                builder.AddComponentParameter(12, "OnClick", OnClick);
            }
        }

        builder.CloseComponent();
    }
}
