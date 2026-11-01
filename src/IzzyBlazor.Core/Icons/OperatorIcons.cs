namespace IzzyBlazor;

/// <summary>
/// The filter operators Lucide has no icon for: Tabler's (@tabler/icons 3.50.0, MIT; see
/// THIRD-PARTY-NOTICES.md), drawn at Lucide's 1.5 stroke to sit with it.
/// </summary>
internal static class OperatorIcons
{
    private const string Contain = "<path d=\"M7 4h-4v16h4\"/><path d=\"M17 4h4v16h-4\"/><path d=\"M8 16h.01\"/><path d=\"M12 16h.01\"/><path d=\"M16 16h.01\"/>";

    /// <summary>From Tabler (<c>brackets-contain</c>).</summary>
    public static IconData Contains => new("0 0 24 24", Contain, IconStyle.Stroke, 1.5, "brackets-contain");

    /// <summary><c>brackets-contain</c> with the line through it that <c>equal-not</c> has.</summary>
    public static IconData DoesNotContain => new("0 0 24 24", Contain + "<path d=\"M5 21l14 -18\"/>", IconStyle.Stroke, 1.5, "brackets-contain-not");

    /// <summary>From Tabler (<c>brackets-contain-start</c>).</summary>
    public static IconData StartsWith => new("0 0 24 24", "<path d=\"M9 4h-4v16h4\"/><path d=\"M18 16h-.01\"/><path d=\"M14 16h-.01\"/><path d=\"M10 16h-.01\"/>", IconStyle.Stroke, 1.5, "brackets-contain-start");

    /// <summary>From Tabler (<c>brackets-contain-end</c>).</summary>
    public static IconData EndsWith => new("0 0 24 24", "<path d=\"M14 4h4v16h-4\"/><path d=\"M5 16h.01\"/><path d=\"M9 16h.01\"/><path d=\"M13 16h.01\"/>", IconStyle.Stroke, 1.5, "brackets-contain-end");

    /// <summary>From Tabler (<c>math-greater</c>).</summary>
    public static IconData GreaterThan => new("0 0 24 24", "<path d=\"M5 18l14 -6l-14 -6\"/>", IconStyle.Stroke, 1.5, "math-greater");

    /// <summary>From Tabler (<c>math-equal-greater</c>).</summary>
    public static IconData GreaterOrEqual => new("0 0 24 24", "<path d=\"M5 18l14 -4\"/><path d=\"M5 14l14 -4l-14 -4\"/>", IconStyle.Stroke, 1.5, "math-equal-greater");

    /// <summary>From Tabler (<c>math-lower</c>).</summary>
    public static IconData LessThan => new("0 0 24 24", "<path d=\"M19 18l-14 -6l14 -6\"/>", IconStyle.Stroke, 1.5, "math-lower");

    /// <summary>From Tabler (<c>math-equal-lower</c>).</summary>
    public static IconData LessOrEqual => new("0 0 24 24", "<path d=\"M19 18l-14 -4\"/><path d=\"M19 14l-14 -4l14 -4\"/>", IconStyle.Stroke, 1.5, "math-equal-lower");
}
