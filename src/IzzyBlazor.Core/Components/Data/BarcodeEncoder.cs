namespace IzzyBlazor;

/// <summary>The kind of barcode a <see cref="Barcode"/> draws.</summary>
public enum BarcodeType
{
    /// <summary>Code 128: any ASCII text, such as a shipping or order number (the default). Digits are packed two to a symbol.</summary>
    Code128,

    /// <summary>EAN-13: 13 digits, on retail products; 12 get their check digit added.</summary>
    Ean13,

    /// <summary>EAN-8: 8 digits, on small products; 7 get their check digit added.</summary>
    Ean8,

    /// <summary>UPC-A: 12 digits, on retail products in North America; 11 get their check digit added.</summary>
    UpcA,
}

/// <summary>
/// A barcode's modules (true is a bar), the quiet zones it needs either side, the bars that run down
/// into the text (EAN and UPC guards), and where its text goes, centered on a module position.
/// </summary>
internal sealed record BarcodeSymbol(bool[] Modules, int QuietLeft, int QuietRight, IReadOnlyList<(int Start, int Length)> Extended, IReadOnlyList<(string Text, double Center)> Texts)
{
    /// <summary>The whole width in modules, quiet zones included.</summary>
    public int Width => QuietLeft + Modules.Length + QuietRight;
}

/// <summary>Encodes Code 128 (subsets chosen as the value needs), EAN-13, EAN-8 and UPC-A.</summary>
internal static class BarcodeEncoder
{
    // Code 128's 106 symbols, 11 modules each, a set bit a bar (from the standard's table); the stop is 13.
    private static readonly ushort[] Code128Patterns =
    [
        0x6CC, 0x66C, 0x666, 0x498, 0x48C, 0x44C, 0x4C8, 0x4C4, 0x464, 0x648, 0x644, 0x624,
        0x59C, 0x4DC, 0x4CE, 0x5CC, 0x4EC, 0x4E6, 0x672, 0x65C, 0x64E, 0x6E4, 0x674, 0x76E,
        0x74C, 0x72C, 0x726, 0x764, 0x734, 0x732, 0x6D8, 0x6C6, 0x636, 0x518, 0x458, 0x446,
        0x588, 0x468, 0x462, 0x688, 0x628, 0x622, 0x5B8, 0x58E, 0x46E, 0x5D8, 0x5C6, 0x476,
        0x776, 0x68E, 0x62E, 0x6E8, 0x6E2, 0x6EE, 0x758, 0x746, 0x716, 0x768, 0x762, 0x71A,
        0x77A, 0x642, 0x78A, 0x530, 0x50C, 0x4B0, 0x486, 0x42C, 0x426, 0x590, 0x584, 0x4D0,
        0x4C2, 0x434, 0x432, 0x612, 0x650, 0x7BA, 0x614, 0x47A, 0x53C, 0x4BC, 0x49E, 0x5E4,
        0x4F4, 0x4F2, 0x7A4, 0x794, 0x792, 0x6DE, 0x6F6, 0x7B6, 0x578, 0x51E, 0x45E, 0x5E8,
        0x5E2, 0x7A8, 0x7A2, 0x5DE, 0x5EE, 0x75E, 0x7AE, 0x684, 0x690, 0x69C,
    ];

    private const int Code128Stop = 0x18EB;
    private const int StartA = 103, StartB = 104, StartC = 105, CodeA = 101, CodeB = 100, CodeC = 99;

    // EAN's digit codes, 7 modules each: L (odd parity), G (even), and R (the right half).
    private static readonly string[] EanL = ["0001101", "0011001", "0010011", "0111101", "0100011", "0110001", "0101111", "0111011", "0110111", "0001011"];
    private static readonly string[] EanG = ["0100111", "0110011", "0011011", "0100001", "0011101", "0111001", "0000101", "0010001", "0001001", "0010111"];
    private static readonly string[] EanR = ["1110010", "1100110", "1101100", "1000010", "1011100", "1001110", "1010000", "1000100", "1001000", "1110100"];

    // EAN-13's first digit, which has no bars of its own: the parity of the left six (G marked).
    private static readonly string[] EanParity = ["LLLLLL", "LLGLGG", "LLGGLG", "LLGGGL", "LGLLGG", "LGGLLG", "LGGGLL", "LGLGLG", "LGLGGL", "LGGLGL"];

    /// <summary>Encodes <paramref name="value"/>; anything the type can't hold is an <see cref="ArgumentException"/>.</summary>
    public static BarcodeSymbol Encode(string value, BarcodeType type) => type switch
    {
        BarcodeType.Ean13 => Ean(Digits(value, 13, type), type),
        BarcodeType.Ean8 => Ean(Digits(value, 8, type), type),
        BarcodeType.UpcA => Ean(Digits(value, 12, type), type),
        _ => Code128(value),
    };

    /// <summary>Code 128's symbol values for <paramref name="value"/>, start first, without the check symbol or stop.</summary>
    internal static List<int> Code128Values(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException("A Code 128 barcode needs a value.", nameof(value));
        }

        foreach (var c in value)
        {
            if (c > 127)
            {
                throw new ArgumentException($"Code 128 holds ASCII only; \"{c}\" isn't.", nameof(value));
            }
        }

        int Run(int at)
        {
            var end = at;
            while (end < value.Length && char.IsAsciiDigit(value[end])) end++;
            return end - at;
        }

        // A if a control character comes before any lowercase letter, else B.
        char Subset(int at)
        {
            for (var i = at; i < value.Length; i++)
            {
                if (value[i] < 32) return 'A';
                if (value[i] >= 96) return 'B';
            }

            return 'B';
        }

        var values = new List<int>();
        var lead = Run(0);
        var set = lead >= 4 || (lead == 2 && value.Length == 2) ? 'C' : Subset(0);
        values.Add(set switch { 'A' => StartA, 'B' => StartB, _ => StartC });
        var i = 0;
        while (i < value.Length)
        {
            var c = value[i];
            if (set == 'C')
            {
                if (Run(i) >= 2)
                {
                    values.Add((c - '0') * 10 + value[i + 1] - '0');
                    i += 2;
                    continue;
                }

                set = Subset(i);
                values.Add(set == 'A' ? CodeA : CodeB);
                continue;
            }

            // Digits pack two to a symbol in C, which pays for its switch from 4 at the end or 6 within.
            var run = Run(i);
            if (run >= 6 || (run >= 4 && i + run == value.Length))
            {
                if (run % 2 == 1)
                {
                    values.Add(c - 32);
                    i++;
                }

                values.Add(CodeC);
                set = 'C';
                continue;
            }

            if (set == 'A' && c >= 96)
            {
                values.Add(CodeB);
                set = 'B';
                continue;
            }

            if (set == 'B' && c < 32)
            {
                values.Add(CodeA);
                set = 'A';
                continue;
            }

            values.Add(set == 'A' && c < 32 ? c + 64 : c - 32);
            i++;
        }

        return values;
    }

    private static BarcodeSymbol Code128(string value)
    {
        var values = Code128Values(value);
        var check = values[0];
        for (var i = 1; i < values.Count; i++)
        {
            check += values[i] * i;
        }

        values.Add(check % 103);
        var modules = new List<bool>(values.Count * 11 + 13);
        foreach (var v in values)
        {
            Append(modules, Code128Patterns[v], 11);
        }

        Append(modules, Code128Stop, 13);
        var text = new string(value.Where(c => c >= 32 && c < 127).ToArray());
        return new BarcodeSymbol([.. modules], 10, 10, [], [(text, 10 + modules.Count / 2.0)]);
    }

    /// <summary>The value's digits with its check digit: added when it's one short, checked when it's there.</summary>
    private static int[] Digits(string value, int length, BarcodeType type)
    {
        var name = type switch { BarcodeType.Ean13 => "EAN-13", BarcodeType.Ean8 => "EAN-8", _ => "UPC-A" };
        if (string.IsNullOrEmpty(value) || !value.All(char.IsAsciiDigit) || (value.Length != length && value.Length != length - 1))
        {
            throw new ArgumentException($"An {name} barcode holds {length} digits, or {length - 1} without the check digit; \"{value}\" isn't.", nameof(value));
        }

        var digits = value.Select(c => c - '0').ToList();
        var sum = 0;
        for (var i = 0; i < length - 1; i++)
        {
            // Weights 3 and 1, from the digit before the check digit leftwards.
            sum += digits[i] * ((length - 1 - i) % 2 == 1 ? 3 : 1);
        }

        var check = (10 - sum % 10) % 10;
        if (digits.Count == length && digits[^1] != check)
        {
            throw new ArgumentException($"\"{value}\" isn't a valid {name}: its check digit should be {check}.", nameof(value));
        }

        if (digits.Count == length - 1)
        {
            digits.Add(check);
        }

        return [.. digits];
    }

    private static BarcodeSymbol Ean(int[] digits, BarcodeType type)
    {
        // UPC-A is EAN-13 with a leading 0.
        var ean = type == BarcodeType.UpcA ? [0, .. digits] : digits;
        var half = ean.Length == 8 ? 4 : 6;
        var left = ean.Length == 8 ? ean[..4] : ean[1..7];
        var right = ean.Length == 8 ? ean[4..] : ean[7..];
        var parity = ean.Length == 8 ? "LLLL" : EanParity[ean[0]];

        var bits = new System.Text.StringBuilder("101");
        for (var i = 0; i < half; i++) bits.Append(parity[i] == 'G' ? EanG[left[i]] : EanL[left[i]]);
        bits.Append("01010");
        foreach (var d in right) bits.Append(EanR[d]);
        bits.Append("101");
        var modules = bits.ToString().Select(c => c == '1').ToArray();

        // The guards run down into the text; UPC-A's first and last digits' bars do too.
        var middle = 3 + half * 7;
        var end = modules.Length - 3;
        List<(int, int)> extended = type == BarcodeType.UpcA
            ? [(0, 10), (middle, 5), (end - 7, 10)]
            : [(0, 3), (middle, 5), (end, 3)];

        // Each digit under its own bars; the leading digit (and UPC-A's last) outside them.
        // The quiet zones the standards ask: EAN-13 11 and 7, EAN-8 7 and 7, UPC-A 9 and 9.
        var quietLeft = type == BarcodeType.UpcA ? 9 : ean.Length == 8 ? 7 : 11;
        var quietRight = type == BarcodeType.UpcA ? 9 : 7;
        var texts = new List<(string, double)>();
        string D(int d) => d.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (type == BarcodeType.UpcA)
        {
            texts.Add((D(digits[0]), quietLeft - 4));
            for (var i = 1; i < 6; i++) texts.Add((D(digits[i]), quietLeft + 3 + i * 7 + 3.5));
            for (var i = 6; i < 11; i++) texts.Add((D(digits[i]), quietLeft + middle + 5 + (i - 6) * 7 + 3.5));
            texts.Add((D(digits[11]), quietLeft + modules.Length + 4));
        }
        else
        {
            if (ean.Length == 13) texts.Add((D(ean[0]), quietLeft - 5));
            for (var i = 0; i < half; i++) texts.Add((D(left[i]), quietLeft + 3 + i * 7 + 3.5));
            for (var i = 0; i < half; i++) texts.Add((D(right[i]), quietLeft + middle + 5 + i * 7 + 3.5));
        }

        return new BarcodeSymbol(modules, quietLeft, quietRight, extended, texts);
    }

    private static void Append(List<bool> modules, int pattern, int width)
    {
        for (var i = width - 1; i >= 0; i--)
        {
            modules.Add(((pattern >> i) & 1) != 0);
        }
    }
}
