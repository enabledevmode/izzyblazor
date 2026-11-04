using System.Globalization;

namespace IzzyBlazor;

/// <summary>Timing parameters, which are <see cref="TimeSpan"/>s, as the milliseconds the scripts read.</summary>
internal static class Durations
{
    /// <summary>Whole milliseconds, for a <c>data-</c> attribute or a CSS <c>ms</c> value.</summary>
    public static string Ms(TimeSpan duration) => ((long)duration.TotalMilliseconds).ToString(CultureInfo.InvariantCulture);

    /// <summary>Throws when a timing parameter is negative.</summary>
    public static TimeSpan NotNegative(TimeSpan duration, string component, string parameter) => duration >= TimeSpan.Zero
        ? duration
        : throw new ArgumentOutOfRangeException(parameter, duration, $"{component}'s {parameter} can't be negative.");
}
