using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace IzzyBlazor;

/// <summary>The number types NumberField and Slider bind to, and conversions they share.</summary>
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

    /// <summary>
    /// How many characters (<c>ch</c>) a slider's shown value can take anywhere on its scale: the
    /// longest whole part of its ends, the step's decimals, and the format's own text. Reserved so
    /// the track doesn't change length, and move the thumbs, as the value gets shorter or longer.
    /// </summary>
    public static int ShownWidth(double min, double max, double? step, string format)
    {
        static string Text(double value) => value.ToString("R", CultureInfo.InvariantCulture);
        static int Decimals(double value) => Text(value) is var text && text.IndexOf('.') is var dot and >= 0 ? text.Length - dot - 1 : 0;

        var whole = Math.Max(Text(Math.Truncate(min)).Length, Text(Math.Truncate(max)).Length);
        var decimals = Math.Max(Decimals(min), step is { } s ? Decimals(s) : 0);
        var number = whole + (decimals > 0 ? decimals + 1 : 0);
        // One more for a format's characters wider than a digit, such as % or $.
        return format.Replace("{0}", new string('0', number), StringComparison.Ordinal).Length + 1;
    }
}
