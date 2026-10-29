using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace IzzyBlazor;

/// <summary>
/// A field of the data: its key in requests and addresses, the expression that reads it, and what
/// can be done with it. One description serves sorting, filtering and search. A Table's columns
/// are fields; <see cref="DataFields.Of{TItem}"/> reads them from a type.
/// </summary>
/// <typeparam name="TItem">The type of the items.</typeparam>
public sealed class DataField<TItem>
{
    private readonly LambdaExpression _value = default!;

    /// <summary>The field's name in requests and the address, such as <c>"total"</c> or <c>"customer.name"</c>.</summary>
    public required string Key { get; init; }

    /// <summary>What the field reads from an item, such as <c>(Expression&lt;Func&lt;Order, decimal&gt;&gt;)(o =&gt; o.Total)</c>.</summary>
    public required LambdaExpression Value
    {
        get => _value;
        init => _value = value is { Parameters.Count: 1 } && value.Parameters[0].Type == typeof(TItem)
            ? value
            : throw new ArgumentException($"A field's {nameof(Value)} takes one {typeof(TItem).Name}.", nameof(value));
    }

    /// <summary>The type of the field's value.</summary>
    public Type Type => Value.ReturnType;

    /// <summary>The field's name for people: <c>[Display(Name)]</c>, else the property name in words.</summary>
    public string? Caption { get; init; }

    /// <summary>A .NET format string for the value, such as <c>C</c>: <c>[DisplayFormat(DataFormatString)]</c>.</summary>
    public string? Format { get; init; }

    /// <summary>Whether a request can sort by it.</summary>
    public bool Sortable { get; init; }

    /// <summary>Whether a filter can name it. A condition on a field that isn't filterable matches nothing.</summary>
    public bool Filterable { get; init; }

    /// <summary>Whether a request's <c>Search</c> looks in it (text that contains the search, ignoring case).</summary>
    public bool Searchable { get; init; }

    /// <summary>The operators it takes, of those its type takes (see <see cref="FilterOperator"/>); null for all of them.</summary>
    public IReadOnlyList<FilterOperator>? Operators { get; init; }

    /// <summary>The values to choose from, value and text: an enum's names (with their <c>[Display(Name)]</c>) by default.</summary>
    public IReadOnlyList<(string Value, string Text)>? Choices { get; init; }

    /// <summary>
    /// Builds the condition for this field yourself: for a computed value, full-text search, or a
    /// value stored differently from how it's shown. It gets the condition with its values parsed
    /// for the field's type (a list for In and NotIn, a <c>DateOnly</c> for OnDay) and returns the
    /// filter on the item, or null for the usual one.
    /// </summary>
    /// <example><code>Filter = f =&gt; f.Operator == FilterOperator.Contains ? o =&gt; o.Tags.Any(t =&gt; t.Name == (string)f.Value!) : null</code></example>
    public Func<FieldFilter, Expression<Func<TItem, bool>>?>? Filter { get; init; }

    /// <summary>What to sort by when it isn't <see cref="Value"/> (a template column's SortBy beside its FilterBy).</summary>
    internal LambdaExpression? SortValue { get; init; }

    internal IReadOnlyList<FilterOperator> EffectiveOperators =>
        Operators is null ? FilterValues.OperatorsFor(Type) : FilterValues.OperatorsFor(Type).Intersect(Operators).ToList();

    /// <inheritdoc />
    public override string ToString() => Key;
}

/// <summary>Reads fields from a type, for a FilterBuilder, export, forms, or a server that applies requests.</summary>
public static class DataFields
{
    /// <summary>
    /// The public properties of <typeparamref name="TItem"/> that a filter can compare (text,
    /// numbers, dates and times, enums, booleans, Guids), keyed by name in lower case
    /// (<c>"total"</c>). Each is sortable and filterable, and text is searchable. Reads
    /// <c>[Display(Name)]</c> for the caption, <c>[DisplayFormat(DataFormatString)]</c> and
    /// <c>[DataType]</c> for the format, and leaves out <c>[ScaffoldColumn(false)]</c> and
    /// <c>[Display(AutoGenerateField = false)]</c> properties.
    /// </summary>
    public static IReadOnlyList<DataField<TItem>> Of<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TItem>() => Cache<TItem>.Fields;

    private static class Cache<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TItem>
    {
        public static readonly IReadOnlyList<DataField<TItem>> Fields = Read();

        [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Enum fields are kept with their enum; Choices reads them as ColumnFilter did.")]
        private static List<DataField<TItem>> Read()
        {
            var fields = new List<DataField<TItem>>();
            foreach (var property in typeof(TItem).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetMethod is null || property.GetIndexParameters().Length > 0 || FilterValues.KindOf(property.PropertyType) == FieldKind.None)
                {
                    continue;
                }

                var display = property.GetCustomAttribute<DisplayAttribute>();
                if (property.GetCustomAttribute<ScaffoldColumnAttribute>() is { Scaffold: false } || display?.GetAutoGenerateField() == false)
                {
                    continue;
                }

                var x = Expression.Parameter(typeof(TItem), "x");
                fields.Add(new DataField<TItem>
                {
                    Key = property.Name.ToLowerInvariant(),
                    Value = Expression.Lambda(Expression.Property(x, property), x),
                    Caption = display?.GetName() ?? Words(property.Name),
                    Format = FormatOf(property),
                    Sortable = true,
                    Filterable = true,
                    Searchable = FilterValues.KindOf(property.PropertyType) == FieldKind.Text,
                    Choices = EnumChoices(property.PropertyType),
                });
            }

            return fields;
        }
    }

    // [DisplayFormat(DataFormatString = "{0:C}")] is "C"; else what [DataType] implies.
    private static string? FormatOf(PropertyInfo property)
    {
        if (property.GetCustomAttribute<DisplayFormatAttribute>()?.DataFormatString is { } format)
        {
            var open = format.IndexOf("{0:", StringComparison.Ordinal);
            var close = format.LastIndexOf('}');
            return open >= 0 && close > open + 3 ? format[(open + 3)..close] : format;
        }

        return property.GetCustomAttribute<DataTypeAttribute>()?.DataType switch
        {
            DataType.Date => "d",
            DataType.Time => "t",
            DataType.Currency => "C",
            _ => null,
        };
    }

    /// <summary>An enum's names, with their <c>[Display(Name)]</c> text; null for other types.</summary>
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "The trimmer keeps every field of an enum it keeps, and their [Display] attributes when DisplayAttribute is kept, which this method's use of it ensures.")]
    internal static IReadOnlyList<(string Value, string Text)>? EnumChoices(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        return t.IsEnum
            ? t.GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => (f.Name, f.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? f.Name)).ToList()
            : null;
    }

    // "OrderDate" -> "Order date"
    internal static string Words(string name)
    {
        var chars = new System.Text.StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]))
            {
                chars.Append(' ').Append(char.ToLowerInvariant(c));
            }
            else
            {
                chars.Append(c);
            }
        }

        return chars.ToString();
    }
}
