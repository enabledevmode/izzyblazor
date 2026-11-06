using System.Collections;
using System.Data;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// A column read by name from rows whose shape is known only at run time: dictionaries,
/// <c>ExpandoObject</c> (so <c>IEnumerable&lt;dynamic&gt;</c>) and <c>DataRow</c>. Its
/// <see cref="Type"/> picks the filter, alignment and format, as a <see cref="PropertyColumn{TItem, TValue}"/>'s
/// property type does. Read-only: it isn't edited in a DataGrid.
/// </summary>
/// <example><code>@foreach (var (name, type) in columns)
/// {
///     &lt;DynamicColumn @key="name" Field="@name" Type="type" Sortable Filterable /&gt;
/// }</code></example>
public class DynamicColumn<TItem> : ColumnBase<TItem>
{
    private static readonly MethodInfo ReadMethod = typeof(DynamicColumn<TItem>).GetMethod(nameof(Read), BindingFlags.NonPublic | BindingFlags.Static)!;

    private (string Field, Type Type)? _built;
    private LambdaExpression? _value;
    private Func<TItem, object?>? _read;
    private Type _valueType = typeof(string);

    /// <summary>The name to read from each row: a dictionary key or a <c>DataRow</c>'s column name.</summary>
    [Parameter, EditorRequired] public string Field { get; set; } = default!;

    /// <summary>
    /// The values' type, such as <c>typeof(int)</c> or <c>typeof(DateTime)</c>. Defaults to text. A
    /// value of another type is converted (text parsed, as from a CSV file); one that doesn't
    /// convert shows empty and sorts as missing.
    /// </summary>
    [Parameter] public Type Type { get; set; } = typeof(string);

    /// <summary>A .NET format string for the value, such as <c>C</c>, <c>N0</c> or <c>yyyy-MM-dd</c>.</summary>
    [Parameter] public string? Format { get; set; }

    /// <inheritdoc />
    protected override object Shown => (base.Shown, Field, Type, Format);

    /// <inheritdoc />
    internal override string? ValueFormat => Format;

    /// <inheritdoc />
    internal override Type? ValueType => _valueType;

    /// <inheritdoc />
    private protected override Type? OwnGroupType => FilterConvert.KindOf(_valueType) == FieldKind.None ? null : _valueType;

    /// <inheritdoc />
    private protected override string? AggregateProblem(DataAggregate type) => ValueAggregateProblem(_valueType, type);

    /// <inheritdoc />
    protected override string? DefaultTitle => Field is null ? null : DataFields.Words(Field);

    /// <inheritdoc />
    protected override string? DefaultSortKey
    {
        get
        {
            // A key goes in the address and in filter text, so only letters, digits and underscores.
            var key = new string((Field ?? "").Where(c => char.IsLetterOrDigit(c) || c == '_').Select(char.ToLowerInvariant).ToArray());
            return key.Length == 0 ? null : key;
        }
    }

    /// <inheritdoc />
    protected override CellAlign DefaultAlign => IsNumeric(_valueType) ? CellAlign.End : CellAlign.Start;

    /// <inheritdoc />
    protected override bool HasSortExpression => true;

    /// <inheritdoc />
    internal override ColumnFilterKind FilterKind => ColumnFilter.KindOf(_valueType);

    /// <inheritdoc />
    protected override string? FilterProblem => FilterKind == ColumnFilterKind.None
        ? $"The \"{HeaderText}\" column can't filter {Type.Name} values: Filterable works with text, numbers, dates, enums and booleans."
        : null;

    /// <inheritdoc />
    protected override string? EditProblem => $"The \"{HeaderText}\" column can't be edited: a {nameof(DynamicColumn<TItem>)} is read-only.";

    /// <inheritdoc />
    internal override IReadOnlyList<(string Value, string Text)> FilterChoices(IzzyText text) => ColumnFilter.Choices(_valueType, text);

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        // Before the column joins the table, so the table doesn't draw it half made and hide the mistake.
        Check();
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        Check();
        if (_built != (Field, Type))
        {
            _built = (Field, Type);

            // Missing values are common in data like this, so a number or date column can always be empty.
            _valueType = Type.IsValueType && Nullable.GetUnderlyingType(Type) is null ? typeof(Nullable<>).MakeGenericType(Type) : Type;
            var row = Expression.Parameter(typeof(TItem), "r");
            var read = Expression.Call(ReadMethod, Expression.Convert(row, typeof(object)), Expression.Constant(Field), Expression.Constant(_valueType));
            _value = Expression.Lambda(Expression.Convert(read, _valueType), row);
            _read = Expression.Lambda<Func<TItem, object?>>(read, row).Compile();
        }

        base.OnParametersSet();
    }

    private void Check()
    {
        if (string.IsNullOrEmpty(Field))
        {
            throw new InvalidOperationException($"{nameof(DynamicColumn<TItem>)} needs a {nameof(Field)}.");
        }

        if (Type is null)
        {
            throw new InvalidOperationException($"The \"{Field}\" column's {nameof(Type)} is null: leave it out for text.");
        }

        if (typeof(TItem) != typeof(object) && !Readable(typeof(TItem)))
        {
            throw new InvalidOperationException($"{nameof(DynamicColumn<TItem>)} reads dictionaries, ExpandoObject and DataRow, not {typeof(TItem).Name}: use a {nameof(PropertyColumn<TItem, object>)} for a row with properties.");
        }
    }

    /// <inheritdoc />
    internal override DataField<TItem>? ToField(IzzyText text) => Key is not { } key || _value is null ? null : new()
    {
        Key = key,
        Value = _value,
        Caption = HeaderText,
        Format = Format,
        Sortable = CanSort,
        Filterable = CanFilter || (GroupBy is null && Table?.GroupsBy(this) == true),
        Searchable = FilterKind == ColumnFilterKind.Text,
        Groupable = GroupBy is null && CanGroup,
        Aggregatable = HasAggregate,
        AggregateBy = AggregateBy,
        Operators = RequestOperators(ColumnFilter.FieldOperators(this)),
        Case = Table?.FilterCase ?? FilterCase.IgnoreCase,
        Choices = FilterKind == ColumnFilterKind.Choice ? FilterChoices(text) : null,
        Filter = Filter,
        FilterValues = FilterValues,
    };

    internal override RenderFragment Cell(TItem item) => builder =>
    {
        var value = _read!(item);
        builder.AddContent(0, Format is not null && value is IFormattable formattable
            ? formattable.ToString(Format, CultureInfo.CurrentCulture)
            : value?.ToString());
    };

    private static bool Readable(Type type) =>
        typeof(IDictionary<string, object?>).IsAssignableFrom(type)
        || typeof(IReadOnlyDictionary<string, object?>).IsAssignableFrom(type)
        || typeof(IDictionary).IsAssignableFrom(type)
        || typeof(DataRow).IsAssignableFrom(type);

    // The row's value by name, as the column's type, or null when it's missing or doesn't convert.
    private static object? Read(object? row, string field, Type type)
    {
        var value = row switch
        {
            null => null,
            IDictionary<string, object?> d => d.TryGetValue(field, out var v) ? v : null,
            IReadOnlyDictionary<string, object?> d => d.TryGetValue(field, out var v) ? v : null,
            IDictionary d => d.Contains(field) ? d[field] : null,
            DataRow r => r.Table.Columns.Contains(field) ? r[field] : null,
            _ => throw new InvalidOperationException($"{nameof(DynamicColumn<TItem>)} reads dictionaries, ExpandoObject and DataRow, not {row.GetType().Name}."),
        };

        if (value is null or DBNull)
        {
            return null;
        }

        if (type == typeof(string))
        {
            return value as string ?? Convert.ToString(FilterConvert.Unwrap(value), CultureInfo.CurrentCulture);
        }

        return FilterConvert.TryConvert(type, value, out var typed) ? typed : null;
    }
}
