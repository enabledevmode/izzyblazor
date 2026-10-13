using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace IzzyBlazor;

/// <summary>The number types NumberInput and Slider bind to, and conversions they share.</summary>
internal static class NumberTypes
{
    private static readonly HashSet<Type> Whole =
    [
        typeof(int), typeof(long), typeof(short), typeof(byte),
        typeof(sbyte), typeof(uint), typeof(ulong), typeof(ushort),
    ];

    private static readonly HashSet<Type> Fractional = [typeof(decimal), typeof(double), typeof(float)];

    /// <summary>
    /// Parses a number input's value, which is always invariant and may use an exponent
    /// (<c>1E-05</c>, as .NET formats small doubles). Empty text is null for nullable types.
    /// </summary>
    public static bool TryParse<TValue>(string? text, [MaybeNullWhen(false)] out TValue result)
    {
        result = default!;
        var type = Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue);
        if (string.IsNullOrWhiteSpace(text))
        {
            return Nullable.GetUnderlyingType(typeof(TValue)) is not null;
        }

        var styles = Whole.Contains(type) ? NumberStyles.Integer : NumberStyles.Float;
        object? parsed = type switch
        {
            _ when type == typeof(int) => int.TryParse(text, styles, CultureInfo.InvariantCulture, out var v) ? v : null,
            _ when type == typeof(long) => long.TryParse(text, styles, CultureInfo.InvariantCulture, out var v) ? v : null,
            _ when type == typeof(short) => short.TryParse(text, styles, CultureInfo.InvariantCulture, out var v) ? v : null,
            _ when type == typeof(byte) => byte.TryParse(text, styles, CultureInfo.InvariantCulture, out var v) ? v : null,
            _ when type == typeof(sbyte) => sbyte.TryParse(text, styles, CultureInfo.InvariantCulture, out var v) ? v : null,
            _ when type == typeof(uint) => uint.TryParse(text, styles, CultureInfo.InvariantCulture, out var v) ? v : null,
            _ when type == typeof(ulong) => ulong.TryParse(text, styles, CultureInfo.InvariantCulture, out var v) ? v : null,
            _ when type == typeof(ushort) => ushort.TryParse(text, styles, CultureInfo.InvariantCulture, out var v) ? v : null,
            _ when type == typeof(decimal) => decimal.TryParse(text, styles, CultureInfo.InvariantCulture, out var v) ? v : null,
            _ when type == typeof(double) => double.TryParse(text, styles, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v) ? v : null,
            _ when type == typeof(float) => float.TryParse(text, styles, CultureInfo.InvariantCulture, out var v) && float.IsFinite(v) ? v : null,
            _ => null,
        };

        if (parsed is null)
        {
            return false;
        }

        result = (TValue)parsed;
        return true;
    }

    /// <summary>Whether the type, or its nullable form, is a whole-number type.</summary>
    public static bool IsWholeNumber(Type type) => Whole.Contains(Nullable.GetUnderlyingType(type) ?? type);

    /// <summary>Whether the type, or its nullable form, is a number type.</summary>
    public static bool IsNumber(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return Whole.Contains(underlying) || Fractional.Contains(underlying);
    }

    /// <summary>
    /// A number, or a number in invariant text (as <c>[Range(typeof(decimal), "0", "10")]</c> stores
    /// it), as a double; null for anything else.
    /// </summary>
    public static double? ToDouble(object? value) => value switch
    {
        null => null,
        string text => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : null,
        // A float widened straight to double shows its binary error: 0.1f becomes 0.10000000149011612.
        float single => double.Parse(single.ToString("R", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
        _ when IsNumber(value.GetType()) => Convert.ToDouble(value, CultureInfo.InvariantCulture),
        _ => null,
    };
}
