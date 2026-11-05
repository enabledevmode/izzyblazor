using System.Globalization;

namespace IzzyBlazor;

/// <summary>
/// How a chart's values read, taken from its .NET format so the script can format new tick values
/// the same way (chart-draw.ts <c>format</c>): what comes before and after the digits, positive
/// and negative, the separators, the decimals and the scale. Read by formatting sample numbers,
/// so standard formats (C0, N1, P0) and custom ones ("0' ms'", "#,##0.##") both work.
/// </summary>
internal sealed record NumberShape(
    string Prefix,
    string Suffix,
    string NegativePrefix,
    string NegativeSuffix,
    string Group,
    string Decimal,
    int Decimals,
    bool Trim,
    double Scale)
{
    /// <summary>The shape of <paramref name="format"/>; null is the axis default, N with the tick step's decimals.</summary>
    public static NumberShape Of(string? format, CultureInfo culture)
    {
        var auto = format is null;
        format ??= "N0";
        var nf = culture.NumberFormat;
        string[] decimalSeparators = [nf.NumberDecimalSeparator, nf.CurrencyDecimalSeparator, nf.PercentDecimalSeparator];

        var (prefix, digits, suffix) = Split(1234567.125.ToString(format, culture));
        var (negPrefix, _, negSuffix) = Split((-1234567.125).ToString(format, culture));

        // The separators between digit runs: a last one that's a decimal separator sets the decimals.
        var runs = System.Text.RegularExpressions.Regex.Matches(digits, @"\d+").Select(m => m.Value).ToList();
        var separators = System.Text.RegularExpressions.Regex.Matches(digits, @"\D+").Select(m => m.Value).ToList();

        var decimals = 0;
        var dec = nf.NumberDecimalSeparator;
        if (separators.Count > 0 && decimalSeparators.Contains(separators[^1]) && runs.Count == separators.Count + 1)
        {
            dec = separators[^1];
            decimals = runs[^1].Length;
            separators.RemoveAt(separators.Count - 1);
        }

        var group = separators.Count > 0 ? separators[0] : "";

        // The scale, from 1: "100%" is 100 at no decimals; "1.00" is 1 at two.
        var one = Split(1.0.ToString(format, culture)).Digits;
        var oneDigits = new string(one.Where(char.IsDigit).ToArray());
        var oneDecimals = one.Contains(dec) ? one[(one.LastIndexOf(dec, StringComparison.Ordinal) + dec.Length)..].Length : 0;
        var scale = double.TryParse(oneDigits, NumberStyles.None, CultureInfo.InvariantCulture, out var x) ? x / Math.Pow(10, oneDecimals) : 1;

        return new NumberShape(prefix, suffix, negPrefix, negSuffix, group, dec, auto ? -1 : decimals, !auto && oneDecimals < decimals, scale);
    }

    private static (string Prefix, string Digits, string Suffix) Split(string text)
    {
        var first = text.IndexOfAny("0123456789".ToCharArray());
        var last = text.LastIndexOfAny("0123456789".ToCharArray());
        return first < 0 ? (text, "", "") : (text[..first], text[first..(last + 1)], text[(last + 1)..]);
    }
}
