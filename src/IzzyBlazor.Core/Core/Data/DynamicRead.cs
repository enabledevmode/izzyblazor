using System.Collections;
using System.Data;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace IzzyBlazor;

/// <summary>
/// Reads a value by name from rows whose shape is known only at run time, for <c>DynamicColumn</c> and
/// <see cref="DataFields.ByName{TItem}"/>, so a grid and its export endpoint agree on keys and values.
/// </summary>
internal static class DynamicRead
{
    private static readonly MethodInfo ReadMethod = typeof(DynamicRead).GetMethod(nameof(Read), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    /// A key goes in the address and in filter text, so letters and digits in lower case, with an
    /// underscore for each run of anything else: "Margin" and "Margin %" stay apart, and "#" has a key.
    /// </summary>
    internal static string? KeyOf(string? field)
    {
        var key = new System.Text.StringBuilder();
        foreach (var c in field ?? "")
        {
            if (char.IsLetterOrDigit(c))
            {
                key.Append(char.ToLowerInvariant(c));
            }
            else if (key.Length == 0 || key[^1] != '_')
            {
                key.Append('_');
            }
        }

        return key.Length == 0 ? null : key.ToString();
    }

    /// <summary>Missing values are common in data like this, so a number or date can always be empty.</summary>
    internal static Type ValueTypeOf(Type type) => type.IsValueType && Nullable.GetUnderlyingType(type) is null ? typeof(Nullable<>).MakeGenericType(type) : type;

    /// <summary>The expression that reads <paramref name="field"/> as <paramref name="valueType"/>, and its compiled form boxed.</summary>
    internal static (LambdaExpression Value, Func<TItem, object?> Read) Reader<TItem>(string field, Type valueType)
    {
        var row = Expression.Parameter(typeof(TItem), "r");
        var read = Expression.Call(ReadMethod, Expression.Convert(row, typeof(object)), Expression.Constant(field), Expression.Constant(valueType));
        return (Expression.Lambda(Expression.Convert(read, valueType), row), Expression.Lambda<Func<TItem, object?>>(read, row).Compile());
    }

    internal static bool Readable(Type type) =>
        typeof(IDictionary<string, object?>).IsAssignableFrom(type)
        || typeof(IReadOnlyDictionary<string, object?>).IsAssignableFrom(type)
        || typeof(IDictionary).IsAssignableFrom(type)
        || typeof(DataRow).IsAssignableFrom(type)
        || typeof(DataRowView).IsAssignableFrom(type);

    // The row's value by name, as the type, or null when it's missing or doesn't convert.
    private static object? Read(object? row, string field, Type type)
    {
        var value = row switch
        {
            null => null,
            IDictionary<string, object?> d => d.TryGetValue(field, out var v) ? v : null,
            IReadOnlyDictionary<string, object?> d => d.TryGetValue(field, out var v) ? v : null,
            IDictionary d => d.Contains(field) ? d[field] : null,
            DataRow r => r.Table.Columns.Contains(field) ? r[field] : null,
            DataRowView v => v.Row.Table.Columns.Contains(field) ? v.Row[field] : null,
            _ => throw new InvalidOperationException($"The \"{field}\" field reads dictionaries, ExpandoObject and DataRow, not {row.GetType().Name}: use a PropertyColumn, or DataFields.Of, for a row with properties."),
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
