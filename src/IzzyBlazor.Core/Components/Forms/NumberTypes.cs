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
        _ when IsNumber(value.GetType()) => Convert.ToDouble(value, CultureInfo.InvariantCulture),
        _ => null,
    };
}
