namespace IzzyBlazor;

/// <summary>
/// Estimates how wide a line of button text is, from Inter's character widths
/// (<c>_generated/TextWidths.g.cs</c>, measured by <c>build/measure-text.mjs</c>). Kerning is left
/// out, which is within a pixel or two for a label; characters outside Latin count as 1em.
/// </summary>
internal static partial class TextWidths
{
    /// <summary>The width in pixels (at a 16px root) of <paramref name="text"/> as a button label of this size, with whitespace collapsed as HTML does.</summary>
    public static double Of(string text, ControlSize size)
    {
        var (table, em) = size switch
        {
            ControlSize.Sm => (Small, 13.0),
            ControlSize.Lg => (Large, 16.0),
            _ => (Medium, 14.0),
        };

        var width = 0.0;
        var space = false;
        foreach (var c in text.Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                space = true;
                continue;
            }

            if (space)
            {
                width += table[0] / 100.0;
                space = false;
            }

            var index = c is >= ' ' and <= '~' ? c - 0x20
                : c is >= ' ' and <= 'ſ' ? AsciiCount + c - 0xa0
                : -1;
            width += index >= 0 ? table[index] / 100.0 : em;
        }

        return width;
    }
}
