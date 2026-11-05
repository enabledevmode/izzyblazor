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
    int MinDecimals,
    double Scale)
{
    /// <summary>
    /// The shape of <paramref name="format"/>; null is the axis default, N with the tick step's
    /// decimals. Returns null for a format a shape can't hold (exponents, general, hex), and the
    /// chart then doesn't draw itself again in the browser.
    /// </summary>
    public static NumberShape? Of(string? format, CultureInfo culture)
    {
        var auto = format is null;
        if (format is not null && (System.Text.RegularExpressions.Regex.IsMatch(format, @"^[EeGgRrXxDd]\d*$") || System.Text.RegularExpressions.Regex.IsMatch(format, @"[Ee][+-]?0")))
        {
            return null;
        }

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

        // The scale, from a million: "100,000,000%" is 100, "1,000K" (#,##0,K) is a thousandth.
        var million = Split(1e6.ToString(format, culture)).Digits;
        if (group.Length > 0) million = million.Replace(group, "");
        million = million.Replace(dec, ".");
        var scale = double.TryParse(million, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var m) && m > 0 ? m / 1e6 : 1;

        // The fewest decimals a value keeps: "0.0#" keeps one, "#,##0.##" none.
        var one = Split(1.0.ToString(format, culture)).Digits;
        var minDecimals = one.Contains(dec) ? one[(one.LastIndexOf(dec, StringComparison.Ordinal) + dec.Length)..].Length : 0;

        return new NumberShape(prefix, suffix, negPrefix, negSuffix, group, dec, auto ? -1 : decimals, auto ? 0 : Math.Min(minDecimals, decimals), scale);
    }

    private static (string Prefix, string Digits, string Suffix) Split(string text)
    {
        var first = text.IndexOfAny("0123456789".ToCharArray());
        var last = text.LastIndexOfAny("0123456789".ToCharArray());
        return first < 0 ? (text, "", "") : (text[..first], text[first..(last + 1)], text[(last + 1)..]);
    }
}
