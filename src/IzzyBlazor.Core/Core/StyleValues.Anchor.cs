namespace IzzyBlazor;

public static partial class StyleValues
{
    /// <summary>
    /// A CSS anchor name for an element id, such as <c>--my-menu</c>. Characters that aren't
    /// allowed in a CSS name (spaces, dots, semicolons) become underscores, so any id is safe.
    /// </summary>
    internal static string AnchorName(string id) =>
        "--" + string.Create(id.Length, id, (span, value) =>
        {
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                span[i] = char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_';
            }
        });
}
