namespace IzzyBlazor;

/// <summary>
/// The filter operators Lucide has no icon for, at its 1.5 stroke to sit with it. The text operators
/// are drawn for IzzyBlazor: ABC with a bar under the part that matches. The comparisons are Tabler's
/// (@tabler/icons 3.50.0, MIT; see THIRD-PARTY-NOTICES.md).
/// </summary>
internal static class OperatorIcons
{
    private const string Letters = "<path d=\"M1.5 14 4.25 6 7 14\"/><path d=\"M2.5 11.5h3.5\"/><path d=\"M9.5 6v8h2.75a2 2 0 0 0 0-4h-2.75h2.25a2 2 0 0 0 0-4z\"/><path d=\"M22 7.5a3.5 4 0 1 0 0 5\"/>";

    private static string Bar(string from, string to) => $"<path d=\"M{from} 19H{to}\" stroke-width=\"2.25\"/>";

    /// <summary>
    /// Lucide's <c>list-filter</c> drawn smaller: its top line runs nearly edge to edge, so at the size of
    /// the icons beside it, it looks larger than they do. The lines fall on whole pixels at 18px, a table's size.
    /// </summary>
    public static IconData Filter => new("0 0 24 24", "<path d=\"M3.33 6h17.34\"/><path d=\"M7 11.33h10\"/><path d=\"M10 16.67h4\"/>", IconStyle.Stroke, 1.5, "list-filter-small");

    /// <summary>The bar under the B.</summary>
    public static IconData Contains => new("0 0 24 24", Letters + Bar("9", "15"), IconStyle.Stroke, 1.5, "abc-contains");

    /// <summary>The same with a line through it.</summary>
    public static IconData DoesNotContain => new("0 0 24 24", Letters + Bar("9", "15") + "<path d=\"M3 22 21 2\"/>", IconStyle.Stroke, 1.5, "abc-does-not-contain");

    /// <summary>The bar under the A.</summary>
    public static IconData StartsWith => new("0 0 24 24", Letters + Bar("1.5", "7"), IconStyle.Stroke, 1.5, "abc-starts-with");

    /// <summary>The bar under the C.</summary>
    public static IconData EndsWith => new("0 0 24 24", Letters + Bar("17", "22.5"), IconStyle.Stroke, 1.5, "abc-ends-with");

    /// <summary>From Tabler (<c>math-greater</c>).</summary>
    public static IconData GreaterThan => new("0 0 24 24", "<path d=\"M5 18l14 -6l-14 -6\"/>", IconStyle.Stroke, 1.5, "math-greater");

    /// <summary>From Tabler (<c>math-equal-greater</c>).</summary>
    public static IconData GreaterOrEqual => new("0 0 24 24", "<path d=\"M5 18l14 -4\"/><path d=\"M5 14l14 -4l-14 -4\"/>", IconStyle.Stroke, 1.5, "math-equal-greater");

    /// <summary>From Tabler (<c>math-lower</c>).</summary>
    public static IconData LessThan => new("0 0 24 24", "<path d=\"M19 18l-14 -6l14 -6\"/>", IconStyle.Stroke, 1.5, "math-lower");

    /// <summary>From Tabler (<c>math-equal-lower</c>).</summary>
    public static IconData LessOrEqual => new("0 0 24 24", "<path d=\"M19 18l-14 -4\"/><path d=\"M19 14l-14 -4l14 -4\"/>", IconStyle.Stroke, 1.5, "math-equal-lower");
}
