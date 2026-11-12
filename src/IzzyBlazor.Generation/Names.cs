using System.Globalization;
using System.Text;

namespace IzzyBlazor.Generation;

/// <summary>Name transforms, matching <c>dotnet new</c>'s value forms so a project name gives the same namespace in both.</summary>
public static class Names
{
    /// <summary>
    /// A valid C# namespace from any name: <c>My-App.2</c> → <c>My_App._2</c>, <c>9 lives</c> → <c>_9_lives</c>.
    /// The same rules as <c>dotnet new</c>'s <c>safe_namespace</c>: characters that can't be in an identifier
    /// become <c>_</c>, a part that starts with a digit gets <c>_</c> in front, and a dot is kept only between parts.
    /// </summary>
    public static string SafeNamespace(string value)
    {
        value = value.Trim();
        var result = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (i < value.Length - 1 && char.IsSurrogatePair(value[i], value[i + 1]))
            {
                result.Append('_');
                i++;
                continue;
            }
            var c = value[i];
            var firstOfPart = result.Length == 0 || result[^1] == '.';
            var canStart = IsIdentifierStart(c);
            var canContinue = IsIdentifierPart(c);
            if (firstOfPart && !canStart && canContinue)
            {
                result.Append('_').Append(c);
            }
            else if ((firstOfPart && canStart) || (!firstOfPart && canContinue) || (result.Length > 0 && i < value.Length - 1 && c == '.'))
            {
                result.Append(c);
            }
            else
            {
                result.Append('_');
            }
        }
        return result.ToString();
    }

    static bool IsIdentifierStart(char c) => c == '_' || char.GetUnicodeCategory(c) is
        UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter or
        UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter or UnicodeCategory.LetterNumber;

    static bool IsIdentifierPart(char c) => IsIdentifierStart(c) || char.GetUnicodeCategory(c) is
        UnicodeCategory.DecimalDigitNumber or UnicodeCategory.ConnectorPunctuation or
        UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.Format;
}
