namespace IzzyBlazor.Units;

/// <summary>
/// Opt-in unit shorthands for <see cref="Length"/>: <c>15.Rem</c>, <c>240.Px</c>, <c>100.Dvh</c>.
/// Add <c>@using IzzyBlazor.Units</c> to use them. They need C# 14 (the .NET 10 default).
/// </summary>
/// <remarks>
/// They live in their own namespace because they add members to every <c>int</c> and
/// <c>double</c> in files that import them. <see cref="Length"/>'s factory methods
/// (<c>Length.Rem(15)</c>) always work without this import.
/// </remarks>
public static class LengthUnits
{
    extension(int value)
    {
        /// <summary>Pixels.</summary>
        public Length Px => Length.Px(value);

        /// <summary>Root ems (16px each by default).</summary>
        public Length Rem => Length.Rem(value);

        /// <summary>Percent of the containing block.</summary>
        public Length Percent => Length.Percent(value);

        /// <summary>Character widths.</summary>
        public Length Ch => Length.Ch(value);

        /// <summary>Percent of the viewport width.</summary>
        public Length Vw => Length.Vw(value);

        /// <summary>Percent of the dynamic viewport height.</summary>
        public Length Dvh => Length.Dvh(value);
    }

    extension(double value)
    {
        /// <summary>Pixels.</summary>
        public Length Px => Length.Px(value);

        /// <summary>Root ems (16px each by default).</summary>
        public Length Rem => Length.Rem(value);

        /// <summary>Percent of the containing block.</summary>
        public Length Percent => Length.Percent(value);

        /// <summary>Character widths.</summary>
        public Length Ch => Length.Ch(value);

        /// <summary>Percent of the viewport width.</summary>
        public Length Vw => Length.Vw(value);

        /// <summary>Percent of the dynamic viewport height.</summary>
        public Length Dvh => Length.Dvh(value);
    }
}
