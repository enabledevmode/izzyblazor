using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// A column read by name from rows whose shape is known only at run time: dictionaries,
/// <c>ExpandoObject</c> (so <c>IEnumerable&lt;dynamic&gt;</c>), <c>DataRow</c> and <c>DataRowView</c>. Its
/// <see cref="Type"/> picks the filter, alignment and format, as a <see cref="PropertyColumn{TItem, TValue}"/>'s
/// property type does. The title is the field name in words, and the sort key its letters and digits
/// in lower case, with an underscore for each run of anything else (<c>Q1 2026</c> is <c>q1_2026</c>); set <see cref="ColumnBase{TItem}.Title"/>
/// or <see cref="ColumnBase{TItem}.SortKey"/> to change them. Read-only: it isn't edited in a DataGrid.
/// </summary>
/// <example><code>@foreach (var (name, type) in columns)
/// {
///     &lt;DynamicColumn @key="name" Field="@name" Type="type" Sortable Filterable /&gt;
/// }</code></example>
public class DynamicColumn<TItem> : ColumnBase<TItem>
{
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
    private protected override object? ReadShape => (Key, Field, Type);

    /// <inheritdoc />
    internal override Type? ValueType => _valueType;

    /// <inheritdoc />
    private protected override Type? OwnGroupType => FilterConvert.KindOf(_valueType) == FieldKind.None ? null : _valueType;

    /// <inheritdoc />
    private protected override string? AggregateProblem(DataAggregate type) => ValueAggregateProblem(_valueType, type);

    /// <inheritdoc />
    protected override string? DefaultTitle => Field is null ? null : DataFields.Words(Field);

    /// <inheritdoc />
    protected override string? DefaultSortKey => DynamicRead.KeyOf(Field);

    /// <inheritdoc />
    protected override CellAlign DefaultAlign => IsNumeric(_valueType) ? CellAlign.End : CellAlign.Start;

    /// <inheritdoc />
    protected override bool HasSortExpression => true;

    /// <inheritdoc />
    internal override ColumnFilterKind FilterKind => ColumnFilter.KindOf(_valueType);

    /// <inheritdoc />
    protected override string? FilterProblem => FilterKind == ColumnFilterKind.None
        ? $"The \"{HeaderText}\" column can't filter {Type.Name} values: Filterable works with text, numbers, dates, enums and booleans. Set {nameof(Type)} to one of those, or leave out {nameof(Filterable)}."
        : null;

    /// <inheritdoc />
    protected override string? EditProblem => $"The \"{HeaderText}\" column can't be edited: a {nameof(DynamicColumn<TItem>)} is read-only. Leave out {nameof(Editable)}, or use a {nameof(PropertyColumn<TItem, object>)} to edit a property.";

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

            _valueType = DynamicRead.ValueTypeOf(Type);
            (_value, _read) = DynamicRead.Reader<TItem>(Field, _valueType);
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

        if (typeof(TItem) != typeof(object) && !DynamicRead.Readable(typeof(TItem)))
        {
            throw new InvalidOperationException($"{nameof(DynamicColumn<TItem>)} reads dictionaries, ExpandoObject and DataRow, not {typeof(TItem).Name}: use a {nameof(PropertyColumn<TItem, object>)} for a row with properties.");
        }
    }

    /// <inheritdoc />
    internal override DataField<TItem>? ToField(IzzyText text) => Key is not { } key || _value is null ? null : new()
    {
        Key = key,
        Value = _value,
        Caption = FullName,
        Format = Format,
        Sortable = CanSort,
        Filterable = CanFilter || (GroupBy is null && Table?.GroupsBy(this) == true),
        Searchable = FilterKind == ColumnFilterKind.Text,
        Groupable = GroupBy is null && CanGroup,
        Aggregatable = HasAggregate || RollUp is not null,
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
}
