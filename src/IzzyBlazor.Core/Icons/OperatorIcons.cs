namespace IzzyBlazor;

/// <summary>Icons drawn for IzzyBlazor where Lucide's don't fit, at its 1.5 stroke to sit with it.</summary>
internal static class OperatorIcons
{
    /// <summary>
    /// Lucide's <c>list-filter</c> drawn smaller: its top line runs nearly edge to edge, so at the size of
    /// the icons beside it, it looks larger than they do. The lines fall on whole pixels at 18px, a table's size.
    /// </summary>
    public static IconData Filter => new("0 0 24 24", "<path d=\"M3.33 6h17.34\"/><path d=\"M7 11.33h10\"/><path d=\"M10 16.67h4\"/>", IconStyle.Stroke, 1.5, "list-filter-small");
}
