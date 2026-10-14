namespace IzzyBlazor;

/// <summary>Helpers for color strings.</summary>
public static class ColorValues
{
    /// <summary>
    /// A hex color as the browser's color input uses it: <c>#rrggbb</c> in lowercase. Accepts
    /// <c>#rgb</c>, <c>#rrggbb</c>, with or without the <c>#</c>, any case, around spaces. Null when
    /// the text isn't a hex color.
    /// </summary>
    public static string? NormalizeHex(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var text = value.Trim();
        if (text.StartsWith('#'))
        {
            text = text[1..];
        }

        if ((text.Length != 3 && text.Length != 6) || !text.All(char.IsAsciiHexDigit))
        {
            return null;
        }

        text = text.ToLowerInvariant();
        return text.Length == 3
            ? $"#{text[0]}{text[0]}{text[1]}{text[1]}{text[2]}{text[2]}"
            : "#" + text;
    }
}
