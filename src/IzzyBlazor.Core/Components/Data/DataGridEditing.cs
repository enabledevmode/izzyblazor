using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>How a <c>DataGrid</c> edits a row.</summary>
public enum DataGridEditMode
{
    /// <summary>It doesn't. The default.</summary>
    None,

    /// <summary>In a dialog, with a field for each editable column.</summary>
    Dialog,

    /// <summary>
    /// In the row, with a field in each editable column that's shown. A new row starts at the top of the
    /// page. Not with rows that load as they're needed, which could scroll it away.
    /// </summary>
    Row,

    /// <summary>
    /// One cell at a time, on an interactive page: a click, Enter, F2 or typing starts it, and Tab, Enter
    /// or a click elsewhere saves it. <c>OnSave</c> runs for each cell that changed. A new row is edited
    /// as in <see cref="Row"/>, and so is every row on a static page, where a round trip for each cell
    /// would make moving through them unusable.
    /// </summary>
    Cell,
}

/// <summary>
/// A save from a <c>DataGrid</c>, for its <c>OnSave</c>. With no errors added, the editor closes and the
/// rows load again. With errors, it stays open and shows them.
/// </summary>
/// <typeparam name="TItem">The type of the rows.</typeparam>
public sealed class DataGridSave<TItem>
{
    private readonly IReadOnlyList<PropertyInfo> _editable;
    private readonly EditContext _context;
    private readonly ValidationMessageStore _messages;

    internal DataGridSave(TItem item, TItem? original, bool isNew, IReadOnlyList<PropertyInfo> editable, EditContext context, ValidationMessageStore messages)
    {
        (Item, Original, IsNew, _editable, _context, _messages) = (item, original, isNew, editable, context, messages);
    }

    /// <summary>The row with its new values: a copy, and valid by its rules. Only editable columns have changed.</summary>
    public TItem Item { get; }

    /// <summary>The row as it was loaded, or the default for a new one.</summary>
    public TItem? Original { get; }

    /// <summary>Whether it's a new row, from <c>NewItem</c>.</summary>
    public bool IsNew { get; }

    internal bool HasErrors { get; private set; }

    /// <summary>Copies the editable columns' new values to <paramref name="target"/>, such as an entity just read from the database, and leaves the rest of it alone.</summary>
    public void CopyTo(TItem target)
    {
        foreach (var property in _editable)
        {
            property.SetValue(target, property.GetValue(Item));
        }
    }

    /// <summary>Reports a problem with one field, and shows it there: <c>save.AddError(o =&gt; o.Customer, "Taken")</c>.</summary>
    public void AddError<TValue>(Expression<Func<TItem, TValue>> field, string message)
    {
        ArgumentNullException.ThrowIfNull(field);
        var name = field.Body is MemberExpression { Expression: ParameterExpression } member
            ? member.Member.Name
            : throw new ArgumentException("An error goes on a property of the row, such as o => o.Customer.", nameof(field));
        Add(new FieldIdentifier(Item!, name), message);
    }

    /// <summary>Reports a problem with the save as a whole, shown at the top of the editor.</summary>
    public void AddError(string message) => Add(new FieldIdentifier(Item!, ""), message);

    private void Add(FieldIdentifier field, string message)
    {
        _messages.Add(field, message);
        HasErrors = true;
        _context.NotifyValidationStateChanged();
    }
}

/// <summary>Gets a static post's values as a row. It's a part of its own for <see cref="FormMappingScope"/>, which keeps each grid's post to itself.</summary>
internal sealed class DataGridEditBinding<TItem> : ComponentBase
{
    /// <summary>The form's name within its grid's scope.</summary>
    internal const string FormName = "edit";

    /// <summary>What the editors' names start with.</summary>
    internal const string Prefix = "ib-edit";

    // No FormName: the scope already limits it to this grid's form, and with one Blazor files a value
    // that won't parse under that name, where the form's validator never looks, so it would save.
    [SupplyParameterFromForm(Name = Prefix)]
    public TItem? Posted { get; set; }

    [Parameter] public RenderFragment<TItem?>? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, ChildContent, Posted);
}

/// <summary>Copies of a row, so that Cancel has nothing to undo.</summary>
internal static class DataGridEditCopy
{
    private static readonly Func<object, object> Clone = (Func<object, object>)Delegate.CreateDelegate(
        typeof(Func<object, object>),
        typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!);

    /// <summary>A shallow copy. Nested objects are shared, which is safe as only a row's own properties can be edited.</summary>
    public static TItem Of<TItem>(TItem item) => (TItem)Clone(item!);

    /// <summary>
    /// Whether Blazor can fill the type from a form with only the editable fields in it. It reports a
    /// constructor parameter with no value as an error, and the alternative, posting the others in hidden
    /// fields, would put values in the page that it doesn't show.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2090", Justification = "Only a static page asks, which runs on a server, not in a trimmed WebAssembly app.")]
    public static bool CanPost<TItem>() => typeof(TItem).GetConstructor(Type.EmptyTypes) is not null;

    /// <summary>
    /// Makes a post into the row it edits: whatever isn't editable comes from the row as loaded, so a
    /// forged field changes nothing. The posted object stays the model, not a copy of the row with the
    /// new values on it, since Blazor keeps a value that won't parse against the object it bound.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2090", Justification = "Only a static post gets here, which runs on a server, not in a trimmed WebAssembly app.")]
    public static TItem Restore<TItem>(TItem posted, TItem item, IReadOnlyList<PropertyInfo> editable)
    {
        foreach (var property in typeof(TItem).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0 && !editable.Contains(property))
            {
                property.SetValue(posted, property.GetValue(item));
            }
        }

        return posted;
    }
}
