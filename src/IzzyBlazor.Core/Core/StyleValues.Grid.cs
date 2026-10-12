namespace IzzyBlazor;

public static partial class StyleValues
{
    /// <summary>The most columns a <see cref="Grid"/> takes.</summary>
    public const int MaxColumns = 12;

    /// <summary>Equal columns: <c>repeat(3, minmax(0, 1fr))</c>.</summary>
    public static string GridColumns(int count) => count is >= 1 and <= MaxColumns
        ? $"repeat({count}, minmax(0, 1fr))"
        : throw new ArgumentOutOfRangeException(nameof(count), count, $"Grid columns go from 1 to {MaxColumns}.");

    /// <summary>
    /// As many columns as fit, each at least this wide (never wider than the grid). With
    /// <paramref name="stretch"/>, a short row's items grow to fill it (auto-fit); otherwise the
    /// empty columns are kept (auto-fill), so items stay the same width.
    /// </summary>
    public static string GridAutoColumns(Length minWidth, bool stretch = false) =>
        $"repeat({(stretch ? "auto-fit" : "auto-fill")}, minmax(min({Length(minWidth)}, 100%), 1fr))";

    /// <summary>A grid item spanning columns or rows: <c>span 2 / span 2</c>.</summary>
    public static string Span(int count) => count is >= 1 and <= MaxColumns
        ? $"span {count} / span {count}"
        : throw new ArgumentOutOfRangeException(nameof(count), count, $"Spans go from 1 to {MaxColumns}.");
}
