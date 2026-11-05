namespace IzzyBlazor;

/// <summary>
/// Round tick values for a number axis: D3's tick algorithm (d3-array <c>ticks</c>, MIT), which
/// steps by 1, 2 or 5 times a power of ten and builds each tick from an integer, so a tick reads
/// 0.3, never 0.30000000000000004.
/// </summary>
internal static class ChartScale
{
    private static readonly double E10 = Math.Sqrt(50), E5 = Math.Sqrt(10), E2 = Math.Sqrt(2);

    /// <summary>
    /// A tick step for about a given count of ticks between start and stop: the integers
    /// <c>I1</c> to <c>I2</c> times the step. <c>Inc</c> is the step, or, when negative, one over
    /// the step (for steps under 1, so the arithmetic stays exact). <c>Factor</c> is 1, 2 or 5.
    /// </summary>
    public readonly record struct Spec(long I1, long I2, double Inc, int Factor)
    {
        public double Value(long i) => Inc < 0 ? i / -Inc : i * Inc;

        public double Step => Inc < 0 ? 1 / -Inc : Inc;
    }

    public static Spec TickSpec(double start, double stop, double count)
    {
        // No span, or a backward or endless one: no ticks (and no endless loop over them).
        if (!(stop > start) || !double.IsFinite(start) || !double.IsFinite(stop) || !(count > 0))
        {
            return new Spec(0, -1, 1, 1);
        }

        var step = (stop - start) / Math.Max(0, count);
        var power = Math.Floor(Math.Log10(step));
        var error = step / Math.Pow(10, power);
        var factor = error >= E10 ? 10 : error >= E5 ? 5 : error >= E2 ? 2 : 1;
        double inc;
        long i1, i2;
        if (power < 0)
        {
            inc = Math.Pow(10, -power) / factor;
            i1 = (long)Math.Round(start * inc);
            i2 = (long)Math.Round(stop * inc);
            if (i1 / inc < start) ++i1;
            if (i2 / inc > stop) --i2;
            inc = -inc;
        }
        else
        {
            inc = Math.Pow(10, power) * factor;
            i1 = (long)Math.Round(start / inc);
            i2 = (long)Math.Round(stop / inc);
            if (i1 * inc < start) ++i1;
            if (i2 * inc > stop) --i2;
        }

        if (i2 < i1 && count is >= 0.5 and < 2)
        {
            return TickSpec(start, stop, count * 2);
        }

        // A factor of 10 is a factor of 1 at the next power.
        return new Spec(i1, i2, inc, factor == 10 ? 1 : factor);
    }

    /// <summary>
    /// Widens a domain to round ends at about <paramref name="count"/> ticks (D3's <c>nice</c>).
    /// An empty or single-value domain widens first, around its value.
    /// </summary>
    public static (double Min, double Max) Nice(double min, double max, int count)
    {
        if (!double.IsFinite(min) || !double.IsFinite(max))
        {
            return (0, 1);
        }

        if (min == max)
        {
            var pad = min == 0 ? 1 : Math.Abs(min) * 0.1;
            (min, max) = (min >= 0 && min - pad < 0 ? 0 : min - pad, max + pad);
        }

        double? previous = null;
        for (var i = 0; i < 10; i++)
        {
            var spec = TickSpec(min, max, count);
            if (previous == spec.Inc)
            {
                break;
            }

            previous = spec.Inc;
            if (spec.Inc > 0)
            {
                min = Math.Floor(min / spec.Inc) * spec.Inc;
                max = Math.Ceiling(max / spec.Inc) * spec.Inc;
            }
            else
            {
                min = Math.Ceiling(min * spec.Inc) / spec.Inc;
                max = Math.Floor(max * spec.Inc) / spec.Inc;
            }
        }

        return (min, max);
    }

    /// <summary>
    /// The multiples of a base step that coarser tick levels keep, each dividing the next so a
    /// tick shown at a narrow width is shown at every wider one: from a step of 1 (times a power of
    /// ten), 1, 2, 10, 20, 100…; from 2, 1, 5, 10, 50…; from 5, 1, 2, 10, 20….
    /// </summary>
    public static IReadOnlyList<long> Ladder(int factor, long span)
    {
        var steps = new List<long> { 1 };
        var (first, second) = factor == 2 ? (5L, 2L) : (2L, 5L);
        var k = 1L;
        for (var i = 0; k < span && steps.Count < 40; i++)
        {
            k *= i % 2 == 0 ? first : second;
            steps.Add(k);
        }

        return steps;
    }

    /// <summary>How many decimals a step's ticks need, so 2.5 isn't shown as 3.</summary>
    public static int Decimals(double step) => step >= 1 || step <= 0 ? 0 : Math.Min(10, (int)Math.Ceiling(-Math.Log10(step) - 1e-9));
}
