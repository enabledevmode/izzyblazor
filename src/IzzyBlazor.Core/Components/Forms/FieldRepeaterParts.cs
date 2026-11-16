using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// A computed value in a <see cref="FieldRepeater{TItem}"/> row, such as a line's amount: shown under its own
/// column heading in the table layout and with its label in the stacked one. It isn't a field, so it doesn't post.
/// </summary>
/// <example>
/// <code>
/// &lt;FieldRepeaterValue Label="Amount"&gt;@line.Amount.ToString("C")&lt;/FieldRepeaterValue&gt;
/// </code>
/// </example>
public sealed class FieldRepeaterValue : ComponentBase
{
    /// <summary>The column heading, and the value's label when the rows are stacked. Required.</summary>
    [Parameter, EditorRequired] public string Label { get; set; } = "";

    /// <summary>The column's width in the table layout, such as <c>Length.Rem(7)</c>. Unset, it shares the room left.</summary>
    [Parameter] public Length? Width { get; set; }

    /// <summary>Where the value sits in its column, such as <see cref="CellAlign.End"/> for an amount.</summary>
    [Parameter] public CellAlign? Align { get; set; }

    /// <summary>The value.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    [CascadingParameter] internal FieldRepeaterRow? Row { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Row is null)
        {
            throw new InvalidOperationException($"{nameof(FieldRepeaterValue)} goes in a {nameof(FieldRepeater<object>)}'s row.");
        }

        Row.Register(Label, Width is { } width ? StyleValues.Length(width) : null, Align, member: null, required: false);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", Align == CellAlign.End ? "ib-repeat-value ib-repeat-end" : "ib-repeat-value");
        builder.OpenElement(2, "span");
        builder.AddAttribute(3, "class", "ib-repeat-value-label");
        builder.AddContent(4, Label);
        builder.CloseElement();
        builder.OpenElement(5, "span");
        builder.AddAttribute(6, "class", "ib-repeat-value-text");
        builder.AddContent(7, ChildContent);
        builder.CloseElement();
        builder.CloseElement();
    }
}

/// <summary>
/// One row of a <see cref="FieldRepeater{TItem}"/>, cascaded to its fields: it names their inputs for the list
/// (<c>Lines[2].Description</c>), and the first row tells the repeater its columns.
/// </summary>
internal sealed class FieldRepeaterRow(object? item, int index, string? prefix, FieldRepeaterColumns? columns)
{
    public object? Item { get; } = item;

    public int Index { get; } = index;

    /// <summary>
    /// The posted name of a field bound to this row's item (<c>line.Description</c> → <c>Lines[2].Description</c>),
    /// or null for one bound to anything else, which keeps its own name.
    /// </summary>
    public string? NameFor(LambdaExpression? expression)
    {
        if (prefix is null || expression is null || FieldNames.Path(expression) is not { Count: > 1 } path || !ReferenceEquals(FieldNames.Root(expression), Item))
        {
            return null;
        }

        return prefix + "." + string.Join('.', path.Skip(1));
    }

    /// <summary>A field or value in the row: the first row's make the table's columns.</summary>
    public void Register(string label, string? width, CellAlign? align, string? member, bool required) =>
        columns?.Register(label, width, align, member, required);
}

/// <summary>The table layout's columns, as the first row's fields and values report them, in order.</summary>
internal sealed class FieldRepeaterColumns(Action changed)
{
    private readonly List<(string Label, string? Width, CellAlign? Align, string? Member, bool Required)> _columns = [];

    public IReadOnlyList<(string Label, string? Width, CellAlign? Align, string? Member, bool Required)> All => _columns;

    public void Register(string label, string? width, CellAlign? align, string? member, bool required)
    {
        var at = _columns.FindIndex(c => c.Label == label);
        var column = (label, width, align, member, required);
        if (at >= 0)
        {
            if (_columns[at] != column)
            {
                _columns[at] = column;
                changed();
            }

            return;
        }

        _columns.Add(column);
        changed();
    }
}

/// <summary>Posted names from binding expressions, as Blazor writes them: <c>Draft.Lines</c>, <c>line.Description</c>.</summary>
internal static class FieldNames
{
    /// <summary>The member names from the root (a captured variable or a property of the component) to the end, or null for anything else.</summary>
    public static IReadOnlyList<string>? Path(LambdaExpression expression)
    {
        var names = new List<string>();
        var node = Unwrap(expression.Body);
        while (node is MemberExpression member)
        {
            names.Add(member.Member.Name);
            if (Unwrap(member.Expression) is ConstantExpression)
            {
                names.Reverse();
                return names;
            }

            node = Unwrap(member.Expression);
        }

        return null;
    }

    /// <summary>The value the expression starts from (<c>line</c> in <c>line.Description</c>), or null.</summary>
    public static object? Root(LambdaExpression expression)
    {
        var node = Unwrap(expression.Body);
        while (node is MemberExpression member)
        {
            if (Unwrap(member.Expression) is ConstantExpression constant)
            {
                return member.Member switch
                {
                    System.Reflection.FieldInfo field => field.GetValue(constant.Value),
                    System.Reflection.PropertyInfo property => property.GetValue(constant.Value),
                    _ => null,
                };
            }

            node = Unwrap(member.Expression);
        }

        return null;
    }

    /// <summary>The posted name of a binding expression, or null.</summary>
    public static string? Format(LambdaExpression? expression) =>
        expression is not null && Path(expression) is { } path ? string.Join('.', path) : null;

    private static Expression? Unwrap(Expression? node)
    {
        while (node is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
        {
            node = unary.Operand;
        }

        return node;
    }
}

/// <summary>
/// Reads a post's repeater command (<c>ib-repeat</c>: which repeater, add or remove, which row) for its Form, so
/// the post redraws the rows instead of submitting.
/// </summary>
internal sealed class FormRepeatCommand : ComponentBase
{
    internal const string FieldName = "ib-repeat";

    [SupplyParameterFromForm(Name = FieldName)]
    public string? Value { get; set; }

    [Parameter] public Action<bool>? Report { get; set; }

    protected override void OnParametersSet() => Report?.Invoke(!string.IsNullOrEmpty(Value));
}

/// <summary>
/// How a form's error summary names repeater rows' messages ("Item 3, Quantity: Enter a quantity."), kept on the
/// EditContext for its FormErrors.
/// </summary>
internal sealed class RepeaterSummaryNames
{
    private static readonly object Key = new();

    private readonly List<Func<IEnumerable<(FieldIdentifier Field, string Name)>>> _sources = [];

    public static RepeaterSummaryNames For(EditContext context)
    {
        if (!context.Properties.TryGetValue(Key, out var names))
        {
            names = new RepeaterSummaryNames();
            context.Properties[Key] = names;
        }

        return (RepeaterSummaryNames)names;
    }

    public static RepeaterSummaryNames? Of(EditContext context) =>
        context.Properties.TryGetValue(Key, out var names) ? (RepeaterSummaryNames)names : null;

    public void Add(Func<IEnumerable<(FieldIdentifier Field, string Name)>> source) => _sources.Add(source);

    public void Remove(Func<IEnumerable<(FieldIdentifier Field, string Name)>> source) => _sources.Remove(source);

    public IEnumerable<(FieldIdentifier Field, string Name)> Fields => _sources.SelectMany(s => s());
}

/// <summary>How a <see cref="FieldRepeater{TItem}"/> lays out its rows.</summary>
public enum RepeaterLayout
{
    /// <summary>A table while the repeater is wide enough for its columns, else stacked. The default.</summary>
    Auto,

    /// <summary>Always a table: a row per item, the fields' labels as column headings.</summary>
    Table,

    /// <summary>Always stacked: each row a group headed by its name, its fields one under another.</summary>
    Stacked,
}
