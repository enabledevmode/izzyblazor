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
    /// <summary>A button in the row. The default.</summary>
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

    /// <summary>Where its actions show, unless one says otherwise. <see cref="RowActionOverflow.Always"/> makes it a ⋮ menu.</summary>
    [Parameter] public RowActionOverflow Overflow { get; set; }

    [CascadingParameter] internal TableViewBase<TItem>? Table { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Table is not DataGrid<TItem> grid)
        {
            throw new InvalidOperationException("An ActionColumn must be inside a DataGrid.");
        }

        grid.ActionColumn = this;
    }

    /// <inheritdoc />
    protected override bool ShouldRender() => false;
}

/// <summary>What a row's actions draw: as buttons, or in its menu.</summary>
internal sealed record RowActionScope(bool InMenu, RowActionOverflow Overflow, Func<bool, RenderFragment?> Edit, Func<bool, RenderFragment?> Delete);

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

        if (Visible && (Overflow ?? scope.Overflow) == RowActionOverflow.Always == scope.InMenu)
        {
            Draw(builder, scope);
        }
    }

    // What a row's actions come to, read without drawing them, as a toolbar's items are.
    internal static (int Buttons, bool Menu) Count(RenderFragment content, RowActionOverflow fallback, bool canEdit, bool canDelete)
    {
#pragma warning disable BL0006
        using var builder = new RenderTreeBuilder();
        content(builder);
        var frames = builder.GetFrames();
        var (buttons, menu) = (0, false);
        for (var i = 0; i < frames.Count; i++)
        {
            ref var frame = ref frames.Array[i];
            if (frame.FrameType != RenderTreeFrameType.Component
                || !frame.ComponentType.IsAssignableTo(typeof(RowActionBase))
                || (frame.ComponentType == typeof(EditAction) && !canEdit)
                || (frame.ComponentType == typeof(DeleteAction) && !canDelete))
            {
                continue;
            }

            var (visible, overflow) = (true, fallback);
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
            }

            if (visible && overflow == RowActionOverflow.Always)
            {
                menu = true;
            }
            else if (visible)
            {
                buttons++;
            }
        }
#pragma warning restore BL0006
        return (buttons, menu);
    }
}

/// <summary>The row's edit action: a pencil, or an item in its menu. Drawn only where the row can be edited.</summary>
public sealed class EditAction : RowActionBase
{
    private protected override void Draw(RenderTreeBuilder builder, RowActionScope scope) =>
        builder.AddContent(0, scope.Edit(scope.InMenu));
}

/// <summary>The row's delete action, which asks first: a trash can, or an item in its menu. Drawn only where the row can be deleted.</summary>
public sealed class DeleteAction : RowActionBase
{
    private protected override void Draw(RenderTreeBuilder builder, RowActionScope scope) =>
        builder.AddContent(0, scope.Delete(scope.InMenu));
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
